# Channel Flip

**繁體中文** | [English](README.en.md)

在 Windows 上交換耳機或喇叭的左右聲道。內建音訊元件，不需安裝 Equalizer APO。

**[下載 2.2.0 預覽版（Windows x64 ZIP）](https://github.com/henry3218/ChannelFlip/releases/download/v2.2.0-preview.1/ChannelFlip-2.2.0-preview.1-windows-x64.zip)** · [版本說明](https://github.com/henry3218/ChannelFlip/releases/tag/v2.2.0-preview.1) · [回報問題](https://github.com/henry3218/ChannelFlip/issues)

> [!IMPORTANT]
> 此預覽版尚未取得微軟音訊簽章。首次設定需管理員權限，會短暫中斷電腦音訊，並停用全系統受保護音訊宿主的簽章限制，可能影響 DRM 內容播放。[查看變更與還原方式](FIRST_RUN.md)。

## 開始使用

1. **開啟程式**：解壓縮下載檔，執行 `ChannelFlip.exe`。
2. **設定裝置**：選擇耳機或喇叭，按「設定此裝置」，依畫面說明完成設定。
3. **確認方向**：開啟「左右互換」，按左右測試鍵，確認左聲道從右耳、右聲道從左耳聽到。

關閉視窗後，互換仍有效。要恢復原方向，關閉「左右互換」；要還原系統變更，選擇「進階設定 → 移除所有裝置的設定」。

右上角可切換 **繁體中文／English**，程式會記住選擇。

## 支援範圍

- **系統需求**：Windows 10 / 11 x64，.NET Framework 4.6.2 以上。
- **實測環境**：Windows 11 + MOMENTUM 4。Windows 10 與其他裝置尚未實測。
- **音訊限制**：需經過 Windows 共用模式音效。ASIO、獨佔模式、RAW、音訊直通或關閉音效強化時，互換可能無效。

[查看相容性與測試紀錄](VALIDATION.md)

## 開發與貢獻

在 Windows x64 的 64 位元 PowerShell 執行：

```powershell
.\tools\bootstrap.ps1
.\build.ps1 -Test
```

輸出位於 `app/ChannelFlip.exe`。[建置與貢獻指南](CONTRIBUTING.md) · [發布流程](docs/RELEASING.md)

## 授權

[MIT](LICENSE)。散布時請保留 `LICENSE` 與 [第三方授權聲明](THIRD_PARTY_NOTICES.txt)。
