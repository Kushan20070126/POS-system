using System;
using System.Data.SqlClient;
using System.Windows;

public static class DatabaseHelper
{
    // Use YOUR actual password from Docker
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

                // Test if we can execute a simple query
                using (var command = new SqlCommand("SELECT 1", connection))
                {
                    var result = command.ExecuteScalar();
                    return result != null;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database Connection Failed:\n\nError: {ex.Message}",
                          "Database Connection Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
            return false;
        }
    }
}