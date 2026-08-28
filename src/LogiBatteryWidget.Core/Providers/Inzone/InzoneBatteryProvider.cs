using System.Diagnostics;
using LogiBatteryWidget.Core.Models;

namespace LogiBatteryWidget.Core.Providers.Inzone;

/// <summary>
/// Reads battery status directly from a Sony INZONE dongle's raw USB HID control channel -
/// no INZONE Hub required (the two can coexist; the device allows more than one open handle).
/// There is no official API for this; the wire protocol is undocumented by Sony. This
/// implementation is based on the community reverse-engineering writeup in
/// penguinwokrs/openinzone's PROTOCOL.md (GPL-3.0) - an independent implementation from that
/// document's description of the wire format, not a copy of its code.
///
/// UNVERIFIED ON REAL HARDWARE: this project's dev machine has no INZONE device to test against.
/// If parsing looks wrong against a real dongle, the checksum/field layout is in
/// <see cref="InzoneHciPacket"/> and the device discovery is in <see cref="InzoneHidLocator"/>.
/// </summary>
public sealed class InzoneBatteryProvider : IBatteryProvider
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(2);

    public string SourceName => "INZONE";

    public async Task<IReadOnlyList<BatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        string? path;
        try
        {
            path = InzoneHidLocator.FindControlChannelPath();
        }
        catch (Exception ex)
        {
            // Device enumeration touches a fair amount of raw Win32 API surface; never let a
            // failure there take down the rest of the app's battery polling.
            Debug.WriteLine($"[InzoneBatteryProvider] device lookup failed: {ex.Message}");
            return [];
        }

        if (path is null)
        {
            return []; // no INZONE dongle plugged in right now
        }

        using var handle = InzoneHidLocator.CreateFile(
            path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero,
            OpenExisting, FileFlagOverlapped, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Debug.WriteLine("[InzoneBatteryProvider] found the device but couldn't open it");
            return [];
        }

        try
        {
            await using var stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize: 64, isAsync: true);

            var transactionId = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            var request = InzoneHciPacket.BuildBatteryGetReport(transactionId);
            await stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ResponseTimeout);

            var buffer = new byte[64];
            while (true)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return []; // no reply in time - not treated as an error, just nothing to report
                }

                if (bytesRead == 0)
                {
                    return [];
                }

                var reading = InzoneHciPacket.TryParseBatteryEvent(buffer.AsSpan(0, bytesRead), transactionId);
                if (reading is { } value)
                {
                    return ToDevices(value);
                }

                // Otherwise this was an unrelated notification (the device pushes these on its
                // own) - keep reading until our reply arrives or the timeout above fires.
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InzoneBatteryProvider] communication failed: {ex.Message}");
            return [];
        }
    }

    private IReadOnlyList<BatteryDevice> ToDevices(BatteryReading reading)
    {
        if (reading.IsHeadset)
        {
            return reading.LeftPercent is { } headsetPercent
                ? [new BatteryDevice("inzone-headset", SourceName, "INZONE Headset", BatteryDeviceKind.Headset, headsetPercent, IsCharging: false)]
                : [];
        }

        var devices = new List<BatteryDevice>(3);
        if (reading.LeftPercent is { } left)
        {
            devices.Add(new BatteryDevice("inzone-buds-left", SourceName, "INZONE Buds (L)", BatteryDeviceKind.Headset, left, IsCharging: false));
        }
        if (reading.RightPercent is { } right)
        {
            devices.Add(new BatteryDevice("inzone-buds-right", SourceName, "INZONE Buds (R)", BatteryDeviceKind.Headset, right, IsCharging: false));
        }
        if (reading.CasePercent is { } caseCharge)
        {
            devices.Add(new BatteryDevice("inzone-buds-case", SourceName, "INZONE Buds (Case)", BatteryDeviceKind.Headset, caseCharge, IsCharging: false));
        }
        return devices;
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
}
