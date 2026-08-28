using System.Diagnostics;
using LogiBatteryWidget.Core.Models;

namespace LogiBatteryWidget.Core.Providers.Vaxee;

/// <summary>
/// Reads battery status from a VAXEE wireless mouse dongle's raw USB HID feature-report command
/// channel - no vendor software required. There is no official API for this; the wire protocol
/// is undocumented by VAXEE. This implementation is based on the community documentation in
/// stuffz/mouse-battery-monitor's docs/VAXEE.md - an independent implementation from that
/// document's description of the command format, not a copy of its code. Confirmed against a
/// real VAXEE 4K Dongle (PID 0x1002): the command channel is at usage page 0xFF05 on the
/// interface's 4th HID collection. One gotcha not obvious from the docs: HidD_GetFeature requires
/// the report id pre-set in the response buffer's byte[0] before the call, or it fails with
/// ERROR_INVALID_PARAMETER. Another: if the mouse's wireless link has gone idle (no recent
/// movement/clicks), the dongle answers with an all-zero response (no command-id echo) instead of
/// an error - indistinguishable here from "no reply", so this provider just reports nothing for
/// that poll rather than surfacing it as a fault. It resolves itself as soon as the mouse is used.
/// </summary>
public sealed class VaxeeBatteryProvider : IBatteryProvider
{
    private const byte ReportId = 0x0E;
    private const byte HeaderByte = 0xA5;
    private const byte ReadFlag = 0x01;
    private const byte CommandBatteryLevel = 0x0B;
    private const byte CommandChargingStatus = 0x10;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    public string SourceName => "VAXEE";

    public async Task<IReadOnlyList<BatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        string? path;
        try
        {
            path = VaxeeHidLocator.FindCommandChannelPath();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VaxeeBatteryProvider] device lookup failed: {ex.Message}");
            return [];
        }

        if (path is null)
        {
            return []; // no VAXEE dongle plugged in right now
        }

        using var handle = VaxeeHidLocator.CreateFile(
            path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero,
            OpenExisting, flagsAndAttributes: 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Debug.WriteLine("[VaxeeBatteryProvider] found the device but couldn't open it");
            return [];
        }

        try
        {
            var levelResponse = await SendCommandAsync(handle, CommandBatteryLevel, cancellationToken).ConfigureAwait(false);
            if (levelResponse is null)
            {
                return [];
            }

            // "0-20, multiply by 5 for percentage" per the documented protocol.
            var percentage = Math.Clamp(levelResponse[5], (byte)0, (byte)20) * 5;

            var chargingResponse = await SendCommandAsync(handle, CommandChargingStatus, cancellationToken).ConfigureAwait(false);
            var isCharging = chargingResponse is { } r && r[5] != 0;

            return [new BatteryDevice("vaxee-mouse", SourceName, "VAXEE Mouse", BatteryDeviceKind.Mouse, percentage, isCharging)];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VaxeeBatteryProvider] communication failed: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Sends one feature-report command and reads the reply. HidD_SetFeature/GetFeature are
    /// synchronous Win32 calls with no cancellable/overlapped variant, so the blocking work runs
    /// on a pool thread; if it doesn't finish within <see cref="CommandTimeout"/> this returns
    /// null (the abandoned background call is harmless - the OS-level feature report IOCTL has
    /// its own driver timeout).
    /// </summary>
    private static async Task<byte[]?> SendCommandAsync(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle, byte commandId, CancellationToken cancellationToken)
    {
        var work = Task.Run(() =>
        {
            var request = new byte[64];
            request[0] = ReportId;
            request[1] = HeaderByte;
            request[2] = commandId;
            request[3] = ReadFlag;
            request[4] = 0x01; // data length

            if (!VaxeeHidLocator.HidD_SetFeature(handle, request, request.Length))
            {
                return null;
            }

            Thread.Sleep(100); // per the documented protocol: the device needs time to prepare the reply

            var response = new byte[64];
            response[0] = ReportId; // HidD_GetFeature requires the report id pre-set in byte[0] -
                                     // omitting this fails with ERROR_INVALID_PARAMETER (confirmed on real hardware).
            return VaxeeHidLocator.HidD_GetFeature(handle, response, response.Length) ? response : null;
        }, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CommandTimeout);

        var completed = await Task.WhenAny(work, Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);
        if (completed != work)
        {
            return null;
        }

        var response2 = await work.ConfigureAwait(false);
        // The command id we sent should be echoed back at byte[2] on a genuine reply.
        return response2 is not null && response2[2] == commandId ? response2 : null;
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
}
