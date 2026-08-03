using ArcadeManager.Core.Services;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ArcadeManager.Core.Models.Zip;

[DebuggerDisplay("{FullName} ({Crc})")]
public class ZipEntry(ZipArchiveEntry entry)
{
    public string Crc => entry?.Crc32.ToString("X4").PadLeft(8, '0').ToLower();
    public string FullName => entry?.FullName;
    public long Length => entry?.Length ?? 0;
    public string Name => entry?.Name;

    public void Delete()
    {
        entry?.Delete();
    }

    public async Task<byte[]> GetContentAsync()
    {
        using var stream = await entry.OpenAsync();
        using var ms = new MemoryStream();

        await stream.CopyToAsync(ms);
        ms.Position = 0;
        return ms.ToArray();
    }

    public string GetSha1()
    {
        // see https://stackoverflow.com/questions/1993903
#pragma warning disable S4790 // I do not control the MAME expected hashes format
        var sha1 = SHA1.Create();
#pragma warning restore S4790
        byte[] hash = sha1.ComputeHash(entry.Open());
        StringBuilder formatted = new(2 * hash.Length);
        foreach (byte b in hash)
        {
            formatted.AppendFormat("{0:X2}", b);
        }

        return formatted.ToString().ToLower();
    }

    public Stream Open()
    {
        return entry?.Open();
    }

    public Task<Stream> OpenAsync()
    {
        return entry?.OpenAsync();
    }
}