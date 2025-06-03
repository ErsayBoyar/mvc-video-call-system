using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.AspNet.SignalR;

namespace VideoCallProject
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Area registration
            AreaRegistration.RegisterAllAreas();
            
            // Route configuration
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            
            // SignalR Global Configuration
            ConfigureSignalR();
            
            // Custom configurations
            ConfigureApplication();
        }

        private void ConfigureSignalR()
        {
            // SignalR global ayarları
            GlobalHost.Configuration.DisconnectTimeout = TimeSpan.FromSeconds(60);
            GlobalHost.Configuration.KeepAlive = TimeSpan.FromSeconds(15);
            GlobalHost.Configuration.ConnectionTimeout = TimeSpan.FromSeconds(30);
            GlobalHost.Configuration.DefaultMessageBufferSize = 1000;
            
            // Debug mode için detaylı hata mesajları
            #if DEBUG
            GlobalHost.Configuration.EnableDetailedErrors = true;
            #endif
            
            // JSON serialization ayarları
            var jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc
            };
            
            var serializer = new Newtonsoft.Json.JsonSerializer();
            foreach (var setting in jsonSerializerSettings.GetType().GetProperties())
            {
                var value = setting.GetValue(jsonSerializerSettings, null);
                setting.SetValue(serializer, value, null);
            }
            
            GlobalHost.DependencyResolver.Register(typeof(Newtonsoft.Json.JsonSerializer), () => serializer);
        }

        private void ConfigureApplication()
        {
            // ViewEngine ayarları
            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(new RazorViewEngine());
            
            // Model binder ayarları
            ModelBinders.Binders.DefaultBinder = new DefaultModelBinder();
            
            // Filter ayarları
            GlobalFilters.Filters.Add(new HandleErrorAttribute());
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            if (exception != null)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Unhandled exception: {exception}");
                
                // Clear the error
                Server.ClearError();
                
                // Redirect to error page
                Response.Redirect("~/Error");
            }
        }

        protected void Application_PreSendRequestHeaders()
        {
            Response.Headers.Remove("Server");
        }

        protected void Session_Start()
        {
            // Session başlatıldığında yapılacak işlemler
        }

        protected void Session_End()
        {
            // Session sonlandırıldığında yapılacak işlemler
        }
    }

    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Video Call specific routes
            routes.MapRoute(
                name: "VideoCallRoom",
                url: "VideoCall/Room/{roomId}",
                defaults: new { controller = "VideoCall", action = "Room" },
                constraints: new { roomId = @"^[a-zA-Z0-9\-_]+$" }
            );

            routes.MapRoute(
                name: "VideoCallApi",
                url: "api/videocall/{action}",
                defaults: new { controller = "VideoCall" }
            );

            // Default route
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "VideoCall", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
