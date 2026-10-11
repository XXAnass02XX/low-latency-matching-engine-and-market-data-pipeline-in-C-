using System.Runtime.InteropServices;

namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;

// Blittable (no references) so it can be copied to/from bytes directly. 36 bytes, packed.
// Must stay byte-for-byte identical to QuotePublisher.Contracts.Quote.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Quote : IEquatable<Quote>
{
    public readonly double Bid;
    public readonly double Ask;
    public readonly double Volume;
    public readonly int InstrumentId;
    public readonly long SendTimestamp; // Stopwatch.GetTimestamp() when published, for latency measurement

    public Quote(double bid, double ask)
        : this(bid, ask, 1000000, Instruments.Default, 0)
    {
    }

    public Quote(double bid, double ask, double volume, int instrumentId, long sendTimestamp)
    {
        Bid = bid;
        Ask = ask;
        Volume = volume;
        InstrumentId = instrumentId;
        SendTimestamp = sendTimestamp;
    }

    public Quote WithTimestamp(long sendTimestamp) => new(Bid, Ask, Volume, InstrumentId, sendTimestamp);

    public bool Equals(Quote other) => Bid == other.Bid && Ask == other.Ask;
    public override bool Equals(object? obj) => obj is Quote other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Bid, Ask);

    public override string ToString()
    {
        return $"bid : \"{Bid}\" - ask \"{Ask}\" instrument : {Instruments.Name(InstrumentId)}";
    }
}
