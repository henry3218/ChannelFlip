using System;
using System.Windows;
using System.Windows.Media;

namespace ChannelFlip
{
    public static class UiTheme
    {
        public static void Apply(Window window, bool? contrast)
        {
            string[] keys = { "Page", "Card", "Control", "Text", "Muted", "Line", "Accent", "AccentText", "Warning", "Focus", "Disabled" };
            bool high = contrast ?? SystemParameters.HighContrast;
            Brush[] brushes;
            if (high)
            {
                // Explicit contrast previews use a deterministic palette; the live app uses the user's system colors.
                bool fixture = contrast == true && !SystemParameters.HighContrast;
                Brush background = fixture ? Brushes.Black : SystemColors.WindowBrush;
                Brush foreground = fixture ? Brushes.White : SystemColors.WindowTextBrush;
                Brush highlight = fixture ? Brushes.Yellow : SystemColors.HighlightBrush;
                Brush selectedText = fixture ? Brushes.Black : SystemColors.HighlightTextBrush;
                brushes = new[] { background, background, background, foreground, foreground, foreground, highlight, selectedText, foreground, foreground, fixture ? Brushes.Silver : SystemColors.GrayTextBrush };
            }
            else
            {
                string[] colors = { "#101719", "#1A2729", "#243438", "#EFF7F4", "#B3C5BF", "#536F68", "#B5EAD4", "#123528", "#FFD0AC", "#F1FFF9", "#91A49F" };
                brushes = Array.ConvertAll(colors, s => (Brush)new BrushConverter().ConvertFromString(s));
            }
            for (int i = 0; i < keys.Length; i++) window.Resources[keys[i]] = brushes[i];
            window.Resources["SuccessText"] = high ? brushes[3] : brushes[6];
        }
    }
    public static class Scenarios
    {
        public static readonly string[] Names = { "setup", "waiting", "processing", "confirmed-idle", "off", "offline", "empty", "core-error", "read-error", "enhancements-off", "unsupported", "test-failed", "busy", "long-name" };
        public static AudioSession Create(string name)
        {
            if (Array.IndexOf(Names, name) < 0) throw new ArgumentException("Unknown scenario: " + name);
            const string guid = "{11111111-1111-1111-1111-111111111111}";
            var device = new OutputDevice { Id = "{0.0.0.00000000}." + guid, Guid = guid, Name = L10n.T("耳機 (MOMENTUM 4)"), Channels = 2, IsDefault = true };
            var backend = new SimulationBackend(); backend.Devices.Add(device);
            var state = new EngineStatus { Attached = name != "setup" && name != "empty", Enabled = name != "off", HostProcess = 123, Loads = 1, Frames = 100000, SwappedFrames = 90000 };
            backend.States[guid] = state;
            if (state.Attached) backend.Setup = new SetupScope { Signature = "simulation", CoreRegistered = true, HostSettings = true,
                Devices = new[] { new SetupDevice { Id = guid, Name = device.Name }, new SetupDevice { Id = "{22222222-2222-2222-2222-222222222222}", Name = L10n.T("USB 喇叭") } } };
            var preference = new DevicePreference { Id = device.Id, Name = device.Name };
            if (name == "offline") backend.Devices.Clear();
            if (name == "empty") { backend.Devices.Clear(); preference.Id = null; }
            if (name == "read-error") backend.ReadFailure = L10n.T("模擬：無法讀取音訊核心狀態檔。");
            if (name == "core-error") state.Error = unchecked((int)0x80004005);
            if (name == "enhancements-off") device.EnhancementsDisabled = true;
            if (name == "unsupported") device.Channels = 1;
            if (name == "long-name") device.Name = L10n.T("客廳電腦的無線降噪耳機 — 很長的裝置名稱，用來確認完整文字仍然可以閱讀 (MOMENTUM 4 Wireless)");
            var session = new AudioSession(backend, preference); session.Refresh();
            if (name == "processing") { state.Frames += 48000; state.SwappedFrames += 48000; session.Refresh(); }
            if (name == "confirmed-idle")
            {
                DateTime tested = DateTime.UtcNow.AddSeconds(-9);
                session.Clock = delegate { return tested; };
                state.Frames += 48000; state.SwappedFrames += 48000; session.Refresh();
                session.Observation.RecordTest(0, true, null, tested); session.Observation.RecordTest(1, true, null, tested);
                session.Observation.ConfirmHearing(); session.Clock = delegate { return DateTime.UtcNow; };
            }
            if (name == "test-failed") session.Observation.RecordTest(0, false, L10n.M("測試期間未偵測到預期處理，請重試或檢查 Windows 音效設定。"), DateTime.UtcNow);
            if (name == "busy") { session.Busy = true; session.SetMessage(L10n.M("正在設定此裝置，電腦音訊會短暫中斷…")); }
            return session;
        }
    }
}
