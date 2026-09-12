# Reliability verification / 可靠性驗證

The 2.3.0 candidate implements F01–F07 and build provenance. Hardware acceptance for the new native core is separate from the earlier 2.2.0/2.1.1 results. 新核心的實機驗收不能沿用舊版結果。

## Regression coverage

| Item | Implementation and repeatable check |
| --- | --- |
| F05 偏好儲存 | Snapshot preferences; mark saved only after success; suppress reentry; retry on a later UI update. Inject first-write failure, recover storage, then verify persisted XML. |
| F01 操作互斥 | Administrators/SYSTEM global mutex covers elevated setup, rollback, removal and service restart. Child processes overlap attach/attach and attach/remove against a private HKCU registry tree. Backups are read after acquiring the gate. |
| F02 權限復原 | Flush original owner/group/DACL and intended phases before permission changes. Kill the child process immediately after owner, DACL and value writes, including before the next phase is recorded; a new process restores the exact descriptor. External ACL edits stop recovery and retain the journal. Local non-elevated tests change the DACL with the same owner; an elevated test runner also exercises Administrators → current user → Administrators ownership. |
| F06 操作錯誤 | Keep the operation's original device ID/name, action and error separately from selection. Switch the default during a failed test, refresh and manually reselect; the original failure stays visible and translates. |
| F04 裝置列舉 | Read GUID with `OpenPropertyStore` / `PKEY_AudioEndpoint_GUID`; isolate each endpoint exception and retain diagnostics. Treat endpoint IDs as opaque. Old offline preferences wait for reconnect to learn their GUID. |
| F07 聲道配置 | Negotiate matching speaker positions; only FL/FR swap. See the explicit policy below. |
| 發布追溯 | Verify version, embedded build context, EXE/core hashes, test receipt and packaged documents. Reject substituted EXEs and mismatched versions; a test makes any packaging-time Git lookup fail. |

The test registry lives under a unique `HKCU\Software\ChannelFlip.ReliabilityTests.*` tree. Its HKLM override is local to each test process. No test changes the installed APO or stops the system audio service.

## F03: callback memory inventory

| Memory reached while processing | Owner and residency arrangement |
| --- | --- |
| APO object, state pointer, channel/frame limits, child interface pointers | Dedicated `VirtualAlloc` allocation per object; `VirtualLock` before the processing lock succeeds. No shared allocator pages. |
| 4096-byte shared control/counter mapping | This APO's mapped view is locked before processing; unlock precedes unmap. |
| Instructions, compiler thunks, vtables, constants, image globals | All committed readable pages of this DLL are locked once per process; a guarded reference count prevents another instance's unlock from releasing shared pages. |
| Copy/swap operations | Bounded byte copies and float swaps within the pinned image. No callback allocation, wait, lock API, logging, I/O or external CRT copy call. Atomic counters use compiler intrinsics. |
| Audio buffers, connection-property arrays, callback stack | Supplied and owned by the Windows audio engine. The APO does not unpin or change the host's memory. The isolated harness explicitly locks its own audio buffers. |
| Chained vendor APO object, code and buffers | The child implements its own `LockForProcess` contract. Parent setup propagates child lock failures; it cannot certify arbitrary third-party DSP. |

All residency operations run outside the real-time callbacks. Lock failures return their HRESULT and record it in the shared error field; the APO cannot report a successful processing lock. Partial acquisitions are released. Unlock failures are recorded, and unreleased image ranges remain tracked for retry. Object destruction releases its dedicated virtual allocation and mapping. The memory guarantee applies while `LockForProcess` is held; calls after unlock are outside the host processing contract.

The test-only DLL injects failure at every acquisition boundary, tests clean retry and release, and verifies shared-image ownership with concurrent instances. The production DLL has no fault-injection exports. Microsoft's [VirtualLock contract](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtuallock) explains residency and the absence of an OS lock reference count; [LockForProcess](https://learn.microsoft.com/en-us/windows/win32/api/audioenginebaseapo/nf-audioenginebaseapo-iaudioprocessingobjectconfiguration-lockforprocess) defines the ready-to-process boundary.

## Speaker-mask policy / 聲道遮罩規則

| Input/output layout | Result |
| --- | --- |
| Matching mono with one defined speaker | Pass through unchanged. 雙方單聲道位置一致時原樣輸出。 |
| Zero-mask mono | Interpret as front center; compatible with mono center. 零遮罩單聲道視為中央。 |
| Zero-mask stereo | Interpret as conventional FL/FR; compatible with mask `0x3`. 零遮罩立體聲明確視為前左／前右。 |
| Two or more channels, defined mask containing FL and FR | Same masks required; swap only FL/FR. Tests include `0x3`, 5.1 `0x3f`, 7.1 `0x63f`. |
| Zero mask with more than two channels, missing FL/FR, undefined bits, mask/channel-count mismatch | Reject as unsupported. 不猜測多聲道位置。 |
| Same channel count but different positions, such as 5.1 rear vs side | Reject as unsupported; do not reinterpret the speaker order. |

The bit order follows [WAVEFORMATEXTENSIBLE](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible). Zero-mask interpretation above is this project's explicit compatibility policy. Endpoint identification uses the documented [PKEY_AudioEndpoint_GUID](https://learn.microsoft.com/en-us/windows/win32/coreaudio/pkey-audioendpoint-guid).

## Pressure and physical acceptance / 壓力與實機驗收

`build.ps1 -Test` writes `work/native-pressure.json`: 5,000 stereo callbacks, 480 frames each, 64 MiB of touched pressure memory and 50 working-set trims. It records process page faults, process faults during the measured call window, mean/p99/max duration, invalid buffers and 10 ms deadline overruns. Call-window fault counts also include the harness and measurement APIs; they cannot establish an APO-only fault count. Zero invalid buffers is not proof of glitch-free headphones.

**Live pressure results are available:** the frozen 2.3.0 candidate passed continuous playback with 512 MiB pressure and off/on comparisons. The continuous trace recorded no Glitch events or AudioDG hard faults; the earlier short-tone trace's nonzero events remain documented. Sleep action and listening still require confirmation before full F03 acceptance. See the [hardware record and exact artifact hashes](HARDWARE-2.3.0.md). 新核心已有實際宿主壓力追蹤；睡眠操作及聽感尚待確認，不能以隔離測試或計數器代替。

For the physical test, retain the exact EXE and core SHA-256 and use an administrator terminal to run Windows Performance Recorder's Audio profile in file mode (`wpr -start Audio -filemode`, then `wpr -stop <local-path>.etl`). Start only when no unrelated WPR recording is active; do not cancel another recording. Play a known stereo source, exercise memory pressure, sleep/resume once, reconnect and repeat the direction test. In Windows Performance Analyzer inspect audio-glitch events, AudioDG scheduling/CPU and hard faults around each event. Pair that trace with the isolated callback-duration report; a sampling trace does not give every callback's wall time. Keep ETL/device diagnostics private unless reviewed for disclosure.
