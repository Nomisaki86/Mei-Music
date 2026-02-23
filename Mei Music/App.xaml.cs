using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mei_Music.Services;
using Mei_Music.ViewModels;

namespace Mei_Music
{
    /// <summary>
    /// Application entry point for Mei Music.
    /// Builds the dependency-injection host and owns host start/stop lifecycle.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Generic host that provides dependency injection and service lifetime management.
        /// </summary>
        private IHost _host;

        /// <summary>
        /// Configures all app services, view-models, and root windows.
        /// </summary>
        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register Services
                    services.AddSingleton<IFileService, FileService>();
                    services.AddSingleton<IPlaylistSortService, PlaylistSortService>();
                    services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
                    services.AddSingleton<IDialogService, DialogService>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();

                    // Register Views
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        /// <summary>
        /// Starts background services and opens the application's main window.
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        /// <summary>
        /// Gracefully stops hosted services when the application exits.
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync();
            }

            base.OnExit(e);
        }
    }
}
