using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FoodPOS
{
    public partial class POSWindow : Window
    {
        public ObservableCollection<Product> Products { get; set; }
        public ObservableCollection<OrderItem> CurrentOrder { get; set; }
        public ObservableCollection<Category> Categories { get; set; }

        public decimal Subtotal => CurrentOrder.Sum(item => item.TotalPrice);
        public decimal TaxAmount => Subtotal * 0.08m; // 8% tax
        public decimal TotalAmount => Subtotal + TaxAmount;

        private string CurrentUser { get; set; }
        private string UserRole { get; set; }

        public POSWindow()
        {
            InitializeComponent();
            Products = new ObservableCollection<Product>();
            CurrentOrder = new ObservableCollection<OrderItem>();
            Categories = new ObservableCollection<Category>();

            DataContext = this;
        }

        // Helper method to format currency as Rs.
        private string FormatCurrency(decimal amount)
        {
            return $"Rs. {amount:N2}";
        }

        // Helper method for quantity formatting
        private string FormatQuantity(int quantity)
        {
            return $"{quantity:N0}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load user info
            if (Application.Current.Properties.Contains("CurrentUser"))
            {
                CurrentUser = Application.Current.Properties["CurrentUser"] as string;
                UserRole = Application.Current.Properties["UserRole"] as string;
                txtUserInfo.Text = $"Welcome, {CurrentUser} ({UserRole})";
            }

            // Apply role-based access control
            ApplyRoleBasedAccess();

            LoadCategories();
            LoadProducts();
        }

        private void ApplyRoleBasedAccess()
        {
            // Hide Product Management and Reports for Cashier role
            if (UserRole == "Cashier")
            {
                productManagementBorder.Visibility = Visibility.Collapsed;
                btnReportsHeader.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadCategories()
        {
            try
            {
                Categories.Clear();
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();
                    var query = "SELECT CategoryId, Name, Description FROM Categories WHERE IsActive = 1";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Categories.Add(new Category
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.GetString(2)
                            });
                        }
                    }
                }

                // Create category buttons
                CategoriesPanel.Children.Clear();

                // Add "All" button
                var allButton = new Button
                {
                    Content = "All Products",
                    Style = (Style)FindResource("MenuButtonStyle"),
                    Background = System.Windows.Media.Brushes.LightBlue
                };
                allButton.Click += (s, e) => LoadProducts();
                CategoriesPanel.Children.Add(allButton);

                foreach (var category in Categories)
                {
                    var button = new Button
                    {
                        Content = category.Name,
                        Style = (Style)FindResource("MenuButtonStyle"),
                        Tag = category.CategoryId
                    };
                    button.Click += CategoryButton_Click;
                    CategoriesPanel.Children.Add(button);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading categories: {ex.Message}", "Error");
            }
        }

        private void LoadProducts(int categoryId = 0)
        {
            try
            {
                Products.Clear();
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = "SELECT ProductId, Name, Description, Price, CostPrice, StockQuantity, Barcode FROM Products WHERE IsActive = 1";
                    if (categoryId > 0)
                    {
                        query += " AND CategoryId = @CategoryId";
                    }

                    using (var command = new SqlCommand(query, connection))
                    {
                        if (categoryId > 0)
                        {
                            command.Parameters.AddWithValue("@CategoryId", categoryId);
                        }

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Products.Add(new Product
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    CostPrice = reader.GetDecimal(4),
                                    StockQuantity = reader.GetInt32(5),
                                    Barcode = reader.GetString(6)
                                });
                            }
                        }
                    }
                }

                ProductsItemsControl.ItemsSource = Products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Error");
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int categoryId)
            {
                LoadProducts(categoryId);
            }
        }

        private void AddToOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Product product)
            {
                // Check if there's enough stock
                if (product.StockQuantity <= 0)
                {
                    MessageBox.Show($"{product.Name} is out of stock!", "Stock Warning");
                    return;
                }

                var existingItem = CurrentOrder.FirstOrDefault(item => item.ProductId == product.ProductId);

                if (existingItem != null)
                {
                    // Check if adding one more would exceed stock
                    if (existingItem.Quantity + 1 > product.StockQuantity)
                    {
                        MessageBox.Show($"Cannot add more {product.Name}. Only {product.StockQuantity} available in stock.", "Stock Limit");
                        return;
                    }

                    existingItem.Quantity++;
                    existingItem.TotalPrice = existingItem.Quantity * existingItem.UnitPrice;
                    OrderItemsListView.Items.Add(existingItem);


                }
                else
                {
                    CurrentOrder.Add(new OrderItem
                    {
                        ProductId = product.ProductId,
                        Name = product.Name,
                        UnitPrice = product.Price,
                        Quantity = 1,
                        TotalPrice = product.Price
                    });
                }

                RefreshOrderDisplay();
            }
        }

        private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var item = CurrentOrder.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    // Get current stock from products list
                    var product = Products.FirstOrDefault(p => p.ProductId == productId);
                    if (product != null && item.Quantity + 1 > product.StockQuantity)
                    {
                        MessageBox.Show($"Cannot add more {item.Name}. Only {product.StockQuantity} available in stock.", "Stock Limit");
                        return;
                    }

                    item.Quantity++;
                    item.TotalPrice = item.Quantity * item.UnitPrice;
                    RefreshOrderDisplay();
                }
            }
        }

        private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var item = CurrentOrder.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    if (item.Quantity > 1)
                    {
                        item.Quantity--;
                        item.TotalPrice = item.Quantity * item.UnitPrice;
                    }
                    else
                    {
                        CurrentOrder.Remove(item);
                    }
                    RefreshOrderDisplay();
                }
            }
        }

        private void btnUpdateStock_Click(object sender, RoutedEventArgs e)
        {
            if (UserRole != "Admin")
            {
                MessageBox.Show("Access denied. Only Administrators can update stock.", "Access Denied");
                return;
            }

            // Open the stock update window
            var stockUpdateWindow = new StockUpdateMenuWindow();
            stockUpdateWindow.Owner = this;
            stockUpdateWindow.ShowDialog();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var item = CurrentOrder.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    CurrentOrder.Remove(item);
                    RefreshOrderDisplay();
                }
            }
        }

        private void RefreshOrderDisplay()
        {
            txtSubtotal.Text = FormatCurrency(Subtotal);
            txtTax.Text = FormatCurrency(TaxAmount);
            txtTotal.Text = FormatCurrency(TotalAmount);
            OrderItemsListView.Items.Refresh();
        }

        private void ClearOrder_Click(object sender, RoutedEventArgs e)
        {
            CurrentOrder.Clear();
            OrderItemsListView.Items.Clear();
            RefreshOrderDisplay();
        }

        private void CardPayment_Click(object sender, RoutedEventArgs e)
        {
            ProcessPayment("Card");
        }

        private void CashPayment_Click(object sender, RoutedEventArgs e)
        {
            ProcessPayment("Cash");
        }

        private void ProcessPayment(string paymentMethod)
        {
            if (CurrentOrder.Count == 0)
            {
                MessageBox.Show("Please add items to the order first.", "Empty Order");
                return;
            }

            try
            {
                // Check stock availability first
                if (!CheckStockAvailability())
                {
                    MessageBox.Show("Some items are out of stock. Please update the order.", "Stock Issue");
                    return;
                }

                // Save order to database and update stock
                SaveOrderToDatabase(paymentMethod);

                // Generate receipt
                GenerateReceipt(paymentMethod);

                MessageBox.Show($"Payment processed successfully via {paymentMethod}!\nTotal: {FormatCurrency(TotalAmount)}", "Payment Complete");

                // Clear order and refresh products
                CurrentOrder.Clear();
                LoadProducts(); // Refresh to show updated stock
                RefreshOrderDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing payment: {ex.Message}", "Error");
            }
        }

        private bool CheckStockAvailability()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    foreach (var orderItem in CurrentOrder)
                    {
                        var query = "SELECT StockQuantity FROM Products WHERE ProductId = @ProductId";
                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@ProductId", orderItem.ProductId);
                            var currentStock = (int)command.ExecuteScalar();

                            if (currentStock < orderItem.Quantity)
                            {
                                MessageBox.Show($"Not enough stock for {orderItem.Name}. Available: {currentStock}, Requested: {orderItem.Quantity}", "Stock Issue");
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking stock: {ex.Message}", "Error");
                return false;
            }
        }

        private void SaveOrderToDatabase(string paymentMethod)
        {
            using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert order
                        var orderQuery = @"INSERT INTO Orders (OrderNumber, Subtotal, TaxAmount, TotalAmount, PaymentMethod) 
                                         OUTPUT INSERTED.OrderId 
                                         VALUES (@OrderNumber, @Subtotal, @TaxAmount, @TotalAmount, @PaymentMethod)";

                        var orderNumber = $"ORD-{DateTime.Now:yyyyMMdd-HHmmss}";

                        using (var command = new SqlCommand(orderQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@OrderNumber", orderNumber);
                            command.Parameters.AddWithValue("@Subtotal", Subtotal);
                            command.Parameters.AddWithValue("@TaxAmount", TaxAmount);
                            command.Parameters.AddWithValue("@TotalAmount", TotalAmount);
                            command.Parameters.AddWithValue("@PaymentMethod", paymentMethod);

                            var orderId = (int)command.ExecuteScalar();

                            // Insert order items and update stock
                            foreach (var item in CurrentOrder)
                            {
                                // Insert order item
                                var itemQuery = @"INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice, TotalPrice) 
                                                VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice, @TotalPrice)";

                                using (var itemCommand = new SqlCommand(itemQuery, connection, transaction))
                                {
                                    itemCommand.Parameters.AddWithValue("@OrderId", orderId);
                                    itemCommand.Parameters.AddWithValue("@ProductId", item.ProductId);
                                    itemCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    itemCommand.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                                    itemCommand.Parameters.AddWithValue("@TotalPrice", item.TotalPrice);
                                    itemCommand.ExecuteNonQuery();
                                }

                                // Update stock quantity
                                var updateStockQuery = "UPDATE Products SET StockQuantity = StockQuantity - @Quantity WHERE ProductId = @ProductId";
                                using (var stockCommand = new SqlCommand(updateStockQuery, connection, transaction))
                                {
                                    stockCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    stockCommand.Parameters.AddWithValue("@ProductId", item.ProductId);
                                    stockCommand.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void GenerateReceipt(string paymentMethod)
        {
            var receipt = $"FOODPOS RECEIPT\n" +
                         $"====================\n" +
                         $"Date: {DateTime.Now}\n" +
                         $"Order#: ORD-{DateTime.Now:yyyyMMdd-HHmmss}\n" +
                         $"====================\n";

            foreach (var item in CurrentOrder)
            {
                receipt += $"{item.Name} x{item.Quantity}\n";
                receipt += $"{FormatCurrency(item.TotalPrice)}\n";
            }

            receipt += $"====================\n" +
                      $"Subtotal: {FormatCurrency(Subtotal)}\n" +
                      $"Tax: {FormatCurrency(TaxAmount)}\n" +
                      $"Total: {FormatCurrency(TotalAmount)}\n" +
                      $"Payment: {paymentMethod}\n" +
                      $"====================\n" +
                      $"Thank you!";

            MessageBox.Show(receipt, "Receipt");
        }

       
        private void btnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (UserRole != "Admin")
            {
                MessageBox.Show("Access denied. Only Administrators can manage products.", "Access Denied");
                return;
            }

           
            var categoriesList = Categories.ToList();

            var dialog = new ProductDialog(categoriesList);
            if (dialog.ShowDialog() == true)
                LoadProducts();
        }

        private void btnManageProducts_Click(object sender, RoutedEventArgs e)
        {
            if (UserRole != "Admin")
            {
                MessageBox.Show("Access denied. Only Administrators can manage products.", "Access Denied");
                return;
            }
            var manageWindow = new ManageProductsWindow();
            manageWindow.ShowDialog();
            LoadProducts();
        }

        private void btnReports_Click(object sender, RoutedEventArgs e)
        {
            if (UserRole != "Admin")
            {
                MessageBox.Show("Access denied. Only Administrators can view reports.", "Access Denied");
                return;
            }

            // Simple creation - no need for Dispatcher since we're already on UI thread
            var reportsWindow = new ReportsWindow();
            reportsWindow.Owner = this;
            reportsWindow.ShowDialog();
        }

        private void btnProducts_Click(object sender, RoutedEventArgs e)
        {
            btnManageProducts_Click(sender, e);
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                new MainWindow().Show();
                this.Close();
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProducts();
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            FilterProducts();
        }

        private void FilterProducts()
        {
            var searchText = txtSearch.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ProductsItemsControl.ItemsSource = Products;
            }
            else
            {
                var filteredProducts = Products.Where(p =>
                    p.Name.ToLower().Contains(searchText) ||
                    p.Description.ToLower().Contains(searchText)
                ).ToList();
                ProductsItemsControl.ItemsSource = filteredProducts;
            }
        }
    }

    //// Data Models
    //public class Product
    //{
    //    public int ProductId { get; set; }
    //    public string Name { get; set; }
    //    public string Description { get; set; }
    //    public decimal Price { get; set; }
    //    public decimal CostPrice { get; set; }
    //    public int CategoryId { get; set; }
    //    public int StockQuantity { get; set; }
    //    public string Barcode { get; set; }

    //    // Add formatted price property
    //    public string FormattedPrice => $"Rs. {Price:N2}";

    //    // Enhanced stock properties
    //    public string StockIcon
    //    {
    //        get
    //        {
    //            if (StockQuantity <= 0) return "❌";
    //            if (StockQuantity <= 3) return "⚠️";
    //            if (StockQuantity <= 10) return "🔶";
    //            return "✅";
    //        }
    //    }

    //    public string StockText
    //    {
    //        get
    //        {
    //            if (StockQuantity <= 0) return "OUT OF STOCK";
    //            if (StockQuantity <= 3) return $"VERY LOW ({StockQuantity})";
    //            if (StockQuantity <= 10) return $"LOW ({StockQuantity})";
    //            return $"IN STOCK ({StockQuantity})";
    //        }
    //    }

    //    public System.Windows.Media.Brush StockBackground
    //    {
    //        get
    //        {
    //            if (StockQuantity <= 0) return System.Windows.Media.Brushes.LightCoral;
    //            if (StockQuantity <= 3) return System.Windows.Media.Brushes.LightGoldenrodYellow;
    //            if (StockQuantity <= 10) return System.Windows.Media.Brushes.LightYellow;
    //            return System.Windows.Media.Brushes.LightGreen;
    //        }
    //    }

    //    public System.Windows.Media.Brush StockStatusBackground
    //    {
    //        get
    //        {
    //            if (StockQuantity <= 0) return System.Windows.Media.Brushes.DarkRed;
    //            if (StockQuantity <= 3) return System.Windows.Media.Brushes.DarkOrange;
    //            if (StockQuantity <= 10) return System.Windows.Media.Brushes.Gold;
    //            return System.Windows.Media.Brushes.LimeGreen;
    //        }
    //    }

    //    public System.Windows.Media.Brush StockStatusColor
    //    {
    //        get
    //        {
    //            return System.Windows.Media.Brushes.White;
    //        }
    //    }

    //    public System.Windows.Media.Brush ButtonColor
    //    {
    //        get
    //        {
    //            if (StockQuantity <= 0) return System.Windows.Media.Brushes.Gray;
    //            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
    //        }
    //    }

    //    public bool IsInStock => StockQuantity > 0;
    //}

    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class OrderItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }
}