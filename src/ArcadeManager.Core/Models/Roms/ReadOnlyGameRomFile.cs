using System.Diagnostics;

namespace ArcadeManager.Models.Roms;

/// <summary>
/// A game rom file (rom files inside the zip)
/// </summary>
[DebuggerDisplay("{Name} ({Crc})")]
public class ReadOnlyGameRomFile(
    string zipFileName,
    string zipFileFolder,
    string name,
    string path,
    long size,
    string crc,
    string sha1) : IGameRomFile
{
    public string ZipFileName => zipFileName;

    public string ZipFileFolder => zipFileFolder;

    public string ZipFilePath => System.IO.Path.Join(ZipFileFolder, ZipFileName);

    public string Name => name;

    public string Path => path;

    public long Size => size;

    public string Crc => crc;

    public string Sha1 => sha1;

    public GameRomFile ToGameRomFile(string zipPath) {
        var fileName = zipFileName;
        var fileFolder = zipFileFolder;

        if (!string.IsNullOrEmpty(zipPath)) {
            fileName = System.IO.Path.GetFileName(zipPath);
            fileFolder = System.IO.Path.GetDirectoryName(zipPath);
        }

        return new GameRomFile(fileName, fileFolder) {
            Name = this.Name,
            Path = this.Path,
            Crc = this.Crc,
            Sha1 = this.Sha1,
            Size = this.Size,
            ErrorReason = ErrorReason.None
        };
    }
}
