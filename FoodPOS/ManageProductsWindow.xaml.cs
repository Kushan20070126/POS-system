using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace FoodPOS
{
    public partial class ManageProductsWindow : Window
    {
        public ObservableCollection<ProductVM> Products { get; set; }
        private List<Category> Categories { get; set; }

        public ManageProductsWindow()
        {
            InitializeComponent();
            Products = new ObservableCollection<ProductVM>();
            Categories = new List<Category>();
            DataContext = this;
            Loaded += ManageProductsWindow_Loaded;
        }

        private void ManageProductsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategories();
            LoadProducts();
            UpdateStatistics();
        }

        private void LoadCategories()
        {
            try
            {
                Categories.Clear();
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOSsys;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();
                    var query = "SELECT CategoryId, Name FROM Categories WHERE IsActive = 1 ORDER BY Name";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Categories.Add(new Category
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading categories: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void LoadProducts()
        {
            try
            {
                Products.Clear();
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT p.ProductId, p.Name, p.Description, p.Price, p.CostPrice, 
                               p.StockQuantity, p.Barcode, p.IsActive, p.CategoryId, 
                               c.Name as CategoryName
                        FROM Products p 
                        LEFT JOIN Categories c ON p.CategoryId = c.CategoryId 
                        ORDER BY p.Name";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var product = new ProductVM
                            {
                                ProductId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Price = reader.GetDecimal(3),
                                CostPrice = reader.GetDecimal(4),
                                StockQuantity = reader.GetInt32(5),
                                Barcode = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                IsActive = reader.GetBoolean(7),
                                CategoryId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                                CategoryName = reader.IsDBNull(9) ? "Uncategorized" : reader.GetString(9)
                            };
                            Products.Add(product);
                        }
                    }
                }

                dgProducts.ItemsSource = Products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatistics()
        {
            var totalProducts = Products.Count;
            var outOfStock = Products.Count(p => p.StockQuantity <= 0);
            var lowStock = Products.Count(p => p.StockQuantity > 0 && p.StockQuantity <= 3);
            var inStock = Products.Count(p => p.StockQuantity > 3);

            txtTotalProducts.Text = $"Total Products: {totalProducts}";
            txtOutOfStock.Text = $"Out of Stock: {outOfStock}";
            txtLowStock.Text = $"Low Stock: {lowStock}";
            txtInStock.Text = $"In Stock: {inStock}";
        }

        private void btnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProductDialog(Categories);
            if (dialog.ShowDialog() == true)
            {
                LoadProducts();
                UpdateStatistics();
                MessageBox.Show("Product added successfully!", "Success",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnEditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var product = Products.FirstOrDefault(p => p.ProductId == productId);
                if (product != null)
                {
                    var dialog = new ProductDialog(Categories, product);
                    if (dialog.ShowDialog() == true)
                    {
                        LoadProducts();
                        UpdateStatistics();
                        MessageBox.Show("Product updated successfully!", "Success",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void btnUpdateStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var product = Products.FirstOrDefault(p => p.ProductId == productId);
                if (product != null)
                {
                    var dialog = new StockUpdateDialog(product);
                    if (dialog.ShowDialog() == true)
                    {
                        LoadProducts();
                        UpdateStatistics();
                        MessageBox.Show("Stock updated successfully!", "Success",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void btnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var product = Products.FirstOrDefault(p => p.ProductId == productId);
                if (product != null)
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete '{product.Name}'?\n\nThis action cannot be undone.",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        DeleteProduct(productId);
                    }
                }
            }
        }

        private void DeleteProduct(int productId)
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    // Check if product has any orders
                    var checkOrdersQuery = "SELECT COUNT(*) FROM OrderItems WHERE ProductId = @ProductId";
                    using (var checkCommand = new SqlCommand(checkOrdersQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@ProductId", productId);
                        int orderCount = (int)checkCommand.ExecuteScalar();

                        if (orderCount > 0)
                        {
                            MessageBox.Show("Cannot delete this product because it has existing orders. Please deactivate it instead.",
                                          "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // Soft delete - set IsActive to false
                    var query = "UPDATE Products SET IsActive = 0 WHERE ProductId = @ProductId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductId", productId);
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Product deleted successfully!", "Success",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadProducts();
                            UpdateStatistics();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting product: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            UpdateStatistics();
            MessageBox.Show("Products refreshed!", "Refresh",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnStockReport_Click(object sender, RoutedEventArgs e)
        {
            ExportStockReport();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProducts();
        }

        private void FilterProducts()
        {
            var searchText = txtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgProducts.ItemsSource = Products;
            }
            else
            {
                var filteredProducts = Products.Where(p =>
                    p.Name.ToLower().Contains(searchText) ||
                    p.Description.ToLower().Contains(searchText) ||
                    p.Barcode.ToLower().Contains(searchText) ||
                    p.CategoryName.ToLower().Contains(searchText)
                ).ToList();
                dgProducts.ItemsSource = filteredProducts;
            }
        }

        private void ExportStockReport()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"Stock_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var csvContent = "ProductID,Name,Description,Price,CostPrice,StockQuantity,Status,Barcode,Category,IsActive\n";

                    foreach (var product in Products)
                    {
                        csvContent += $"{product.ProductId},\"{product.Name}\",\"{product.Description}\"," +
                                    $"{product.Price},{product.CostPrice},{product.StockQuantity}," +
                                    $"\",\"{product.Barcode}\",\"{product.CategoryName}\",{product.IsActive}\n";
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, csvContent);
                    MessageBox.Show("Stock report exported successfully!", "Export Complete",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting stock report: {ex.Message}", "Export Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}