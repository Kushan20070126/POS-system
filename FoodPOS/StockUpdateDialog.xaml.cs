using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FoodPOS
{
    public partial class StockUpdateDialog : Window
    {
        private Product Product { get; set; }

        public StockUpdateDialog(Product product)
        {
            InitializeComponent();
            Product = product;
            Loaded += StockUpdateDialog_Loaded;
        }

        public StockUpdateDialog(ProductVM product)
        {
        }

        private void StockUpdateDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Set product information
            txtProductName.Text = Product.Name;
            txtCurrentStock.Text = $"Current Stock: {Product.StockQuantity}";

            // Update preview
            UpdateStockPreview();
        }

        private void cmbUpdateType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStockPreview();
        }

        private void txtQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numeric input
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void txtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStockPreview();
        }

        private void UpdateStockPreview()
        {
            if (int.TryParse(txtQuantity.Text, out int quantity) && quantity >= 0)
            {
                var updateType = (cmbUpdateType.SelectedItem as ComboBoxItem)?.Content.ToString();
                int newStock = CalculateNewStock(quantity, updateType);

                string status = GetStockStatus(newStock);
                string statusColor = GetStockStatusColor(newStock);

                txtNewStock.Text = $"New Stock: {newStock}\nStatus: {status}";

                // Update color based on stock level
                if (newStock == 0)
                    txtNewStock.Foreground = System.Windows.Media.Brushes.Red;
                else if (newStock <= 3)
                    txtNewStock.Foreground = System.Windows.Media.Brushes.Orange;
                else
                    txtNewStock.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                txtNewStock.Text = "Please enter a valid quantity";
                txtNewStock.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private int CalculateNewStock(int quantity, string updateType)
        {
            return updateType switch
            {
                "➕ Add Stock" => Product.StockQuantity + quantity,
                "📝 Set Stock" => quantity,
                "➖ Remove Stock" => Product.StockQuantity - quantity,
                _ => Product.StockQuantity
            };
        }

        private string GetStockStatus(int stock)
        {
            if (stock <= 0) return "❌ OUT OF STOCK";
            if (stock <= 3) return "⚠️ VERY LOW";
            if (stock <= 10) return "🔶 LOW";
            return "✅ IN STOCK";
        }

        private string GetStockStatusColor(int stock)
        {
            if (stock <= 0) return "Red";
            if (stock <= 3) return "Orange";
            if (stock <= 10) return "Yellow";
            return "Green";
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                UpdateStock();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating stock: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity < 0)
            {
                MessageBox.Show("Please enter a valid quantity (0 or higher).",
                              "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtQuantity.Focus();
                return false;
            }

            var updateType = (cmbUpdateType.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (updateType == "➖ Remove Stock" && quantity > Product.StockQuantity)
            {
                MessageBox.Show($"Cannot remove {quantity} items. Only {Product.StockQuantity} available in stock.",
                              "Stock Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int newStock = CalculateNewStock(quantity, updateType);

            if (newStock < 0)
            {
                MessageBox.Show("Stock quantity cannot be negative.",
                              "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void UpdateStock()
        {
            var updateType = (cmbUpdateType.SelectedItem as ComboBoxItem)?.Content.ToString();
            var quantity = int.Parse(txtQuantity.Text);
            int newStock = CalculateNewStock(quantity, updateType);

            using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
            {
                connection.Open();

                var query = "UPDATE Products SET StockQuantity = @StockQuantity WHERE ProductId = @ProductId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StockQuantity", newStock);
                    command.Parameters.AddWithValue("@ProductId", Product.ProductId);

                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        // Update the product object with new stock
                        Product.StockQuantity = newStock;
                    }
                }
            }
        }
    }
}