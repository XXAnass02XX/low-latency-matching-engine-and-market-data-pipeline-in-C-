namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;

// Instruments travel on the wire as int ids (no strings on the hot path).
// Must stay in sync with QuotePublisher.Contracts.Instruments.
public static class Instruments
{
    public const int Default = 0;
    public const int EURUSD = 1;
    public const int GBPUSD = 2;
    public const int USDJPY = 3;
    public const int MSFT = 4;

    private static readonly string[] Names = ["Default", "EURUSD", "GBPUSD", "USDJPY", "MSFT"];

    public static string Name(int id) => (uint)id < (uint)Names.Length ? Names[id] : $"#{id}";
}
