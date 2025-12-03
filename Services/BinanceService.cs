using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CunaPay.Api.Services;

public class BinanceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BinanceService> _logger;
    private const string BINANCE_P2P_URL = "https://p2p.binance.com/bapi/c2c/v2/friendly/c2c/adv/search";

    public BinanceService(IHttpClientFactory httpClientFactory, ILogger<BinanceService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool Success, double? AveragePrice, string? Error)> GetAverageBuyPriceAsync(
        string asset = "USDT",
        string fiat = "BOB",
        int rows = 10)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("default");
            
            var payload = new
            {
                asset = asset,
                fiat = fiat,
                merchantCheck = false,
                page = 1,
                payTypes = new string[] { },
                publisherType = (string?)null,
                rows = rows,
                tradeType = "BUY"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, BINANCE_P2P_URL);
            request.Headers.Add("User-Agent", "Mozilla/5.0");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error consultando Binance P2P: {StatusCode}", response.StatusCode);
                return (false, null, $"Error consultando Binance: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(json);

            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                return (false, null, "No se encontró la propiedad 'data' en la respuesta");
            }

            var prices = new List<double>();
            
            foreach (var item in dataElement.EnumerateArray())
            {
                if (item.TryGetProperty("adv", out var advElement) &&
                    advElement.TryGetProperty("price", out var priceElement))
                {
                    if (double.TryParse(priceElement.GetString(), out var price))
                    {
                        prices.Add(price);
                    }
                }
            }

            if (prices.Count == 0)
            {
                return (false, null, "No se encontraron precios en la respuesta");
            }

            var average = prices.Average() / 100.0; // Binance devuelve el precio en centavos, dividir por 100
            return (true, average, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener promedio de Binance P2P");
            return (false, null, $"Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, double? AveragePrice, string? Error)> GetAverageSellPriceAsync(
        string asset = "USDT",
        string fiat = "BOB",
        int rows = 10)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("default");
            
            var payload = new
            {
                asset = asset,
                fiat = fiat,
                merchantCheck = false,
                page = 1,
                payTypes = new string[] { },
                publisherType = (string?)null,
                rows = rows,
                tradeType = "SELL"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, BINANCE_P2P_URL);
            request.Headers.Add("User-Agent", "Mozilla/5.0");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error consultando Binance P2P: {StatusCode}", response.StatusCode);
                return (false, null, $"Error consultando Binance: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(json);

            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                return (false, null, "No se encontró la propiedad 'data' en la respuesta");
            }

            var prices = new List<double>();
            
            foreach (var item in dataElement.EnumerateArray())
            {
                if (item.TryGetProperty("adv", out var advElement) &&
                    advElement.TryGetProperty("price", out var priceElement))
                {
                    if (double.TryParse(priceElement.GetString(), out var price))
                    {
                        prices.Add(price);
                    }
                }
            }

            if (prices.Count == 0)
            {
                return (false, null, "No se encontraron precios en la respuesta");
            }

            var average = prices.Average() / 100.0; // Binance devuelve el precio en centavos, dividir por 100
            return (true, average, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener promedio de Binance P2P");
            return (false, null, $"Error: {ex.Message}");
        }
    }
}

