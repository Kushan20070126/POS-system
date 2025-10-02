using Microsoft.Owin.Hosting;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace FoodPOS
{
    public class MicroservicesHost
    {
        private IDisposable _authService;
        private IDisposable _productService;
        private IDisposable _salesService;

        public void StartMicroservices()
        {
            try
            {
                // Start all microservices on different ports
                _authService = WebApp.Start<Startup>("http://localhost:5001");
                _productService = WebApp.Start<Startup>("http://localhost:5002");
                _salesService = WebApp.Start<Startup>("http://localhost:5003");

                Application.Current.Properties["MicroservicesRunning"] = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start microservices: {ex.Message}", "Error");
            }
        }

        public void StopMicroservices()
        {
            _authService?.Dispose();
            _productService?.Dispose();
            _salesService?.Dispose();

            Application.Current.Properties["MicroservicesRunning"] = false;
        }
    }
}