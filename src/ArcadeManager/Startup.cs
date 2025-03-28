using ArcadeManager.Core;
using ArcadeManager.Core.Infrastructure;
using ArcadeManager.Core.Infrastructure.Interfaces;
using ArcadeManager.Core.Services;
using ArcadeManager.Core.Services.Interfaces;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ArcadeManager;

/// <summary>
/// Startup app
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Startup"/> class.
/// </remarks>
/// <param name="configuration">The configuration.</param>
public class Startup(IConfiguration configuration)
{
    private readonly Container container = new();

    private IWebHostEnvironment env;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    /// <value>The configuration.</value>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Configures the application pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="env">The host environment.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
        this.env = env;

        app.UseSimpleInjector(container);

        if (env.IsDevelopment()) {
            app.UseDeveloperExceptionPage();
        }
        else {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });

        try {
            container.Verify();
        }
        catch (Exception ex) {
            Console.WriteLine($"An error has occurred during injection: {ex.Message}");
            Environment.Exit(1);
        }

        if (HybridSupport.IsElectronActive) {
            ElectronBootstrap().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Configures the services.
    /// </summary>
    /// <param name="services">The services.</param>
    public void ConfigureServices(IServiceCollection services) {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options => {
            var supportedCultures = Localizer.GetSupportedCultures();

            options.DefaultRequestCulture = new RequestCulture("en", "en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
        });

        services.AddControllersWithViews();

        services.AddLogging();

        // bind SimpleInjector to .Net injection
        services.AddSimpleInjector(container, options => {
            options.AddAspNetCore().AddControllerActivation();
            options.AddLocalization();
            options.AddLogging();
        });

        this.InitializeInjection(services);
    }

    /// <summary>
    /// Initializes the Electron app
    /// </summary>
    public async Task ElectronBootstrap() {
        BuildAppMenu();

        var mainWindow = await CreateMainWindow();

        // re-create main window if last window has been closed
        await Electron.App.On("activate", obj => {
            var hasWindows = (bool)obj;

            if (!hasWindows) {
                mainWindow = Task.Run(CreateMainWindow).Result;
            }
            else {
                mainWindow?.Show();
            }
        });

        // initializes RPC message handling
        var msgHandler = container.GetInstance<IElectronMessageHandler>();
        msgHandler?.Handle(mainWindow);
    }

    /// <summary>
    /// Builds the application menus
    /// </summary>
    private static void BuildAppMenu() {
        static MenuItem firstMenu() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return new() {
                    Label = "ArcadeManager",
                    Submenu =
                    [
                        new() { Role = MenuRole.about },
                        new() { Type = MenuType.separator },
                        new()
                        {
                            Label = "Open Developer Tools",
                            Accelerator = "CmdOrCtrl+I",
                            Click = () => Electron.WindowManager.BrowserWindows.First().WebContents.OpenDevTools()
                        },
                        new() { Type = MenuType.separator },
                        new() { Role = MenuRole.hide },
                        new() { Role = MenuRole.hideothers },
                        new() { Type = MenuType.separator },
                        new() { Role = MenuRole.quit }
                    ]
                };
            }
            else {
                return new MenuItem {
                    Label = "File",
                    Submenu =
                    [
                        new() { Role = MenuRole.about },
                        new() { Type = MenuType.separator },
                        new()
                        {
                            Label = "Open Developer Tools",
                            Accelerator = "CmdOrCtrl+I",
                            Click = () => Electron.WindowManager.BrowserWindows.First().WebContents.OpenDevTools()
                        },
                        new() { Type = MenuType.separator },
                        new() { Role = MenuRole.quit }
                    ]
                };
            }
        }

        var menu = new MenuItem[]
        {
			// App name/file menu
			firstMenu(),

			// Edit
			new() {
                Label = "Edit",
                Type = MenuType.submenu,
                Submenu = [
                    new() { Label = "Undo", Accelerator = "CmdOrCtrl+Z", Role = MenuRole.undo },
                    new() { Label = "Redo", Accelerator = "Shift+CmdOrCtrl+Z", Role = MenuRole.redo },
                    new() { Type = MenuType.separator },
                    new() { Label = "Cut", Accelerator = "CmdOrCtrl+X", Role = MenuRole.cut },
                    new() { Label = "Copy", Accelerator = "CmdOrCtrl+C", Role = MenuRole.copy },
                    new() { Label = "Paste", Accelerator = "CmdOrCtrl+V", Role = MenuRole.paste },
                ]
            },

			// Window
			new() {
                Label = "Window",
                Role = MenuRole.window,
                Type = MenuType.submenu,
                Submenu = [
                    new() { Label = "Minimize", Accelerator = "CmdOrCtrl+M", Role = MenuRole.minimize },
                    new() { Label = "Close", Accelerator = "CmdOrCtrl+W", Role = MenuRole.close }
                ]
            },

			// Help
			new() {
                Label = "Help",
                Role = MenuRole.help,
                Type = MenuType.submenu,
                Submenu = [
                    new()
                    {
                        Label = "Learn More",
                        #pragma warning disable S1075
                        Click = async () => await Electron.Shell.OpenExternalAsync("https://github.com/cosmo0/arcade-manager/")
                        #pragma warning restore S1075
                    }
                ]
            }
        };

        Electron.Menu.SetApplicationMenu(menu);
    }

    /// <summary>
    /// Creates the main browser window
    /// </summary>
    /// <returns>The main browser window</returns>
    private async Task<BrowserWindow> CreateMainWindow() {
        var browserWindow = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions {
            Width = 1280,
            Height = 800,
            Show = true,
            Resizable = true
        });

        await browserWindow.WebContents.Session.ClearCacheAsync();

        browserWindow.OnReadyToShow += () => browserWindow.Show();
        browserWindow.SetTitle("Arcade Manager");

        if (this.env.IsDevelopment()) {
            browserWindow.WebContents.OpenDevTools();
        }

        return browserWindow;
    }

    /// <summary>
    /// Configures the dependency injection.
    /// </summary>
    private void InitializeInjection(IServiceCollection services) {
        try {
            // environment
            container.Register<IEnvironment, ArcadeManagerEnvironment>(Lifestyle.Singleton);

            // infrastructure
            container.Register<IWebClientFactory, WebClientFactory>(Lifestyle.Singleton);
            container.Register<IFileSystem, FileSystem>(Lifestyle.Singleton);
            container.Register<IDatFile, DatFile>(Lifestyle.Singleton);

            // services
            container.Register<IDownloader, Downloader>(Lifestyle.Singleton);
            container.Register<ICsv, Csv>(Lifestyle.Singleton);
            container.Register<IOverlays, Overlays>(Lifestyle.Singleton);
            container.Register<IRoms, Roms>(Lifestyle.Singleton);
            container.Register<IUpdater, Updater>(Lifestyle.Singleton);
            container.Register<ILocalizer, Localizer>(Lifestyle.Singleton);
            container.Register<IWizard, Wizard>(Lifestyle.Singleton);
            container.Register<IDatChecker, DatChecker>(Lifestyle.Singleton);
            container.Register<Core.Services.Interfaces.IServiceProvider, Core.Services.ServiceProvider>(Lifestyle.Singleton);

            // message handler (SimpleInjector returns the same singleton if it's the same implementation)
            container.Register<IMessageHandler, ElectronMessageHandler>(Lifestyle.Singleton);
            container.Register<IElectronMessageHandler, ElectronMessageHandler>(Lifestyle.Singleton);

            // view localization uses dotnet tooling
            services.AddSingleton(provider => container.GetInstance<ILocalizer>());
        }
        catch (Exception ex) {
            Console.WriteLine($"An error has occurred during injection: {ex.Message}");
            Environment.Exit(1);
        }
    }
}