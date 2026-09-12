# 保留 Windows 音訊保護的正式簽章路線

**繁體中文** | [English](SIGNING.en.md)

查核日期：2026-09-12。這份文件記錄後續發布方向；本專案目前沒有取得微軟簽章或完成硬體認證。

## 簽章與 MIT 授權的差別

MIT 決定別人如何使用、修改及散布原始碼。Windows 數位簽章驗證特定二進位檔案的身分與完整性，受保護音訊環境還要求符合額外信任條件。取得或公開原始碼，不會自動取得 Windows 的信任。

若 APO DLL、其載入依賴及部署方式符合受保護環境規範，並取得該環境接受的有效簽章，就能以保留音訊保護為目標，不再依靠 `DisableProtectedAudioDG=1`。必須用最後簽署的套件在保護開啟時實測；不能只看 EXE 顯示「數位簽章有效」。微軟說明受保護環境只載入適當簽署且受信任的元件。[Protected Media Path](https://learn.microsoft.com/en-us/windows/win32/medfound/protected-media-path)

## 一般的正式認證流程

1. 確認 Hardware Developer Program 的申請資格，準備組織資料、Microsoft Entra ID 全域管理員、可代表組織簽約的聯絡人與 EV 程式碼簽署憑證。EV 用於開發者帳戶身分驗證，不等於 APO 已獲得受保護環境信任。[註冊要求](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/hardware-program-register)
2. 將音訊核心與安裝方式整理成可提交的 APO / 驅動套件，依目標 Windows 版本與支援的裝置建立 INF、部署和還原方式。Windows 11 的 APO 套件類別為 `AudioProcessingObject`。[APO 部署](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/deploying-audio-processing-objects)
3. 依適用功能執行 HLK / WHCP 測試，在套件中申請需要的受保護環境簽章屬性。微軟的 `SignatureAttributes` 文件明列音訊 DLL 的 PETrust 與 DRM 範例；不能假定一般 Authenticode 或自我簽署即可取代。[簽章屬性](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-signatureattributes-section)
4. 向 Partner Center 提交測試與套件，取得對應簽章後，以實際回傳的二進位檔驗證、打包、再發布。[官方提交流程](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/)

微軟也提供適用於測試情境的 Attestation 簽署；它不代表通過 Windows 相容性認證，不能直接視為本專案已滿足公開正式版的全部條件。[簽署方式比較](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings)

## 本專案還需要做的工作

- 審查並調整目前的自製核心及裝置接入方式，符合目標認證的 API、部署與受保護環境規範。微軟明確指出，即使有 WHQL 簽章，不符合載入規範的 APO 仍可能無法載入。[APO 實作規範](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects#disable-use-of-an-embedded-manifest)
- 加入正式簽署套件的建置與發布流程；簽署金鑰與憑證不加入 Git。
- 將已認證版本的首次設定流程改為保留原音訊保護值，再測試聲道交換、既有音效串接、DRM 播放、更新與完整還原。
- 簽章只適用於被簽署的確切檔案。別人修改或重新編譯 MIT 原始碼後，新的 DLL 仍需自己的合適簽章，或使用已明確說明限制的開發測試方式。

即使簽章通過，首次安裝 / 登記音訊核心仍可能需要管理員授權與重新載入音訊服務。簽章可移除繞過音訊保護的需求，不會把系統音訊處理元件變成完全不需安裝設定的普通播放器。
