using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FoodPOS
{
    public partial class StockUpdateMenuWindow : Window
    {
        public ObservableCollection<Product> Products { get; set; }
        private List<Product> _allProducts; // Store all products for filtering

        public StockUpdateMenuWindow()
        {
            InitializeComponent();
            Products = new ObservableCollection<Product>();
            _allProducts = new List<Product>();
            DataContext = this;
            Loaded += StockUpdateMenuWindow_Loaded;
        }

        private void StockUpdateMenuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void LoadProducts()
        {
            try
            {
                Products.Clear();
                _allProducts.Clear();

                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT ProductId, Name, Description, Price, StockQuantity, Barcode
                        FROM Products 
                        WHERE IsActive = 1
                        ORDER BY 
                            CASE WHEN StockQuantity = 0 THEN 0
                                 WHEN StockQuantity <= 3 THEN 1
                                 ELSE 2 END,
                            Name";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var product = new Product
                            {
                                ProductId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Price = reader.GetDecimal(3),
                                StockQuantity = reader.GetInt32(4),
                                Barcode = reader.IsDBNull(5) ? "" : reader.GetString(5)
                            };
                            Products.Add(product);
                            _allProducts.Add(product);
                        }
                    }
                }

                dgProducts.ItemsSource = Products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProducts();
        }

        private void cmbStockFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterProducts();
        }

        private void FilterProducts()
        {
            // Early return if products are not loaded
            if (Products == null || !Products.Any())
                return;

            var searchText = txtSearch?.Text?.ToLower() ?? "";
            var filterItem = cmbStockFilter?.SelectedItem as ComboBoxItem;
            var filter = filterItem?.Content?.ToString() ?? "All Stock";

            // Create a new filtered list
            var filteredProducts = Products.Where(p =>
                p != null && // Check if product is not null
                (string.IsNullOrWhiteSpace(searchText) ||
                 (p.Name?.ToLower().Contains(searchText) == true) ||
                 (p.Description?.ToLower().Contains(searchText) == true)) &&
                (filter == "All Stock" ||
                 (filter == "Out of Stock" && p.StockQuantity <= 0) ||
                 (filter == "Low Stock" && p.StockQuantity > 0 && p.StockQuantity <= 3) ||
                 (filter == "In Stock" && p.StockQuantity > 3))
            ).ToList();

            // Update DataGrid source
            dgProducts.ItemsSource = filteredProducts;
        }

        private void btnAddStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var product = _allProducts?.FirstOrDefault(p => p.ProductId == productId);
                if (product != null)
                {
                    // Get quantity from button content (e.g., "Add 10" -> 10)
                    var buttonText = button.Content?.ToString() ?? "";
                    if (int.TryParse(buttonText.Split(' ').Last(), out int quantity))
                    {
                        UpdateStock(product, quantity, "add");
                    }
                }
            }
        }

        private void btnRemoveStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var product = _allProducts?.FirstOrDefault(p => p.ProductId == productId);
                if (product != null)
                {
                    UpdateStock(product, 1, "remove");
                }
            }
        }

        private void btnCustomStock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var product = _allProducts?.FirstOrDefault(p => p.ProductId == productId);
                if (product != null)
                {
                    var dialog = new StockUpdateDialog(product);
                    if (dialog.ShowDialog() == true)
                    {
                        LoadProducts(); // Reload all products
                        MessageBox.Show($"Stock updated for {product.Name}!", "Success",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void btnAddToAllLowStock_Click(object sender, RoutedEventArgs e)
        {
            var lowStockProducts = _allProducts?.Where(p => p.StockQuantity > 0 && p.StockQuantity <= 3).ToList();

            if (lowStockProducts == null || !lowStockProducts.Any())
            {
                MessageBox.Show("No low stock products found.", "Info",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Add 10 stock to all {lowStockProducts.Count} low stock products?",
                "Bulk Stock Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                    {
                        connection.Open();

                        foreach (var product in lowStockProducts)
                        {
                            var query = "UPDATE Products SET StockQuantity = StockQuantity + 10 WHERE ProductId = @ProductId";
                            using (var command = new SqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@ProductId", product.ProductId);
                                command.ExecuteNonQuery();
                            }
                        }
                    }

                    LoadProducts();
                    MessageBox.Show($"Added 10 stock to {lowStockProducts.Count} products!", "Success",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating stock: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStock(Product product, int quantity, string operation)
        {
            try
            {
                int newStock = operation switch
                {
                    "add" => product.StockQuantity + quantity,
                    "remove" => product.StockQuantity - quantity,
                    _ => product.StockQuantity
                };

                if (newStock < 0)
                {
                    MessageBox.Show("Cannot remove more stock than available.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    var query = "UPDATE Products SET StockQuantity = @StockQuantity WHERE ProductId = @ProductId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StockQuantity", newStock);
                        command.Parameters.AddWithValue("@ProductId", product.ProductId);
                        command.ExecuteNonQuery();
                    }
                }

                // Update the product in both collections
                product.StockQuantity = newStock;

                // Find and update in _allProducts
                var allProduct = _allProducts.FirstOrDefault(p => p.ProductId == product.ProductId);
                if (allProduct != null)
                {
                    allProduct.StockQuantity = newStock;
                }

                // Refresh the display
                FilterProducts();

                MessageBox.Show($"Updated {product.Name}: {operation} {quantity} stock\nNew stock: {newStock}",
                              "Stock Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating stock: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnOpenFullManager_Click(object sender, RoutedEventArgs e)
        {
            var manageWindow = new ManageProductsWindow();
            manageWindow.Owner = this;
            manageWindow.ShowDialog();
            LoadProducts(); // Refresh after manager closes
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


    }
}