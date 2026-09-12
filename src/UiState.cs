using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ChannelFlip
{
    public sealed class DevicePreference
    {
        public bool FollowDefault;
        public string Id, Name, Language;
        public static DevicePreference Load(string directory)
        {
            var result = new DevicePreference();
            string path = Path.Combine(directory, "preferences.xml");
            if (File.Exists(path))
            {
                var xml = XElement.Load(path);
                result.FollowDefault = (string)xml.Attribute("mode") == "default";
                result.Id = (string)xml.Element("id"); result.Name = (string)xml.Element("name");
                string language = (string)xml.Attribute("language");
                result.Language = L10n.IsSupported(language) ? language : null;
            }
            else
            {
                string legacy = Path.Combine(directory, "device.txt");
                if (File.Exists(legacy)) result.Id = File.ReadAllText(legacy).Trim();
            }
            return result;
        }
        public void Save(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "preferences.xml"), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                new XElement("preferences", new XAttribute("mode", FollowDefault ? "default" : "fixed"),
                    L10n.IsSupported(Language) ? new XAttribute("language", Language) : null,
                    new XElement("id", Id ?? ""), new XElement("name", Name ?? "")).Save(temp);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public OutputDevice Resolve(IList<OutputDevice> active)
        {
            if (FollowDefault) return active.FirstOrDefault(d => d.IsDefault);
            if (String.IsNullOrEmpty(Id))
            {
                var initial = active.FirstOrDefault(d => d.IsDefault);
                if (initial != null) { Id = initial.Id; Name = initial.Name; }
                return initial;
            }
            var found = active.FirstOrDefault(d => String.Equals(d.Id, Id, StringComparison.OrdinalIgnoreCase));
            if (found != null) { Name = found.Name; return found; }
            string guid = null;
            int start = Id.LastIndexOf('{'); Guid parsed;
            if (start >= 0 && Guid.TryParse(Id.Substring(start), out parsed)) guid = parsed.ToString("B");
            return new OutputDevice { Id = Id, Guid = guid, Name = String.IsNullOrEmpty(Name) ? L10n.T("先前選取的裝置") : Name, Offline = true };
        }
    }

    public sealed class ProcessingObservation
    {
        private string id;
        private EngineStatus previous;
        public DateTime LastProcessedUtc { get; private set; }
        public DateTime TestUtc { get; private set; }
        private LocalizedText testFailure;
        public string TestFailure { get { return LocalizedText.Render(testFailure); } }
        public int TestedChannels { get; private set; }
        public bool HearingConfirmed { get; private set; }
        public void Reset()
        {
            previous = null; id = null; LastProcessedUtc = DateTime.MinValue;
            TestUtc = DateTime.MinValue; testFailure = null; TestedChannels = 0; HearingConfirmed = false;
        }
        public void Sample(string endpoint, EngineStatus current, bool available, bool hostAlive, DateTime now)
        {
            if (!available || current == null) { Reset(); return; }
            bool same = previous != null && id == endpoint && previous.Enabled == current.Enabled && previous.Attached == current.Attached &&
                previous.HostProcess == current.HostProcess &&
                current.Frames >= previous.Frames && current.SwappedFrames >= previous.SwappedFrames;
            if (!same) Reset();
            if (current.Error != 0 || !hostAlive) { LastProcessedUtc = DateTime.MinValue; TestedChannels = 0; HearingConfirmed = false; }
            else if (same && current.Attached && current.HostProcess > 0 && current.Frames > previous.Frames &&
                (!current.Enabled || current.SwappedFrames > previous.SwappedFrames)) LastProcessedUtc = now;
            // Copy values: test backends and MMF readers must not mutate the previous sample.
            previous = new EngineStatus { Attached = current.Attached, Enabled = current.Enabled, HostProcess = current.HostProcess,
                Loads = current.Loads, Frames = current.Frames, SwappedFrames = current.SwappedFrames };
            id = endpoint;
        }
        public bool Recent(DateTime now) { return now >= LastProcessedUtc && (now - LastProcessedUtc).TotalSeconds <= 8; }
        public void RecordTest(int channel, bool passed, LocalizedText failure, DateTime now)
        {
            TestUtc = now; testFailure = passed ? null : failure; HearingConfirmed = false;
            if (passed) TestedChannels |= 1 << channel; else TestedChannels = 0;
        }
        public void ConfirmHearing() { if (TestedChannels == 3 && TestFailure == null) HearingConfirmed = true; }
    }

    public sealed class UiState
    {
        public string Code, Title, Detail, Setting, Action;
        public bool Warning, Success, CanSetup, CanToggle, CanTest, Swap;
        public static UiState Evaluate(OutputDevice device, EngineStatus engine, string error, ProcessingObservation observation, DateTime now)
        {
            var result = new UiState { Setting = error != null ? L10n.T("未知") : !engine.Attached ? L10n.T("未設定") : engine.Enabled ? L10n.T("開啟") : L10n.T("關閉"),
                Swap = error == null && engine.Attached && engine.Enabled, Action = engine.Attached ? (engine.Enabled ? L10n.T("關閉互換") : L10n.T("開啟互換")) : L10n.T("設定此裝置") };
            if (device == null && error != null) return result.With("read-error", L10n.T("無法取得裝置狀態"), L10n.T("請重新整理。錯誤詳情可在下方檢視。"), true);
            if (device == null) return result.With("empty", L10n.T("尚無輸出裝置"), L10n.T("請連接耳機或喇叭，再按重新整理。"), false);
            if (device.Offline) return result.With("offline", L10n.T("裝置已離線"), L10n.T("已保留這個裝置。重新連接，或手動選擇其他裝置。"), true);
            if (error != null) return result.With("read-error", L10n.T("無法取得狀態"), L10n.T("請重新整理。錯誤詳情可在下方檢視。"), true);
            if (device.Channels < 2 || device.FormatError != null)
                return result.With("unsupported", L10n.T("目前無法使用左右互換"), device.FormatError != null ? L10n.T("無法取得裝置音訊格式，請檢查連線或 Windows 音效。") : L10n.T("此裝置目前少於兩個聲道，請切換至立體聲輸出。"), true);
            result.CanTest = true;
            result.CanSetup = !engine.Attached;
            result.CanToggle = engine.Attached && (engine.Enabled || (engine.Error == 0 && !device.EnhancementsDisabled));
            if (!engine.Attached) return result.With("setup", L10n.T("尚未設定"), L10n.T("首次設定後，即可直接開關互換。也能先測試原始方向。"), false);
            if (engine.Error != 0) return result.With("core-error", L10n.T("核心異常"), L10n.T("可先關閉互換，或到進階設定重新啟動電腦音訊服務。"), true);
            if (device.EnhancementsDisabled) return result.With("enhancements-off", L10n.T("音效強化已關閉"), L10n.T("請在 Windows 音效中開啟此裝置的音效強化，再測試方向。"), true);
            if (observation.TestFailure != null) return result.With("test-failed", L10n.T("本次測試未通過"), observation.TestFailure, true);
            if (!engine.Enabled) return result.With("off", L10n.T("互換已關閉"), L10n.T("設定方向為原本左右；音訊核心與裝置設定仍保留。"), false);
            if (observation.Recent(now))
            {
                result.Success = true;
                return result.With("processing", L10n.T("已偵測到互換處理"), observation.HearingConfirmed ?
                    L10n.T("近期音訊通過互換核心；你已確認的左右方向結果仍保留。") :
                    L10n.T("近期音訊通過互換核心。請用左右測試確認實際聽到的方向。"), false);
            }
            if (observation.LastProcessedUtc != DateTime.MinValue || observation.TestedChannels != 0 || observation.HearingConfirmed)
                return result.With("idle", L10n.T("目前沒有新音訊"), observation.HearingConfirmed ?
                    L10n.T("互換設定保持開啟；上次左右方向確認仍有效，無須因暫停播放而重新測試。") :
                    L10n.T("互換設定保持開啟，先前的處理與測試紀錄仍保留；恢復播放後會更新運作狀態。"), false);
            return result.With("waiting", L10n.T("等待音訊／尚待測試"), L10n.T("互換設定已開啟；播放音訊或測試音後才能確認核心處理。"), false);
        }
        private UiState With(string code, string title, string detail, bool warning)
        { Code = code; Title = title; Detail = detail; Warning = warning; return this; }
    }
}
