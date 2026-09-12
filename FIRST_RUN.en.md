# First-time setup and restoration

[繁體中文](FIRST_RUN.md) | **English**

Channel Flip includes its own audio core. First-time activation requires accepting the system changes below and checking the direction with test tones. See [VALIDATION.en.md](VALIDATION.en.md) for the tested scope.

Device effects are attached only to the headphones or speakers selected in the interface. The protected audio host setting below affects the whole system.

## Change requiring consent

This custom core has no Microsoft WHQL signature. To load it, setup sets the following Windows test setting to `1`:

`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio\DisableProtectedAudioDG`

The original value is backed up. This setting disables the protected audio host's signature restriction and may affect DRM playback requiring a protected audio path. It stays in effect until restored with **Advanced settings → Remove all device settings**. Closing the window or turning swap off does not restore it.

This requirement comes from the [protected environment and testing guidance in Microsoft's APO documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects#disable-use-of-an-embedded-manifest). It is not a requirement to install another audio program. This version does not call, bundle, or rename Equalizer APO's audio DLL.

## Setup steps performed by the application

1. Extract the embedded DLL to `%ProgramFiles%\ChannelFlip\2.0\ChannelFlipApo.dll` and register Channel Flip's own COM/APO class.
2. Save the selected device's original effects settings in the Journal under `HKLM\SOFTWARE\ChannelFlip\Devices\{device GUID}`. Retain the original effect while attaching the swap core.
3. Create `%ProgramData%\ChannelFlip\State\{device GUID}.bin` for immediate switching and processing counters.
4. Apply the audio host setting and restart audio services. Computer audio is briefly interrupted.
5. Play two short tones to check core processing. Report failure and attempt restoration if processing is not detected.

The `2.0` installation folder identifies the native core location; later interface versions can use that unchanged core.

## Restore the previous configuration

Choose **Remove all device settings** in **Advanced settings** and complete Windows administrator approval. The program restores its saved effects configuration, removes its class registration, restores the previous audio host setting, and restarts audio services. Registry values subsequently changed by another program are preserved. Extracted DLL/state files may remain on disk but are no longer registered in the audio path.

**Turn off all configured swaps** only restores the original channel direction. The core and audio host setting remain installed.

Before removal, the application lists all configured devices, including offline ones, and explains the interruption to computer audio. The affected settings are checked again before execution and when the administrator operation starts. If the scope changes, the operation stops and asks you to review the list again.
