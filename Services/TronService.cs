using System.Net.Http.Json;
using System.Text.Json;

namespace CunaPay.Api.Services
{
    public class TronService
    {
        private readonly HttpClient _http;
        private readonly string _tronApi;
        private readonly string _accessToken;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfiguration _config;
        private readonly ILogger<TronService> _logger;

        public TronService(IHttpClientFactory factory, IConfiguration config, ILogger<TronService> logger)
        {
            _http = factory.CreateClient();
            _config = config;
            _logger = logger;

            _tronApi = _config["Tron:ApiUrl"] ?? "https://tron-api.onrender.com";
            _accessToken = _config["Tron:AccessToken"] ?? "";

            if (!string.IsNullOrWhiteSpace(_accessToken))
                _http.DefaultRequestHeaders.Add("x-api-key", _accessToken);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        // --------------------------------------------------------------
        // CREATE WALLET
        // --------------------------------------------------------------
        public async Task<(string Address, string PrivateKey)> CreateWalletAsync()
        {
            var result = await _http.GetFromJsonAsync<JsonElement>($"{_tronApi}/wallet/create");

            return (
                result.GetProperty("address").GetString()!,
                result.GetProperty("privateKey").GetString()!
            );
        }

        // --------------------------------------------------------------
        // GET ADDRESS FROM PRIVATE KEY
        // --------------------------------------------------------------
        public async Task<string> GetAddressFromPrivateKeyAsync(string privateKey)
        {
            var result = await _http.GetFromJsonAsync<JsonElement>($"{_tronApi}/wallet/address-from-key/{privateKey}");

            return result.GetProperty("address").GetString()!;
        }

        // --------------------------------------------------------------
        // VALIDATE ADDRESS
        // --------------------------------------------------------------
        public async Task<bool> IsValidAddressAsync(string address)
        {
            var json = await _http.GetFromJsonAsync<JsonElement>($"{_tronApi}/wallet/isAddress/{address}");

            return json.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        }

        // --------------------------------------------------------------
        // GET TRX BALANCE
        // --------------------------------------------------------------
        public async Task<decimal> GetTrxBalanceAsync(string address)
        {
            var json = await _http.GetFromJsonAsync<JsonElement>($"{_tronApi}/wallet/balance/{address}");

            return json.TryGetProperty("balance", out var balanceProp)
                ? balanceProp.GetDecimal()
                : 0m;
        }

        // --------------------------------------------------------------
        // GET USDT BALANCE
        // --------------------------------------------------------------
        public async Task<decimal> GetUsdtBalanceAsync(string address)
        {
            var json = await _http.GetFromJsonAsync<JsonElement>($"{_tronApi}/wallet/usdt/{address}");

            return json.TryGetProperty("balance", out var balanceProp)
                ? balanceProp.GetDecimal()
                : 0m;
        }

        // --------------------------------------------------------------
        // SEND USDT (TRC20)
        // --------------------------------------------------------------
        public async Task<(bool ok, string? txid, string? error)> SendUsdtAsync(
            string from,
            string privateKey,
            string to,
            decimal amount
        )
        {
            var payload = new
            {
                from,
                pk = privateKey,
                to,
                amount
            };

            var response = await _http.PostAsJsonAsync($"{_tronApi}/wallet/usdt/send", payload);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
            string? txid = root.TryGetProperty("txid", out var txidProp) ? txidProp.GetString() : null;
            string? error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;

            return (ok, txid, error);
        }

        // --------------------------------------------------------------
        // SEND TRX
        // --------------------------------------------------------------
        public async Task<(bool ok, string? txid, string? error)> SendTrxAsync(
            string from,
            string privateKey,
            string to,
            decimal amount
        )
        {
            try
            {
                // Formatear el monto como número con máximo 6 decimales para evitar problemas de precisión
                // El API de Tron espera recibir TRX (decimal) y lo convierte internamente a SUN (entero)
                // Ejemplo: 150 TRX → API lo convierte a 150,000,000 SUN → Blockchain
                var formattedAmount = Math.Round(amount, 6);
                
                _logger.LogInformation(
                    "Sending TRX: {Amount} TRX from {From} to {To} (will be converted to {SunAmount} SUN by API)",
                    formattedAmount, from, to, formattedAmount * 1_000_000);
                
                var payload = new
                {
                    from,
                    pk = privateKey,
                    to,
                    amount = formattedAmount
                };

                var response = await _http.PostAsJsonAsync($"{_tronApi}/wallet/trx/send", payload);
                var json = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("TRX send API response: Status={Status}, Body={Body}", response.StatusCode, json);

                // Si la respuesta HTTP no es exitosa, intentar parsear el error
                if (!response.IsSuccessStatusCode)
                {
                    try
                    {
                        using var errorDoc = JsonDocument.Parse(json);
                        var errorRoot = errorDoc.RootElement;
                        var httpErrorMsg = errorRoot.TryGetProperty("error", out var httpErrorProp) ? httpErrorProp.GetString() : null;
                        return (false, null, httpErrorMsg ?? $"HTTP {response.StatusCode}: {json}");
                    }
                    catch
                    {
                        return (false, null, $"HTTP {response.StatusCode}: {json}");
                    }
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                string? txid = root.TryGetProperty("txid", out var txidProp) ? txidProp.GetString() : null;
                string? errorMsg = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;

                if (!ok && string.IsNullOrEmpty(errorMsg))
                {
                    errorMsg = "Transaction failed without error message";
                }

                return (ok, txid, errorMsg);
            }
            catch (Exception ex)
            {
                return (false, null, $"Exception sending TRX: {ex.Message}");
            }
        }

        // --------------------------------------------------------------
        // GET TRANSACTION INFO
        // --------------------------------------------------------------
        public async Task<JsonElement?> GetTransactionInfoAsync(string txid)
        {
            var url = $"{_tronApi}/wallet/tx/{txid}";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);
        }

        // --------------------------------------------------------------
        // TRC20 TRANSFERS (TronGrid)
        // --------------------------------------------------------------
        public async Task<JsonElement> GetTrc20TransfersAsync(
            string address,
            int limit = 50,
            string? fingerprint = null
        )
        {
            var url = $"{_tronApi}/wallet/trc20/{address}?limit={limit}";

            if (!string.IsNullOrEmpty(fingerprint))
                url += $"&fingerprint={fingerprint}";

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        }

        // --------------------------------------------------------------
        // TRX NATIVE TRANSACTIONS (TronGrid)
        // --------------------------------------------------------------
        public async Task<JsonElement> GetTrxTransactionsAsync(
            string address,
            int limit = 50,
            string? fingerprint = null
        )
        {
            var url = $"{_tronApi}/wallet/transactions/{address}?limit={limit}";

            if (!string.IsNullOrEmpty(fingerprint))
                url += $"&fingerprint={fingerprint}";

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        }
    }
}