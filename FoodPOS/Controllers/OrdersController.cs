 using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Http;

namespace FoodPOS.Controllers
{
    public class SalesController : ApiController
    {
        private readonly string _connectionString = "Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;";

        [HttpPost]
        [Route("api/sales/order")]
        public async Task<IHttpActionResult> CreateOrder(OrderRequest request)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Create order
                            var orderId = await connection.ExecuteScalarAsync<int>(
                                @"INSERT INTO Orders (OrderNumber, UserId, Subtotal, TaxAmount, DiscountAmount, TotalAmount, PaymentMethod) 
                                  OUTPUT INSERTED.OrderId
                                  VALUES (@OrderNumber, @UserId, @Subtotal, @TaxAmount, @DiscountAmount, @TotalAmount, @PaymentMethod)",
                                new
                                {
                                    OrderNumber = GenerateOrderNumber(),
                                    request.UserId,
                                    request.Subtotal,
                                    request.TaxAmount,
                                    request.DiscountAmount,
                                    request.TotalAmount,
                                    request.PaymentMethod
                                }, transaction);

                            // Add order items
                            foreach (var item in request.OrderItems)
                            {
                                await connection.ExecuteAsync(
                                    @"INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, TotalPrice) 
                                      VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice, @TotalPrice)",
                                    new
                                    {
                                        OrderId = orderId,
                                        item.ProductId,
                                        item.Quantity,
                                        item.UnitPrice,
                                        item.TotalPrice
                                    }, transaction);
                            }

                            transaction.Commit();
                            return Ok(new { orderId, message = "Order created successfully" });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("api/sales/health")]
        public IHttpActionResult Health()
        {
            return Ok(new { status = "Healthy", service = "SalesService" });
        }

        private string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
        }
    }

    public class OrderRequest
    {
        public int UserId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public List<OrderItemRequest> OrderItems { get; set; }
    }

    public class OrderItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}