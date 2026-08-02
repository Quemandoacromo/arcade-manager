using ArcadeManager.Core;
using ArcadeManager.Core.Infrastructure;
using ArcadeManager.Core.Models;
using ElectronNET.API;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ArcadeManager;

/// <summary>
/// Provides environment values relative to ArcadeManager
/// </summary>
public class ArcadeManagerEnvironment : IEnvironment
{
    private static readonly SettingsManager mgr = new(@"ArcadeManager\userSettings.json");
    private static readonly UserSettings settings = mgr.LoadSettings() ?? new UserSettings();
    private static AppData _appData;
    private static string _basePath;
    private static string _platform;

    /// <summary>
    /// Gets the current AppData values
    /// </summary>
    public static AppData AppData
    {
        get
        {
            if (_appData != null) { return _appData; }

            string content = File.ReadAllText(Path.Join(BasePath, "Data", "appdata.json"));
            _appData = Serializer.Deserialize<AppData>(content);

            return _appData;
        }
    }

    /// <summary>
    /// Gets the base application path
    /// </summary>
    public static string BasePath
    {
        get
        {
            if (!string.IsNullOrEmpty(_basePath)) { return _basePath; }

            // See stackoverflow.com/a/58307732/6776
            using var processModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
            _basePath = System.IO.Path.GetDirectoryName(processModule?.FileName);

            return _basePath;
        }
    }

    /// <summary>
    /// Gets the application platform (win32, darwin, linux)
    /// </summary>
    public static string Platform
    {
        get
        {
            if (!string.IsNullOrEmpty(_platform)) { return _platform; }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _platform = "darwin";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platform = "win32";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = "linux";
            }
            else
            {
                throw new NotImplementedException("If you want to run Arcade Manager on something else than Linux, Mac or Windows, you'll have some coding to do!");
            }

            return _platform;
        }
    }

    /// <summary>
    /// Gets or sets the path to the last used main CSV file
    /// </summary>
    public static string SettingsLastCsvMainPath
    {
        get
        {
            return settings.LastCsvMainPath ?? string.Empty;
        }

        set
        {
            settings.LastCsvMainPath = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets or sets the path to the last used secondary CSV file
    /// </summary>
    public static string SettingsLastCsvSecondaryPath
    {
        get
        {
            return settings.LastCsvSecondaryPath ?? string.Empty;
        }

        set
        {
            settings.LastCsvSecondaryPath = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets or sets the path to the last used target CSV file
    /// </summary>
    public static string SettingsLastCsvTargetPath
    {
        get
        {
            return settings.LastCsvTargetPath ?? string.Empty;
        }

        set
        {
            settings.LastCsvTargetPath = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets or sets the path to the last used CSV to manage roms
    /// </summary>
    public static string SettingsLastRomCsvPath
    {
        get
        {
            return settings.LastRomCsvPath ?? string.Empty;
        }

        set
        {
            settings.LastRomCsvPath = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets or sets the path to the last used romset
    /// </summary>
    public static string SettingsLastRomFullsetPath
    {
        get
        {
            return settings.LastRomFullsetPath ?? string.Empty;
        }

        set
        {
            settings.LastRomFullsetPath = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets or sets the path to the last used target rom selection
    /// </summary>
    public static string SettingsLastRomTargetPath
    {
        get
        {
            return settings.LastRomTargetPath ?? string.Empty;
        }

        set
        {
            settings.LastRomTargetPath = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets or sets the settings OS.
    /// </summary>
    public static string SettingsOs
    {
        get
        {
            return settings.Os ?? string.Empty;
        }
        set
        {
            settings.Os = value;

            mgr.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Gets the app version.
    /// </summary>
    /// <returns>The app version</returns>
    public static async Task<string> GetVersion()
    {
        using var processModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        return processModule?.FileVersionInfo.FileVersion ?? await Electron.App.GetVersionAsync();
    }

    /// <summary>
    /// Gets the application data
    /// </summary>
    /// <returns>The application data</returns>
    public AppData GetAppData()
    {
        return AppData;
    }

    /// <summary>
    /// Gets the application base path
    /// </summary>
    /// <returns></returns>
    public string GetBasePath()
    {
        return BasePath;
    }

    /// <summary>
    /// Gets the OS from the settings
    /// </summary>
    /// <returns>The OS</returns>
    public string GetSettingsOs()
    {
        return SettingsOs;
    }

    /// <summary>
    /// Gets the current user settings
    /// </summary>
    /// <returns>
    /// The current user settings
    /// </returns>
    public UserSettings SettingsGet()
    {
        return settings;
    }

    /// <summary>
    /// Adds the specified version to the list of ignored versions
    /// </summary>
    /// <param name="version">The version.</param>
    public void SettingsIgnoredVersionAdd(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) { return; }

        settings.IgnoredVersions.Add(version);

        mgr.SaveSettings(settings);
    }

    /// <summary>
    /// Gets a value indicating whether the specified version should be ignored
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>Whether the version should be ignored</returns>
    public bool SettingsIgnoredVersionHas(string version)
    {
        return settings.IgnoredVersions.Contains(version);
    }

    /// <summary>
    /// Saves the specified user settings
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public void SettingsSave(UserSettings settings)
    {
        mgr.SaveSettings(settings);
    }
}