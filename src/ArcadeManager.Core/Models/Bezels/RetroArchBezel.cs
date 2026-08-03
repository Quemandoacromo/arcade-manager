using System;
using System.Collections.Generic;
using System.Text;

namespace ArcadeManager.Core.Models.Bezels;

/// <summary>
/// A RetroArch overlay config file
/// </summary>
public class RetroArchBezel
{
    /// <summary>
    /// Gets the name of the overlay image file.
    /// </summary>
    public string OverlayImageFileName { get; set; }

    /// <summary>
    /// Gets the overlay image full path.
    /// </summary>
    public string OverlayImagePath { get; set; }

    /// <summary>
    /// Gets the source resolution.
    /// </summary>
    public Bounds SourceResolution { get; set; }

    /// <summary>
    /// Gets the source screen position.
    /// </summary>
    public Bounds SourceScreenPosition { get; set; }
}