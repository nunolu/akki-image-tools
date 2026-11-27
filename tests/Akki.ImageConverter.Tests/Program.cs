using Akki.ImageConverter.Core;
using ImageMagick;
using Engine = Akki.ImageConverter.Core.ImageConverter;

var root = Path.Combine(Path.GetTempPath(), "AkkiConverterTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    passed++; Console.WriteLine("PASS: " + name);
}
try
{
    var legacyPreferences = System.Text.Json.JsonSerializer.Deserialize<Preferences>("""
        {"Theme":2,"Updates":2,"ShareUsageStatistics":true,"StatisticsInstallationId":"old-id","StatisticsLastRunVersion":"0.1.5"}
        """)!;
    Check(legacyPreferences.Theme == AppTheme.Dark && legacyPreferences.Updates == UpdatePolicy.Manual,
        "Previous settings preserve theme and update preference");
    Check(legacyPreferences.CompactEdge == CompactEdge.Right, "Existing settings default to the right edge");
    legacyPreferences.CompactEdge = CompactEdge.Top;
    Check(System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(legacyPreferences))!.CompactEdge == CompactEdge.Top,
        "Compact edge preference survives serialization");
    var savedPreferences = System.Text.Json.JsonSerializer.Serialize(legacyPreferences);
    Check(!savedPreferences.Contains("Statistics", StringComparison.OrdinalIgnoreCase) && !savedPreferences.Contains("old-id"),
        "Saving previous settings discards retired analytics fields and installation ID");
    legacyPreferences.Presets.Add("Web", new ConversionOptions { Format = OutputKind.Jpeg, Crop = CropKind.Square, NumberFiles = true });
    var presetRoundTrip = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(legacyPreferences))!;
    Check(presetRoundTrip.Presets["Web"] is { Format: OutputKind.Jpeg, Crop: CropKind.Square, NumberFiles: true }, "Preset settings survive serialization");
    foreach (var name in BuiltInPresets.Names)
    {
        var preset = BuiltInPresets.Apply(name, @"C:\example", false, false);
        preset.Validate();
        Check(preset.OutputDirectory == @"C:\example" && !preset.OutputToSourceFolder && !preset.KeepFolderStructure &&
            preset.KeepAspectRatio && preset.PreventUpscale && preset.StripMetadata && preset.Crop == CropKind.None && preset.WatermarkText == "",
            $"Built-in preset {name} preserves destination and uses safe defaults");
    }
    Check(BuiltInPresets.Apply(BuiltInPresets.Names[0], "", true, true) is { Format: OutputKind.WebP, Width: 1920, Height: 1920, OutputToSourceFolder: true }, "Web preset respects source-folder destination");
    Check(BuiltInPresets.Apply(BuiltInPresets.Names[1], "", true, true) is { Format: OutputKind.Jpeg, Width: 1600, Height: 1600, Background: "#FFFFFF" }, "Email preset uses JPEG with a white background");
    Check(BuiltInPresets.Apply(BuiltInPresets.Names[2], "", true, true) is { Format: OutputKind.Png, Resize: ResizeKind.Original }, "PNG preset retains original dimensions");
    var input = Path.Combine(root, "input");
    var output = Path.Combine(root, "output");
    Directory.CreateDirectory(Path.Combine(input, "nested"));
    var cache = new OriginalPreviewCache(maxEntries: 2, maxBytes: 4);
    var cacheA = Path.Combine(input, "cache-a.dat");
    var cacheB = Path.Combine(input, "cache-b.dat");
    var cacheC = Path.Combine(input, "cache-c.dat");
    File.WriteAllText(cacheA, "a"); File.WriteAllText(cacheB, "b"); File.WriteAllText(cacheC, "c");
    int generated = 0;
    byte[] Generate(string _) { generated++; return new byte[] { 1, 2 }; }
    cache.GetOrCreate(cacheA, Generate); cache.GetOrCreate(cacheA, Generate);
    Check(generated == 1, "Original preview cache reuses unchanged files");
    File.AppendAllText(cacheA, "changed"); cache.GetOrCreate(cacheA, Generate);
    Check(generated == 2, "Original preview cache invalidates changed files");
    cache.GetOrCreate(cacheB, Generate); cache.GetOrCreate(cacheC, Generate); cache.GetOrCreate(cacheA, Generate);
    Check(generated == 5, "Original preview cache evicts least recently used data");
    cache.Clear(); cache.GetOrCreate(cacheA, Generate);
    Check(generated == 6, "Original preview cache clears stored data");
    var source = Path.Combine(input, "nested", "transparent.png");
    using (var image = new MagickImage(MagickColors.Transparent, 400, 200))
    {
        using var overlay = new MagickImage(MagickColors.Red, 200, 100);
        image.Composite(overlay, 0, 0, CompositeOperator.Over);
        image.Write(source);
    }
    var options = new ConversionOptions { OutputDirectory = output, Lossless = true };
    var engine = new Engine();
    var src = new ImageSource(source, input);
    var profiledSource = Path.Combine(input, "profiled.jpg");
    using (var image = new MagickImage(MagickColors.Red, 64, 32))
    {
        image.SetProfile(ColorProfiles.SRGB);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Artist, "Metadata test");
        exif.SetValue(ExifTag.Make, "Test camera");
        exif.SetValue(ExifTag.Model, "Test model");
        exif.SetValue(ExifTag.DateTimeOriginal, "2020:01:02 03:04:05");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        exif.SetValue(ExifTag.GPSLatitude, new[] { new Rational(49, 1), new Rational(36, 1), new Rational(0, 1) });
        exif.SetValue(ExifTag.GPSLongitude, new[] { new Rational(6, 1), new Rational(8, 1), new Rational(0, 1) });
        image.SetProfile(exif);
        image.SetProfile(new XmpProfile(System.Text.Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description xmlns:exif='http://ns.adobe.com/exif/1.0/' exif:GPSLatitude='49,36N'/></rdf:RDF></x:xmpmeta>")));
        image.Write(profiledSource);
    }
    using (var inputImage = new MagickImage(profiledSource))
        Check(inputImage.GetColorProfile() is not null && inputImage.GetExifProfile() is not null && inputImage.GetXmpProfile() is not null, "Profile test fixture contains ICC, EXIF and XMP");
    var inspectedMetadata = ImageMetadata.Read(profiledSource);
    Check(inspectedMetadata is { Width: 64, Height: 32, HasExif: true, HasExifGps: true, CameraMake: "Test camera" } && inspectedMetadata.ColorProfile is not null,
        "Metadata inspection detects image details, EXIF GPS and colour profile");
    var unprofiledMetadata = ImageMetadata.Read(source);
    Check(unprofiledMetadata.Width == 400 && unprofiledMetadata.Height == 200 && !unprofiledMetadata.HasExifGps,
        "Metadata inspection handles images without EXIF GPS");
    byte[] originalProfile;
    using (var inputImage = new MagickImage(profiledSource)) originalProfile = inputImage.GetColorProfile()!.ToByteArray();
    var profileOptions = options with { Format = OutputKind.Jpeg, StripMetadata = true, PreserveColorProfile = true };
    foreach (var format in new[] { OutputKind.Jpeg, OutputKind.Png, OutputKind.WebP, OutputKind.Tiff, OutputKind.Avif, OutputKind.Bmp })
    {
        var outputProfile = engine.Convert(new(profiledSource), profileOptions with { Format = format }, CancellationToken.None);
        using var resultImage = new MagickImage(outputProfile.Path);
        var resultProfile = resultImage.GetColorProfile();
        Check(resultProfile is not null && resultProfile.ToByteArray().SequenceEqual(originalProfile) && resultImage.GetExifProfile() is null,
            $"{format} preserves identical ICC bytes while removing EXIF");
    }
    var adobeSource = Path.Combine(input, "adobe-profiled.jpg");
    using (var image = new MagickImage(MagickColors.Red, 64, 32))
    {
        image.SetProfile(ColorProfiles.AdobeRGB1998);
        var exif = new ExifProfile(); exif.SetValue(ExifTag.Artist, "Private metadata"); image.SetProfile(exif);
        image.SetProfile(new XmpProfile(System.Text.Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description xmlns:exif='http://ns.adobe.com/exif/1.0/' exif:GPSLatitude='49,36N'/></rdf:RDF></x:xmpmeta>")));
        image.Write(adobeSource);
    }
    using (var inputAdobe = new MagickImage(adobeSource))
    {
        Check(inputAdobe.GetColorProfile() is not null && inputAdobe.GetExifProfile() is not null && inputAdobe.GetXmpProfile() is not null,
            "Adobe RGB test fixture contains ICC, EXIF and XMP");
        Check(ImageMetadata.Read(adobeSource).HasXmp, "Metadata inspection detects XMP on JPEG input");
        var expectedProfile = inputAdobe.GetColorProfile()!.ToByteArray();
        var pngOutput = engine.Convert(new(adobeSource), profileOptions with { Format = OutputKind.Png }, CancellationToken.None);
        using var pngImage = new MagickImage(pngOutput.Path);
        Check(pngImage.GetColorProfile() is { } pngProfile && pngProfile.ToByteArray().SequenceEqual(expectedProfile) && pngImage.GetExifProfile() is null && pngImage.GetXmpProfile() is null,
            "PNG preserves Adobe RGB profile while removing EXIF and XMP");
        Check(pngImage.Compare(inputAdobe, ErrorMetric.Absolute) == 0, "PNG profile preservation keeps image pixels");
    }
    try { (profileOptions with { Format = OutputKind.Ico }).Validate(); throw new Exception("Unverified ICO ICC retention accepted"); }
    catch (ArgumentException) { Check(true, "ICC retention rejects unverified ICO output"); }
    var defaultProfile = engine.Convert(new(profiledSource), options with { Format = OutputKind.Jpeg, StripMetadata = true }, CancellationToken.None);
    using (var outputImage = new MagickImage(defaultProfile.Path))
        Check(outputImage.GetColorProfile() is null && outputImage.GetExifProfile() is null, "Default metadata removal also strips ICC");
    var basicOptions = profileOptions with { KeepBasicCameraMetadata = true };
    var basicResult = engine.Convert(new(profiledSource), basicOptions, CancellationToken.None);
    using (var basicImage = new MagickImage(basicResult.Path))
    {
        var basicExif = basicImage.GetExifProfile();
        Check(basicExif?.GetValue(ExifTag.Make)?.Value == "Test camera" && basicExif.GetValue(ExifTag.Model)?.Value == "Test model" &&
            basicExif.GetValue(ExifTag.DateTimeOriginal)?.Value == "2020:01:02 03:04:05" &&
            basicExif.GetValue(ExifTag.GPSLatitudeRef) is null && basicExif.GetValue(ExifTag.Artist) is null &&
            basicImage.GetXmpProfile() is null && basicImage.GetColorProfile() is not null,
            "JPEG basic camera mode preserves camera/date and ICC while removing GPS and other metadata");
    }
    var basicWithoutProfile = engine.Convert(new(profiledSource), basicOptions with { PreserveColorProfile = false }, CancellationToken.None);
    using (var basicImage = new MagickImage(basicWithoutProfile.Path))
        Check(basicImage.GetColorProfile() is null && basicImage.GetExifProfile()?.GetValue(ExifTag.Make)?.Value == "Test camera" &&
            basicImage.GetExifProfile()?.GetValue(ExifTag.GPSLatitudeRef) is null,
            "JPEG basic camera metadata works without retaining the colour profile");
    var basicPreview = engine.Preview(profiledSource, basicOptions);
    Check(basicPreview.EncodedBytes == basicResult.OutputBytes, "Basic camera metadata preview reports the real output size");
    try { (basicOptions with { Format = OutputKind.Png }).Validate(); throw new Exception("Unverified basic metadata format accepted"); }
    catch (ArgumentException) { Check(true, "Basic camera metadata mode is restricted to JPEG output"); }
    Check(System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}")?.KeepBasicCameraMetadata == false,
        "Basic camera metadata mode defaults off for previous settings");
    var gpsOnlyOptions = options with { Format = OutputKind.Jpeg, StripMetadata = false, RemoveExifGpsOnly = true };
    var gpsOnlyOutput = engine.Convert(new(profiledSource), gpsOnlyOptions, CancellationToken.None);
    using (var gpsOnlyImage = new MagickImage(gpsOnlyOutput.Path))
    {
        var outputExif = gpsOnlyImage.GetExifProfile();
        Check(outputExif is not null && !outputExif.Values.Any(v => v.Tag.Ifd == ExifIfds.Gps || v.Tag.ToString().StartsWith("GPS", StringComparison.OrdinalIgnoreCase)) &&
            outputExif.GetValue(ExifTag.Artist)?.Value == "Metadata test" && outputExif.GetValue(ExifTag.Make)?.Value == "Test camera" &&
            gpsOnlyImage.GetXmpProfile() is not null && gpsOnlyImage.GetColorProfile() is not null,
            "JPEG EXIF-only GPS removal retains camera, artist, XMP and ICC");
    }
    try { (gpsOnlyOptions with { Format = OutputKind.Png }).Validate(); throw new Exception("Unverified GPS-only output accepted"); }
    catch (ArgumentException) { Check(true, "EXIF-only GPS removal is restricted to JPEG output"); }
    var gpsOnlyPreview = engine.Preview(profiledSource, gpsOnlyOptions);
    Check(gpsOnlyPreview.EncodedBytes == gpsOnlyOutput.OutputBytes, "EXIF-only GPS preview reports the encoded output size");
    Check(System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}")?.RemoveExifGpsOnly == false,
        "EXIF-only GPS removal defaults off for older settings");
    Check(System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}")?.PreserveColorProfile == false &&
        System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(options with { PreserveColorProfile = true }))?.PreserveColorProfile == true,
        "ICC option persists and defaults off for older settings");
    var sizes = MultiSize.Parse("100, 300");
    Check(sizes.SequenceEqual(new uint[] { 100, 300 }) && MultiSize.Parse("").Length == 0, "Multiple output widths parse in order");
    try { MultiSize.Parse("100,100"); throw new Exception("Duplicate sizes accepted"); }
    catch (ArgumentException) { Check(true, "Duplicate output widths rejected"); }
    var multiOptions = options with { OutputWidths = "100,300", Format = OutputKind.Png };
    multiOptions.Validate();
    foreach (var width in sizes)
    {
        var variant = MultiSize.ForWidth(multiOptions, width);
        var multiResult = engine.Convert(src, variant, CancellationToken.None);
        var multiPreview = engine.Preview(source, variant);
        Check(multiResult.Width == width && multiResult.Height == width / 2 && Path.GetFileName(multiResult.Path).Contains($"-w{width}") &&
            multiResult.OutputBytes == multiPreview.EncodedBytes, $"Width {width} output dimensions, name and preview size");
    }
    var savedMulti = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(multiOptions));
    Check(savedMulti?.OutputWidths == "100,300" && System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}")?.OutputWidths == "", "Multiple widths persist and default off");
    using (var fastOriginal = new MagickImage(engine.PreviewOriginal(source, 100)))
        Check(fastOriginal.Width == 100 && fastOriginal.Height == 50 && fastOriginal.HasAlpha,
            "Fast original preview fits its limit and retains transparency");
    using (var smallOriginal = new MagickImage(engine.PreviewOriginal(source, 1000)))
        Check(smallOriginal.Width == 400 && smallOriginal.Height == 200, "Fast original preview does not upscale");
    Check(Engine.InputExtensions.Contains(".HEIC") && Engine.InputExtensions.Contains(".heif"), "HEIC and HEIF accepted case-insensitively");
    var heifFixture = Environment.GetEnvironmentVariable("AKKI_HEIF_TEST_IMAGE");
    if (!string.IsNullOrWhiteSpace(heifFixture))
    {
        var originalHeif = File.ReadAllBytes(heifFixture);
        var heifOptions = options with { Format = OutputKind.Png, Lossless = false, Resize = ResizeKind.Pixels, Width = 1000, Height = 1000 };
        Console.WriteLine("HEIF: starting conversion");
        var converted = engine.Convert(new(heifFixture), heifOptions, CancellationToken.None);
        Console.WriteLine("HEIF: conversion finished; starting preview");
        var heifPreview = engine.Preview(heifFixture, heifOptions);
        Console.WriteLine("HEIF: preview finished; comparing output");
        using var convertedImage = new MagickImage(converted.Path);
        using var previewImage = new MagickImage(heifPreview.Converted);
        Check(convertedImage.Width > 0 && convertedImage.Height > 0 &&
            convertedImage.Compare(previewImage, ErrorMetric.Absolute) == 0, "HEIF conversion and preview agree");
        Check(originalHeif.SequenceEqual(File.ReadAllBytes(heifFixture)), "HEIF original preserved");
    }
    else Console.WriteLine("SKIP: HEIF conversion requires AKKI_HEIF_TEST_IMAGE pointing to a real still HEIC/HEIF file");
    Check(Engine.CalculateSize(400, 200, options with { Resize = ResizeKind.Pixels, Width = 100, Height = 100 }) == (100u, 50u), "Aspect-preserving fit");
    Check(Engine.CalculateSize(400, 200, options with { Resize = ResizeKind.Percent, Percent = 25 }) == (100u, 50u), "Percentage resize");
    Check(Engine.CalculateSize(400, 200, options with { Resize = ResizeKind.Pixels, Width = 800, Height = 800 }) == (400u, 200u), "Prevent upscaling");
    Check(Engine.CalculateSize(400, 200, options with { Resize = ResizeKind.Pixels, Width = 80, Height = 90, KeepAspectRatio = false }) == (80u, 90u), "Exact dimensions");
    var before = File.ReadAllBytes(source);
    foreach (var rotation in new[] { RotationKind.Clockwise90, RotationKind.Clockwise180, RotationKind.Clockwise270 })
    {
        var rotatedOptions = options with { Format = OutputKind.Png, Rotation = rotation };
        var rotated = engine.Convert(src, rotatedOptions, CancellationToken.None);
        using var actual = new MagickImage(rotated.Path);
        using var expected = new MagickImage(source);
        expected.Rotate((int)rotation * 90);
        Check(actual.Width == expected.Width && actual.Height == expected.Height &&
            actual.Compare(expected, ErrorMetric.Absolute) == 0, $"{rotation} conversion pixels");
        var rotatedPreview = engine.Preview(source, rotatedOptions);
        using var previewImage = new MagickImage(rotatedPreview.Converted);
        Check(rotatedPreview.Width == rotated.Width && rotatedPreview.Height == rotated.Height &&
            previewImage.Compare(actual, ErrorMetric.MeanAbsolute) < 0.01, $"{rotation} preview matches conversion");
    }
    var rotatedResized = engine.Convert(src, options with { Format = OutputKind.Png, Rotation = RotationKind.Clockwise90,
        Resize = ResizeKind.Pixels, Width = 100, Height = 100 }, CancellationToken.None);
    Check(rotatedResized.Width == 50 && rotatedResized.Height == 100, "Rotation precedes resizing");
    var cropped = engine.Convert(src, options with { Format = OutputKind.Png, Crop = CropKind.Square }, CancellationToken.None);
    Check(cropped.Width == 200 && cropped.Height == 200, "Centre square crop dimensions");
    var cropPreview = engine.Preview(source, options with { Format = OutputKind.Png, Crop = CropKind.Square });
    Check(cropPreview.Width == cropped.Width && cropPreview.Height == cropped.Height, "Crop preview dimensions match conversion");
    var customCropOptions = options with { Format = OutputKind.Png, Crop = CropKind.Custom, CropWidth = 3, CropHeight = 2 };
    var customCrop = engine.Convert(src, customCropOptions, CancellationToken.None);
    var customPreview = engine.Preview(source, customCropOptions);
    using (var actualCrop = new MagickImage(customCrop.Path))
    using (var shownCrop = new MagickImage(customPreview.Converted))
        Check(actualCrop.Width == 300 && actualCrop.Height == 200 && actualCrop.Compare(shownCrop, ErrorMetric.Absolute) == 0,
            "Custom crop output and preview match");
    var tallCrop = engine.Convert(src, customCropOptions with { CropWidth = 2, CropHeight = 3 }, CancellationToken.None);
    Check(tallCrop.Width == 133 && tallCrop.Height == 200, "Portrait custom crop dimensions");
    var restoredCrop = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(customCropOptions));
    Check(restoredCrop is { Crop: CropKind.Custom, CropWidth: 3, CropHeight: 2 }, "Custom crop ratio survives serialization");
    try { (customCropOptions with { CropWidth = 0 }).Validate(); throw new Exception("Invalid crop ratio accepted"); }
    catch (ArgumentException) { Check(true, "Invalid custom crop ratio rejected"); }
    var anchorSource = Path.Combine(input, "anchor.png");
    using (var image = new MagickImage(MagickColors.Red, 400, 200))
    using (var half = new MagickImage(MagickColors.Blue, 200, 200)) { image.Composite(half, 200, 0, CompositeOperator.Over); image.Write(anchorSource); }
    var anchorOptions = options with { Format = OutputKind.Png, Crop = CropKind.Square };
    var leftCrop = engine.Convert(new(anchorSource), anchorOptions with { CropAnchor = CropAnchor.Left }, CancellationToken.None);
    var rightOptions = anchorOptions with { CropAnchor = CropAnchor.Right };
    var rightCrop = engine.Convert(new(anchorSource), rightOptions, CancellationToken.None);
    using (var leftImage = new MagickImage(leftCrop.Path))
    using (var rightImage = new MagickImage(rightCrop.Path))
    using (var leftPixels = leftImage.GetPixels())
    using (var rightPixels = rightImage.GetPixels())
        Check(leftPixels.GetPixel(100, 100).ToColor()!.R > 60000 && rightPixels.GetPixel(100, 100).ToColor()!.B > 60000,
            "Crop position selects the correct side of the image");
    var rightPreview = engine.Preview(anchorSource, rightOptions);
    using (var actualRight = new MagickImage(rightCrop.Path))
    using (var previewRight = new MagickImage(rightPreview.Converted))
        Check(actualRight.Compare(previewRight, ErrorMetric.Absolute) == 0, "Crop position preview matches output");
    Check(System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}")?.CropAnchor == CropAnchor.Center &&
        System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(rightOptions))?.CropAnchor == CropAnchor.Right,
        "Crop position persists and old settings default to centre");
    var leftBounds = Engine.CropPreviewBounds(400, 200, anchorOptions, CropAnchor.Left);
    var rightBounds = Engine.CropPreviewBounds(400, 200, anchorOptions, CropAnchor.Right);
    Check(leftBounds == (0.0, 0.0, 200.0, 200.0) && rightBounds == (200.0, 0.0, 200.0, 200.0),
        "Visual crop outline reflects left and right anchors");
    using (var transformedCropSource = new MagickImage(engine.PreviewCropSource(anchorSource, anchorOptions with { Rotation = RotationKind.Clockwise90 }, 1000)))
        Check(transformedCropSource.Width == 200 && transformedCropSource.Height == 400, "Visual crop source reflects rotation");
    var padOptions = options with { Format = OutputKind.Png, Resize = ResizeKind.Pixels, Width = 100, Height = 100, PadToDimensions = true };
    var padded = engine.Convert(src, padOptions, CancellationToken.None);
    var paddedPreview = engine.Preview(source, padOptions);
    using (var image = new MagickImage(padded.Path))
    using (var shown = new MagickImage(paddedPreview.Converted))
    using (var pixels = image.GetPixels())
        Check(image.Width == 100 && image.Height == 100 && image.HasAlpha && pixels.GetPixel(50, 0).ToColor()!.A == 0 && image.Compare(shown, ErrorMetric.Absolute) == 0,
            "Transparent padding creates the requested canvas and matches preview");
    var paddedJpeg = engine.Convert(src, padOptions with { Format = OutputKind.Jpeg, Background = "#FFFFFF" }, CancellationToken.None);
    using (var image = new MagickImage(paddedJpeg.Path))
    using (var pixels = image.GetPixels())
    {
        var corner = pixels.GetPixel(50, 0).ToColor()!;
        Check(image.Width == 100 && image.Height == 100 && corner.R > 60000 && corner.G > 60000 && corner.B > 60000,
            "JPEG padding uses the selected background");
    }
    var restoredPad = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(padOptions));
    Check(restoredPad?.PadToDimensions == true && System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}")?.PadToDimensions == false, "Padding persists and defaults off for previous settings");
    Check(Engine.CalculateSize(400, 200, padOptions with { Width = 800, Height = 800 }) == (400u, 200u), "Padding keeps the image at original size when upscaling is disabled");
    try { (padOptions with { KeepAspectRatio = false }).Validate(); throw new Exception("Invalid padding accepted"); }
    catch (ArgumentException) { Check(true, "Padding without aspect preservation rejected"); }
    var watermarkPath = Path.Combine(input, "mark.png");
    using (var watermark = new MagickImage(MagickColors.Blue, 50, 50)) watermark.Write(watermarkPath);
    var watermarkOptions = options with { Format = OutputKind.Png, WatermarkImage = watermarkPath, WatermarkSize = 20, WatermarkOpacity = 70 };
    var marked = engine.Convert(src, watermarkOptions, CancellationToken.None);
    using (var actual = new MagickImage(marked.Path))
    using (var originalImage = new MagickImage(source))
        Check(actual.Compare(originalImage, ErrorMetric.Absolute) > 0, "Image watermark changes output pixels");
    var markedPreview = engine.Preview(source, watermarkOptions);
    using (var actual = new MagickImage(marked.Path))
    using (var shown = new MagickImage(markedPreview.Converted))
    {
        Check(shown.Compare(actual, ErrorMetric.MeanAbsolute) < 0.01, "Watermark preview matches conversion");
    }
    var transparentMark = engine.Convert(src, watermarkOptions with { WatermarkOpacity = 0 }, CancellationToken.None);
    using (var actual = new MagickImage(transparentMark.Path))
    using (var originalImage = new MagickImage(source))
        Check(actual.Compare(originalImage, ErrorMetric.Absolute) == 0, "Zero opacity leaves pixels unchanged");
    var textMark = engine.Convert(src, options with { Format = OutputKind.Png, WatermarkText = "Akki" }, CancellationToken.None);
    using (var actual = new MagickImage(textMark.Path))
    using (var originalImage = new MagickImage(source))
        Check(actual.Compare(originalImage, ErrorMetric.Absolute) > 0, "Text watermark changes output pixels");
    var targetOptions = options with { Format = OutputKind.Jpeg, Lossless = false, TargetKilobytes = 4 };
    var targetResult = engine.Convert(src, targetOptions, CancellationToken.None);
    var targetPreview = engine.Preview(source, targetOptions);
    Check(targetResult.OutputBytes == targetPreview.EncodedBytes && targetResult.OutputBytes <= 4096, "Target size and preview use the same encoded output");
    var largeSource = Path.Combine(input, "large.png");
    using (var large = new MagickImage(MagickColors.Red, 1000, 1000)) large.Write(largeSource);
    var impossibleTarget = engine.Convert(new(largeSource), targetOptions with { TargetKilobytes = 1 }, CancellationToken.None);
    Check(impossibleTarget.OutputBytes > 1024 && File.Exists(impossibleTarget.Path), "Unreachable target keeps lowest-quality output for reporting");
    try { (options with { TargetKilobytes = 1 }).Validate(); throw new Exception("Lossless target accepted"); }
    catch (ArgumentException) { Check(true, "Target size rejects lossless encoding"); }
    foreach (var (horizontal, vertical) in new[] { (true, false), (false, true), (true, true) })
    {
        var flipOptions = options with { Format = OutputKind.Png, FlipHorizontal = horizontal, FlipVertical = vertical };
        var flipped = engine.Convert(src, flipOptions, CancellationToken.None);
        using var actual = new MagickImage(flipped.Path);
        using var expected = new MagickImage(source);
        if (horizontal) expected.Flop();
        if (vertical) expected.Flip();
        Check(actual.Compare(expected, ErrorMetric.Absolute) == 0, $"Flip horizontal={horizontal}, vertical={vertical}");
        var flipPreview = engine.Preview(source, flipOptions);
        using var previewImage = new MagickImage(flipPreview.Converted);
        Check(previewImage.Compare(actual, ErrorMetric.MeanAbsolute) < 0.01, $"Flip preview horizontal={horizontal}, vertical={vertical}");
    }
    var combinedOptions = options with { Format = OutputKind.Png, Rotation = RotationKind.Clockwise90, FlipHorizontal = true };
    var combined = engine.Convert(src, combinedOptions, CancellationToken.None);
    using (var actual = new MagickImage(combined.Path))
    using (var expected = new MagickImage(source))
    {
        expected.Rotate(90);
        expected.Flop();
        Check(actual.Compare(expected, ErrorMetric.Absolute) == 0, "Rotation precedes horizontal flip");
    }
    var oldOptions = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>("{}");
    Check(oldOptions is { FlipHorizontal: false, FlipVertical: false }, "Existing preferences default to no flips");
    var restoredOptions = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(combinedOptions));
    Check(restoredOptions is { FlipHorizontal: true, FlipVertical: false }, "Flip preference persists");
    foreach (var format in Engine.SupportedOutputs)
    {
        var result = engine.Convert(src, options with { Format = format }, CancellationToken.None);
        using var decoded = new MagickImage(result.Path);
        Check(decoded.Width > 0 && decoded.Height > 0 && result.OutputBytes > 0, $"Encode and decode {format}");
        Check(Path.GetDirectoryName(result.Path) == Path.Combine(output, "nested"), $"Preserve folder structure {format}");
    }
    var webp = engine.Convert(src, options, CancellationToken.None);
    using (var decoded = new MagickImage(webp.Path))
    {
        Check(decoded.HasAlpha, "WebP preserves transparency");
        using var originalImage = new MagickImage(source);
        Check(decoded.Compare(originalImage, ErrorMetric.Absolute) == 0, "Lossless WebP pixel equality");
    }
    var collision = engine.Convert(src, options, CancellationToken.None);
    Check(collision.Path != webp.Path && File.Exists(webp.Path), "Collision renames instead of overwriting");
    var naming = options with { FilenamePrefix = "web-", FilenameSuffix = "-small", NumberFiles = true };
    var named = engine.Convert(src, naming, CancellationToken.None, 7);
    Check(Path.GetFileName(named.Path) == "web-transparent-small-007.webp" && Engine.OutputName(source, naming, 7) == Path.GetFileName(named.Path), "Batch filename and preview agree");
    var namedCollision = engine.Convert(src, naming, CancellationToken.None, 7);
    Check(Path.GetFileName(namedCollision.Path) == "web-transparent-small-007 (1).webp" && File.Exists(named.Path), "Batch filename collisions preserve existing output");
    try { (naming with { FilenamePrefix = "../" }).Validate(); throw new Exception("Invalid filename accepted"); }
    catch (ArgumentException) { Check(true, "Filename path separators rejected"); }
    var sameFolder = engine.Convert(src, options with { Format = OutputKind.Png, OutputDirectory = Path.GetDirectoryName(source)!, KeepFolderStructure = false }, CancellationToken.None);
    Check(sameFolder.Path != source && File.ReadAllBytes(source).SequenceEqual(before), "Same-format conversion preserves source");
    var alongside = engine.Convert(src, options with { Format = OutputKind.Png, OutputDirectory = "", OutputToSourceFolder = true, KeepFolderStructure = true }, CancellationToken.None);
    Check(Path.GetDirectoryName(alongside.Path) == Path.GetDirectoryName(source) && alongside.Path != source && File.ReadAllBytes(source).SequenceEqual(before), "Save beside original preserves source and handles collisions");
    var besideOptions = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(System.Text.Json.JsonSerializer.Serialize(options with { OutputToSourceFolder = true }));
    Check(besideOptions?.OutputToSourceFolder == true && oldOptions?.OutputToSourceFolder == false, "Source folder option persists and defaults off");
    var jpeg = engine.Convert(src, options with { Format = OutputKind.Jpeg }, CancellationToken.None);
    using (var decoded = new MagickImage(jpeg.Path))
    using (var pixels = decoded.GetPixels())
    {
        var pixel = pixels.GetPixel(399, 199).ToColor()!;
        Check(pixel.R > 60000 && pixel.G > 60000 && pixel.B > 60000, "JPEG transparency uses white background");
    }
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        try { engine.Convert(src, options, cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { Check(true, "Cancellation honoured"); }
    }
    var animation = Path.Combine(input, "animation.gif");
    using (var frames = new MagickImageCollection())
    {
        frames.Add(new MagickImage(MagickColors.Red, 10, 10));
        frames.Add(new MagickImage(MagickColors.Blue, 10, 10));
        frames.Write(animation);
    }
    try { engine.Convert(new(animation), options, CancellationToken.None); throw new Exception("Animation was truncated"); }
    catch (NotSupportedException) { Check(true, "Animation explicitly rejected"); }
    try { engine.Convert(new(source, Path.Combine(input, "other")), options, CancellationToken.None); throw new Exception("Escaped root"); }
    catch (ArgumentException) { Check(true, "Folder traversal rejected"); }
    var preview = engine.Preview(source, options with { Resize = ResizeKind.Percent, Percent = 50 });
    Check(preview.Width == 200 && preview.Height == 100 && preview.EncodedBytes > 0, "Preview uses real encoding and resizing");
    Check(!Directory.EnumerateFiles(output, ".akki-*.tmp", SearchOption.AllDirectories).Any(), "Temporary files cleaned");
    Check(File.ReadAllBytes(source).SequenceEqual(before), "Original source unchanged after all conversions");
    Console.WriteLine($"\n{passed} checks passed. Supported output formats: {string.Join(", ", Engine.SupportedOutputs)}");
}
finally
{
    // The path is created by this test under the OS temporary directory, never user input.
    if (Path.GetDirectoryName(root)!.Equals(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(root).StartsWith("AkkiConverterTests-", StringComparison.Ordinal)) Directory.Delete(root, true);
}
