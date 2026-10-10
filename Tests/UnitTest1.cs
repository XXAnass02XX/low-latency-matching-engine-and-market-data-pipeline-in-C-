using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using low_latency_matching_engine_and_market_data_pipeline_in_C_.Listeners;

namespace low_latency_matching_engine.Tests
{
    public class QuoteTcpListenerTests
    {
        [Fact]
        public async Task QuoteReceived_ShouldFire_WhenDataIsSentToPort()
        {
            // Arrange
            int testPort = 18080; // Use a high port to avoid conflicts
            string expectedMessage = "EURUSD,1.1050,100";
            
            var quoteListener = new QuoteTcpListener(testPort);
            
            // Note: Assuming your SimpleTcpListener starts listening automatically in its constructor, 
            // or you have a Start() method you need to call here.
            // quoteListener.Start(); 

            // We use a TaskCompletionSource to wait for the event to fire asynchronously
            var eventCompletion = new TaskCompletionSource<(string Message, EndPoint Client)>();

            // Subscribe to the event
            quoteListener.QuoteReceived += (message, endPoint) =>
            {
                // When the event fires, complete the task with the received data
                eventCompletion.TrySetResult((message, endPoint));
            };

            // Act: Simulate a client connecting and sending data
            using (var tcpClient = new TcpClient())
            {
                // Connect to localhost on the test port
                await tcpClient.ConnectAsync(IPAddress.Loopback, testPort);
                
                using (var stream = tcpClient.GetStream())
                {
                    byte[] data = Encoding.UTF8.GetBytes(expectedMessage);
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }

            // Await the event firing, with a 2-second timeout so the test doesn't hang if it fails
            var completedTask = await Task.WhenAny(eventCompletion.Task, Task.Delay(2000));

            // Assert
            Assert.True(completedTask == eventCompletion.Task, "The event did not fire within the timeout period.");
            
            var result = await eventCompletion.Task;
            Assert.Equal(expectedMessage, result.Message);
            Assert.NotNull(result.Client);
        }
    }
}