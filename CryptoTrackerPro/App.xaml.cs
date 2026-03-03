using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Net.Http;
using System.Windows;

namespace CryptoTrackerPro
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; }

        public App()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<HttpClient>();
            sc.AddSingleton<ICryptoProvider, BinanceProvider>();
            sc.AddSingleton<MainViewModel>();
            sc.AddSingleton<MainWindow>();
            Services = sc.BuildServiceProvider();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            Services.GetRequiredService<MainWindow>().Show();
        }
    }

}
