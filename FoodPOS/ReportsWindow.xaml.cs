using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace FoodPOS
{
    public partial class ReportsWindow : Window
    {
        private DateTime startDate;
        private DateTime endDate;
        private string reportType;

        // Chart data properties
        public SeriesCollection SalesSeries { get; set; }
        public SeriesCollection PaymentSeries { get; set; }
        public SeriesCollection TopProductsSeries { get; set; }
        public SeriesCollection HourlySeries { get; set; }
        public SeriesCollection ProfitSeries { get; set; }
        public SeriesCollection ProductProfitSeries { get; set; }

        public string[] Dates { get; set; }
        public string[] Hours { get; set; }
        public string[] ProductNames { get; set; }
        public string[] ProfitDates { get; set; }
        public string[] ProfitProductNames { get; set; }

        public ReportsWindow()
        {
            // Initialize chart collections FIRST
            InitializeChartCollections();

            // Then initialize components
            InitializeComponent();

            // Set default date range
            startDate = DateTime.Today.AddDays(-30);
            endDate = DateTime.Today;
            dpStartDate.SelectedDate = startDate;
            dpEndDate.SelectedDate = endDate;

            reportType = "Monthly";

            // Set DataContext
            DataContext = this;
        }

        private void InitializeChartCollections()
        {
            // Initialize all chart series collections on UI thread
            SalesSeries = new SeriesCollection();
            PaymentSeries = new SeriesCollection();
            TopProductsSeries = new SeriesCollection();
            HourlySeries = new SeriesCollection();
            ProfitSeries = new SeriesCollection();
            ProductProfitSeries = new SeriesCollection();

            // Initialize arrays
            Dates = new string[0];
            Hours = new string[0];
            ProductNames = new string[0];
            ProfitDates = new string[0];
            ProfitProductNames = new string[0];
        }

        // Helper method to format currency as Rs.
        private string FormatCurrency(decimal amount)
        {
            return $"Rs. {amount:N2}";
        }

        // Helper method for percentage formatting
        private string FormatPercentage(double percentage)
        {
            return $"{percentage:F1}%";
        }

        // Helper method for quantity formatting
        private string FormatQuantity(int quantity)
        {
            return $"{quantity:N0}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadReportData();
        }

        private void LoadReportData()
        {
            try
            {
                ClearCharts();
                LoadSalesSummary();
                LoadSalesTrend();
                LoadPaymentMethods();
                LoadTopProducts();
                LoadHourlySales();
                LoadProfitAnalysis();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading report data: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearCharts()
        {
            SalesSeries.Clear();
            PaymentSeries.Clear();
            TopProductsSeries.Clear();
            HourlySeries.Clear();
            ProfitSeries.Clear();
            ProductProfitSeries.Clear();
        }

        private void LoadSalesSummary()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    // Total Revenue
                    var revenueQuery = @"
                        SELECT ISNULL(SUM(TotalAmount), 0) as TotalRevenue,
                               COUNT(*) as TotalOrders,
                               ISNULL(AVG(TotalAmount), 0) as AvgOrderValue
                        FROM Orders 
                        WHERE CreatedDate BETWEEN @StartDate AND @EndDate";

                    using (var command = new SqlCommand(revenueQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    txtTotalRevenue.Text = FormatCurrency(reader.GetDecimal(0));
                                    txtTotalOrders.Text = FormatQuantity(reader.GetInt32(1));
                                    txtAvgOrderValue.Text = FormatCurrency(reader.GetDecimal(2));
                                });
                            }
                        }
                    }

                    // Top Product
                    var topProductQuery = @"
                        SELECT TOP 1 p.Name, SUM(oi.Quantity) as TotalSold
                        FROM OrderItems oi
                        INNER JOIN Products p ON oi.ProductId = p.ProductId
                        INNER JOIN Orders o ON oi.OrderId = o.OrderId
                        WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY p.Name
                        ORDER BY TotalSold DESC";

                    using (var command = new SqlCommand(topProductQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    txtTopProduct.Text = $"{reader.GetString(0)} ({FormatQuantity(reader.GetInt32(1))} sold)";
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    txtTopProduct.Text = "No sales data";
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading sales summary: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadSalesTrend()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT CAST(CreatedDate AS DATE) as SaleDate, 
                               SUM(TotalAmount) as DailySales
                        FROM Orders 
                        WHERE CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY CAST(CreatedDate AS DATE)
                        ORDER BY SaleDate";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        var dates = new List<string>();
                        var sales = new ChartValues<decimal>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dates.Add(reader.GetDateTime(0).ToString("MMM dd"));
                                sales.Add(reader.GetDecimal(1));
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            Dates = dates.ToArray();

                            SalesSeries.Add(new LineSeries
                            {
                                Title = "Daily Sales",
                                Values = sales,
                                Stroke = (Brush)new BrushConverter().ConvertFrom("#2196F3"),
                                Fill = Brushes.Transparent,
                                StrokeThickness = 3,
                                PointGeometry = DefaultGeometries.Circle,
                                PointGeometrySize = 8,
                                PointForeground = (Brush)new BrushConverter().ConvertFrom("#2196F3")
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading sales trend: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private Brush GetPaymentMethodColor(string paymentMethod)
        {
            switch (paymentMethod)
            {
                case "Cash":
                    return (Brush)new BrushConverter().ConvertFrom("#4CAF50");
                case "Card":
                    return (Brush)new BrushConverter().ConvertFrom("#2196F3");
                case "Digital":
                    return (Brush)new BrushConverter().ConvertFrom("#FF9800");
                default:
                    return (Brush)new BrushConverter().ConvertFrom("#9C27B0");
            }
        }

        private void LoadPaymentMethods()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT PaymentMethod, COUNT(*) as Count, SUM(TotalAmount) as Amount
                        FROM Orders 
                        WHERE CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY PaymentMethod";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var paymentMethod = reader.GetString(0);
                                var amount = reader.GetDecimal(2);

                                Brush color = GetPaymentMethodColor(paymentMethod);

                                Dispatcher.Invoke(() =>
                                {
                                    PaymentSeries.Add(new PieSeries
                                    {
                                        Title = paymentMethod,
                                        Values = new ChartValues<decimal> { amount },
                                        DataLabels = true,
                                        LabelPoint = point => FormatCurrency((decimal)point.Y),
                                        Fill = color
                                    });
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading payment methods: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadTopProducts()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT TOP 10 p.Name, SUM(oi.Quantity) as TotalSold
                        FROM OrderItems oi
                        INNER JOIN Products p ON oi.ProductId = p.ProductId
                        INNER JOIN Orders o ON oi.OrderId = o.OrderId
                        WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY p.Name
                        ORDER BY TotalSold DESC";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        var productNames = new List<string>();
                        var quantities = new ChartValues<int>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                productNames.Add(reader.GetString(0));
                                quantities.Add(reader.GetInt32(1));
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            ProductNames = productNames.ToArray();

                            TopProductsSeries.Add(new ColumnSeries
                            {
                                Title = "Quantity Sold",
                                Values = quantities,
                                Fill = (Brush)new BrushConverter().ConvertFrom("#2196F3"),
                                DataLabels = true
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading top products: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadHourlySales()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT DATEPART(HOUR, CreatedDate) as Hour, 
                               SUM(TotalAmount) as HourlySales
                        FROM Orders 
                        WHERE CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY DATEPART(HOUR, CreatedDate)
                        ORDER BY Hour";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        var hours = new List<string>();
                        var sales = new ChartValues<decimal>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var hour = reader.GetInt32(0);
                                hours.Add($"{hour:00}:00");
                                sales.Add(reader.GetDecimal(1));
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            Hours = hours.ToArray();

                            HourlySeries.Add(new LineSeries
                            {
                                Title = "Hourly Sales",
                                Values = sales,
                                Stroke = (Brush)new BrushConverter().ConvertFrom("#FF9800"),
                                Fill = Brushes.Transparent,
                                StrokeThickness = 3,
                                PointGeometry = DefaultGeometries.Circle,
                                PointGeometrySize = 8,
                                PointForeground = (Brush)new BrushConverter().ConvertFrom("#FF9800")
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading hourly sales: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadProfitAnalysis()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    // Load profit metrics
                    var profitQuery = @"
                        SELECT 
                            SUM(o.TotalAmount) as TotalRevenue,
                            SUM(oi.Quantity * p.CostPrice) as TotalCost,
                            SUM(o.TotalAmount - (oi.Quantity * p.CostPrice)) as GrossProfit
                        FROM Orders o
                        INNER JOIN OrderItems oi ON o.OrderId = oi.OrderId
                        INNER JOIN Products p ON oi.ProductId = p.ProductId
                        WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate";

                    using (var command = new SqlCommand(profitQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var revenue = reader.GetDecimal(0);
                                var cost = reader.GetDecimal(1);
                                var profit = reader.GetDecimal(2);
                                var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                                Dispatcher.Invoke(() =>
                                {
                                    txtGrossProfit.Text = FormatCurrency(profit);
                                    txtProfitMargin.Text = FormatPercentage((double)margin);
                                    txtCostOfGoods.Text = FormatCurrency(cost);
                                });
                            }
                        }
                    }

                    // Load most profitable product
                    var bestMarginQuery = @"
                        SELECT TOP 1 p.Name, 
                               (SUM(oi.UnitPrice * oi.Quantity) - SUM(p.CostPrice * oi.Quantity)) as TotalProfit,
                               ((SUM(oi.UnitPrice * oi.Quantity) - SUM(p.CostPrice * oi.Quantity)) / NULLIF(SUM(oi.UnitPrice * oi.Quantity), 0)) * 100 as Margin
                        FROM OrderItems oi
                        INNER JOIN Products p ON oi.ProductId = p.ProductId
                        INNER JOIN Orders o ON oi.OrderId = o.OrderId
                        WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY p.Name
                        ORDER BY Margin DESC";

                    using (var command = new SqlCommand(bestMarginQuery, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var productName = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                                var marginValue = reader.IsDBNull(2) ? 0 : (double)reader.GetDecimal(2);

                                Dispatcher.Invoke(() =>
                                {
                                    txtBestMarginProduct.Text = $"{productName} ({FormatPercentage(marginValue)} margin)";
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    txtBestMarginProduct.Text = "No data";
                                });
                            }
                        }
                    }

                    // Load profit trend
                    LoadProfitTrend();
                    LoadProductProfitability();
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading profit analysis: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadProfitTrend()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT CAST(o.CreatedDate AS DATE) as SaleDate,
                               SUM(o.TotalAmount) as Revenue,
                               SUM(oi.Quantity * p.CostPrice) as Cost,
                               SUM(o.TotalAmount - (oi.Quantity * p.CostPrice)) as Profit
                        FROM Orders o
                        INNER JOIN OrderItems oi ON o.OrderId = oi.OrderId
                        INNER JOIN Products p ON oi.ProductId = p.ProductId
                        WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY CAST(o.CreatedDate AS DATE)
                        ORDER BY SaleDate";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        var dates = new List<string>();
                        var revenueValues = new ChartValues<decimal>();
                        var profitValues = new ChartValues<decimal>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dates.Add(reader.GetDateTime(0).ToString("MMM dd"));
                                revenueValues.Add(reader.GetDecimal(1));
                                profitValues.Add(reader.GetDecimal(3));
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            ProfitDates = dates.ToArray();

                            ProfitSeries.Add(new LineSeries
                            {
                                Title = "Revenue",
                                Values = revenueValues,
                                Stroke = (Brush)new BrushConverter().ConvertFrom("#2196F3"),
                                Fill = Brushes.Transparent,
                                StrokeThickness = 3
                            });

                            ProfitSeries.Add(new LineSeries
                            {
                                Title = "Profit",
                                Values = profitValues,
                                Stroke = (Brush)new BrushConverter().ConvertFrom("#4CAF50"),
                                Fill = Brushes.Transparent,
                                StrokeThickness = 3
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading profit trend: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadProductProfitability()
        {
            try
            {
                using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                {
                    connection.Open();

                    string query = @"
                        SELECT TOP 8 p.Name,
                               SUM(oi.UnitPrice * oi.Quantity) as Revenue,
                               SUM(p.CostPrice * oi.Quantity) as Cost,
                               SUM(oi.UnitPrice * oi.Quantity - p.CostPrice * oi.Quantity) as Profit
                        FROM OrderItems oi
                        INNER JOIN Products p ON oi.ProductId = p.ProductId
                        INNER JOIN Orders o ON oi.OrderId = o.OrderId
                        WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate
                        GROUP BY p.Name
                        ORDER BY Profit DESC";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                        var productNames = new List<string>();
                        var profitValues = new ChartValues<decimal>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                productNames.Add(reader.GetString(0));
                                profitValues.Add(reader.GetDecimal(3));
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            ProfitProductNames = productNames.ToArray();

                            ProductProfitSeries.Add(new ColumnSeries
                            {
                                Title = "Profit",
                                Values = profitValues,
                                Fill = (Brush)new BrushConverter().ConvertFrom("#4CAF50"),
                                DataLabels = true
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading product profitability: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        // Report Period Button Handlers
        private void DailyReport_Click(object sender, RoutedEventArgs e)
        {
            startDate = DateTime.Today;
            endDate = DateTime.Today;
            reportType = "Daily";
            dpStartDate.SelectedDate = startDate;
            dpEndDate.SelectedDate = endDate;
            LoadReportData();
        }

        private void WeeklyReport_Click(object sender, RoutedEventArgs e)
        {
            startDate = DateTime.Today.AddDays(-7);
            endDate = DateTime.Today;
            reportType = "Weekly";
            dpStartDate.SelectedDate = startDate;
            dpEndDate.SelectedDate = endDate;
            LoadReportData();
        }

        private void MonthlyReport_Click(object sender, RoutedEventArgs e)
        {
            startDate = DateTime.Today.AddDays(-30);
            endDate = DateTime.Today;
            reportType = "Monthly";
            dpStartDate.SelectedDate = startDate;
            dpEndDate.SelectedDate = endDate;
            LoadReportData();
        }

        private void YearlyReport_Click(object sender, RoutedEventArgs e)
        {
            startDate = DateTime.Today.AddDays(-365);
            endDate = DateTime.Today;
            reportType = "Yearly";
            dpStartDate.SelectedDate = startDate;
            dpEndDate.SelectedDate = endDate;
            LoadReportData();
        }

        private void CustomRange_Click(object sender, RoutedEventArgs e)
        {
            if (dpStartDate.SelectedDate == null || dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Please select both start and end dates.", "Date Range Required");
                return;
            }

            startDate = dpStartDate.SelectedDate.Value;
            endDate = dpEndDate.SelectedDate.Value;
            reportType = "Custom";
            LoadReportData();
        }

        // Button Event Handlers
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReportData();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportToCSV();
                MessageBox.Show("Report exported successfully!", "Export Complete");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error");
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Print functionality would be implemented here", "Print Report");
        }

        private void btnSavePDF_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PDF export functionality would be implemented here", "Save as PDF");
        }

        private async void ExportToCSV()
        {
            try
            {
                await Task.Run(() => 
                {
                    using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
                    {
                        connection.Open();

                        string query = @"
                    SELECT o.OrderNumber, o.CreatedDate, o.TotalAmount, o.PaymentMethod,
                           p.Name as ProductName, oi.Quantity, oi.UnitPrice, oi.TotalPrice
                    FROM Orders o
                    INNER JOIN OrderItems oi ON o.OrderId = oi.OrderId
                    INNER JOIN Products p ON oi.ProductId = p.ProductId
                    WHERE o.CreatedDate BETWEEN @StartDate AND @EndDate
                    ORDER BY o.CreatedDate DESC";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@StartDate", startDate);
                            command.Parameters.AddWithValue("@EndDate", endDate.AddDays(1).AddSeconds(-1));

                            var dataTable = new DataTable();
                            using (var adapter = new SqlDataAdapter(command))
                            {
                                adapter.Fill(dataTable);
                            }

                            // Create CSV content
                            var csvContent = "OrderNumber,Date,TotalAmount(Rs),PaymentMethod,Product,Quantity,UnitPrice(Rs),TotalPrice(Rs)\n";

                            foreach (DataRow row in dataTable.Rows)
                            {
                                csvContent += $"\"{row["OrderNumber"]}\",\"{row["CreatedDate"]}\",{row["TotalAmount"]},\"{row["PaymentMethod"]}\",\"{row["ProductName"]}\",{row["Quantity"]},{row["UnitPrice"]},{row["TotalPrice"]}\n";
                            }

                            // Save to file on UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var saveDialog = new SaveFileDialog
                                {
                                    Filter = "CSV files (*.csv)|*.csv",
                                    FileName = $"FoodPOS_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                                };

                                if (saveDialog.ShowDialog() == true)
                                {
                                    System.IO.File.WriteAllText(saveDialog.FileName, csvContent);
                                }
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Export Error");
            }
        }

        private void btnsetting_Click(object sender, RoutedEventArgs e)
        {
            // Settings button implementation
        }
    }
}