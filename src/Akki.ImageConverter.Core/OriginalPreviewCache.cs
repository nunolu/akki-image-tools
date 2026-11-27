namespace Akki.ImageConverter.Core;

// In-memory only. File identity is checked before and after decoding.
public sealed class OriginalPreviewCache(int maxEntries = 3, long maxBytes = 24 * 1024 * 1024)
{
    private sealed record Entry(string Path, long Length, DateTime Modified, byte[] Data);
    private readonly object _gate = new();
    private readonly LinkedList<Entry> _entries = new();
    private long _bytes;
    private int _generation;

    public byte[] GetOrCreate(string path, Func<string, byte[]> create)
    {
        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        var length = file.Length;
        var modified = file.LastWriteTimeUtc;
        int generation;
        lock (_gate)
        {
            generation = _generation;
            for (var node = _entries.First; node is not null; node = node.Next)
            {
                var entry = node.Value;
                if (!string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.Length == length && entry.Modified == modified)
                {
                    _entries.Remove(node); _entries.AddFirst(node);
                    return entry.Data;
                }
                _bytes -= entry.Data.LongLength; _entries.Remove(node); break;
            }
        }
        var data = create(fullPath);
        file.Refresh();
        if (!file.Exists || file.Length != length || file.LastWriteTimeUtc != modified || data.LongLength > maxBytes || maxEntries <= 0) return data;
        lock (_gate)
        {
            if (generation != _generation) return data;
            for (var node = _entries.First; node is not null; node = node.Next)
            {
                if (!string.Equals(node.Value.Path, fullPath, StringComparison.OrdinalIgnoreCase)) continue;
                _bytes -= node.Value.Data.LongLength; _entries.Remove(node); break;
            }
            _entries.AddFirst(new Entry(fullPath, length, modified, data)); _bytes += data.LongLength;
            while (_entries.Count > maxEntries || _bytes > maxBytes)
            {
                var last = _entries.Last!; _bytes -= last.Value.Data.LongLength; _entries.RemoveLast();
            }
        }
        return data;
    }

    public void Clear()
    {
        lock (_gate) { _generation++; _entries.Clear(); _bytes = 0; }
    }
}
