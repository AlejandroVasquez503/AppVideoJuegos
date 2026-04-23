using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace appWeb2.Services
{
    public class PayPalService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private string _accessToken;

        public PayPalService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var clientId = _configuration["PayPal:ClientId"];
            var clientSecret = _configuration["PayPal:ClientSecret"];
            var mode = _configuration["PayPal:Mode"];
            var baseUrl = mode == "sandbox" ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token");
            request.Headers.Add("Authorization", $"Basic {auth}");
            request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            return json.RootElement.GetProperty("access_token").GetString();
        }

        public async Task<string> CreateOrderAsync(decimal amount, string description, string returnUrl, string cancelUrl)
        {
            try
            {
                _accessToken = await GetAccessTokenAsync();
                var mode = _configuration["PayPal:Mode"];
                var baseUrl = mode == "sandbox" ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

                var orderData = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            amount = new
                            {
                                currency_code = "USD",
                                value = amount.ToString("F2")
                            },
                            description = description
                        }
                    },
                    application_context = new
                    {
                        return_url = returnUrl,
                        cancel_url = cancelUrl,
                        user_action = "PAY_NOW"
                    }
                };

                var json = JsonSerializer.Serialize(orderData);
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                return jsonDoc.RootElement.GetProperty("id").GetString();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al crear la orden en PayPal", ex);
            }
        }

        public async Task<JsonElement> CaptureOrderAsync(string orderId)
        {
            try
            {
                Console.WriteLine($"=== PayPalService: Iniciando captura de orden {orderId} ===");
                
                _accessToken = await GetAccessTokenAsync();
                var mode = _configuration["PayPal:Mode"];
                var baseUrl = mode == "sandbox" ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

                Console.WriteLine($"PayPalService: Modo = {mode}, BaseUrl = {baseUrl}");
                Console.WriteLine($"PayPalService: AccessToken obtenido (primeros 10 chars): {_accessToken?.Substring(0, Math.Min(10, _accessToken?.Length ?? 0))}...");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders/{orderId}/capture");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                Console.WriteLine($"PayPalService: Enviando request a {request.RequestUri}");

                var response = await _httpClient.SendAsync(request);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"PayPalService: Response Status: {response.StatusCode}");
                Console.WriteLine($"PayPalService: Response Content: {responseContent}");

                response.EnsureSuccessStatusCode();

                Console.WriteLine($"PayPalService: Captura exitosa para orden {orderId}");
                return JsonDocument.Parse(responseContent).RootElement;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PayPalService: ERROR capturando orden {orderId}: {ex.Message}");
                Console.WriteLine($"PayPalService: StackTrace: {ex.StackTrace}");
                throw new Exception($"Error al capturar la orden {orderId}", ex);
            }
        }

        public async Task<JsonElement> GetOrderAsync(string orderId)
        {
            try
            {
                _accessToken = await GetAccessTokenAsync();
                var mode = _configuration["PayPal:Mode"];
                var baseUrl = mode == "sandbox" ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

                var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v2/checkout/orders/{orderId}");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(content).RootElement;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener detalles de la orden {orderId}", ex);
            }
        }
    }
}
