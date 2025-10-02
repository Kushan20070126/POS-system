using System;
using System.Windows;

namespace FoodPOS
{
    public partial class App : Application
    {
        private MicroservicesHost _microservicesHost;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Start microservices when application starts
            _microservicesHost = new MicroservicesHost();
            _microservicesHost.StartMicroservices();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop microservices when application exits
            _microservicesHost?.StopMicroservices();
            base.OnExit(e);
        }
    }
}