using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Http;
using Dapper;

namespace FoodPOS.Controllers
{
    public class ProductsController : ApiController
    {
        private readonly string _connectionString = "Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;";

        [HttpGet]
        [Route("api/products")]
        public async Task<IHttpActionResult> GetProducts()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var products = await connection.QueryAsync<Product>(
                        "SELECT * FROM Products WHERE IsActive = 1");

                    return Ok(products);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("api/products/health")]
        public IHttpActionResult Health()
        {
            return Ok(new { status = "Healthy", service = "ProductService" });
        }
    }

    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int CategoryId { get; set; }
        public int StockQuantity { get; set; }
        public string Barcode { get; set; }
        public bool IsActive { get; set; }
    }
}