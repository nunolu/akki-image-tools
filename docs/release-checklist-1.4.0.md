# Akki Image Tools 1.4.0 release candidate

Version 1.4.0 is a candidate: verify it is unused in GitHub releases, the update feed and Partner Center before publishing. The candidate is built from local uncommitted changes; commit the reviewed source and create the matching tag before publishing. Do not treat a successful build as acceptance.

## Acceptance before publication

- Run the combined Windows UI and batch checks: HEIC/HEIF, presets, naming, multiple widths, padding, crop positions, preview navigation, metadata options and ICC profiles.
- Check the partial EXIF GPS control is described accurately: XMP, IPTC and other location-bearing content may remain.
- Test the unsigned installer, portable build, uninstall and an upgrade from 1.3.0 using a staging feed. Confirm feed manifests reference existing packages and leave prior packages available.
- Run Windows App Certification Kit, validate the Store package and test Explorer/Open with activation in the signed installed Store edition.
- Verify the exact files and SHA-256 checksums; include the corresponding third-party source bundle with the release.
- Refresh the NuGet dependency vulnerability audit with network access; the candidate build could not reach the audit service.
- Confirm the Microsoft Store listing, GitHub release and website text and screenshots match the accepted build. Publish the release and update feed only when ready.

## Build

Use `scripts/Package.ps1 -Version 1.4.0 -ReleaseNotes .\docs\release-notes-1.4.0.md` and `scripts/PackageStore.ps1 -Version 1.4.0`. The standalone package identity and feed names remain unchanged. The public setup and portable names may be byte-identical aliases, with their own checksums. The Store submission MSIX is unsigned and is not a direct-distribution installer.

The isolated candidate feed was generated without previous releases, so it has no delta package or prior release history. A separate local `artifacts/upgrade-test-1.4.0` feed was subsequently generated from a verified local 1.3.0 package. It contains 1.3.0 full, 1.4.0 full and 1.4.0 delta, and its 1.4.0 full package matches the candidate. Its package sizes and hashes were checked, and Velopack applied the delta to the 1.3.0 package to reconstruct the 1.4.0 full package byte-for-byte. This feed has not been compared with the live feed or used for an installed upgrade. The app's update URL is compiled into the binary; a controlled staging test must account for this without altering production. Do not replace production manifests without reconciling the live feed and testing the resulting upgrade.
