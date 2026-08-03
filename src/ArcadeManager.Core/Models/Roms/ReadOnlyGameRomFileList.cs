using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ArcadeManager.Core.Models.Roms;

public class ReadOnlyGameRomFileList(IEnumerable<ReadOnlyGameRomFile> roms) : IReadOnlyList<ReadOnlyGameRomFile>
{
    public ReadOnlyGameRomFile this[int index] => roms.ElementAt(index);

    public int Count => roms.Count();

    public IEnumerator<ReadOnlyGameRomFile> GetEnumerator() => roms.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => roms.GetEnumerator();
}
