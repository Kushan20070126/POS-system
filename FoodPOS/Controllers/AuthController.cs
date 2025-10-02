using System;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace FoodPOS.Controllers
{
    public class AuthController : ApiController
    {
        private readonly string _connectionString = "Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;";

        [HttpPost]
        [Route("api/auth/login")]
        public async Task<IHttpActionResult> Login(LoginRequest request)
        {
            try
            {
                var user = await AuthenticateUser(request.Username, request.Password);

                if (user != null)
                {
                    var token = GenerateSimpleToken(user);
                    return Ok(new { success = true, token = token, user = user });
                }

                return Unauthorized();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("api/auth/health")]
        public IHttpActionResult Health()
        {
            return Ok(new { status = "Healthy", service = "AuthService" });
        }

        private async Task<User> AuthenticateUser(string username, string password)
        {
            var hashedPassword = HashPassword(password);
            var query = "SELECT UserId, Username, Role FROM Users WHERE Username = @Username AND PasswordHash = @PasswordHash AND IsActive = 1";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);
                    command.Parameters.AddWithValue("@PasswordHash", hashedPassword);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new User
                            {
                                UserId = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                Role = reader.GetString(2)
                            };
                        }
                    }
                }
            }

            return null;
        }

        private string GenerateSimpleToken(User user)
        {
            var tokenData = $"{user.UserId}:{user.Username}:{user.Role}:{DateTime.Now.Ticks}";
            var bytes = Encoding.UTF8.GetBytes(tokenData);
            return Convert.ToBase64String(bytes);
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
    }
}