# Release and update hosting

## Build

Use a version higher than existing packages:

```powershell
.\scripts\Build.ps1 -Configuration Release
$version = '<next-version>' # Replace with an unused major.minor.patch version.
.\scripts\Package.ps1 -Version $version -ReleaseNotes ".\docs\release-notes-$version.md"
.\scripts\CreateSourceBundle.ps1
```

The installer is self-contained, x64, with Desktop/Start-menu shortcuts. The
packaging script checks the compiled WinUI PRI resource in both the publish folder
and release package to prevent an installed-app startup failure. Licence texts,
third-party notices, the application's MIT licence and dependency audit are bundled.

The current publisher build is unsigned. Configure code signing before presenting
future releases as signed, and test the installer plus an installed version-to-version
upgrade. A successful build alone does not validate installation or live updates.

Create the matching release-notes file before running the packaging command.

## Public filenames and upgrade compatibility

The next release should use user-facing installer and portable download names
that say **Akki Image Tools**. The packaging script produces the existing
`lu.akki.ImageConverter` names; prepare byte-identical public copies named
`Akki-Image-Tools-win-Setup.exe` and `Akki-Image-Tools-win-Portable.zip`.
Keep the existing Velopack package ID, package and manifest filenames, installed
application identity, and self-hosted feed URL unless an explicitly planned migration
has been verified. A public download alias can be a byte-identical copy of the
generated installer or portable ZIP; do not rename packages referenced by a feed.
Generate checksums for the exact public files and test a clean install, portable
launch, and an upgrade from the previous installed version against a staging feed.
Verify the Store package identity and installed Store upgrade separately. Do not
remove prior release assets or change the current production feed as part of this work.

## GitHub release assets

Publish the source tag and attach:

- `lu.akki.ImageConverter-win-Setup.exe`
- `lu.akki.ImageConverter-win-Portable.zip`
- The version's full `.nupkg`, and its delta package if applicable.
- The generated `releases.win.json`, `assets.win.json`, and `RELEASES` feed files.
- `akki-image-converter-third-party-sources.zip`
- `SHA256SUMS.txt` covering downloadable assets.

The GitHub source ZIP is the application source. It does not replace the separate
native source bundle for re-distributors of the image-processing DLL.

## Self-hosted update feed

The application checks:

```text
https://update.akki.lu/akki-image-converter/win-x64/stable/
```

Serve generated JSON manifests and release packages over HTTPS with their exact
filenames. No authentication should be required. Upload packages before replacing
manifests, so clients never discover a missing package. Keep older packages while
clients reference them. Keep the native source bundle and notices accessible with
the corresponding binary releases.

Publishing a GitHub Release does not deploy this separate webserver feed.

## Update preferences

- **Notify** (default): check on launch/every six hours while running; the user
  initiates download, then installation occurs on exit or restart.
- **Automatic**: check/download while running, install when the app closes.
- **Manual**: check only from Settings.

There is no background service or scheduled task. Conversion works offline.
Updates are active only in Velopack-installed builds. Test the complete upgrade
against a staging feed, including offline behaviour, before enabling it for public
clients. Use a publisher signing strategy; feed hashes and HTTPS alone are not
equivalent to publisher authentication.
