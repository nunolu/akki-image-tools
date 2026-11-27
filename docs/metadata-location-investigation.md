# Selective location removal: investigation

Status: a limited JPEG camera/date allowlist and a separate EXIF GPS-only removal option are implemented in the development version. The EXIF GPS-only option deliberately retains XMP, IPTC, maker notes and all other metadata, which may still contain location; it is not comprehensive location removal. The camera/date allowlist strips other metadata and optionally restores the ICC profile; it does not preserve arbitrary non-location metadata. Retained text, filenames and pixels may reveal information. Do not describe the inspector as a complete privacy scanner or claim comprehensive location removal. A synthetic JPEG fixture was checked using Magick.NET and independently using Pillow; broader format and real-device verification remains necessary.

Location is not confined to the EXIF GPS directory. XMP may mirror EXIF GPS or contain IPTC location structures; IPTC can contain city, region and country; camera-specific maker notes and embedded thumbnails may contain additional information. Free-text descriptions may also reveal location. Removing only EXIF GPS tags is not a reliable implementation of “remove location”.

References: [ExifTool geotagging](https://exiftool.org/geotag.html), [ExifTool supported metadata families](https://exiftool.org/), and [Exiv2 IPTC Extension XMP tags](https://exiv2.org/tags-xmp-iptcExt.html).

## Candidate implementation

Evaluate a local, compatible metadata engine against representative files and the app's existing licences and Store packaging. Define a narrowly stated guarantee for supported metadata and formats; fail closed on unsupported or unreadable structures rather than report successful redaction. Consider a separately named allowlist mode that keeps only explicitly identified camera and date fields, if preserving every other metadata field cannot be made reliable. Treat free-text, manufacturer notes and embedded thumbnails as separate privacy concerns; do not silently assert that they contain no location.

The implementation must preserve originals, work on the converted output before atomic publication, accurately report partial or unsupported cases, and use the same policy for preview and conversion. It should not depend on network services or put image metadata in logs.

## Verification required before exposing the feature

- Independent EXIF, XMP, IPTC, maker-note and thumbnail fixtures, including duplicate or conflicting locations, non-location fields and malformed metadata.
- Verify output using a separate metadata reader, not solely the library that performed removal. Test both absence of covered location fields and preservation of explicitly promised non-location fields.
- Check every input/output combination advertised by the UI, embedded colour profiles, orientation, multi-size outputs, preview, cancellation and error handling.
- Confirm behaviour for format-specific metadata, unsupported structures, missing metadata, corrupted files and images whose pixels or filenames themselves reveal a location.
- Validate dependency licensing, native binaries, offline distribution, third-party notices, corresponding-source obligations and Microsoft Store packaging if a new tool is bundled.

Independent verification: a synthetic JPEG with EXIF GPS coordinates, camera make, artist, XMP GPS and an ICC profile was converted using EXIF GPS-only mode. Pillow found no EXIF GPS IFD while confirming that camera, artist, ICC and XMP GPS remained. This verifies the intentionally limited behaviour for that fixture, not all JPEG variants.
