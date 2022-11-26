﻿namespace ArcadeManager.Models;

/// <summary>
/// Model for the wizard
/// </summary>
public class Wizard {

    /// <summary>
    /// Gets or sets a value indicating whether to do the overlays install.
    /// </summary>
    public bool DoOverlays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to do the roms selection.
    /// </summary>
    public bool DoRoms { get; set; }

    /// <summary>
    /// Gets or sets the target emulator.
    /// </summary>
    public string Emulator { get; set; }

    /// <summary>
    /// Gets or sets the roms lists.
    /// </summary>
    public string[] Lists { get; set; }

    /// <summary>
    /// Gets or sets the rom action (copy/clean).
    /// </summary>
    public string RomAction { get; set; }
}