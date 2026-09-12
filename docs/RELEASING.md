# GitHub release preparation

The project is MIT licensed. Source and binary releases must include `LICENSE` and `THIRD_PARTY_NOTICES.txt`. Microsoft SDK headers downloaded for testing retain their original terms and are not included in release packages.

## Build and inspect

From a clean Windows x64 checkout:

```powershell
.\tools\verify-translations.ps1
.\tools\bootstrap.ps1
.\build.ps1 -Test
.\tests\native-tests.ps1
.\tools\verify-ui.ps1
.\tools\verify-package.ps1
.\tools\package-release.ps1
```

The last command creates a local preview ZIP and its SHA-256 file under `dist/`. It does not create a GitHub release. The ZIP contains the EXE, user instructions, first-run changes and validation notes in both English and Traditional Chinese, plus license notices, build information and checksums. Both UI translations are embedded in the EXE. Verify the archive contents and both languages before uploading.

The GitHub Actions workflow builds and uploads a preview artifact for review. It has read-only repository permissions and does not publish releases. It omits physical-device diagnostics; hosted CI cannot validate actual headphone direction.

## Release evidence

Keep releases marked as previews until the current binary has passed these device tests:

- Normal and swapped source-L/source-R audio reach the expected ears.
- Games and media through the selected Windows shared-mode output are processed.
- Disabling swap, closing/reopening, restarting audio, and reconnecting the device behave as documented.
- Removing system settings restores the prior endpoint effect registrations, protected-audio setting and registry permissions.
- Failed setup restores usable audio and leaves enough information for recovery.

Document tested Windows builds and devices, and list bypass paths such as ASIO, exclusive mode, RAW and disabled enhancements. Do not label a binary WHQL certified or signed unless that exact distributed binary has been verified accordingly.

Open sourcing the program does not change Windows audio-signature requirements. Explain the protected-audio setting before users enable the unsigned core, including its system-wide scope, persistence and possible DRM impact.

For a future release that keeps the protected audio host enabled, see [SIGNING.en.md](SIGNING.en.md). A generic EXE signature or an EV enrollment certificate alone is not evidence that the APO can load in that protected environment.

## Publication

Choose the repository owner/name and release version, inspect the final source diff, run CI in that repository, and review the generated assets. Publish only after the maintainer decides the tested preview is ready to share. Record the release asset hash and source commit so reports can identify the exact build.
