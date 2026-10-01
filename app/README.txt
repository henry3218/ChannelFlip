Channel Flip — standalone left/right audio swap

繁體中文說明：README.zh-TW.txt

Run ChannelFlip.exe. Original project code is MIT licensed.
The EXE contains its own audio core. Equalizer APO, a virtual sound card,
Python, and Node.js are not required.
Use Windows x64 with .NET Framework 4.6.2 or later.

Language: select English or 繁體中文 at the top right. Your choice is saved.
First launch uses Traditional Chinese for Chinese Windows display languages
and English for other display languages. Device names are supplied by Windows.

1. Select the headphones or speakers you want to control.
2. Choose Set up this device, read the changes, then Set up and turn on swap.
3. Complete Windows administrator approval. Sound is briefly interrupted,
   and two short tones check core processing.
4. Use the Swap left/right switch after setup.
5. Test each source channel. With swap on, source left should reach your
   right ear, and source right should reach your left ear.
6. After both tests, choose I hear the expected direction to record it.

Setting: On means the switch value is saved.
Waiting for audio or a test means the current configuration has no evidence yet.
No new audio right now retains earlier processing/tests and hearing confirmation.
Swap processing detected uses recent counters; still confirm the actual ears.
Errors, disconnection, and unreadable settings have separate status messages.
Changing device/swap, disconnecting, or restarting the host clears old direction
evidence. Hearing confirmation lasts only for the current application session.
Changing language preserves the current test evidence and audio setting.

Use Tab to navigate and Space to operate the switch. System text size and
display scaling are supported; scroll when enlarged text needs more room.

Fixed device keeps the selected endpoint while offline and restores its display
when the same endpoint reconnects. Follow system default changes the UI target;
each device retains its own settings, and new devices still need setup.
Closing the application window keeps processing settings on configured devices.

Advanced settings:
- Turn off swap on all devices: restore the original direction without
  interrupting sound. Turn swapping back on in the main window.
- Restart Windows audio services: use this if sound misbehaves or the status
  shows an error. Sound on all devices stops for a second or two.
- Uninstall Channel Flip: undo every system change, including on offline
  devices, and delete the installed files. Sound is briefly interrupted.

Setup copies ChannelFlip.exe to Program Files and adds Channel Flip to Apps in
Windows Settings. Uninstalling it there does the same as Uninstall Channel Flip
in Advanced settings.
When a new version includes a newer audio core, choose Update audio core.
If a Windows or driver update removes a device's setup, choose Set up this
device again.

This audio core has no Microsoft audio signature. First-time setup requires
the system-wide DisableProtectedAudioDG=1 setting. DRM playback requiring
protected audio may be affected. Turning swap off or closing the window keeps
this setting; removal restores it according to the saved backup.
Read FIRST_RUN.md before enabling the unsigned core.

Processing applies to the selected Windows shared-mode audio effects path.
ASIO, exclusive mode, RAW, passthrough, or disabled enhancements may bypass it.
Mono has no stereo direction. Multichannel processing swaps only front L/R.
Windows 10 has not been tested. See VALIDATION.md for the tested scope.

Keep LICENSE and THIRD_PARTY_NOTICES.txt when redistributing.
Source and updates: https://github.com/henry3218/ChannelFlip
