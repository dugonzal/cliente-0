using System.Buffers.Binary;

namespace SnmpContracts.Telemetry;

/// <summary>One synchrophasor measured by a PMU: magnitude and phase angle.</summary>
public readonly record struct SynchrophasorPhasor(double Magnitude, double AngleDegrees);

/// <summary>
/// An IEEE C37.118-2011-style synchrophasor DATA frame value.
/// The frame is a self-describing, network-byte-order (big-endian) record carrying the
/// time tag (SOC + FRACSEC), a status word and one or more phasor measurements plus
/// frequency and its rate of change, exactly as a PMU streams to a PDC/NMS over UDP.
/// <para>
/// Wire layout (little machine endianness already normalized to big-endian on the wire):
///   <c>sync(2) framesize(2) streamId(2) soc(4) fracsec(4) stat(2) {mag(4) angle(2)}*n freq(4) dfreq(4)</c>
/// where <c>framesize</c> is the number of bytes following that field (so total = framesize + 4)
/// and <c>n</c> is derived from it. Angle is an int16 of 0.001 degrees; fracsec is microseconds.
/// </para>
/// <remarks>
/// This is the C37.118-2011 semantic model (fields, units, ordering) carried over UDP.
/// 2026: byte-for-byte conformance against a specific commercial PDC is validated in Phase 4
/// against real equipment; the round-trip encoder/decoder here is self-consistent.
/// </remarks>
public sealed record SynchrophasorFrame
{
    private const ushort DataSync = 0xAA51;   // data-frame marker (upper byte synched, lower byte = frame type + version)

    public required int StreamId { get; init; }
    public required uint Soc { get; init; }
    public required uint FracSecMicros { get; init; }
    public required ushort Status { get; init; }
    public required IReadOnlyList<SynchrophasorPhasor> Phasors { get; init; }
    public required float FrequencyHz { get; init; }
    public required float DfrequencyHzPerSec { get; init; }

    /// <summary>Bit 0 of the status word: the phasor measurement. Is time-synchronized/valid.</summary>
    public bool IsSyncValid => (Status & 0x0001) != 0;

    /// <summary>Encodes the frame into its wire byte representation.</summary>
    public byte[] Encode()
    {
        int n = Phasors.Count;
        int content = 2 + 4 + 4 + 2 + n * 6 + 4 + 4;   // streamId..dfreq, excludes the 4-byte sync+frameSize header
        byte[] buf = new byte[4 + content];
        int o = 0;

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), DataSync); o += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), (ushort)content); o += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), (ushort)StreamId); o += 2;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(o), Soc); o += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(o), FracSecMicros); o += 4;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(o), Status); o += 2;

        foreach (SynchrophasorPhasor p in Phasors)
        {
            BinaryPrimitives.WriteSingleBigEndian(buf.AsSpan(o), (float)p.Magnitude); o += 4;
            BinaryPrimitives.WriteInt16BigEndian(buf.AsSpan(o), (short)Math.Round(p.AngleDegrees * 1000.0)); o += 2;
        }

        BinaryPrimitives.WriteSingleBigEndian(buf.AsSpan(o), FrequencyHz); o += 4;
        BinaryPrimitives.WriteSingleBigEndian(buf.AsSpan(o), DfrequencyHzPerSec); o += 4;

        return buf;
    }

    /// <summary>Attempts to decode a DATA frame from the given bytes.</summary>
    /// <returns>The decoded frame, or <c>null</c> if the buffer is not a complete, valid frame.</returns>
    public static SynchrophasorFrame? TryDecode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16 + 4 + 4)
            return null;

        ushort sync = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        if (sync != DataSync)
            return null;

        ushort frameSize = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(2));
        // frameSize counts bytes after this field; a complete single-frame datagram ends here.
        if (bytes.Length < 4 + frameSize)
            return null;

        int o = 4;
        int streamId = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(o)); o += 2;
        uint soc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(o)); o += 4;
        uint fracSec = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(o)); o += 4;
        ushort status = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(o)); o += 2;

        // phasors occupy the region between the header (o) and the 8-byte freq/dfreq tail.
        int tail = 4 + frameSize - 8;
        List<SynchrophasorPhasor> phasors = new List<SynchrophasorPhasor>();
        while (o + 6 <= tail)
        {
            float mag = BinaryPrimitives.ReadSingleBigEndian(bytes.Slice(o)); o += 4;
            short angle = BinaryPrimitives.ReadInt16BigEndian(bytes.Slice(o)); o += 2;
            phasors.Add(new SynchrophasorPhasor(mag, angle / 1000.0));
        }

        if (o + 8 != 4 + frameSize)
            return null;

        float freq = BinaryPrimitives.ReadSingleBigEndian(bytes.Slice(o)); o += 4;
        float dfreq = BinaryPrimitives.ReadSingleBigEndian(bytes.Slice(o)); o += 4;

        return new SynchrophasorFrame
        {
            StreamId = streamId,
            Soc = soc,
            FracSecMicros = fracSec,
            Status = status,
            Phasors = phasors,
            FrequencyHz = freq,
            DfrequencyHzPerSec = dfreq,
        };
    }
}
