# First-time setup and restoration

**English** | [繁體中文](FIRST_RUN.zh-TW.md)

Channel Flip includes its own audio core. First-time activation requires accepting the system changes below and checking the direction with test tones. See [VALIDATION.md](VALIDATION.md) for the tested scope.

Device effects are attached only to the headphones or speakers selected in the interface. The protected audio host setting below affects the whole system.

## Change requiring consent

This custom core has no Microsoft WHQL signature. To load it, setup sets the following Windows test setting to `1`:

`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio\DisableProtectedAudioDG`

The original value is backed up. This setting disables the protected audio host's signature restriction and may affect DRM playback requiring a protected audio path. It stays in effect until you uninstall Channel Flip from **Apps** in Windows Settings or from **Advanced settings**. Closing the window or turning swap off does not restore it.

This requirement comes from the [protected environment and testing guidance in Microsoft's APO documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects#disable-use-of-an-embedded-manifest). It is not a requirement to install another audio program. This version does not call, bundle, or rename Equalizer APO's audio DLL.

## Setup steps performed by the application

1. Extract the embedded DLL to `%ProgramFiles%\ChannelFlip\2.0\ChannelFlipApo.dll` and register Channel Flip's own COM/APO class.
2. Copy `ChannelFlip.exe` to `%ProgramFiles%\ChannelFlip\` and add **Channel Flip** to **Apps** in Windows Settings, so it can be uninstalled after the download folder is gone.
3. Save the selected device's original effects settings in the Journal under `HKLM\SOFTWARE\ChannelFlip\Devices\{device GUID}`. Retain the original effect while attaching the swap core.
4. Create `%ProgramData%\ChannelFlip\State\{device GUID}.bin` for immediate switching and processing counters.
5. Apply the audio host setting and restart audio services. Computer audio is briefly interrupted.
6. Play two short tones to check core processing. Report failure and attempt restoration if processing is not detected.

The `2.0` installation folder identifies the native core location; later interface versions can use that unchanged core.

## Updating

When a new version includes a different audio core, the main window offers **Update audio core**. With administrator approval, it stops audio services, replaces the core, starts them again and, if the selected device is connected, plays two tones to check the new core. If that check fails, the previous core is restored. Installations from 2.3.0 or earlier are offered **Add to Apps list** instead when their core is unchanged; that step does not interrupt audio.

If a Windows or audio driver update resets a device's sound effects, swapping stops and the device shows **Windows removed the swap setup**. Choose **Set up this device again**; the old backup record is cleared and the device is attached again.

## Restore the previous configuration

Uninstall **Channel Flip** from **Apps** in Windows Settings, or choose **Uninstall Channel Flip** in **Advanced settings**, and complete Windows administrator approval. The program restores its saved effects configuration, removes its class registration and Apps entry, restores the previous audio host setting, restarts audio services, and then deletes the files it installed under `%ProgramFiles%\ChannelFlip` and `%ProgramData%\ChannelFlip`. Files still in use, such as the uninstaller that is running, are deleted when Windows restarts. Registry values subsequently changed by another program are preserved. Your language and device preferences in `%LocalAppData%\ChannelFlip` are kept.

**Turn off swap on all devices** only restores the original channel direction. The core and audio host setting remain installed.

Before removal, the application lists all configured devices, including offline ones, and explains the interruption to computer audio. The affected settings are checked again before execution and when the administrator operation starts. If the scope changes, the operation stops and asks you to review the list again.
