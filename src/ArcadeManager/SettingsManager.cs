using System;
using System.IO;
using System.Threading;
using ArcadeManager.Core.Infrastructure;
using ArcadeManager.Core.Models;
using ArcadeManager.Models;

namespace ArcadeManager;

/// <summary>
/// Settings manager
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SettingsManager"/> class.
/// </remarks>
/// <param name="fileName">Name of the file.</param>
public class SettingsManager(string fileName)
{
    private static readonly Lock FileLock = new();
    private static AppSettingsModel appSettingsModel;
    private readonly string _filePath = GetLocalFilePath(fileName);

    /// <summary>
    /// Gets the application settings and parameters.
    /// </summary>
    public static AppSettingsModel AppSettings
    {
        get
        {
            if (appSettingsModel == null)
            {
                var file = Path.Combine(ArcadeManagerEnvironment.BasePath, "appsettings.json");
                appSettingsModel = Serializer.Deserialize<AppSettingsModel>(File.ReadAllText(file));
            }

            return appSettingsModel;
        }
    }

    /// <summary>
    /// Loads the settings.
    /// </summary>
    /// <returns>The settings</returns>
    public UserSettings LoadSettings() =>
        File.Exists(_filePath) ?
        Serializer.Deserialize<UserSettings>(File.ReadAllText(_filePath)) :
        null;

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public void SaveSettings(UserSettings settings)
    {
        string json = Serializer.Serialize(settings);

        lock (FileLock)
        {
            var fi = new FileInfo(_filePath);
            if (!Directory.Exists(fi.DirectoryName))
            {
                Directory.CreateDirectory(fi.DirectoryName);
            }

            File.WriteAllText(_filePath, json);
        }
    }

    /// <summary>
    /// Gets the local file path.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <returns>The local file path</returns>
    private static string GetLocalFilePath(string fileName)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, fileName);
    }
}