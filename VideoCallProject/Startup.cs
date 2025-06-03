using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(VideoCallProject.Startup))]

namespace VideoCallProject
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // SignalR konfigürasyonu
            var hubConfiguration = new HubConfiguration()
            {
                EnableDetailedErrors = true,
                EnableJavaScriptProxies = true
            };
            
            // SignalR endpoint'ini map et
            app.MapSignalR("/signalr", hubConfiguration);
            
            // Global SignalR ayarları
            GlobalHost.Configuration.DisconnectTimeout = System.TimeSpan.FromSeconds(60);
            GlobalHost.Configuration.KeepAlive = System.TimeSpan.FromSeconds(15);
            GlobalHost.Configuration.ConnectionTimeout = System.TimeSpan.FromSeconds(30);
            GlobalHost.Configuration.DefaultMessageBufferSize = 1000;
        }
    }
}
