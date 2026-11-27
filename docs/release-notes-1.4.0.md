# Akki Image Tools 1.4.0

- Open HEIC and HEIF images for conversion.
- Create multiple output widths in one batch, with width suffixes, and use built-in presets for common conversions.
- Choose custom crop ratios and visually select one of nine crop positions. Add padding to fit images into the desired dimensions.
- Browse images with a bounded in-memory original-preview cache and preload of the next image after the comparison finishes.
- Inspect basic image metadata. For JPEG output, optionally keep only camera make, model and capture date, or remove EXIF GPS tags while keeping other metadata.
- Preserve embedded ICC profiles when stripping metadata from JPEG, PNG, WebP, TIFF, AVIF and BMP output.

The JPEG EXIF GPS option does not remove location from XMP, IPTC, maker notes, text, filenames or image content. Use the existing strip-metadata option when retaining metadata is not needed; image content and filenames can still reveal location. ICO ICC preservation is not supported.

Images are processed locally and originals are preserved. Update checks and downloads use the network according to your settings.
