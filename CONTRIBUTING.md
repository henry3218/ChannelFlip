# Contributing

Channel Flip uses the MIT license for its own source code. Contributions are accepted under the same license; retain third-party notices when adding dependencies.

## Build on Windows x64

Use 64-bit PowerShell. The checked-in build scripts use .NET Framework 4.x and a pinned Zig toolchain; no Visual Studio or NuGet packages are required.

```powershell
.\tools\bootstrap.ps1
.\build.ps1 -Test
.\tests\native-tests.ps1
.\tools\verify-package.ps1
```

The preparation script verifies SHA-256 hashes from `dependencies.lock.json`. Downloaded compilers and Microsoft SDK headers stay in the ignored `work/` directory. SDK headers retain their original licenses and are not redistributed in source or release archives.

## Testing changes

Keep the real-time audio callback bounded and free of allocation, locking, logging, or file I/O. Preserve existing endpoint effects and reversible setup. Changes to registration, access control, recovery, or state layout need relevant regression checks.

The normal tests use a private registry tree and do not attach an APO to a playback device. Do not run privileged attachment commands in CI. On a machine without audio hardware, use `verify-package.ps1 -SkipDeviceDiagnostics` for the packaging checks.

Hardware testing must explicitly record device model, Windows build, normal and swapped L/R playback, closing/reopening the app, service/device reconnect, and removal/restoration. Passing offline tests does not prove system-wide operation or DRM compatibility.

Avoid committing personal logs, local endpoint identifiers, credentials, compiled outputs, or the `work/` tree. Diagnostics can contain device names and IDs; review them before sharing.

## Translations

The interface supports English (`en`) and Traditional Chinese (`zh-TW`). Both are embedded in the standalone EXE through `src/Translations.xml`; no external language pack is needed. Chinese source messages are the catalog keys. Use `L10n.T` for immediate text, `L10n.M` for operation messages that must follow later language changes, and `DynamicResource Ui.<source message>` for XAML text. Keep full sentences together and preserve numbered formatting placeholders in both languages.

Update both translations and the corresponding English/Chinese documentation when changing user-facing behavior. Run `tools/verify-translations.ps1`, `build.ps1 -Test`, and `tools/verify-ui.ps1`. The checks cover missing messages, formatting arguments, language preference persistence, accessible names, preserved audio/test state, and small-window/large-text layouts in both languages. `--language en --simulate setup` opens an isolated English first-run example.

Device names and Windows/driver diagnostics should retain their supplied values. Do not change endpoint identifiers or audio configuration when changing the interface language. Actual English Narrator speech still requires human validation.

## Pull requests

Describe the user-visible change and the tests performed. State any remaining hardware or compatibility limits. Keep changes focused and preserve MIT and third-party license notices.
