using low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Events;
using NetMQ;
using NetMQ.Sockets;

namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Listeners;

// Subscribes to the quote publisher (NetMQ PUB/SUB), decodes each binary frame
// into a Quote and pushes it into the ring buffer.
// Runs on its own dedicated thread: no thread pool, no async, no allocation per quote.
public class QuoteListener : IDisposable
{
    private readonly string _endpoint;
    private readonly MultiWriterRingBuffer _ring;
    private Thread? _thread;
    private volatile bool _stop;
    private long _droppedFrames;

    // Frames received with the wrong size (not a Quote). Non-zero means the publisher's format doesn't match.
    public long DroppedFrames => Volatile.Read(ref _droppedFrames);

    public QuoteListener(string endpoint, MultiWriterRingBuffer ring)
    {
        _endpoint = endpoint;
        _ring = ring;
    }

    public void Start()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "QuoteListener"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _thread?.Join();
    }

    public void Dispose() => Stop();

    private void Run()
    {
        using var sub = new SubscriberSocket();
        sub.Options.ReceiveHighWatermark = 100_000;
        sub.Connect(_endpoint);
        sub.Subscribe(QuoteCodec.TopicString);

        var msg = new Msg();
        msg.InitEmpty();
        try
        {
            while (!_stop)
            {
                // Timeout only so we can notice _stop; quotes are handled as soon as they arrive
                if (!sub.TryReceive(ref msg, TimeSpan.FromMilliseconds(100)))
                    continue;

                if (msg.Size == QuoteCodec.Size)
                    _ring.Publish(QuoteCodec.Read(msg.Slice()));
                else
                    Volatile.Write(ref _droppedFrames, _droppedFrames + 1); // wrong format, e.g. publisher out of date
            }
        }
        finally
        {
            msg.Close();
        }
    }
}
