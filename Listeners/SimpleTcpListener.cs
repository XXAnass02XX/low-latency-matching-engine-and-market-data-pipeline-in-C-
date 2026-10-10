using System.Net;
using System.Net.Sockets;
using System.Text;

namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Listeners;

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