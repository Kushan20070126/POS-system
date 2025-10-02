using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FoodPOS
{
    public partial class ProductDialog : Window
    {
        private List<Category> Categories { get; set; }
        private ProductVM EditingProduct { get; set; }
        private bool IsEditing => EditingProduct != null;

        // Constructor for adding new product
        public ProductDialog(List<Category> categories)
        {
            InitializeComponent();
            Categories = categories;
            Loaded += ProductDialog_Loaded;
        }

        // Constructor for editing existing product
        public ProductDialog(List<Category> categories, ProductVM product)
        {
            InitializeComponent();
            Categories = categories;
            EditingProduct = product;
            Loaded += ProductDialog_Loaded;
        }

        private void ProductDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate categories
            cmbCategory.ItemsSource = Categories;

            if (IsEditing)
            {
                Title = "Edit Product - FoodPOS";
                // Populate fields with existing data
                txtName.Text = EditingProduct.Name;
                txtDescription.Text = EditingProduct.Description;
                txtPrice.Text = EditingProduct.Price.ToString("N2");
                txtCostPrice.Text = EditingProduct.CostPrice.ToString("N2");
                txtStockQuantity.Text = EditingProduct.StockQuantity.ToString();
                txtBarcode.Text = EditingProduct.Barcode;
                chkIsActive.IsChecked = EditingProduct.IsActive;

                // Select the correct category
                if (EditingProduct.CategoryId > 0)
                {
                    var category = Categories.FirstOrDefault(c => c.CategoryId == EditingProduct.CategoryId);
                    if (category != null)
                        cmbCategory.SelectedItem = category;
                }
            }
            else
            {
                Title = "Add New Product - FoodPOS";
                txtStockQuantity.Text = "0";
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                if (IsEditing)
                {
                    UpdateProduct();
                }
                else
                {
                    CreateProduct();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter a product name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return false;
            }

            if (!decimal.TryParse(txtPrice.Text, out decimal price) || price < 0)
            {
                MessageBox.Show("Please enter a valid price.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPrice.Focus();
                return false;
            }

            if (!decimal.TryParse(txtCostPrice.Text, out decimal costPrice) || costPrice < 0)
            {
                MessageBox.Show("Please enter a valid cost price.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCostPrice.Focus();
                return false;
            }

            if (!int.TryParse(txtStockQuantity.Text, out int stock) || stock < 0)
            {
                MessageBox.Show("Please enter a valid stock quantity.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtStockQuantity.Focus();
                return false;
            }

            return true;
        }

        private void CreateProduct()
        {
            using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
            {
                connection.Open();

                var query = @"
                    INSERT INTO Products (Name, Description, Price, CostPrice, StockQuantity, Barcode, IsActive, CategoryId)
                    VALUES (@Name, @Description, @Price, @CostPrice, @StockQuantity, @Barcode, @IsActive, @CategoryId)";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", txtName.Text.Trim());
                    command.Parameters.AddWithValue("@Description", txtDescription.Text.Trim());
                    command.Parameters.AddWithValue("@Price", decimal.Parse(txtPrice.Text));
                    command.Parameters.AddWithValue("@CostPrice", decimal.Parse(txtCostPrice.Text));
                    command.Parameters.AddWithValue("@StockQuantity", int.Parse(txtStockQuantity.Text));
                    command.Parameters.AddWithValue("@Barcode", txtBarcode.Text.Trim());
                    command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? true);
                    command.Parameters.AddWithValue("@CategoryId", cmbCategory.SelectedItem is Category category ? category.CategoryId : (object)DBNull.Value);

                    command.ExecuteNonQuery();
                }
            }
        }

        private void UpdateProduct()
        {
            using (var connection = new SqlConnection("Server=localhost;Database=FoodPOS;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=true;"))
            {
                connection.Open();

                var query = @"
                    UPDATE Products 
                    SET Name = @Name, Description = @Description, Price = @Price, 
                        CostPrice = @CostPrice, StockQuantity = @StockQuantity, 
                        Barcode = @Barcode, IsActive = @IsActive, CategoryId = @CategoryId
                    WHERE ProductId = @ProductId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ProductId", EditingProduct.ProductId);
                    command.Parameters.AddWithValue("@Name", txtName.Text.Trim());
                    command.Parameters.AddWithValue("@Description", txtDescription.Text.Trim());
                    command.Parameters.AddWithValue("@Price", decimal.Parse(txtPrice.Text));
                    command.Parameters.AddWithValue("@CostPrice", decimal.Parse(txtCostPrice.Text));
                    command.Parameters.AddWithValue("@StockQuantity", int.Parse(txtStockQuantity.Text));
                    command.Parameters.AddWithValue("@Barcode", txtBarcode.Text.Trim());
                    command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? true);
                    command.Parameters.AddWithValue("@CategoryId", cmbCategory.SelectedItem is Category category ? category.CategoryId : (object)DBNull.Value);

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}