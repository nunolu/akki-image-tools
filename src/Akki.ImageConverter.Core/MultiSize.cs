namespace Akki.ImageConverter.Core;

public static class MultiSize
{
    public static uint[] Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<uint>();
        var values = text.Split(',');
        if (values.Length > 8) throw new ArgumentException("Choose at most eight output sizes.");
        var sizes = new List<uint>();
        foreach (var entry in values)
        {
            if (!uint.TryParse(entry.Trim(), out var width) || width is < 1 or > 32768)
                throw new ArgumentException("Enter comma-separated whole-number widths from 1 to 32768.");
            if (sizes.Contains(width)) throw new ArgumentException("Each output width must be different.");
            sizes.Add(width);
        }
        return sizes.ToArray();
    }

    public static ConversionOptions ForWidth(ConversionOptions options, uint width) => options with
    {
        OutputWidths = "", Resize = ResizeKind.Pixels, Width = width, Height = 32768,
        KeepAspectRatio = true, PadToDimensions = false, FilenameSuffix = options.FilenameSuffix + $"-w{width}"
    };
}
