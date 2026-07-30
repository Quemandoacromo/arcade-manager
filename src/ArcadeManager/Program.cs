using ArcadeManager.Core;
using ArcadeManager.Core.Infrastructure.Interfaces;
using ArcadeManager.Core.Services;
using ElectronNET.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeManager;

/// <summary>
/// Base program entry point
/// </summary>
public static class Program
{
    private static readonly Lock messageHandlerLock = new();
    private static IElectronMessageHandler messageHandler;

    /// <summary>
    /// Gets the message handler.
    /// </summary>
    /// <param name="container">The DI container.</param>
    /// <returns>The message handler</returns>
    public static IElectronMessageHandler GetMessageHandler(SimpleInjector.Container container)
    {
        if (container != null)
        {
            lock (messageHandlerLock)
            {
                messageHandler ??= new ElectronMessageHandler(
                        container.GetInstance<Core.Services.Interfaces.IServiceProvider>(),
                        container.GetInstance<IEnvironment>(),
                        container.GetInstance<IFileSystem>());
            }
        }

        if (container == null && messageHandler == null)
        {
            throw new Exception("No DI container provided and message handler not initialized");
        }

        return messageHandler;
    }

    /// <summary>
    /// Defines the entry point of the application.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static async Task Main(string[] args)
    {
        Localizer.EnsureLocale();

        await CreateHostBuilder(args).Build().RunAsync();
    }

    /// <summary>
    /// Creates the host builder.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The host builder</returns>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseElectron(args, async () =>
                {
                    BrowserWindow mainWindow = await StartupElectron.CreateMainWindow();
                    await StartupElectron.ElectronBootstrap(mainWindow);
                });
                webBuilder.UseStartup<Startup>();
            });
}