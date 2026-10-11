using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;

// Wire format: one NetMQ frame = [topic byte 'Q'][36 bytes of Quote].
// The topic byte lets SUB sockets filter with Subscribe("Q").
public static class QuoteCodec
{
    public const byte Topic = (byte)'Q';
    public const string TopicString = "Q";
    public static readonly int Size = 1 + Unsafe.SizeOf<Quote>();

    public static void Write(Span<byte> dst, in Quote quote)
    {
        dst[0] = Topic;
        MemoryMarshal.Write(dst[1..], in quote);
    }

    public static Quote Read(ReadOnlySpan<byte> src) => MemoryMarshal.Read<Quote>(src[1..]);
}
