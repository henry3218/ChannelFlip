# 首次啟用的實際變更

獨立版程式內建自己的音訊核心。首次啟用須確認下列系統變更，並以測試音確認裝置上的實際效果；已完成的驗證範圍見原始碼倉庫中的 VALIDATION.md。

目標裝置是介面中選取的耳機或喇叭。裝置音效只接入該端點；下述音訊宿主設定則影響整個系統。

## 需要同意的項目

此自製音訊核心沒有 Microsoft WHQL 簽章。要載入它，會將下列 Windows 測試設定設為 `1`：

`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio\DisableProtectedAudioDG`

程式會備份變更前的值。這項設定會停用受保護音訊宿主的簽章限制，作用範圍是整個系統，某些要求受保護音訊路徑的 DRM 播放可能受影響。它會持續存在，直到透過進階設定的「移除所有裝置的設定」還原；只關閉程式或關閉互換不會還原這項設定。

這項需求來自 [Microsoft APO 文件中的受保護環境與測試設定](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects#disable-use-of-an-embedded-manifest)，不是另一套軟體的安裝要求。此版使用自製核心，沒有呼叫、封裝或更名使用 Equalizer APO 的音訊 DLL。

## 程式會做的設定

1. 將內嵌 DLL 展開至 `%ProgramFiles%\ChannelFlip\2.0\ChannelFlipApo.dll`，登記本程式自己的 COM / APO 類別。
2. 將所選耳機的原音效設定存入 `HKLM\SOFTWARE\ChannelFlip\Devices\{裝置 GUID}` 的 Journal，保留原音效核心並接入左右互換。
3. 建立 `%ProgramData%\ChannelFlip\State\{裝置 GUID}.bin`，讓介面即時切換聲道並讀取處理計數。
4. 套用上面的音訊宿主設定，重新啟動音訊服務。電腦聲音會短暫中斷。
5. 播放兩聲短音，確認 Windows 中的核心有處理聲音；失敗時回報並嘗試還原。

## 還原

在進階設定按「移除所有裝置的設定」並完成 Windows 管理員授權。程式會還原本程式保留的音效設定、移除自己的類別登記、將音訊宿主設定恢復為安裝前狀態，再重新啟動音訊服務。已被外部程式改成不同內容的登錄值會保留。已展開的 DLL 和狀態檔可能留在上述資料夾，但不再登記於音訊流程中。

「關閉所有已設定裝置的互換」只恢復左右方向，仍保留核心與上述宿主設定。

移除前會列出所有已接入裝置（包含離線裝置），並說明整台電腦音訊會短暫中斷。執行前及管理員操作開始時會重新核對範圍；清單改變時停止並要求重新檢視。
