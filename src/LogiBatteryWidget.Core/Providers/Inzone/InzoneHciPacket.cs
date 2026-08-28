namespace LogiBatteryWidget.Core.Providers.Inzone;

/// <summary>
/// Builds/parses the Sony INZONE dongle's control-channel packets (a Bluetooth-HCI-shaped, but
/// Sony-specific, protocol carried over 64-byte HID reports). Field layout and checksum rule are
/// from community reverse-engineering (penguinwokrs/openinzone's PROTOCOL.md, GPL-3.0 - this is an
/// independent implementation from that document's description of the wire format, not a copy of
/// its code) confirmed against real INZONE Buds hardware. Not independently verified against
/// hardware by this project.
/// </summary>
internal static class InzoneHciPacket
{
    private const byte KeyIdHigh = 0x96;
    private const byte KeyIdLow = 0xC3;
    private const byte AddressPc = 0x1;
    private const byte AddressReceiver = 0x4; // the earbuds/headset, as opposed to the dongle itself
    private const byte EventIdBattery = 0x04;
    private const byte EventTypeGet = 0x01;
    private const byte PacketTypeEvent = 0x04;

    /// <summary>Builds a full 64-byte HID output report requesting the current battery state.</summary>
    public static byte[] BuildBatteryGetReport(ushort transactionId)
    {
        byte destSrc = (AddressReceiver << 4) | AddressPc;
        Span<byte> command = stackalloc byte[]
        {
            0x01, 0x00, 0xFC, 8, // packet type, opcode (LE), data length (8 + param len 0)
            KeyIdHigh, KeyIdLow,
            destSrc,
            EventIdBattery,
            EventTypeGet,
            (byte)(transactionId & 0xFF), (byte)(transactionId >> 8), // transaction id, LE
            0, // checksum placeholder
        };
        command[^1] = Checksum(command[..^1], headerLength: 4);

        var report = new byte[64];
        report[0] = 0x02; // report id, always 0x02
        report[1] = (byte)command.Length;
        command.CopyTo(report.AsSpan(2));
        return report;
    }

    /// <summary>
    /// A device→PC battery event, if the given 64-byte HID input report contains one whose
    /// transaction id matches what we asked for. Ignores anything else (the device's own
    /// unsolicited notifications, replies to other requests, etc).
    /// </summary>
    public static BatteryReading? TryParseBatteryEvent(ReadOnlySpan<byte> report, ushort expectedTransactionId)
    {
        if (report.Length < 2 || report[0] != 0x02)
        {
            return null;
        }

        var payloadLength = report[1];
        if (payloadLength < 12 || 2 + payloadLength > report.Length)
        {
            return null;
        }

        var packet = report.Slice(2, payloadLength);

        // Event packet: [0]=0x04 type, [1]=0xFF code, [2]=dataLen, [3]=reserved, [4..5]=key id,
        // [6]=addr, [7]=event id, [8]=event type, [9..10]=txn id LE, [11..]=param, [last]=checksum.
        if (packet[0] != PacketTypeEvent || packet[4] != KeyIdHigh || packet[5] != KeyIdLow)
        {
            return null;
        }

        if (packet[7] != EventIdBattery)
        {
            return null; // some other event id - not for us
        }

        var transactionId = (ushort)(packet[9] | (packet[10] << 8));
        if (transactionId != expectedTransactionId)
        {
            return null; // an unsolicited notification, or a reply to an earlier/different request
        }

        var expectedChecksum = Checksum(packet[..^1], headerLength: 3);
        if (packet[^1] != expectedChecksum)
        {
            return null; // corrupted read
        }

        var param = packet[11..^1];
        return param.Length switch
        {
            // [status_left, percent_left, status_right, percent_right, status_case, percent_case]
            6 => new BatteryReading(
                LeftPercent: NormalizePercent(param[1]),
                RightPercent: NormalizePercent(param[3]),
                CasePercent: NormalizePercent(param[5]),
                IsHeadset: false),
            // headsets (non-earbud models) only report a single [status, percent] pair
            2 => new BatteryReading(
                LeftPercent: NormalizePercent(param[1]),
                RightPercent: null,
                CasePercent: null,
                IsHeadset: true),
            _ => null,
        };
    }

    /// <summary>0xFF means "not currently reporting" (e.g. an earbud that's out of the case and off).</summary>
    private static int? NormalizePercent(byte value) => value == 0xFF ? null : value;

    private static byte Checksum(ReadOnlySpan<byte> packetExcludingChecksum, int headerLength)
    {
        var sum = 0;
        for (var i = headerLength; i < packetExcludingChecksum.Length; i++)
        {
            sum += packetExcludingChecksum[i];
        }
        return (byte)(sum & 0xFF);
    }
}

/// <param name="IsHeadset">True when this came from a 2-byte (non-earbud) battery report, in which case only LeftPercent is meaningful.</param>
internal readonly record struct BatteryReading(int? LeftPercent, int? RightPercent, int? CasePercent, bool IsHeadset);
