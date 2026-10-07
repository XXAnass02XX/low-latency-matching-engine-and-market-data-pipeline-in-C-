using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

class Program
{
    static void Main(string[] args)
    {
        // The publisher binds (@tcp://localhost:58965), so we connect as a subscriber
        string endpoint = "tcp://localhost:58965";

        using var subSocket = new SubscriberSocket();
        subSocket.Connect(endpoint);
        subSocket.Subscribe("Quotes");

        Console.WriteLine($"Subscribed to 'Quotes' on {endpoint}... Press Ctrl+C to stop.");

        while (true)
        {
            string topic = subSocket.ReceiveFrameString();
            string quote = subSocket.ReceiveFrameString();
            Console.WriteLine($"[{topic}] {quote}");
        }
    }
}

public class SimpleTcpListener
{
    private readonly int _port;
    private readonly Action<string, EndPoint> _onMessageReceived;

    public SimpleTcpListener(int port, Action<string, EndPoint> onMessageReceived)
    {
        _port = port;
        _onMessageReceived = onMessageReceived;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        TcpListener server = new TcpListener(IPAddress.Any, _port);
        server.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for a client connection asynchronously
                TcpClient client = await server.AcceptTcpClientAsync(cancellationToken);

                // Handle each client in a separate background task so the listener isn't blocked
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        finally
        {
            server.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                EndPoint clientEndPoint = client.Client.RemoteEndPoint;

                // Fire the callback
                _onMessageReceived?.Invoke(message, clientEndPoint);
            }
        }
    }
}