using low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;

namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Events;

public class MultiWriterRingBuffer
{
    private readonly Quote[] _buffer;

    // For each slot, the ticket number of the quote fully written there.
    // A reader trusts a slot only once this matches the sequence it wants.
    private readonly long[] _published;

    // The "deli ticket machine". It starts at -1 so the first ticket is 0.
    private long _writeSequence = -1;

    // A math trick to make wrapping around the circle lightning fast
    private readonly int _mask;

    // Size MUST be a power of 2 (e.g., 1024, 2048, 4096)
    public MultiWriterRingBuffer(int size)
    {
        if (size <= 0 || (size & (size - 1)) != 0)
            throw new ArgumentException("Size must be a power of 2", nameof(size));

        _buffer = new Quote[size];
        _published = new long[size];
        Array.Fill(_published, -1L);
        _mask = size - 1;
    }

    public void Publish(in Quote quote)
    {
        long ticketNumber = Interlocked.Increment(ref _writeSequence);

        // 2. Find our seat. If size is 1024, ticket 1025 maps back to slot 1
        long slotIndex = ticketNumber & _mask;
        _buffer[slotIndex] = quote;
        Volatile.Write(ref _published[slotIndex], ticketNumber);
    }
    
    public bool TryRead(long sequence, out Quote quote)
    {
        long slotIndex = sequence & _mask;
        if (Volatile.Read(ref _published[slotIndex]) != sequence)
        {
            quote = default;
            return false;
        }

        quote = _buffer[slotIndex];
        return true;
    }
}
