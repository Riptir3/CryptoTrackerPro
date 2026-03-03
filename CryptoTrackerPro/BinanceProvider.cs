using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;

namespace CryptoTrackerPro
{
    public interface ICryptoProvider
    {
        event Action<double> PriceUpdated;
        Task StartStreamingAsync(string symbol);
        Task StopStreamingAsync();
    }

    public class BinanceWebSocketProvider : ICryptoProvider
    {
        private ClientWebSocket _webSocket;
        public event Action<double> PriceUpdated;
        private CancellationTokenSource _cts;

        public async Task StartStreamingAsync(string symbol)
        {
            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            string url = $"wss://stream.binance.com:9443/ws/{symbol.ToLower()}@trade";

            await _webSocket.ConnectAsync(new Uri(url), _cts.Token);

            _ = Task.Run(async () =>
            {
                var buffer = new byte[1024 * 4];
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    var jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    var data = JObject.Parse(jsonString);
                    if (data["p"] != null) 
                    {
                        double price = data["p"].Value<double>();
                        PriceUpdated?.Invoke(price);
                    }
                }
            });
        }

        public async Task StopStreamingAsync()
        {
            _cts?.Cancel();
            _webSocket?.Abort(); 
            _webSocket?.Dispose();
        }
    }
}
