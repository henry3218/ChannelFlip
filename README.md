# 左右聲道互換 / Channel Flip 2

**繁體中文** | [English](README.en.md)

[![Windows build and tests](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml/badge.svg)](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml)

Windows 10 / 11 x64 桌面程式，內建自行實作的 C++ 音訊核心，交換所選輸出裝置的左、右聲道。採用 [MIT 授權](LICENSE)。**不需要安裝 Equalizer APO、虛擬音效卡、Python 或 Node.js。**

[下載 2.2.0 預覽版](https://github.com/henry3218/ChannelFlip/releases/tag/v2.2.0-preview.1) · [回報問題](https://github.com/henry3218/ChannelFlip/issues) · [驗證範圍](VALIDATION.md)

下載 Release 附件中的 `ChannelFlip-2.2.0-preview.1-windows-x64.zip` 並完整解壓縮；GitHub 自動提供的 `Source code` 壓縮檔是原始碼，需要自行編譯。

開啟預覽包內的 `ChannelFlip.exe`；從原始碼編譯時，輸出位於 `app/ChannelFlip.exe`。EXE 內含介面、圖示、音訊 DLL 與授權聲明；執行時可單獨複製 EXE，使用 Windows 的 .NET Framework 4.6.2 以上版本。散布時仍須附上 MIT 與第三方授權聲明。

## 日常操作與狀態

介面支援 **繁體中文與 English**，可從右上角語言選單即時切換，並記住下次開啟的選擇。首次啟動時，中文 Windows 顯示語言使用繁體中文，其他顯示語言使用英文。切換語言會保留裝置、互換設定及這次的聽感確認；裝置名稱仍使用 Windows 提供的原文。Windows／驅動訊息與已擷取的技術例外內容可能保留原始語言。兩種翻譯都內嵌於 EXE，下載包也附上中英文說明。

1. 選取輸出裝置，首次按「設定此裝置」，閱讀影響範圍後選擇「設定並開啟互換」。
2. 設定完成後使用「左右互換」開關；主卡分別顯示設定值與運作狀態。
3. 分別按左右測試鍵，確認耳機方向；兩邊測試完成後可記錄自己的聽感確認。

「等待音訊／尚待測試」表示這次設定尚無處理或測試證據。「目前沒有新音訊」表示曾有處理或測試紀錄，目前已閒置；上次聽感確認會保留，不必因暫停播放而重測。「已偵測到互換處理」來自近期計數增量，不能代替實際耳機聽測。核心錯誤、離線、無法讀取及音效強化停用會有專用提示。改變裝置或開關、斷線或音訊宿主重啟後，先前的方向驗證會失效。聽感紀錄僅保留於這次開啟程式期間。

預設「固定裝置」；斷線時保留相同端點，不改選其他裝置。「跟隨系統預設」會更新視窗中的操作目標，各裝置的設定獨立保存，新裝置仍須先設定。關閉視窗期間，已設定裝置的開關保留；下次開啟再解析多媒體預設裝置。

「進階設定」集中提供全裝置關閉、重新啟動電腦音訊服務、移除所有裝置的設定。移除清單包含離線裝置，確認後還會重新核對影響範圍。重新啟動與移除會使整台電腦的音訊短暫中斷；單純關閉互換會保留核心與音訊宿主設定。

## 首次啟用

程式可直接開啟。全系統聲音必須接入 Windows 的音訊處理流程，首次啟用仍會由本程式完成一次系統設定，並要求管理員授權。之後切換左右方向不需提升權限，關閉視窗仍會保留效果。

**此自製核心尚未取得 Microsoft WHQL 簽章。首次啟用需要使用 Windows 的 `DisableProtectedAudioDG=1` 測試設定，會停用受保護音訊宿主的簽章限制，可能影響要求受保護路徑的 DRM 播放。程式會先說明並取得同意。** 開源不會改變這項 Windows 限制。詳細變更與還原方式見 [首次啟用說明](FIRST_RUN.md)。

啟用流程會備份原設定、接入核心、重新啟動音訊服務，然後播放兩聲短音並檢查核心實際處理的音訊計數。若未偵測到核心運作，會回報錯誤並嘗試還原。完成後仍應透過左右測試鍵確認實體耳機方向。

## 功能與範圍

- 固定裝置／跟隨系統預設、每個裝置各自開關、左右測試、進階設定中的全域操作。
- 滑塊開關、可見鍵盤焦點、高對比系統配色、隨螢幕 DPI 與 Windows 文字大小調整的 WPF 介面。大文字會換行，必要時捲動；Tab 可到達操作鍵。
- 直接交換浮點音訊的 L/R 取樣，其他聲道不變；不錄製或重播聲音。
- 若所選音效位置原本有核心，透過 COM 保留該核心，先執行原處理，再交換左右。原核心無法載入時會明確報錯。
- 適用於所選裝置經過 Windows 共用模式音效的聲音。ASIO、WASAPI 獨佔模式、RAW、音訊直通或停用音效強化的路徑可能繞過核心。
- 單聲道無法產生左右方向差異；多聲道只互換前方 L/R。藍牙免持模式可能使用另一個端點。
- 尚不支援沒有 FxProperties、或使用多重 MFX 登記的裝置。驅動程式更新可能重設接入設定。

本版為 2.2.0 預覽版本，未經 WHQL 與跨硬體認證。雙語介面與流程的驗證、MOMENTUM 4 核心聽測，以及尚待實測的範圍分別記錄於 [驗證紀錄](VALIDATION.md)。Windows 10 尚未實測。

## 編譯與驗證

```powershell
.\tools\verify-translations.ps1
.\tools\bootstrap.ps1
.\build.ps1 -Test
.\tests\native-tests.ps1
.\tools\verify-ui.ps1
.\tools\verify-package.ps1
.\tools\package-release.ps1
```

使用 Windows x64 的 64 位元 PowerShell。介面使用 Windows 內建 .NET Framework x64 C# 編譯器；C++ 核心使用官方 Zig 0.16.0。`bootstrap.ps1` 依 `dependencies.lock.json` 下載固定版本工具與 SDK 測試標頭，核對 SHA-256 後才使用。工具、標頭和快取放在忽略的 `work/`，不需加入 Git。Microsoft SDK 標頭保留原授權，不隨專案或發布包重新散布。

`build.ps1 -Zig <path>` 和 `tests/native-tests.ps1 -Zig <path>` 可指定其他相同版本工具位置。建置工具只用於編譯，不是執行相依。最後一個命令會在 `dist/` 建立本機預覽 ZIP 與雜湊檔。

GitHub Actions 會執行編譯、離線測試與預覽包產生；每次提交的實際結果可查看 [建置紀錄](https://github.com/henry3218/ChannelFlip/actions/workflows/build.yml)。完整流程見 [發布說明](docs/RELEASING.md)，貢獻者請參閱 [CONTRIBUTING.md](CONTRIBUTING.md)。

若要發布不依靠 `DisableProtectedAudioDG=1` 的版本，還需要受保護音訊環境接受的簽章、相應套件與實測；目前尚未完成。流程見 [正式簽章規劃](docs/SIGNING.md)。

原生測試透過 Microsoft 官方 SDK 的 COM 介面載入正式 DLL；涵蓋同時交換、開關、靜音、邊界、格式、原核心串接、資源釋放。設定測試用目前測試程序內的 HKLM 覆寫，資料實際存入隔離的 HKCU 測試區，不接觸系統音訊設定。

手動實機驗證可執行 `tools/test-live-audio.ps1 -EndpointId '<完整播放端點 ID>' -VerifyInstalled`。這會對已接入的裝置播放短音，暫時關閉及開啟互換，檢查 Windows 音訊宿主的處理計數，最後恢復原開關狀態；不會自行安裝核心。完整端點 ID 可由下列診斷命令取得。這項測試不在 CI 執行，實體左右耳方向仍須人工聽測。

```powershell
.\app\ChannelFlip.exe --diagnose C:\absolute\path\diagnostic.txt
.\app\ChannelFlip.exe --render-preview C:\absolute\path\preview.png
.\app\ChannelFlip.exe --render-preview C:\absolute\path\error.png core-error 584 600 144 contrast
.\app\ChannelFlip.exe --render-preview C:\absolute\path\idle.png confirmed-idle 584 600 96 normal 225
.\app\ChannelFlip.exe --simulate offline
.\app\ChannelFlip.exe --language en --simulate offline
.\app\ChannelFlip.exe --licenses C:\absolute\path\notices.txt
```

其他命令入口：`--off` 關閉所有互換；`--remove` 和 `--restart-audio` 需管理員權限。首次接入入口 `--attach {endpoint-guid} --allow-audio-host-change` 僅供已明確同意系統變更的情況使用。

`--render-preview` 與 `--simulate` 使用與正式介面相同的狀態／操作邏輯，注入完全隔離的模擬後端；不接觸真實音訊、登錄或使用者偏好。預覽的參數為輸出路徑、情境、寬／高 DIP、DPI、主題 `normal`／`contrast`、文字百分比（100–225）。可用情境列於 `src/UiTheme.cs`；`verify-ui.ps1` 產生中英文共 94 組狀態／尺寸／DPI／主題／大文字畫面。離線渲染不等於 Windows 設定與實體螢幕驗證。

命令開頭可加 `--language en` 或 `--language zh-TW`，只覆寫該程序的語言，不會自行保存偏好。介面中的語言選單才會保存選擇。隔離預覽預設繁體中文，不讀寫真實偏好。翻譯集中於 `src/Translations.xml`，貢獻方式見 [CONTRIBUTING.md](CONTRIBUTING.md)。

每螢幕 DPI 設定依據 [Microsoft WPF 範例](https://github.com/microsoft/WPF-Samples/blob/main/PerMonitorDPI/readme.md)。

文字大小讀取 Windows 的使用者設定，方式參照 [.NET ScaleHelper](https://source.dot.net/System.Windows.Forms.Primitives/System/Windows/Forms/Internals/ScaleHelper.cs.html)；由本程式自行更新 WPF 字級資源並換行，不增加執行相依。較新 .NET Framework 上已啟用 [WPF 無障礙改善](https://github.com/microsoft/dotnet/blob/main/Documentation/compatibility/wpf-accessibility-improvements-48.md)，避免隱藏控制項仍出現在無障礙樹。

## 實作與來源

`native/ChannelFlipApo.cpp` 為自製 DSP / COM 核心；`src/Engine.cs` 負責設定備份、還原與共享狀態；`src/Audio.cs` 處理端點列舉與 WASAPI 測試音。

依照 [Microsoft APO 實作文件](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects) 與 [APO 架構](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/audio-processing-object-architecture) 實作。Windows 介面宣告對照 [Microsoft Win32 metadata SDK](https://github.com/microsoft/win32metadata/tree/main/generation/WinSDK/RecompiledIdlHeaders/um)。Windows 原有音效屬於作業系統或裝置驅動的一部分。

本專案自行實作的原始碼採用 [MIT](LICENSE)，允許商用、修改與散布，並要求保留授權聲明。工具鏈與第三方元件的授權見 `THIRD_PARTY_NOTICES.txt`，也內嵌於 EXE。這些元件保留各自原有授權。

`work/` 與 `dist/` 均不加入 Git；原始碼倉庫不包含安裝程式、工具鏈、測試裝置資料或私人紀錄。
