# Channel Flip

[繁體中文](README.md) | **English**

Swap the left and right channels of your headphones or speakers on Windows. The audio component is built in; no Equalizer APO installation is needed.

## [Download 2.3.0 (Windows x64 ZIP)](https://github.com/henry3218/ChannelFlip/releases/download/v2.3.0/ChannelFlip-2.3.0-windows-x64.zip)

[Release notes](https://github.com/henry3218/ChannelFlip/releases/tag/v2.3.0) · [Report an issue](https://github.com/henry3218/ChannelFlip/issues)

> [!IMPORTANT]
> This release has no Microsoft audio signature. First-time setup requires administrator approval, briefly interrupts computer audio, and disables the protected audio host's signature restriction system-wide. This may affect DRM playback. [Review the changes and how to restore them](FIRST_RUN.en.md).

## Get started

1. **Open the app:** Extract the download and run `ChannelFlip.exe`.
2. **Set up a device:** Select your headphones or speakers, choose **Set up this device**, and follow the on-screen steps.
3. **Check the direction:** Turn on **Swap left/right** and use both test buttons. The left source should reach your right ear; the right source should reach your left ear.

Swapping stays active after you close the window. Turn off **Swap left/right** to restore the original direction. To undo system changes, choose **Advanced settings → Remove all device settings**.

Switch between **English and Traditional Chinese** at the top right. The app remembers your choice.

## Compatibility

- **Requirements:** Windows 10 / 11 x64 with .NET Framework 4.6.2 or later.
- **Tested:** Windows 11 with MOMENTUM 4. Windows 10, other devices and actual sleep/resume remain unvalidated.
- **Audio paths:** Audio must pass through Windows shared-mode effects. ASIO, exclusive mode, RAW, passthrough, or disabled audio enhancements may bypass swapping.

[View compatibility and test results](VALIDATION.en.md)

## Build and contribute

Run in 64-bit PowerShell on Windows x64:

```powershell
.\tools\bootstrap.ps1
.\build.ps1 -Test
```

The output is `app/ChannelFlip.exe`. [Build and contribution guide](CONTRIBUTING.md) · [Release process](docs/RELEASING.md)

## License

[MIT](LICENSE). Include `LICENSE` and [third-party notices](THIRD_PARTY_NOTICES.txt) when redistributing.
