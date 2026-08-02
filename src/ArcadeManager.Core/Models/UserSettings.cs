using System.Collections.Generic;

namespace ArcadeManager.Core.Models;

/// <summary>
/// The application settings
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Gets or sets the ignored app versions.
    /// </summary>
    public List<string> IgnoredVersions { get; set; } = new();

    /// <summary>
    /// Gets or sets the path to the last used main CSV file
    /// </summary>
    public string LastCsvMainPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the last used secondary CSV file
    /// </summary>
    public string LastCsvSecondaryPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the last used target CSV file
    /// </summary>
    public string LastCsvTargetPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the last used CSV to manage roms
    /// </summary>
    public string LastRomCsvPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the last used romset
    /// </summary>
    public string LastRomFullsetPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the last used target rom selection
    /// </summary>
    public string LastRomTargetPath { get; set; }

    /// <summary>
    /// Gets or sets the OS (Retropie/Recalbox)
    /// </summary>
    public string Os { get; set; }
}