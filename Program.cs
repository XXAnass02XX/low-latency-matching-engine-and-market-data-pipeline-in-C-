using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        int port = 0; //  tcp://localhost:0

        // Start the listener and pass our callback method
        var listener = new SimpleTcpListener(port, (message, clientEndPoint) =>
        {
            Console.WriteLine($"[Callback Fired] Received from {clientEndPoint}: {message}");
        });

        CancellationTokenSource cts = new CancellationTokenSource();
        
        Console.WriteLine($"Listening on port {port}... Press Ctrl+C to stop.");
        Task listenerTask = listener.StartListeningAsync(cts.Token);

        // Keep the application running
        await Task.Delay(Timeout.Infinite, cts.Token);
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