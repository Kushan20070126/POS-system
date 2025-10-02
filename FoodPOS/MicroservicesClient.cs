using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FoodPOS
{
    public static class MicroservicesClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string AUTH_SERVICE_URL = "http://localhost:5001";
        private const string PRODUCT_SERVICE_URL = "http://localhost:5002";
        private const string SALES_SERVICE_URL = "http://localhost:5003";

        static MicroservicesClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static async Task<LoginResponse> LoginAsync(string username, string password)
        {
            try
            {
                var loginRequest = new { Username = username, Password = password };
                var json = JsonConvert.SerializeObject(loginRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{AUTH_SERVICE_URL}/api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<LoginResponse>(responseContent);
                }

                return new LoginResponse { Success = false, Message = "Login failed" };
            }
            catch (Exception ex)
            {
                return new LoginResponse { Success = false, Message = ex.Message };
            }
        }

        public static async Task<bool> CheckAuthHealth()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{AUTH_SERVICE_URL}/api/auth/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> CheckProductsHealth()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{PRODUCT_SERVICE_URL}/api/products/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> CheckSalesHealth()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{SALES_SERVICE_URL}/api/sales/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public User User { get; set; }
        public string Message { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
    }
}