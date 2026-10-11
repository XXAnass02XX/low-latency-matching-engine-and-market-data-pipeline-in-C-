using System;
using System.Diagnostics;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Xunit;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Contracts;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Events;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Listeners;

namespace low_latency_matching_engine.Tests
{
    public class QuoteCodecTests
    {
        [Fact]
        public void WriteThenRead_ReturnsSameQuote()
        {
            var quote = new Quote(1.1050, 1.1052, 250_000, Instruments.EURUSD, 123456789);
            var buffer = new byte[QuoteCodec.Size];

            QuoteCodec.Write(buffer, quote);
            var decoded = QuoteCodec.Read(buffer);

            Assert.Equal(QuoteCodec.Topic, buffer[0]);
            Assert.Equal(quote.Bid, decoded.Bid);
            Assert.Equal(quote.Ask, decoded.Ask);
            Assert.Equal(quote.Volume, decoded.Volume);
            Assert.Equal(quote.InstrumentId, decoded.InstrumentId);
            Assert.Equal(quote.SendTimestamp, decoded.SendTimestamp);
        }
    }

    public class MultiWriterRingBufferTests
    {
        [Fact]
        public void TryRead_ReturnsPublishedQuote_AndFalseForUnpublished()
        {
            var ring = new MultiWriterRingBuffer(8);
            var quote = new Quote(100, 101);

            Assert.False(ring.TryRead(0, out _));

            ring.Publish(quote);

            Assert.True(ring.TryRead(0, out var read));
            Assert.Equal(quote, read);
            Assert.False(ring.TryRead(1, out _));
        }

        [Fact]
        public void Constructor_RejectsNonPowerOfTwo()
        {
            Assert.Throws<ArgumentException>(() => new MultiWriterRingBuffer(10));
        }
    }

    public class QuoteListenerTests
    {
        [Fact]
        public void QuotePublishedOnSocket_EndsUpInRing()
        {
            // Arrange
            string endpoint = "tcp://127.0.0.1:18080"; // Use a high port to avoid conflicts
            using var pub = new PublisherSocket();
            pub.Bind(endpoint);

            var ring = new MultiWriterRingBuffer(1024);
            using var listener = new QuoteListener(endpoint, ring);
            listener.Start();

            var expected = new Quote(1.1050, 1.1052, 1_000_000, Instruments.EURUSD, 42);
            var frame = new byte[QuoteCodec.Size];
            QuoteCodec.Write(frame, expected);

            // Act: PUB/SUB drops messages until the subscription has reached the publisher,
            // so keep publishing until the listener has received one (2 s timeout)
            Quote received = default;
            bool gotIt = false;
            var timeout = Stopwatch.StartNew();
            while (!gotIt && timeout.ElapsedMilliseconds < 2000)
            {
                pub.SendFrame(frame);
                Thread.Sleep(20);
                gotIt = ring.TryRead(0, out received);
            }

            // Assert
            Assert.True(gotIt, "The listener did not push a quote into the ring within the timeout period.");
            Assert.Equal(expected, received);
            Assert.Equal(Instruments.EURUSD, received.InstrumentId);
        }
    }
}
