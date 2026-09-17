using System.Buffers.Binary;

namespace SnmpContracts.Telemetry;

/// <summary>A single telecontrol point carried in an IEC 60870-5-104 ASDU.</summary>
public readonly record struct Iec104Point(int InfoObjectAddress, bool IsSinglePoint, double Value);

/// <summary>
/// An IEC 60870-5-104 message: a TCP APDU (0x68 start, length, control field, ASDU). The ASDU
/// carries one or more telecontrol points of a single type (M_SP_NA_1 single point or M_ME_NC_1 short
/// float measured value). This is the "DNP3-class" telecontrol stream a utility NMS ingests from an RTU.
/// <para>
/// Wire layout:
///   <c>start(0x68) len(1) ctl(4) typeId(1) qual(1) cot(2) ca(2) {ioa(3) value(n)}*count</c>
/// where <c>len</c> = 4 + ASDU length. Single point value is 1 byte; measured value is a 4-byte IEEE float.
/// CTL is an I-format frame carrying send/recv sequence numbers.
/// </para>
/// <remarks>Self-consistent encoder/decoder; interop with a real SCADA/PDC is validated in Phase 4.</remarks>
public sealed record Iec104Frame(
    uint SendSeq,
    uint RecvSeq,
    ushort CommonAddress,
    int CauseOfTransmission,
    IReadOnlyList<Iec104Point> Points)
{
    private const byte StartByte = 0x68;

    private static byte TypeIdOf(bool single) => single ? (byte)1 : (byte)13;   // M_SP_NA_1 / M_ME_NC_1

    /// <summary>Encodes the frame into its wire byte representation.</summary>
    public byte[] Encode()
    {
        if (Points.Count == 0)
            throw new ArgumentException("A 104 frame must carry at least one point.");

        bool single = Points[0].IsSinglePoint;
        if (Points.Any(p => p.IsSinglePoint != single))
            throw new ArgumentException("All points in one ASDU must share the same type.");

        int pointBytes = Points.Count * (single ? 4 : 7);   // 3-byte IOA + value
        int asduLen = 1 + 1 + 2 + 2 + pointBytes;           // typeId + qual + cot + ca + points
        int total = 2 + 4 + asduLen;                        // start + len + ctl + asdu

        byte[] buf = new byte[total];
        int o = 0;
        buf[o++] = StartByte;
        buf[o++] = (byte)(4 + asduLen);

        // I-format control field: send and recv sequence numbers packed into 4 bytes (LSB-first).
        uint ctl = (uint)((SendSeq << 1) & 0xFFFF) | ((uint)((RecvSeq << 1) & 0xFFFF) << 16);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(o), ctl); o += 4;

        buf[o++] = TypeIdOf(single);
        buf[o++] = (byte)Points.Count;                                       // qualifier: number of objects (SQ=0)
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), (ushort)CauseOfTransmission); o += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), CommonAddress); o += 2;

        foreach (Iec104Point p in Points)
        {
            // IOA: 3-byte little-endian address.
            buf[o++] = (byte)(p.InfoObjectAddress & 0xFF);
            buf[o++] = (byte)((p.InfoObjectAddress >> 8) & 0xFF);
            buf[o++] = (byte)((p.InfoObjectAddress >> 16) & 0xFF);
            if (single)
            {
                buf[o++] = p.Value != 0 ? (byte)1 : (byte)0;
            }
            else
            {
                BinaryPrimitives.WriteSingleBigEndian(buf.AsSpan(o), (float)p.Value);
                o += 4;
            }
        }

        return buf;
    }

    /// <summary>Attempts to decode a complete, valid 104 APDU.</summary>
    public static Iec104Frame? TryDecode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 + 4 + 1 + 1 + 2 + 2)
            return null;
        if (bytes[0] != StartByte)
            return null;

        int length = bytes[1];
        if (bytes.Length < 2 + length)
            return null;

        int o = 2;
        uint ctl = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(o)); o += 4;
        uint sendSeq = (ctl & 0xFFFF) >> 1;
        uint recvSeq = (ctl >> 16) >> 1;

        byte typeId = bytes[o++];
        byte count = bytes[o++];
        ushort cot = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(o)); o += 2;
        ushort ca = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(o)); o += 2;

        bool single = typeId == 1;
        List<Iec104Point> points = new List<Iec104Point>(count);
        for (int i = 0; i < count; i++)
        {
            int ioa = bytes[o] | (bytes[o + 1] << 8) | (bytes[o + 2] << 16); o += 3;
            if (single)
            {
                points.Add(new Iec104Point(ioa, true, bytes[o]));
                o += 1;
            }
            else
            {
                float val = BinaryPrimitives.ReadSingleBigEndian(bytes.Slice(o)); o += 4;
                points.Add(new Iec104Point(ioa, false, val));
            }
        }

        if (o != 2 + length)
            return null;

        return new Iec104Frame(sendSeq, recvSeq, ca, cot, points);
    }
}
