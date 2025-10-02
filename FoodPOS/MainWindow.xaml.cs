using System;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace FoodPOS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            CheckSystemStatus();
        }

        // Check if Docker and Database are running
        private async void CheckSystemStatus()
        {
            // Check Docker container status (simplified - always green for demo)
            dockerStatus.Fill = Brushes.Green;

            // Check database connection
            if (DatabaseHelper.TestConnection())
            {
                dbStatus.Fill = Brushes.Green;
                InitializeDatabase(); // Create database and tables if needed
            }
            else
            {
                dbStatus.Fill = Brushes.Red;
            }

            // Set microservices status (not implemented yet)
            authStatus.Fill = Brushes.Red;
            productStatus.Fill = Brushes.Red;
            salesStatus.Fill = Brushes.Red;
        }

        // Create database and tables if they don't exist
        private void InitializeDatabase()
        {
            try
            {
                CreateDatabaseAndTables();
            }
            catch (Exception ex)
            {
                ShowError($"Database setup failed: {ex.Message}");
            }
        }

        // Create the FoodPOS database and necessary tables
        private void CreateDatabaseAndTables()
        {
            try
            {
                // First, create database if it doesn't exist
                using (var connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    string createDbQuery = @"
                    IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'FoodPOS')
                    BEGIN
                        CREATE DATABASE FoodPOS;
                    END";

                    using (var command = new SqlCommand(createDbQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                // Now switch to FoodPOS database and create tables
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string createTablesQuery = @"
                    -- Create Users table
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                    BEGIN
                        CREATE TABLE Users (
                            UserId INT PRIMARY KEY IDENTITY(1,1),
                            Username NVARCHAR(50) UNIQUE NOT NULL,
                            PasswordHash NVARCHAR(255) NOT NULL,
                            Role NVARCHAR(20) NOT NULL CHECK (Role IN ('Cashier', 'Manager', 'Admin')),
                            IsActive BIT DEFAULT 1,
                            CreatedDate DATETIME2 DEFAULT GETDATE()
                        );
                    END

                    -- Create Categories table
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                    BEGIN
                        CREATE TABLE Categories (
                            CategoryId INT PRIMARY KEY IDENTITY(1,1),
                            Name NVARCHAR(100) NOT NULL,
                            Description NVARCHAR(255),
                            IsActive BIT DEFAULT 1
                        );
                    END

                    -- Create Products table
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Products' AND xtype='U')
                    BEGIN
                        CREATE TABLE Products (
                            ProductId INT PRIMARY KEY IDENTITY(1,1),
                            Name NVARCHAR(100) NOT NULL,
                            Description NVARCHAR(255),
                            Price DECIMAL(10,2) NOT NULL,
                            CostPrice DECIMAL(10,2),
                            StockQuantity INT DEFAULT 0,
                            Barcode NVARCHAR(50),
                            CategoryId INT,
                            IsActive BIT DEFAULT 1,
                            FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId)
                        );
                    END

                    -- Create Orders table
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Orders' AND xtype='U')
                    BEGIN
                        CREATE TABLE Orders (
                            OrderId INT PRIMARY KEY IDENTITY(1,1),
                            OrderNumber NVARCHAR(50) UNIQUE NOT NULL,
                            Subtotal DECIMAL(10,2) NOT NULL,
                            TaxAmount DECIMAL(10,2) NOT NULL,
                            TotalAmount DECIMAL(10,2) NOT NULL,
                            PaymentMethod NVARCHAR(20) NOT NULL,
                            CreatedDate DATETIME2 DEFAULT GETDATE()
                        );
                    END

                    -- Create OrderItems table
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OrderItems' AND xtype='U')
                    BEGIN
                        CREATE TABLE OrderItems (
                            OrderItemId INT PRIMARY KEY IDENTITY(1,1),
                            OrderId INT NOT NULL,
                            ProductId INT NOT NULL,
                            Quantity INT NOT NULL,
                            UnitPrice DECIMAL(10,2) NOT NULL,
                            TotalPrice DECIMAL(10,2) NOT NULL,
                            FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
                            FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
                        );
                    END

                    -- Insert default admin user (password: 'admin')
                    IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'admin')
                    BEGIN
                        INSERT INTO Users (Username, PasswordHash, Role) 
                        VALUES ('admin', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'Admin');
                    END

                    -- Insert default cashier user (password: '1234')
                    IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'cashier')
                    BEGIN
                        INSERT INTO Users (Username, PasswordHash, Role) 
                        VALUES ('cashier', 'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3', 'Cashier');
                    END

                    -- Insert some sample categories
                    IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Beverages')
                    BEGIN
                        INSERT INTO Categories (Name, Description) VALUES 
                        ('Beverages', 'Soft drinks, coffee, tea, beer, milk'),
                        ('Food', 'Bread, cereals, rice, pasta, noodles'),
                        ('Snacks', 'Chips, cookies, crackers, nuts');
                    END";

                    using (var command = new SqlCommand(createTablesQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore if tables already exist
                System.Diagnostics.Debug.WriteLine($"Database setup: {ex.Message}");
            }
        }

        // Login button click handler
        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter username and password");
                return;
            }

            await LoginUser(username, password);
        }

        // Main login logic
        private async Task LoginUser(string username, string password)
        {
            try
            {
                ShowLoading(true);
                ClearError();

                // Try database authentication first
                if (await DatabaseAuthenticate(username, password))
                {
                    ShowStatus("Login successful! Redirecting...", false);
                    await Task.Delay(1000);

                    // Store user information in application properties
                    Application.Current.Properties["CurrentUser"] = username;
                    Application.Current.Properties["UserRole"] = await GetUserRole(username);

                    // Open main POS window
                    OpenPOSWindow();
                }
                else
                {
                    ShowError("Invalid username or password");
                }
            }
            catch (Exception ex)
            {
                // If database auth fails, try simple authentication
                if (await SimpleAuthenticate(username, password))
                {
                    ShowStatus("Login successful! Redirecting...", false);
                    await Task.Delay(1000);

                    Application.Current.Properties["CurrentUser"] = username;
                    Application.Current.Properties["UserRole"] = (username == "admin") ? "Admin" : "Cashier";

                    OpenPOSWindow();
                }
                else
                {
                    ShowError($"Login failed: {ex.Message}");
                }
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // Database authentication (secure)
        private async Task<bool> DatabaseAuthenticate(string username, string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                    {
                        connection.Open();

                        string query = "SELECT PasswordHash, Role FROM Users WHERE Username = @Username AND IsActive = 1";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@Username", username);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string storedHash = reader["PasswordHash"].ToString();
                                    string computedHash = ComputeSHA256Hash(password);

                                    return storedHash == computedHash;
                                }
                            }
                        }
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        // Simple authentication (fallback - for demo purposes)
        private async Task<bool> SimpleAuthenticate(string username, string password)
        {
            await Task.Delay(500); // Simulate async operation

            // Simple hardcoded credentials (for demo when database is not available)
            return (username == "admin" && password == "admin") ||
                   (username == "cashier" && password == "1234");
        }

        // Get user role from database
        private async Task<string> GetUserRole(string username)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                    {
                        connection.Open();

                        string query = "SELECT Role FROM Users WHERE Username = @Username";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@Username", username);
                            var result = command.ExecuteScalar();
                            return result?.ToString() ?? "Cashier";
                        }
                    }
                }
                catch
                {
                    return (username == "admin") ? "Admin" : "Cashier";
                }
            });
        }

        // Compute SHA256 hash for passwords
        private string ComputeSHA256Hash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Open the main POS window
        private void OpenPOSWindow()
        {
            POSWindow posWindow = new POSWindow();
            posWindow.Show();
            this.Close();
        }

        // UI Helper Methods
        private void ShowLoading(bool show)
        {
            if (loadingOverlay != null)
                loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            if (btnLogin != null)
                btnLogin.IsEnabled = !show;
        }

        private void ShowError(string message)
        {
            if (txtStatus != null)
            {
                txtStatus.Text = message;
                txtStatus.Foreground = Brushes.Red;
                txtStatus.Visibility = Visibility.Visible;
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            if (txtStatus != null)
            {
                txtStatus.Text = message;
                txtStatus.Foreground = isError ? Brushes.Red : Brushes.Green;
                txtStatus.Visibility = Visibility.Visible;
            }
        }

        private void ClearError()
        {
            if (txtStatus != null)
                txtStatus.Visibility = Visibility.Collapsed;
        }
    }

    // Database helper class
    public static class DatabaseHelper
    {
        public static string ConnectionString = "Server=localhost;Database=master;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;";

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public static bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = new SqlCommand("SELECT 1", connection))
                    {
                        var result = command.ExecuteScalar();
                        return result != null;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}