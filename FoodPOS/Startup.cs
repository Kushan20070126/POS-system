using Microsoft.Owin;
using Owin;
using System.Web.Http;

[assembly: OwinStartup(typeof(FoodPOS.Startup))]
namespace FoodPOS
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configure CORS
            app.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);

            // Configure Web API
            var config = new HttpConfiguration();

            // Enable attribute routing
            config.MapHttpAttributeRoutes();

            // Default route
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Use Web API
            app.UseWebApi(config);
        }
    }
}