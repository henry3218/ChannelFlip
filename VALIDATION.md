# 獨立版驗證紀錄

**繁體中文** | [English](VALIDATION.en.md)

## 2.3.0 可靠性修正候選版

日期：2026-09-12。Windows 11 build 26200，x64。修正 F01–F07 與發布追溯；新音訊核心的實機驗收獨立進行，不沿用下方舊版結果。[實作、測試方法與限制](docs/RELIABILITY.md)。

- C# 回歸測試 **325 項通過**，包含偏好重試、錯誤保留、逐裝置隔離、兩程序互斥及權限交易強制終止後的復原。
- 正式核心 **63 項通過**；獨立故障注入核心 **94 項通過**。零遮罩、不同聲道位置、單聲道、2／6／8 聲道與記憶體鎖定失敗均有明確斷言。
- **7 項打包測試通過**：拒絕 EXE 替換、版本不符與失效測試紀錄，驗證包內雜湊，確認打包不查詢 HEAD 作為建置來源。
- **189 條雙語訊息**與 **94 個隔離 UI 畫面**檢查通過；單 EXE 可產生雙語預覽、授權及唯讀裝置診斷。
- 壓力測試記錄 5,000 次回呼、64 MiB 記憶體觸頁與 50 次工作集驅逐。輸出包含程序 page fault、量測區間 page fault、平均／p99／最大耗時、10 ms 逾時次數與無效緩衝區。這是隔離測試宿主的數據，並非實體耳機音訊中斷或 AudioDG 回呼專屬 page fault 的證明。

實機睡眠／喚醒與記憶體壓力驗收需記錄當次 EXE／核心雜湊、ETW、播放方向與中斷情況，隨該版本的驗收結果另行提供。在這些結果具備前，F03 實機驗收保持待完成，Windows 10 亦未驗收。

本地證據位於 `work/reliability-build.log`、`work/reliability-packaging.log`、`work/reliability-standalone.log`、`work/reliability-ui.log` 及 `work/native-pressure.json`。CI 另外保存核心測試、壓力報告與產物紀錄，私人的装置診斷與 ETL 不公開。

## 2.2.0 繁體中文與英文

日期：2026-09-12。Windows 11 build 26200，x64。本輪新增英文、語言選單與雙語文件；原生音訊 DLL 未修改。

- **281 項 C# 測試通過，0 失敗**。在 2.1.1 的 184 項上新增 97 項，涵蓋語言選擇與偏好保存、兩種語言的格式參數、英文可存取名稱和朗讀語言中繼資料、切換後保留裝置／互換設定／測試時間／聽感確認，以及錯誤和閒置訊息的即時翻譯。
- **180 組雙語訊息檢查通過**：英文無遺漏、格式參數一致，C# 和 XAML 自有中文文案均對應翻譯表；語言選單保留各語言自稱。Windows 提供的裝置名稱和技術例外內容不強制翻譯。
- **94 組隔離畫面產生成功**，即原本 47 組情境各以中英文呈現。檢視英文已確認閒置、小視窗 225% 文字、高對比核心錯誤及中文首次設定代表畫面；自動測試另驗證兩語言全部 14 種狀態在小視窗的測試鍵可見性，以及大文字下無水平捲動、進階入口可達。
- **單一 EXE 驗證通過**：獨立目錄只放 EXE，成功產生中英文預覽、唯讀診斷與授權輸出。內嵌 DLL 與已測試的原生 DLL 完全相同，相依全部由 Windows System32 提供。
- 正式 2.2.0 視窗已實際從繁體中文切換英文，主畫面、按鈕、無障礙名稱、使用說明對話框均更新。關閉並重新開啟後仍使用保存的英文偏好；唯讀診斷確認核心沒有錯誤，MOMENTUM 4 的固定目標及已開啟互換保持不變；本輪沒有重新接入核心或重新啟動音訊服務。

本輪未重新進行英文 Narrator 人工聽讀、實體耳機方向聽測、Windows 文字大小／高對比設定切換或跨螢幕拖曳。英文語音中繼資料與排版的自動檢查不能取代上述驗收。2.1.1 的人工結果保留於下方，適用於當時版本與環境；Windows 10 仍未實測。

本機紀錄：`work/english-build-tests.txt`、`work/english-ui-renders.txt`、`work/english-ui-validation/cases.json`、`work/english-package-verification.txt`。私人診斷與桌面資料不加入公開倉庫。公開 CI 的每次結果另見 [GitHub Actions](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml)。

2.2.0 EXE SHA-256：`44F3C8063E1D65C8CB16FF5217E77DE15282221C2C1C7BC7C70E843FDE5EABFF`。

原生 DLL SHA-256（未修改）：`F431CC7BB0AC4479C6AACB4B957A064AB073F33D15E54AEE3E7C29097F3629F4`。

2.2.0 預覽包：`ChannelFlip-2.2.0-preview.1-windows-x64.zip`，提供於 [GitHub 發布頁](https://github.com/henry3218/ChannelFlip/releases/tag/v2.2.0-preview.1)。包內 `BUILD_INFO.json` 記錄來源提交、EXE 雜湊及支援語言，並附中英文使用、首次啟用及驗證說明。

## 2.1.1 閒置狀態、滑塊與無障礙修正

日期：2026-09-12。Windows 11 build 26200，x64。這輪依後續三項審核意見修正；不包含 Windows 10。

- **184 項 C# 測試通過，0 失敗**。新增「兩次測試 + 明確聽感確認 + 九秒無音訊」回歸：顯示「目前沒有新音訊」，保留測試時間、兩個已測聲道及確認；恢復播放不要求重測。裝置斷線、宿主更換或設定改變仍會使舊確認失效。另驗證斷線後清除舊的成功操作提示，不會再要求按已停用的測試鍵。
- 滑塊保留 WPF ToggleButton 與 TogglePattern；左／右位置及文字呈現關閉／開啟。自動測試實際透過 TogglePattern 切換並核對模擬後端設定，修正只處理 Click 時輔助工具可能只改外觀的缺陷。
- **47 組隔離畫面產生成功**，包含新增的已確認閒置、225% 文字，以及原本的尺寸、DPI 與高對比情境。檢視閒置、開／關、高對比、最大文字等代表畫面。另驗證 225% 文字在小視窗可垂直捲動到進階入口，無水平捲動。
- 單一 EXE 在隔離目錄的預覽、診斷、授權輸出及系統 DLL 相依檢查通過。正式 EXE 與 184 項測試使用的檔案 SHA-256 相同。原生 DLL 未修改，仍與 2.1.0 的 52 項原生測試版本、已安裝版本一致；本輪沒有重跑未修改的原生測試。

### Windows 設定與正式視窗實測

| 項目 | 本輪觀察與界線 |
| --- | --- |
| 系統文字大小 | 實際將 Windows 設定從 100% 改為 208%。發現 WPF 原本只有標題列變大，修正後程式內文、裝置欄及按鈕文字都會放大、換行；Tab 可依序到達測試鍵與底部操作。100% 還原後，同一程序立即回復，無須重新開啟。另有 225% 隔離排版檢查。 |
| 跨實體螢幕 | 右側螢幕實際由 100% 暫改為 150%；使用者將執行中的程式拖至左側 100% 螢幕並回覆「已移到左側」。擷取位置由正 X 移到負 X，主卡、開關、測試及進階入口仍正常呈現。已恢復右側原本 100%。本輪記錄單程移動，不將未取得結果的回程算作通過。 |
| 無障礙樹 | 啟用較新 .NET Framework 的 WPF 無障礙行為後，已隱藏的設定、聽感確認和錯誤按鈕不再出現在控制項樹。可讀出功能名稱與操作說明。 |
| Narrator | 已實際啟動 Windows Narrator，正式視窗顯示其焦點標示。Tab 巡覽裝置、重新整理、互換及左右測試鍵；空白鍵關閉再開啟互換成功，焦點保留。使用者實際操作後，對「功能名稱、目前開／關狀態、左右測試按鈕名稱是否清楚朗讀」回覆「都有清楚朗讀，狀態也正確」。此人工驗收通過；不能推廣為所有閱讀器、聲音與語言組合都通過。測試後已結束此次啟動的 Narrator。 |
| 耳機實體重連 | 使用者將 MOMENTUM 4 關機：Windows 的可用清單已無此端點、預設轉為螢幕喇叭，程式仍保留 MOMENTUM 4 並顯示離線，互換和左右測試鍵均停用。發現底部殘留舊的成功提示後，補修提示清除與離線開關說明，通過回歸測試並在耳機仍關機時更新 EXE。使用者重新開機後，同一個新視窗自動恢復原端點、開啟設定與測試鍵；沒有手動改選、重新設定核心或在重連時重開視窗。 |
| 重連後方向與閒置 | 使用者完成左右測試後，對方向與暫停音訊的檢查回覆「方向正確，閒置後也保留確認」。最後依這份明確回覆在程式內記錄聽感，確認畫面顯示已記錄，且後續處理音訊時保留結果。方向與閒置觀察以使用者回報為證據；九秒閒置時維持聽感確認的精確回歸另由自動測試涵蓋。最後診斷為 Attached=True、Enabled=True、Error=0，固定裝置偏好仍為原端點。 |

系統高對比的即時切換尚未重做，本輪維持配色資源與隔離畫面檢查。這些結果適用於上述本機環境，不代表已完成所有輔助技術、硬體與縮放組合驗收。

結束時 Windows 文字大小為原本的 100%，兩台實體螢幕的有效 DPI 均為 96（100%）；此次啟動的 Narrator 已結束。正式程式保持開啟，既有捷徑指向更新後的 EXE。

紀錄：`work/ui-polish-tests.txt`、`work/ui-polish-renders.txt`、`work/ui-polish-validation/cases.json`、`work/ui-polish-package.txt`、`work/ui-polish-display-restored.json`、`work/polish-reconnect-offline.txt`、`work/polish-reconnect-online.txt`、`work/ui-polish-final-diagnostic.txt`。Narrator、跨螢幕與耳機的操作和使用者回覆記錄在這次開發工作對話中；不會包入使用者的桌面截圖或其他應用程式資料。

2.1.1 EXE SHA-256：`60AD625DB5DCF302DE0E18645BB20826AFCBBDE3B901AA561387207409B6B300`。

2.1.1 預覽包：`ChannelFlip-2.1.1-preview.1-windows-x64.zip`，附同名 SHA-256 檔。[GitHub 發布頁](https://github.com/henry3218/ChannelFlip/releases/tag/v2.1.1-preview.1) 提供下載與發布時的驗證資訊；包內 `BUILD_INFO.json` 記錄來源提交與 EXE 雜湊。公開原始碼的 [GitHub Actions 紀錄](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml) 與上述本機實機驗證分開呈現。下方 2.1.0 / 2.0.1 為當時的歷史紀錄。

## 2.1.0 介面與操作流程修正

日期：2026-09-12。Windows 11 build 26200，x64。五項介面審核問題已實作修正；以下區分自動驗證、實機觀察與尚待人工驗收的範圍。

- **166 項 C# 測試通過，0 失敗**。涵蓋既有設定／測試音、登錄還原核對、影響範圍變更拒絕、離線裝置保留、跟隨預設、偏好遷移、近期計數、錯誤優先級、取消、操作目標固定、聽感確認失效，以及 13 情境 × 2 尺寸的關鍵操作可見性。
- **52 項原生 COM／DSP 測試通過，0 失敗**。本輪未更改原生音訊核心；已安裝 DLL、通過測試的 DLL 與新 EXE 內嵌 DLL 位元組相同。
- **40 組隔離畫面成功產生**：13 種情境的兩種尺寸，另含 125%／150%／200% DPI 與高對比代表案例。已檢視首次使用、核心錯誤、無裝置、長裝置名稱、200% DPI、高對比等代表畫面。DPI 套用在 WPF 排版根節點，不是僅把截圖放大。
- 單一 EXE 複製至獨立目錄後，預覽、診斷、授權輸出通過；所有原生相依仍解析至 Windows System32。

實際視窗操作已檢查：

- 深色裝置下拉選單及已選取樣式；Esc 關閉下拉選單。
- 左聲道按鈕播放、Tab 移到右聲道、Space 播放，以及播放完成後焦點回到原測試按鈕。
- 兩次測試都偵測到核心處理，介面顯示正確的預期耳朵；只有兩邊測試完成後才出現使用者聽感確認入口。未代替使用者按下聽感確認。
- 進階設定列出 MOMENTUM 4、全域操作及整台電腦音訊中斷的說明；移除按鈕先開啟專用確認畫面，預設焦點為取消。
- 按取消回到主畫面，焦點回到進階設定；裝置仍接入且互換仍開啟。取消流程未啟動管理員移除操作。
- UI Automation 能讀取控制項名稱、狀態及操作說明。這不等於已完成螢幕閱讀器的朗讀驗收。

本輪實機測試發現並修正一個介面誤判：`.NET Process.HasExited` 對仍在運作的 AudioDG 回報「存取被拒」，不能當作宿主已停止。改由程序表核對 AudioDG，並結合新處理計數；實機回歸已確認正常。

MOMENTUM 4 的共用模式音訊測試通過：關閉互換時只增加處理計數；開啟時互換計數增加；PID 確認為 AudioDG；測試最後恢復原先開啟狀態。本輪沿用原生核心，在 2.0.1 已取得的實體左右耳確認保留於下方歷史紀錄。

**仍待人工驗收／本輪未重做：** 系統高對比設定的即時切換、跨實體螢幕 DPI 移動、螢幕閱讀器實際朗讀。裝置斷線重連與操作途中改變目標，本輪以隔離情境測試；完整移除／重新接入的系統還原證據沿用下方 2.0.1 實測，本輪另驗證確認、取消及範圍核對。Windows 10 與其他播放路徑仍未實測。

本輪紀錄：`work/ui-revision-tests.txt`、`work/ui-render-verification.txt`、`work/ui-validation/cases.json`、`work/ui-package-verification.txt`、`work/ui-revision-live-audio.txt`。畫面與工具操作紀錄用於開發驗證，不是程式執行相依。

2.1.0 EXE SHA-256：`B342B2C17D7BE121BFBFF11DF3F7456021991493CF3CA42B42D1D2C7F3E7B456`。

本機預覽包：`dist/ChannelFlip-2.1.0-preview.1-windows-x64.zip`，附同名 SHA-256 檔；未發佈至 GitHub。

## 2.0.1 歷史驗證

日期：2026-09-12。版本：2.0.1 預覽版。環境：Windows 11 build 26200，x64，.NET Framework 4.x。

- 正式 EXE 與原生 DLL 編譯成功。DLL 為有效的 Windows x64 PE 動態程式庫。
- C# 設定與測試音：**62 項通過，0 項失敗**。涵蓋共享記憶體即時開關、損壞狀態拒絕、設定型別與 Unicode 還原、外部變更保留、部分交易還原、只有讀取權限時跳過尚未套用的還原項目、PCM / float 測試音隔離。
- 原生 COM / DSP：**52 項通過，0 項失敗**。測試以 Microsoft 官方 SDK 介面呼叫實際 DLL，涵蓋同時交換、mono / 6 聲道、即時開關、靜音、格式與緩衝邊界、COM 聚合的介面身分與參考計數、原核心串接與錯誤後重試。
- 偵測到 5 個可用播放裝置。預設為 `耳機 (MOMENTUM 4)`，2 聲道。
- WPF 介面由正式 EXE 內嵌 XAML 產生預覽並視覺檢查；左右互換、裝置選擇、首次說明、測試音與還原入口皆可見。
- 僅複製一個 EXE 至新的獨立資料夾後，診斷、WPF 預覽與授權聲明輸出皆成功。內嵌 DLL 與通過原生測試的 DLL 位元組完全相同。所有原生相依皆解析至 Windows System32（包含 Windows 的 UCRT API set）。
- 現行 `src/`、`native/` 和建置腳本不引用 Equalizer APO。V1 相依元件移出正式 `app/`。
- 開源建置驗證：2.0.0 初版曾從 Git 預定納入的原始檔建立乾淨副本，重新展開經 SHA-256 驗證的 Zig 套件、下載固定提交版本的 SDK 測試標頭後，完成編譯、60 項 C# 測試、46 項原生測試及單一 EXE 驗證。2.0.1 修正沿用相同鎖定工具鏈，在本機重新編譯並完成上述 114 項測試與單一 EXE 驗證；尚未重新執行完整乾淨副本流程。
- MIT LICENSE、第三方聲明、版本與雜湊資訊已納入本機預覽 ZIP。GitHub Actions 已提供設定，尚未在 GitHub 託管執行器上執行；本機的乾淨建置不能替代該環境的驗證。

## MOMENTUM 4 實機驗證

已取得使用者同意，接入 MOMENTUM 4 的 Windows 共用模式音訊流程，並設定 `DisableProtectedAudioDG=1`。其他播放端點未接入。

- 首次實機接入發現並修正 .NET Framework 的登錄權限例外、ServiceController 相依服務過早釋放及 APO 缺少 COM 聚合支援；修正後接入回傳成功。
- 互換關閉時：實際音訊框架持續處理，互換計數不增加，未回報核心錯誤。
- 互換開啟時：實際互換計數增加，記錄的宿主程序已核對為 `audiodg`，未回報核心錯誤。
- 「移除系統設定」實測回傳成功：全部原端點音效值與機碼權限均與安裝前相符；原先不存在的 `DisableProtectedAudioDG` 已移除；本程式 COM 類別已取消登記；音訊服務正常執行，左右測試音仍可透過 WASAPI 播放。
- 完成還原測試後，已重新接入並啟用互換。2.0.1 最後一次接入的初始診斷為 `Attached=True`、`Enabled=True`、`Swapped=81120`、`Error=0`，並開啟主視窗供使用者聽測。
- Program Files 中實際接入的 DLL、EXE 內嵌 DLL 與通過原生測試的 DLL 的 SHA-256 相同。
- 使用者已按主視窗的左右測試鍵，回覆「符合，左右已互換」；確認來源左聲道由右耳、來源右聲道由左耳播放。

**尚未完成：** DRM、ASIO、獨佔模式、RAW、藍牙免持切換及其他硬體相容性。以上通過結果僅適用於本次 MOMENTUM 4 的 Windows 共用模式測試，不代表所有播放路徑或裝置均已驗證。

正式程式的首次啟用流程包含 Windows 中的處理計數檢查；只有核心實際處理音訊後才會回報設定完成。啟用後仍需按左右測試鍵聽測實際方向。WHQL、DRM 與跨硬體相容性未認證。

紀錄：`work/native-tests.txt`、`work/managed-tests-first-enable-fix.txt`、`work/package-verification-installed.txt`、`work/installed-audio-verification.txt`、`work/system-restore-verification.json`、`work/restored-audio-verification.txt`、`work/diagnostic-installed-final.txt`、`work/preview-installed.png`。`work/` 是開發資料，不是執行相依。

MIT 預覽 EXE SHA-256：`21D939653B2CE642667055D27D471C3820A701B6376F3933B5D29B36CC581898`。
原生 DLL SHA-256：`F431CC7BB0AC4479C6AACB4B957A064AB073F33D15E54AEE3E7C29097F3629F4`。

2.0.1 歷史預覽包：`dist/ChannelFlip-2.0.1-preview.1-windows-x64.zip`，SHA-256 見同名 `.sha256.txt`。
2.0.0 初版的乾淨建置紀錄保留於 `work/clean-managed-tests.txt`、`work/clean-native-tests.txt`、`work/clean-package-check.txt`、`work/clean-release-check.txt`。
