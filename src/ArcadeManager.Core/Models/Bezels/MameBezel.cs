namespace ArcadeManager.Core.Models.Bezels;

/// <summary>
/// A MAME bezel representation
/// </summary>
public class MameBezel(Offset offset, Bounds sourceResolution, Bounds sourceScreenPosition, string bezelFileName)
{
    /// <summary>
    /// Gets the bezel file name
    /// </summary>
    public string BezelFileName { get; set; } = bezelFileName;

    /// <summary>
    /// Gets the offset, if any
    /// </summary>
    public Offset Offset { get; set; } = offset;

    /// <summary>
    /// Gets the source resolution
    /// </summary>
    public Bounds SourceResolution { get; set; } = sourceResolution;

    /// <summary>
    /// Gets the source screen coordinates
    /// </summary>
    public Bounds SourceScreenPosition { get; set; } = sourceScreenPosition;
}