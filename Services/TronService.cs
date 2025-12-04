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
            _config = config;
            _logger = logger;

            _tronApi = _config["Tron:ApiUrl"] ?? "https://tron-api.onrender.com";
            _accessToken = _config["Tron:AccessToken"] ?? "";

            // Crear HttpClient con configuración adecuada
            _http = factory.CreateClient("default");
            _http.Timeout = TimeSpan.FromSeconds(30); // Timeout de 30 segundos
            
            // Configurar headers
            if (!string.IsNullOrWhiteSpace(_accessToken))
            {
                _http.DefaultRequestHeaders.Add("x-api-key", _accessToken);
            }
            
            // Headers adicionales
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
            _http.DefaultRequestHeaders.Add("User-Agent", "CunaPay-API/1.0");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _logger.LogInformation("TronService initialized. API URL: {ApiUrl}", _tronApi);
        }

        // --------------------------------------------------------------
        // CREATE WALLET
        // --------------------------------------------------------------
        public async Task<(string Address, string PrivateKey)> CreateWalletAsync()
        {
            try
            {
                var url = $"{_tronApi}/wallet/create";
                _logger.LogDebug("Calling Tron API: {Url}", url);
                
                var result = await _http.GetFromJsonAsync<JsonElement>(url);

                var address = result.GetProperty("address").GetString();
                var privateKey = result.GetProperty("privateKey").GetString();

                if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(privateKey))
                {
                    throw new InvalidOperationException("Invalid response from Tron API: missing address or privateKey");
                }

                _logger.LogDebug("Wallet created successfully. Address: {Address}", address);
                return (address, privateKey);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Tron API: {Url}", $"{_tronApi}/wallet/create");
                throw new InvalidOperationException($"Failed to connect to Tron API: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout calling Tron API: {Url}", $"{_tronApi}/wallet/create");
                throw new InvalidOperationException("Tron API request timed out", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Tron API: {Url}", $"{_tronApi}/wallet/create");
                throw;
            }
        }

        // --------------------------------------------------------------
        // GET ADDRESS FROM PRIVATE KEY
        // --------------------------------------------------------------
        public async Task<string> GetAddressFromPrivateKeyAsync(string privateKey)
        {
            try
            {
                var url = $"{_tronApi}/wallet/address-from-key/{privateKey}";
                _logger.LogDebug("Getting address from private key");
                
                var result = await _http.GetFromJsonAsync<JsonElement>(url);

                var address = result.GetProperty("address").GetString();
                if (string.IsNullOrEmpty(address))
                {
                    throw new InvalidOperationException("Invalid response from Tron API: missing address");
                }

                return address;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting address from private key");
                throw new InvalidOperationException($"Failed to get address from Tron API: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout getting address from private key");
                throw new InvalidOperationException("Tron API request timed out", ex);
            }
        }

        // --------------------------------------------------------------
        // VALIDATE ADDRESS
        // --------------------------------------------------------------
        public async Task<bool> IsValidAddressAsync(string address)
        {
            try
            {
                var url = $"{_tronApi}/wallet/isAddress/{address}";
                _logger.LogDebug("Validating address: {Address}", address);
                
                var json = await _http.GetFromJsonAsync<JsonElement>(url);

                var isValid = json.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                _logger.LogDebug("Address validation result: {IsValid}", isValid);
                
                return isValid;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error validating address: {Address}", address);
                return false; // En caso de error, retornar false en lugar de lanzar excepción
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout validating address: {Address}", address);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating address: {Address}", address);
                return false;
            }
        }

        // --------------------------------------------------------------
        // GET TRX BALANCE
        // --------------------------------------------------------------
        public async Task<decimal> GetTrxBalanceAsync(string address)
        {
            try
            {
                var url = $"{_tronApi}/wallet/balance/{address}";
                _logger.LogInformation("Getting TRX balance for address: {Address} from URL: {Url}", address, url);
                
                var response = await _http.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("TRX balance API response: Status={Status}, Body={Body}", response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Tron API returned error status {Status} for TRX balance. Body: {Body}", 
                        response.StatusCode, responseContent);
                    return 0m;
                }

                using var doc = JsonDocument.Parse(responseContent);
                var json = doc.RootElement;

                // Intentar diferentes formatos de respuesta
                decimal balance = 0m;
                
                if (json.TryGetProperty("balance", out var balanceProp))
                {
                    if (balanceProp.ValueKind == JsonValueKind.Number)
                    {
                        balance = balanceProp.GetDecimal();
                    }
                    else if (balanceProp.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(balanceProp.GetString(), out var parsedBalance))
                        {
                            balance = parsedBalance;
                        }
                    }
                }
                else if (json.TryGetProperty("trx", out var trxProp))
                {
                    if (trxProp.ValueKind == JsonValueKind.Number)
                    {
                        balance = trxProp.GetDecimal();
                    }
                    else if (trxProp.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(trxProp.GetString(), out var parsedBalance))
                        {
                            balance = parsedBalance;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("TRX balance response does not contain 'balance' or 'trx' property. Response: {Response}", responseContent);
                }
                
                _logger.LogInformation("TRX balance: {Balance} for address: {Address}", balance, address);
                return balance;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting TRX balance for address: {Address}. Error: {Message}", address, ex.Message);
                return 0m;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout getting TRX balance for address: {Address}", address);
                return 0m;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error getting TRX balance for address: {Address}. Response may be invalid.", address);
                return 0m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting TRX balance for address: {Address}. Error: {Message}", address, ex.Message);
                return 0m;
            }
        }

        // --------------------------------------------------------------
        // GET USDT BALANCE
        // --------------------------------------------------------------
        public async Task<decimal> GetUsdtBalanceAsync(string address)
        {
            try
            {
                var url = $"{_tronApi}/wallet/usdt/{address}";
                _logger.LogInformation("Getting USDT balance for address: {Address} from URL: {Url}", address, url);
                
                var response = await _http.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("USDT balance API response: Status={Status}, Body={Body}", response.StatusCode, responseContent);

                // Si el status code no es exitoso, intentar parsear el error
                if (!response.IsSuccessStatusCode)
                {
                    // Intentar parsear la respuesta para ver si tiene información útil
                    string? errorMessage = null;
                    try
                    {
                        using var errorDoc = JsonDocument.Parse(responseContent);
                        var errorRoot = errorDoc.RootElement;
                        
                        if (errorRoot.TryGetProperty("error", out var errorProp))
                        {
                            errorMessage = errorProp.GetString();
                        }
                        else if (errorRoot.TryGetProperty("ok", out var okProp) && okProp.GetBoolean() == false)
                        {
                            // Si ok: false, buscar el mensaje de error
                            if (errorRoot.TryGetProperty("message", out var msgProp))
                            {
                                errorMessage = msgProp.GetString();
                            }
                        }
                    }
                    catch
                    {
                        // Si no se puede parsear, usar el contenido completo
                        errorMessage = responseContent;
                    }
                    
                    _logger.LogError(
                        "Tron API returned error status {Status} for USDT balance. Address: {Address}. Error: {ErrorMessage}. Full response: {Body}", 
                        response.StatusCode, address, errorMessage ?? "Unknown error", responseContent);
                    
                    return 0m;
                }

                using var doc = JsonDocument.Parse(responseContent);
                var json = doc.RootElement;

                // Intentar diferentes formatos de respuesta
                decimal balance = 0m;
                
                if (json.TryGetProperty("balance", out var balanceProp))
                {
                    if (balanceProp.ValueKind == JsonValueKind.Number)
                    {
                        balance = balanceProp.GetDecimal();
                    }
                    else if (balanceProp.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(balanceProp.GetString(), out var parsedBalance))
                        {
                            balance = parsedBalance;
                        }
                    }
                }
                else if (json.TryGetProperty("usdt", out var usdtProp))
                {
                    if (usdtProp.ValueKind == JsonValueKind.Number)
                    {
                        balance = usdtProp.GetDecimal();
                    }
                    else if (usdtProp.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(usdtProp.GetString(), out var parsedBalance))
                        {
                            balance = parsedBalance;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("USDT balance response does not contain 'balance' or 'usdt' property. Response: {Response}", responseContent);
                }
                
                _logger.LogInformation("USDT balance: {Balance} for address: {Address}", balance, address);
                return balance;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting USDT balance for address: {Address}. Error: {Message}", address, ex.Message);
                return 0m;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout getting USDT balance for address: {Address}", address);
                return 0m;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error getting USDT balance for address: {Address}. Response may be invalid.", address);
                return 0m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting USDT balance for address: {Address}. Error: {Message}", address, ex.Message);
                return 0m;
            }
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
            try
            {
                var url = $"{_tronApi}/wallet/usdt/send";
                _logger.LogInformation("Sending USDT: {Amount} from {From} to {To}", amount, from, to);
                
                var payload = new
                {
                    from,
                    pk = privateKey,
                    to,
                    amount
                };

                var response = await _http.PostAsJsonAsync(url, payload);
                var json = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Tron API response: Status={Status}, Body={Body}", response.StatusCode, json);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Tron API returned error status: {Status}, Body: {Body}", response.StatusCode, json);
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                string? txid = root.TryGetProperty("txid", out var txidProp) ? txidProp.GetString() : null;
                string? error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;

                if (ok && !string.IsNullOrEmpty(txid))
                {
                    _logger.LogInformation("USDT sent successfully. TXID: {Txid}", txid);
                }
                else
                {
                    _logger.LogWarning("USDT send failed. Error: {Error}", error ?? "Unknown error");
                }

                return (ok, txid, error);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error sending USDT to Tron API");
                return (false, null, $"HTTP error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout sending USDT to Tron API");
                return (false, null, "Request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending USDT to Tron API");
                return (false, null, $"Exception: {ex.Message}");
            }
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
            try
            {
                var url = $"{_tronApi}/wallet/tx/{txid}";
                _logger.LogDebug("Getting transaction info for TXID: {Txid}", txid);
                
                var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Tron API returned error status {Status} for TXID: {Txid}", response.StatusCode, txid);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting transaction info for TXID: {Txid}", txid);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout getting transaction info for TXID: {Txid}", txid);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction info for TXID: {Txid}", txid);
                return null;
            }
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
            try
            {
                var url = $"{_tronApi}/wallet/trc20/{address}?limit={limit}";

                if (!string.IsNullOrEmpty(fingerprint))
                    url += $"&fingerprint={fingerprint}";

                _logger.LogDebug("Getting TRC20 transfers for address: {Address}", address);
                
                var response = await _http.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Tron API returned error status {Status} for TRC20 transfers. Body: {Body}", 
                        response.StatusCode, errorContent);
                    response.EnsureSuccessStatusCode();
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting TRC20 transfers for address: {Address}", address);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout getting TRC20 transfers for address: {Address}", address);
                throw;
            }
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
            try
            {
                var url = $"{_tronApi}/wallet/transactions/{address}?limit={limit}";

                if (!string.IsNullOrEmpty(fingerprint))
                    url += $"&fingerprint={fingerprint}";

                _logger.LogDebug("Getting TRX transactions for address: {Address}", address);
                
                var response = await _http.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Tron API returned error status {Status} for TRX transactions. Body: {Body}", 
                        response.StatusCode, errorContent);
                    response.EnsureSuccessStatusCode();
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error getting TRX transactions for address: {Address}", address);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout getting TRX transactions for address: {Address}", address);
                throw;
            }
        }
    }
}