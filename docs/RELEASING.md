# GitHub release preparation

The project is MIT licensed. Source and binary releases must include `LICENSE` and `THIRD_PARTY_NOTICES.txt`. Microsoft SDK headers downloaded for testing retain their original terms and are not included in release packages.

## Build and inspect

From a clean Windows x64 checkout:

```powershell
.\tools\verify-translations.ps1
.\tools\bootstrap.ps1
.\build.ps1 -Test
.\tools\verify-ui.ps1
.\tools\verify-package.ps1
.\tests\package-tests.ps1
.\tools\package-release.ps1
```

The last command creates a local preview ZIP and its SHA-256 file under `dist/`. It does not create a GitHub release. The ZIP contains the EXE, user instructions, first-run changes and validation notes in both English and Traditional Chinese, plus license notices, build information and checksums. Both UI translations are embedded in the EXE. Verify the archive contents and both languages before uploading.

The GitHub Actions workflow builds and uploads a preview artifact for review. It has read-only repository permissions and does not publish releases. It omits physical-device diagnostics; hosted CI cannot validate actual headphone direction.

## Build provenance

`build.ps1` captures the source commit, tree, dirty status, input hashes, version and compiler hashes **before compilation**. The EXE embeds this context. `BUILD_RECORD.json` records the resulting EXE/core hashes; `TEST_RESULTS.json` binds the managed, native and fault-injection test results to those same bytes. `-SkipNative` refuses a stale native source record or a replaced DLL.

Packaging requires these records, checks the embedded context and version, checks every packaged document against the build's source snapshot, and verifies the staged EXE again. It never reads the packaging checkout's HEAD as build provenance. An altered EXE, mismatched version or stale/failed test receipt is rejected. A dirty build is explicitly marked as such; its commit alone does not identify its modified source.

Use `-OutputDirectory .\work\candidate` to build without overwriting the running app, and pass `-BuildDirectory .\work\candidate` to the verification and packaging scripts. Publish the resulting ZIP and its checksum directly. Do not rebuild the EXE between hardware verification and publication. These unsigned integrity records do not authenticate a publisher or prevent an attacker from forging both artifacts and records.

## Release evidence

Keep releases marked as previews until the current binary has passed these device tests:

- Normal and swapped source-L/source-R audio reach the expected ears.
- Games and media through the selected Windows shared-mode output are processed.
- Disabling swap, closing/reopening, restarting audio, and reconnecting the device behave as documented.
- Removing system settings restores the prior endpoint effect registrations, protected-audio setting and registry permissions.
- Failed setup restores usable audio and leaves enough information for recovery.
- Memory pressure and sleep/resume have ETW evidence for audio glitches and faults, with callback timing evidence and the exact core hash. The isolated pressure harness does not replace this test; see [reliability verification](RELIABILITY.md).

Document tested Windows builds and devices, and list bypass paths such as ASIO, exclusive mode, RAW and disabled enhancements. Do not label a binary WHQL certified or signed unless that exact distributed binary has been verified accordingly.

Open sourcing the program does not change Windows audio-signature requirements. Explain the protected-audio setting before users enable the unsigned core, including its system-wide scope, persistence and possible DRM impact.

For a future release that keeps the protected audio host enabled, see [SIGNING.en.md](SIGNING.en.md). A generic EXE signature or an EV enrollment certificate alone is not evidence that the APO can load in that protected environment.

## Publication

Choose the repository owner/name and release version, inspect the final source diff, run CI in that repository, and review the generated assets. Publish only after the maintainer decides the tested preview is ready to share. Record the release asset hash and source commit so reports can identify the exact build.
