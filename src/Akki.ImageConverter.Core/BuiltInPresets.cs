namespace Akki.ImageConverter.Core;

public static class BuiltInPresets
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "Website images (WebP)", "Smaller email images (JPEG)", "Lossless images (PNG)"
    });

    public static ConversionOptions Apply(string name, string outputDirectory, bool outputToSourceFolder, bool keepFolderStructure)
    {
        var options = new ConversionOptions
        {
            OutputDirectory = outputDirectory,
            OutputToSourceFolder = outputToSourceFolder,
            KeepFolderStructure = keepFolderStructure
        };
        return name switch
        {
            "Website images (WebP)" => options with { Format = OutputKind.WebP, Quality = 80, Resize = ResizeKind.Pixels, Width = 1920, Height = 1920 },
            "Smaller email images (JPEG)" => options with { Format = OutputKind.Jpeg, Quality = 80, Resize = ResizeKind.Pixels, Width = 1600, Height = 1600 },
            "Lossless images (PNG)" => options with { Format = OutputKind.Png, Resize = ResizeKind.Original },
            _ => throw new ArgumentException("Unknown built-in preset.", nameof(name))
        };
    }
}
