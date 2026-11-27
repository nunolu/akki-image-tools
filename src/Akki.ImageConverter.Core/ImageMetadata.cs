using ImageMagick;

namespace Akki.ImageConverter.Core;

public sealed record ImageMetadata(
    string Format, uint Width, uint Height, long FileBytes, string? ColorProfile,
    bool HasExif, bool HasExifGps, bool HasXmp, bool HasIptc, string? CameraMake, string? CameraModel, string? DateTaken)
{
    public static ImageMetadata Read(string path)
    {
        using var image = new MagickImage();
        image.Ping(path);
        var exif = image.GetExifProfile();
        var color = image.GetColorProfile();
        return new ImageMetadata(
            image.Format.ToString(), image.Width, image.Height, new FileInfo(path).Length,
            color is null ? null : color.Description ?? "Embedded ICC profile",
            exif is not null, exif?.Values.Any(value => value.Tag.ToString().StartsWith("GPS", StringComparison.OrdinalIgnoreCase)) == true,
            image.GetXmpProfile() is not null, image.GetIptcProfile() is not null,
            exif?.GetValue(ExifTag.Make)?.Value?.TrimEnd('\0', ' '),
            exif?.GetValue(ExifTag.Model)?.Value?.TrimEnd('\0', ' '),
            exif?.GetValue(ExifTag.DateTimeOriginal)?.Value?.TrimEnd('\0', ' '));
    }
}
