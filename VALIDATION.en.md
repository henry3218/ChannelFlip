# Validation record

[繁體中文 and full historical records](VALIDATION.md) | **English**

## 2.3.0: reliability candidate

Date: September 12, 2026. Windows 11 build 26200, x64. This candidate addresses F01–F07 and release provenance. Its new audio core requires its own hardware acceptance; earlier results below do not establish that. See the [implementation, tests and limits](docs/RELIABILITY.md).

- **325 managed checks passed**, covering preference retries, retained operation errors, per-device isolation, concurrent processes and recovery after forced process termination.
- **63 production-core checks and 94 separate fault-injection checks passed**, including speaker positions, zero masks, mono/stereo/6/8 channels and failed memory residency acquisition.
- **7 packaging checks passed**: reject replaced EXEs, mismatched versions and stale/failed test records; verify packaged hashes; prevent packaging-time HEAD from supplying build provenance.
- **189 bilingual messages and 94 isolated UI renders passed**. A standalone EXE produced both-language previews, licenses and read-only device diagnostics.
- The pressure harness records 5,000 callbacks, 64 MiB of touched memory and 50 working-set trims. Its report includes process and call-window page faults, mean/p99/max duration, 10 ms deadline overruns and invalid buffers. These are isolated-host measurements, not evidence of physical headphone glitches or faults attributable solely to an AudioDG callback.

On September 12–13, the frozen candidate was attached to MOMENTUM 4 and passed live switch checks and continuous playback with 512 MiB of touched pressure memory. The user confirmed correct direction without audible distortion or sudden interruptions. The continuous ETW trace recorded no Glitch events, AudioDG hard faults or lost events. The earlier short-tone trace's seven Glitch events and five host hard faults are also retained in the [hardware record](docs/HARDWARE-2.3.0.md); the overall session is not described as anomaly-free. Actual sleep remains unverified; power diagnostics identified an AWAYMODE request from Mobile Hotspot, so full F03 hardware acceptance remains pending. Windows 10 remains untested. The EXE and ZIP were not rebuilt for this documentation update.

Local evidence: `work/reliability-build.log`, `work/reliability-packaging.log`, `work/reliability-standalone.log`, `work/reliability-ui.log`, and `work/native-pressure.json`. CI separately retains native test output, pressure reports and artifact records. Private endpoint diagnostics and ETL traces are not published.

## 2.2.0: English and Traditional Chinese

Date: September 12, 2026. Environment: Windows 11 build 26200, x64. This update adds English, a language selector, and bilingual documentation. The native audio DLL is unchanged.

- **281 C# checks passed, 0 failed.** The 97 additions to the previous 184 checks cover language selection and saved preferences, formatting arguments in both languages, English accessible names and spoken-language metadata, preserved device/swap/test/hearing state, and retranslation of saved test failures and idle messages.
- **180 bilingual messages verified.** English entries are complete and formatting arguments match. Application-owned Chinese strings in C# and XAML are checked against the catalog; the language selector intentionally uses each language's own name. Windows device names and technical exception details retain their supplied values.
- **94 isolated UI renders succeeded:** the previous 47 scenarios in each language. Representative English confirmed-idle, small-window 225% text, high-contrast core-error, and Chinese first-run images were visually reviewed. Automated layout checks also cover all 14 states in both languages: primary test visibility in a small window, no horizontal scrolling, and access to Advanced settings at 225% text.
- **Standalone EXE checks passed.** A directory containing only the EXE produced Chinese/English previews, a read-only diagnostic report, and license text. The embedded native DLL matches the tested DLL byte for byte, and all native dependencies resolve to Windows System32.
- The actual 2.2.0 window was switched from Traditional Chinese to English. Main controls, accessible names, and the Help dialog updated. Closing and reopening used the saved English preference. Read-only diagnostics reported no core error; the fixed MOMENTUM 4 target and enabled swap setting were preserved. No core reattachment or audio-service restart was performed for this update.

Actual English Narrator speech, physical ear-direction tests, live Windows text-size/high-contrast changes, and cross-monitor dragging were not repeated in this update. Automated language metadata and layout checks do not replace those manual tests. The earlier 2.1.1 evidence below applies to that version and environment. **Windows 10 remains untested.**

Local records: `work/english-build-tests.txt`, `work/english-ui-renders.txt`, `work/english-ui-validation/cases.json`, and `work/english-package-verification.txt`. Private diagnostics and desktop data are excluded from the public repository. Each hosted CI result is recorded separately in [GitHub Actions](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml).

2.2.0 EXE SHA-256: `44F3C8063E1D65C8CB16FF5217E77DE15282221C2C1C7BC7C70E843FDE5EABFF`.

Unchanged native DLL SHA-256: `F431CC7BB0AC4479C6AACB4B957A064AB073F33D15E54AEE3E7C29097F3629F4`.

The [2.2.0 preview release](https://github.com/henry3218/ChannelFlip/releases/tag/v2.2.0-preview.1) provides `ChannelFlip-2.2.0-preview.1-windows-x64.zip` and its SHA-256 file. The ZIP includes English/Chinese usage, first-run and validation notes. `BUILD_INFO.json` records the source commit, executable hash, and supported interface languages.

## Earlier 2.1.1 hardware evidence

The following is a summary of the [full Traditional Chinese record](VALIDATION.md#211-閒置狀態滑塊與無障礙修正), collected on the same Windows 11 build on September 12, 2026:

- 184 C# checks passed and 47 isolated UI renders succeeded. Confirmed-idle evidence survived nine seconds without new audio. The switch exposed TogglePattern, and UI Automation changed the simulated backend correctly.
- Actual Windows text size was increased from 100% to 208%. Application text wrapped and controls remained keyboard-reachable; returning to 100% updated the same process. The separate layout suite also covered 225% text.
- One physical monitor was temporarily set to 150%; the user moved the running window to the other 100% monitor. The controls remained visible. Only that observed direction of travel was counted. Both monitors were restored to 100%.
- With Windows Narrator running, the user confirmed that the Chinese control names, current switch state, and left/right test names were clearly and correctly read. This does not establish English Narrator or other screen-reader coverage.
- The user powered MOMENTUM 4 off and back on. While offline, the app retained the original target and disabled tests. After reconnection, the same window recovered the same endpoint and enabled controls without manual reselection or core setup.
- The user confirmed correct swapped ear directions after reconnection and that idle status retained the hearing confirmation. Final diagnostics reported Attached=True, Enabled=True, and no core error.

That release's EXE SHA-256 was `60AD625DB5DCF302DE0E18645BB20826AFCBBDE3B901AA561387207409B6B300`. It is available as the [2.1.1 preview](https://github.com/henry3218/ChannelFlip/releases/tag/v2.1.1-preview.1).

## Earlier native core and restoration evidence

The 2.1.0 and 2.0.1 records include **52 passing native COM/DSP checks**, run against the actual DLL using Microsoft SDK interfaces. Coverage includes simultaneous swapping, mono/multichannel behavior, the live switch, silence, format/buffer limits, COM identity/reference counting, original-effect chaining, and retry after errors.

On MOMENTUM 4, shared-mode audio increased processing counters with swapping off and increased swap counters with swapping on. The host was identified as `audiodg`. The user confirmed that the left source reached the right ear and the right source reached the left ear.

Removal was tested on that earlier version: original endpoint effect values and registry permissions matched the saved state, the originally absent `DisableProtectedAudioDG` value was removed, the application's COM class was unregistered, audio services ran, and WASAPI test tones still played. The device was then reattached with consent. The installed, embedded, and tested native DLL hashes matched.

See [the complete historical record](VALIDATION.md) for version-specific details and earlier evidence limits. Historical observations are not claims that every system operation was rerun for 2.2.0.

## Remaining compatibility limits

DRM, ASIO, exclusive mode, RAW, passthrough, Bluetooth hands-free switching, other audio devices, and Windows 10 have not been validated. Some of those paths may bypass Windows shared-mode effects entirely. Live high-contrast changes and all combinations of monitors, text scales, languages, and assistive technology have not been certified.

The preview is unsigned and has no WHQL or broad hardware certification. Passing CI or detecting processed frames does not prove physical direction or protected-path compatibility. Read [first-time system changes](FIRST_RUN.en.md) before enabling the core.
