using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace CryptoTrackerPro
{
    public interface ICryptoProvider
    {
        Task<double> GetPriceAsync(string symbol);
    }
    public class BinanceProvider : ICryptoProvider
    {
        private readonly HttpClient _httpClient;
        public BinanceProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<double> GetPriceAsync(string symbol)
        {
            var response = await _httpClient.GetStringAsync($"https://api.binance.com/api/v3/ticker/price?symbol={symbol}");
            var json = JObject.Parse(response);
            return json["price"]!.Value<double>();
        }
    }
}
