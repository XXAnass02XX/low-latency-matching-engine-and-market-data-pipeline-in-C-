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