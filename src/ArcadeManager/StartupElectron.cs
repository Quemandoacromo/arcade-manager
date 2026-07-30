using ElectronNET.API;
using ElectronNET.API.Entities;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ArcadeManager;

public static class StartupElectron
{
    /// <summary>
    /// Creates the main browser window
    /// </summary>
    /// <returns>The main browser window</returns>
    public static async Task<BrowserWindow> CreateMainWindow()
    {
        var browserWindow = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
        {
            Width = 1280,
            Height = 800,
            Show = true,
            Resizable = true
        });

        await browserWindow.WebContents.Session.ClearCacheAsync();

        browserWindow.OnReadyToShow += () => browserWindow.Show();
        browserWindow.SetTitle("Arcade Manager");

        return browserWindow;
    }

    /// <summary>
    /// Initializes the Electron app
    /// </summary>
    public static async Task ElectronBootstrap(BrowserWindow mainWindow)
    {
        BuildAppMenu();

        // re-create main window if last window has been closed
        await Electron.App.On("activate", async obj =>
        {
            var hasWindows = (bool)obj;

            if (!hasWindows)
            {
                mainWindow = await CreateMainWindow();
            }
            else
            {
                mainWindow?.Show();
            }

            // initializes RPC message handling
            Program.GetMessageHandler(null)?.Handle(mainWindow);

        });
    }

    /// <summary>
    /// Builds the application menus
    /// </summary>
    private static void BuildAppMenu()
    {
        static MenuItem firstMenu()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new()
                {
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
            else
            {
                return new MenuItem
                {
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
}