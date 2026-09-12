# Channel Flip 2

[繁體中文](README.md) | **English**

[![Windows build and tests](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml/badge.svg)](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml)

Swap the left and right audio channels of a selected Windows output device. Channel Flip includes its own C++ audio processing object (APO) and is licensed under [MIT](LICENSE). **No Equalizer APO, virtual sound card, Python, or Node.js is required.**

[Download the 2.2.0 preview](https://github.com/henry3218/ChannelFlip/releases/tag/v2.2.0-preview.1) · [Report an issue](https://github.com/henry3218/ChannelFlip/issues) · [Validation and limits](VALIDATION.en.md)

Download `ChannelFlip-2.2.0-preview.1-windows-x64.zip` from the release assets, extract it, and open `ChannelFlip.exe`. The automatically generated **Source code** archives require compilation. The application uses Windows' .NET Framework 4.6.2 or later and targets Windows 10 / 11 x64; **Windows 10 has not been tested**.

## Languages

The interface supports **English** and **Traditional Chinese**. Use the language selector at the top right; your choice is saved for the next launch. On first launch, Chinese Windows display languages use Traditional Chinese, and other display languages use English.

Changing language preserves the selected device, its swap setting, and the current session's hearing confirmation. Device names remain as supplied by Windows. Windows/driver messages and previously captured technical exception details may retain their original language.

Both translations are embedded in the EXE. English and Chinese instructions are also included in the release ZIP.

## Everyday use

1. Select your headphones or speakers. Choose **Set up this device** the first time and read the changes before accepting **Set up and turn on swap**.
2. Use **Swap left/right** to turn swapping on or off. The card shows the configured setting separately from processing status.
3. Test the left and right source channels. When swap is on, the left source should reach your right ear and the right source your left ear. After both tests, record your own hearing confirmation.

**Waiting for audio or a test** means the current configuration has no processing or test evidence yet. **No new audio right now** means earlier evidence exists but audio is currently idle; pausing playback does not discard a hearing confirmation. **Swap processing detected** means recent processing counters increased and does not replace a listening test.

Changing the device or switch, disconnecting the device, or restarting the audio host invalidates earlier direction evidence. Hearing confirmations are kept only for the current application session.

**Fixed device** preserves the same endpoint while it is offline. **Follow system default** updates the target shown in the window; each device retains its own settings, and a new device still needs setup. Closing the window keeps swap settings active on configured devices.

**Advanced settings** contains actions to turn off all configured swaps, restart Windows audio services, and remove all device settings. Removal includes offline devices and checks the affected settings again before execution. Restarting audio or removing settings briefly interrupts sound across the computer.

## First-time setup

The interface opens without elevation. Attaching processing to Windows audio requires a one-time setup with administrator approval. Ordinary switching does not require elevation, and processing continues after the window closes.

**This preview core is not Microsoft WHQL signed. Enabling it requires the system-wide `DisableProtectedAudioDG=1` test setting, which disables the protected audio host's signature restriction and may affect DRM playback that requires a protected path.** The application explains this and requests consent. Open sourcing the code does not change that Windows requirement. Read [First-time setup and restoration](FIRST_RUN.en.md).

Setup backs up the existing configuration, attaches the core, restarts audio services, then plays two short tones and checks processing counters. If processing is not detected, setup reports an error and attempts restoration. You should still confirm the physical ear direction with both test buttons.

## Processing and compatibility

- Per-device settings, fixed/default selection modes, a slider switch, test tones, visible keyboard focus, and system text-size/DPI support.
- Direct floating-point L/R sample swapping. Other channels are unchanged; audio is not recorded or replayed.
- If an effect already occupies the selected position, the core retains it through COM, runs it first, then swaps L/R. Failure to load the original effect is reported.
- Applies to audio passing through the selected device's Windows shared-mode effects. ASIO, WASAPI exclusive mode, RAW, passthrough, or disabled audio enhancements may bypass it.
- Mono has no separate left/right direction. Multichannel processing swaps only the front L/R pair. Bluetooth hands-free mode may use another endpoint.
- Devices without `FxProperties`, or with multiple MFX registrations, are not supported. Driver updates may reset attachment settings.

This is an unsigned **2.2.0 preview**, not a WHQL-certified or broadly hardware-certified product. [VALIDATION.en.md](VALIDATION.en.md) separates automated checks, local hardware evidence, and untested cases.

## Build and verify

Use 64-bit PowerShell on Windows x64:

```powershell
.\tools\verify-translations.ps1
.\tools\bootstrap.ps1
.\build.ps1 -Test
.\tests\native-tests.ps1
.\tools\verify-ui.ps1
.\tools\verify-package.ps1
.\tools\package-release.ps1
```

The UI uses the Windows .NET Framework x64 C# compiler. The native core uses pinned Zig 0.16.0. `bootstrap.ps1` downloads the toolchain and SDK test headers listed in `dependencies.lock.json` and verifies SHA-256 before use. Tools, headers, and caches stay in the ignored `work/` directory. Microsoft SDK headers retain their original terms and are not redistributed in the repository or release ZIP.

Use `build.ps1 -Zig <path>` or `tests/native-tests.ps1 -Zig <path>` to select another copy of the same toolchain version. Build tools are not runtime dependencies. Packaging creates a local ZIP and checksums in `dist/`; it does not publish a GitHub release.

[GitHub Actions](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml) builds the project, verifies translations, runs offline tests and UI renders, and uploads a preview artifact. Hosted CI cannot confirm physical ear direction. See [CONTRIBUTING.md](CONTRIBUTING.md) and [release instructions](docs/RELEASING.md).

The native tests exercise the actual DLL through Microsoft SDK COM interfaces. Settings tests redirect HKLM access inside the test process to an isolated HKCU tree; they do not attach the core to a real device.

## Diagnostics and isolated previews

```powershell
.\app\ChannelFlip.exe --language en
.\app\ChannelFlip.exe --language en --diagnose C:\absolute\path\diagnostic.txt
.\app\ChannelFlip.exe --language en --render-preview C:\absolute\path\preview.png
.\app\ChannelFlip.exe --language en --render-preview C:\absolute\path\idle.png confirmed-idle 584 600 96 normal 225
.\app\ChannelFlip.exe --language en --simulate offline
.\app\ChannelFlip.exe --licenses C:\absolute\path\notices.txt
```

The optional `--language en` or `--language zh-TW` prefix overrides the language for that process. The UI selector saves a preference; a command-line override alone does not. Isolated previews default to Traditional Chinese and never read or write real audio settings or saved preferences.

Preview arguments are output path, scenario, width/height in DIP, DPI, theme (`normal` or `contrast`), and text size (100–225%). `verify-ui.ps1` renders 94 cases across both languages. These renders do not replace physical monitor or screen-reader testing.

`--off` turns off all swaps. `--remove` and `--restart-audio` require elevation. The setup entry point `--attach {endpoint-guid} --allow-audio-host-change` is only for cases where the user has explicitly accepted the system changes.

For an already configured device, `tools/test-live-audio.ps1 -EndpointId '<full endpoint ID>' -VerifyInstalled` plays short tones, temporarily switches processing off/on, checks counters, then restores the original switch state. It does not install the core and is not run in CI. Diagnostics contain local device names and IDs; review them before sharing.

## Implementation and licensing

`native/ChannelFlipApo.cpp` implements the DSP and COM core; `src/Engine.cs` manages backup/restoration and shared state; `src/Audio.cs` enumerates endpoints and plays WASAPI test tones. `src/Translations.xml` contains the embedded bilingual message catalog.

Implementation references: [Microsoft APO documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects), [APO architecture](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/audio-processing-object-architecture), and [Win32 metadata SDK](https://github.com/microsoft/win32metadata/tree/main/generation/WinSDK/RecompiledIdlHeaders/um). DPI and text-size handling follow [WPF samples](https://github.com/microsoft/WPF-Samples/blob/main/PerMonitorDPI/readme.md), [.NET ScaleHelper](https://source.dot.net/System.Windows.Forms.Primitives/System/Windows/Forms/Internals/ScaleHelper.cs.html), and [WPF accessibility improvements](https://github.com/microsoft/dotnet/blob/main/Documentation/compatibility/wpf-accessibility-improvements-48.md).

Original project code is [MIT licensed](LICENSE), allowing commercial use, modification, and redistribution with the required notice. Third-party components retain their own licenses in [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt), also embedded in the EXE. Redistribution must include both notices.

The [signing roadmap](docs/SIGNING.en.md) describes the additional work needed for a release that preserves protected audio. The source repository excludes compiled programs, build caches, device diagnostics, and private development records.
