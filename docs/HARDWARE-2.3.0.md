# 2.3.0 hardware record / 實機紀錄

Tested September 12–13, 2026 (Asia/Taipei), on Windows 11 build 26200 x64 with MOMENTUM 4 in Windows shared mode. The installed core and running application match the frozen candidate below.

測試環境為 Windows 11 build 26200 x64、MOMENTUM 4、Windows 共用模式。已安裝核心與執行中的程式均符合下方固定產物的雜湊。

**Status: live processing and continuous pressure passed; full F03 acceptance remains pending on sleep/resume and listening confirmation.**

**狀態：即時處理與連續播放壓力測試通過；完整 F03 驗收仍待睡眠／喚醒與聽感確認。**

## Results / 結果

| Check / 檢查 | Observation / 觀察結果 |
| --- | --- |
| Install / 接入 | The new core processed 76,800 swapped frames during setup with no core error. 新核心接入時處理 76,800 個互換影格，核心錯誤為 0。 |
| Short tones under pressure / 短音壓力測試 | Two 12-second runs, each touching 512 MiB and playing six alternating tones, increased processing and swap counters with no core error. 兩輪各 12 秒、512 MiB、六次交替測試音，處理與互換計數增加，核心錯誤為 0。 |
| Real switch behavior / 實際開關 | WASAPI playback advanced processing without swapping when disabled, and advanced swap counters when enabled. Processing ran in AudioDG. 關閉時正常處理且不互換；開啟時互換計數增加，宿主為 AudioDG。 |
| Continuous pressure / 連續播放壓力 | A single 32-second stream with 512 MiB of touched memory cycled off/on/off/on in 8-second stages. All four stages matched their expected counter behavior. 同一音訊串流連續播放 32 秒，512 MiB 記憶體持續觸頁，每 8 秒交替關閉／開啟，四階段計數皆符合預期。 |
| Continuous-run ETW / 連續播放追蹤 | No audio Glitch events, no AudioDG hard faults, and no lost ETW events were recorded in that trace. 該追蹤未記錄到音訊 Glitch 事件、AudioDG 硬分頁錯誤或 ETW 事件遺失。 |
| Sleep/resume / 睡眠喚醒 | The user reported waking the PC; a second short-tone run passed afterward. The System log had no matching suspend/resume events, and `powercfg /lastwake` returned zero entries. The exact sleep action still needs confirmation. 使用者回報已喚醒，後續短音測試通過；System 記錄沒有對應睡眠／恢復事件，`powercfg /lastwake` 為 0 筆，仍待確認實際睡眠操作。 |
| Listening / 聽感 | Direction and audible interruptions for this binary are awaiting user confirmation. 此版本的左右方向與有無可聽見的中斷仍待使用者確認。 |

## Trace findings / 追蹤發現

The earlier short-tone trace contained **seven Glitch events**: five `SERVER_INPUT_STARVATION` and two `BASE_OUTPUT_UNEXPECTED_BUFFER_COMPLETED`. They occurred near short-tone startup or shutdown. The short-tone player waits for an empty buffer before stopping; this is a plausible source of starvation events, not an established causal attribution. These events remain part of the record. The later continuous off/on comparison recorded none.

早期短音追蹤有 **7 次 Glitch 事件**：5 次 `SERVER_INPUT_STARVATION`、2 次 `BASE_OUTPUT_UNEXPECTED_BUFFER_COMPLETED`，時間接近短音起停。短音播放器會等緩衝區清空後才停止，可能產生缺料事件，但目前未建立因果歸屬；不能刪除或忽略這些觀察。後續連續播放的開／關對照未記錄到同類事件。

The short-tone trace also recorded five AudioDG hard faults, all before the two measured pressure windows. Their virtual addresses were outside the recorded ChannelFlip DLL image range. This does not prove that every callback-owned or host-owned page is fault-free. The continuous trace recorded zero AudioDG hard faults. Both traces reported zero lost events.

短音追蹤另有 5 次 AudioDG 硬分頁錯誤，均早於兩段壓力測量區間，虛擬位址不在記錄到的 ChannelFlip DLL 映像範圍內。這不代表所有回呼或宿主記憶體都沒有 page fault。連續播放追蹤的 AudioDG 硬分頁錯誤為 0，兩份追蹤的事件遺失數均為 0。

Direct callback timing comes from the separate isolated harness: 5,000 callbacks of 480 frames, 64 MiB pressure, 50 working-set trims; mean **0.000685 ms**, p99 **0.001100 ms**, maximum **0.003000 ms**, zero 10 ms deadline overruns and zero invalid buffers. Its 200 call-window process faults include the harness and measurement APIs. ETW sampling does not measure every live callback's wall time, and the Audio profile's hard-fault events do not cover all soft page faults.

回呼耗時另由隔離宿主量測：5,000 次、每次 480 影格、64 MiB 壓力、50 次工作集驅逐；平均 **0.000685 ms**、p99 **0.001100 ms**、最大 **0.003000 ms**，10 ms 逾時及無效緩衝區均為 0。量測區間的 200 次程序 page fault 包含測試宿主與計時 API。ETW 取樣不是每次實際回呼的耗時計時；Audio profile 的硬分頁事件也不涵蓋全部軟分頁錯誤。

These are bounded observations on one machine, not a universal latency or compatibility guarantee. Windows 10 remains untested. Both WPR recordings have stopped; raw ETL files and device diagnostics remain private.

以上為單台電腦、有限工作負載的觀察，不是所有情境的延遲或相容性保證；Windows 10 未驗收。兩次 WPR 錄製均已停止，原始 ETL 與裝置診斷保持私有。

## Frozen artifacts / 固定產物

The documentation can be updated after testing; it does not change this binary's build source. The candidate and ZIP were not rebuilt after hardware testing. Local deployment copied the same verified EXE.

文件可在測試後更新，但不會改變這份二進位的建置來源。實機測試後未重新建置候選 EXE 或 ZIP，本機部署使用同一份已核對的 EXE。

| Item / 項目 | Value / 值 |
| --- | --- |
| Build source commit | `d4d15ee7174eb9b39a8350ee2808754fc4308032` (clean) |
| EXE SHA-256 | `bab061dffa66f65325e817f004eb5dc7c9b8ca9d83d6926389c2416dab61b727` |
| Native DLL SHA-256 | `531565c1b66a312f50c0a46665c5583c1d730b37c63253cf08fe810f9427fb2a` |
| `ChannelFlip-2.3.0-preview.1-windows-x64.zip` SHA-256 | `7f96abc3f842fd3be74480df86b8647fd08033e6af16b4f6fdcbb07c021a82de` |
| Short-tone ETL SHA-256 | `07ff4f0aa90e7871453cc6062707cea106a305cb1af42f0dde12d732f186daa3` |
| Continuous ETL SHA-256 | `e3b59a8cd651afe5c8098dc0ad223c610bd35827344323cf3ac4fe745229074d` |

Local records: `work/reliability-hardware/`, including `core-ready.xml`, both `*-pressure.xml` files, `live-audio-after-resume.log`, `continuous/continuous.xml`, the two private trace summaries and `hardware-hashes.json`. ETL parsing used Microsoft's [TraceEvent library](https://github.com/microsoft/perfview/blob/main/documentation/TraceEvent/TraceEventProgrammersGuide.md), version 3.1.8, in the ignored test workspace only; it is not an application dependency.
