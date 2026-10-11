using System.Diagnostics;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Events;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Listeners;

class Program
{
    // Usage:
    //   dotnet run                         listen forever and print quotes
    //   dotnet run -- --check [count]      connectivity check: receive `count` quotes (default 10),
    //                                      validate them, exit 0 on success / 1 on failure
    //   add --port <n> to either (default 58965)
    static int Main(string[] args)
    {
        int port = 58965;
        int portArg = Array.IndexOf(args, "--port");
        if (portArg >= 0 && portArg + 1 < args.Length)
            port = int.Parse(args[portArg + 1]);

        // The publisher binds (@tcp://localhost:<port>), so we connect as a subscriber
        string endpoint = $"tcp://localhost:{port}";

        var ring = new MultiWriterRingBuffer(1 << 16);
        using var listener = new QuoteListener(endpoint, ring);
        listener.Start();

        int checkArg = Array.IndexOf(args, "--check");
        if (checkArg >= 0)
        {
            int count = checkArg + 1 < args.Length && int.TryParse(args[checkArg + 1], out var n) ? n : 10;
            return ConnectivityCheck.Run(endpoint, ring, listener, count, TimeSpan.FromSeconds(Math.Max(10, count * 2)));
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine($"Listening for quotes on {endpoint}... Press Ctrl+C to stop.");

        // Stand-in for the matching engine: read quotes from the ring in order
        long nextSequence = 0;
        var spinner = new SpinWait();
        while (!cts.IsCancellationRequested)
        {
            if (!ring.TryRead(nextSequence, out var quote))
            {
                // -1 = never fall back to Thread.Sleep(1), which costs ~15 ms on Windows.
                // Busy-spinning burns one CPU core but keeps latency in microseconds.
                spinner.SpinOnce(sleep1Threshold: -1);
                continue;
            }
            spinner.Reset();
            nextSequence++;

            double latencyUs = (Stopwatch.GetTimestamp() - quote.SendTimestamp) * 1_000_000.0 / Stopwatch.Frequency;
            Console.WriteLine($"[{nextSequence}] {quote} | latency {latencyUs:F1} us");
        }
        return 0;
    }
}

// Checks that a live publisher is reachable and sends quotes the engine can decode correctly.
static class ConnectivityCheck
{
    public static int Run(string endpoint, MultiWriterRingBuffer ring, QuoteListener listener, int count, TimeSpan timeout)
    {
        Console.WriteLine($"Connectivity check: waiting for {count} quotes on {endpoint} (timeout {timeout.TotalSeconds:F0} s)...");

        var errors = new List<string>();
        var latenciesUs = new List<double>();
        long sequence = 0;
        var clock = Stopwatch.StartNew();
        var spinner = new SpinWait();

        while (sequence < count && clock.Elapsed < timeout)
        {
            if (!ring.TryRead(sequence, out var quote))
            {
                // Spin rather than Thread.Sleep(1): sleeping adds ~15 ms on Windows and would skew the latency numbers
                spinner.SpinOnce(sleep1Threshold: -1);
                continue;
            }
            spinner.Reset();
            sequence++;

            double latencyUs = (Stopwatch.GetTimestamp() - quote.SendTimestamp) * 1_000_000.0 / Stopwatch.Frequency;
            latenciesUs.Add(latencyUs);
            Console.WriteLine($"  [{sequence}] {quote} | volume {quote.Volume} | latency {latencyUs:F1} us");

            // Sanity checks that would catch a garbled or misaligned decode
            if (!(quote.Bid > 0 && double.IsFinite(quote.Bid))) errors.Add($"#{sequence}: invalid bid {quote.Bid}");
            if (!(quote.Ask > quote.Bid && double.IsFinite(quote.Ask))) errors.Add($"#{sequence}: ask {quote.Ask} not above bid {quote.Bid}");
            if (!(quote.Volume > 0 && double.IsFinite(quote.Volume))) errors.Add($"#{sequence}: invalid volume {quote.Volume}");
            if (Instruments.Name(quote.InstrumentId).StartsWith('#')) errors.Add($"#{sequence}: unknown instrument id {quote.InstrumentId}");
            // Stopwatch timestamps are machine-wide, so this only holds when both run on the same machine
            if (latencyUs < 0 || latencyUs > 1_000_000) errors.Add($"#{sequence}: implausible latency {latencyUs:F0} us (bad SendTimestamp?)");
        }

        Console.WriteLine();
        if (sequence == 0)
        {
            errors.Add(listener.DroppedFrames > 0
                ? $"Connected, but all {listener.DroppedFrames} frames had the wrong size (expected {QuoteCodec.Size} bytes): publisher format doesn't match"
                : "No quotes received: is the publisher running on this port?");
        }
        else if (sequence < count)
        {
            errors.Add($"Only {sequence}/{count} quotes received before timeout");
        }
        if (listener.DroppedFrames > 0 && sequence > 0)
            errors.Add($"{listener.DroppedFrames} frames dropped for wrong size");

        if (latenciesUs.Count > 0)
        {
            latenciesUs.Sort();
            Console.WriteLine($"Latency: min {latenciesUs[0]:F1} us, median {latenciesUs[latenciesUs.Count / 2]:F1} us, max {latenciesUs[^1]:F1} us");
        }

        if (errors.Count == 0)
        {
            Console.WriteLine($"PASS: received {sequence} valid quotes from {endpoint}");
            return 0;
        }

        Console.WriteLine("FAIL:");
        foreach (var error in errors)
            Console.WriteLine($"  - {error}");
        return 1;
    }
}
