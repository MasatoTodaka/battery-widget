using System.Diagnostics;
using LogiBatteryWidget.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace LogiBatteryWidget.Core.Providers.Pulsar;

/// <summary>
/// Reads battery status from a Pulsar wireless mouse dongle's raw USB HID feature-report command
/// channel - no vendor software required. There is no official API for this; the wire protocol is
/// undocumented by Pulsar. Command bytes and checksum are from the MIT-licensed
/// jonkristian/pulsar-x3-python (an independent implementation from its description of the
/// command format, not a copy of its code - that project talks to the device via raw libusb
/// control transfers, this one via the Windows HID API, which needs its own extra report-id byte
/// on top of what that project's 64-byte packets already carry; see the offset note below).
///
/// Confirmed against a real Pulsar wireless dongle (VID 0x3710, PID 0x5403): of the two
/// vendor-defined HID collections it exposes, only one actually answers commands (the other fails
/// every request with ERROR_GEN_FAILURE) - this provider tries each candidate in turn rather than
/// assuming which one it'll be.
/// </summary>
public sealed class PulsarBatteryProvider : IBatteryProvider
{
    // Reference command bytes (from jonkristian/pulsar-x3-python's send_command(dev, [0x08, 0x81, 0x01])).
    private const byte BatteryCommand1 = 0x08;
    private const byte BatteryCommand2 = 0x81;
    private const byte BatteryCommand3 = 0x01;

    // Confirmed on real hardware: response[2..4] echo the three command bytes back, and the
    // battery percentage sits right after a 2-byte gap at response[7].
    private const int BatteryResponseOffset = 7;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    public string SourceName => "Pulsar";

    public async Task<IReadOnlyList<BatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = PulsarHidLocator.FindCommandChannelPaths();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PulsarBatteryProvider] device lookup failed: {ex.Message}");
            return [];
        }

        foreach (var path in paths)
        {
            var percentage = await TryReadBatteryAsync(path, cancellationToken).ConfigureAwait(false);
            if (percentage is { } value)
            {
                return [new BatteryDevice("pulsar-mouse", SourceName, "Pulsar Mouse", BatteryDeviceKind.Mouse, value, IsCharging: false)];
            }
        }

        return []; // no Pulsar dongle plugged in, or none of its collections answered
    }

    private static async Task<int?> TryReadBatteryAsync(string path, CancellationToken cancellationToken)
    {
        using var handle = PulsarHidLocator.CreateFile(
            path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero,
            OpenExisting, flagsAndAttributes: 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        try
        {
            var response = await SendCommandAsync(handle, BatteryCommand1, BatteryCommand2, BatteryCommand3, cancellationToken)
                .ConfigureAwait(false);
            return response?[BatteryResponseOffset];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PulsarBatteryProvider] communication failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends one feature-report command and reads the reply. HidD_SetFeature/GetFeature are
    /// synchronous Win32 calls with no cancellable/overlapped variant, so the blocking work runs
    /// on a pool thread; if it doesn't finish within <see cref="CommandTimeout"/> this returns
    /// null (the abandoned background call is harmless).
    /// </summary>
    private static async Task<byte[]?> SendCommandAsync(
        SafeFileHandle handle, byte cmd1, byte cmd2, byte cmd3, CancellationToken cancellationToken)
    {
        var work = Task.Run(() =>
        {
            var request = BuildRequest(cmd1, cmd2, cmd3);
            if (!PulsarHidLocator.HidD_SetFeature(handle, request, request.Length))
            {
                return null;
            }

            Thread.Sleep(100); // matches the reference implementation's delay between set and get

            var response = new byte[65];
            response[0] = 0x00; // HidD_GetFeature requires the report id pre-set in byte[0]
            if (!PulsarHidLocator.HidD_GetFeature(handle, response, response.Length))
            {
                return null;
            }

            // The command we sent should be echoed back at response[2..4] on a genuine reply.
            return response[2] == cmd1 && response[3] == cmd2 && response[4] == cmd3 ? response : null;
        }, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CommandTimeout);

        var completed = await Task.WhenAny(work, Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);
        return completed == work ? await work.ConfigureAwait(false) : null;
    }

    /// <summary>
    /// Builds the 65-byte Windows feature report. Byte[0] is Windows' own report-id placeholder
    /// (this device uses HID report id 0, which Windows still requires a byte for); byte[1] is the
    /// reference implementation's own leading "report id" byte within its 64-byte packet, so the
    /// three command bytes land at [2..4] here versus [1..3] there. The checksum covers the same
    /// 62 bytes either way - just shifted by one to make room for Windows' placeholder.
    /// </summary>
    private static byte[] BuildRequest(byte cmd1, byte cmd2, byte cmd3)
    {
        var request = new byte[65];
        request[2] = cmd1;
        request[3] = cmd2;
        request[4] = cmd3;

        var checksum = 0;
        for (var i = 1; i < 63; i++)
        {
            checksum += request[i];
        }
        checksum &= 0xFFFF;

        request[63] = (byte)(checksum & 0xFF);
        request[64] = (byte)(checksum >> 8);
        return request;
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
}
