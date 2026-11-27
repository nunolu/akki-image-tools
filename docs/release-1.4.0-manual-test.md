# Release candidate 1.4.0: grouped Windows test

The candidate is not yet published. Use copies of representative images and an empty destination folder. Verify the expected result for each item and record any failures before publication.

1. Extract `artifacts\release-candidate-1.4.0\Akki-Image-Tools-win-Portable.zip` completely into a new folder and run `Akki.ImageConverter.exe` there. Check launch, light/dark layout and compact mode.
2. Add JPEG, PNG, `sample1.heif` and `shelf-christmas-decoration.heic`. Move between them, refresh the comparison, zoom and verify the displayed preview and output dimensions.
3. Test a built-in preset, save and reload a custom preset, and convert a batch with naming, multiple output widths and collision handling. Check that the expected files exist and the originals remain unchanged.
4. Test padding and a custom crop ratio. Choose different positions in the visual crop dialog, check the outline and resulting output, and repeat after rotation or flipping.
5. Test metadata inspection and the JPEG metadata options. Check that EXIF GPS-only mode is clearly labelled and warns that other location information can remain. Use an independent metadata viewer if testing removal; don't use real sensitive photos as proof of complete removal.
6. Test ICC retention using images with known embedded profiles, and check colour and transparency on supported formats. ICO profile retention is unavailable.
7. Exercise the standalone installer and uninstall in a disposable Windows VM. For upgrade testing, use a staging feed and a verified 1.3.0 installation; do not point test builds at a modified production feed. Check settings, output and uninstall behaviour.
8. Run Windows App Certification Kit for the Store build and test Explorer/Open with on an installed Microsoft-signed Store package when available. The unsigned submission MSIX is not a normal sideload installer.

The candidate was built from local uncommitted source. Confirm that 1.4.0 is unused, complete the dependency audit and commit the reviewed source before tagging and publication.
