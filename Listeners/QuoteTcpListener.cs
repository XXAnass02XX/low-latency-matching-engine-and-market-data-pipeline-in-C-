namespace low_latency_matching_engine_and_market_data_pipeline_in_C_.Listeners;

public class QuoteTcpListener
{
    private SimpleTcpListener listener;
    private int port;
    public event Action<string, System.Net.EndPoint> QuoteReceived;
    
    public QuoteTcpListener(int port)
    {
        this.port = port;
        listener = new SimpleTcpListener(port, (message, clientEndPoint) => QuoteReceived?.Invoke(message, clientEndPoint));
    }
}