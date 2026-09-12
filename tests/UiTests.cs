using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using ChannelFlip;

public static class UiTests
{
    public static void Run(Action<bool, string> check, string directory)
    {
        check(!new WindowsAudioBackend().HostAlive(0) && !new WindowsAudioBackend().HostAlive(System.Diagnostics.Process.GetCurrentProcess().Id), "Missing and non-audio process IDs are rejected");
        var s = Scenarios.Create("waiting"); var b = (SimulationBackend)s.Backend;
        string id = s.Selected.Id, guid = s.Selected.Guid; var original = s.Selected;
        var other = new OutputDevice { Id = "{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}", Guid = "{22222222-2222-2222-2222-222222222222}", Name = original.Name, Channels = 2, IsDefault = true };
        original.IsDefault = false; b.Devices.Clear(); b.Devices.Add(other); s.Refresh();
        check(s.Selected.Id == id && s.Selected.Offline && s.Presentation.Code == "offline", "Fixed endpoint stays offline despite another default with the same name");
        check(!s.Presentation.CanTest && !s.Presentation.CanToggle && s.Scope.HasChanges, "Offline endpoint blocks local commands while global recovery remains available");
        b.Devices.Add(original); s.Refresh(); check(s.Selected.Id == id && !s.Selected.Offline, "Exact endpoint reconnect restores selection");
        s.SetFollow(true); check(s.Selected.Id == other.Id && s.Presentation.Code == "setup", "Following default changes UI target without attaching a new device");
        check(b.Calls.Count == 0, "Selecting or refreshing never changes audio settings");
        other.IsDefault = false; s.Refresh(); check(s.Selected == null, "Missing default never falls back to the first active device");
        s.SetFollow(false); s.Select(original);

        string prefs = Path.Combine(directory, "preferences"); Directory.CreateDirectory(prefs);
        File.WriteAllText(Path.Combine(prefs, "device.txt"), id);
        var migrated = DevicePreference.Load(prefs); check(migrated.Id == id && !migrated.FollowDefault, "Legacy preference migrates to fixed endpoint");
        migrated.Name = "耳機 <原本> & 名稱"; migrated.FollowDefault = true; migrated.Save(prefs);
        var restored = DevicePreference.Load(prefs); check(restored.Id == id && restored.Name == migrated.Name && restored.FollowDefault, "Selection mode and Unicode name survive restart");
        restored.FollowDefault = false; restored.Save(prefs);
        check(!DevicePreference.Load(prefs).FollowDefault && !Directory.GetFiles(prefs, "*.tmp").Any(), "Atomic preference replacement leaves no pending file");

        s = Scenarios.Create("core-error");
        check(s.Presentation.Code == "core-error" && s.Presentation.Warning && !s.Presentation.Success && s.Presentation.Setting == "開啟", "Core failure overrides green success without falsifying the configured switch");
        s.ToggleAsync(false).GetAwaiter().GetResult(); check(!s.State.Enabled, "A readable enabled switch can be turned off during a core failure");
        s.ToggleAsync(true).GetAwaiter().GetResult(); check(!s.State.Enabled && s.MessageError, "Unresolved core failure blocks enabling");
        s = Scenarios.Create("empty"); check(!s.Presentation.CanSetup && !s.Presentation.CanTest && s.Presentation.Code == "empty", "No-device state never offers an unusable setup or test action");
        s = Scenarios.Create("read-error"); check(s.Presentation.Setting == "未知" && !s.Presentation.CanTest && !s.Presentation.Success, "Unreadable state is unknown, not off or healthy");
        s = Scenarios.Create("unsupported"); check(!s.Presentation.CanToggle && !s.Presentation.CanTest, "Mono cannot claim stereo controls");
        s = Scenarios.Create("enhancements-off"); check(s.Presentation.Code == "enhancements-off" && !s.Presentation.Success, "Disabled enhancements override apparent activation");

        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend; guid = s.Selected.Guid;
        DateTime now = DateTime.UtcNow; s.Clock = delegate { return now; };
        check(s.State.SwappedFrames > 0 && s.Presentation.Code == "waiting", "Historical nonzero counters do not prove current processing");
        b.States[guid].Frames += 100; b.States[guid].SwappedFrames += 100; s.Refresh();
        check(s.Presentation.Code == "processing", "Fresh counter growth proves recent processing");
        now = now.AddSeconds(9); s.Refresh(); check(s.Presentation.Code == "idle" && !s.Presentation.Warning && !s.Presentation.Success, "Idle audio expires recent success without discarding past processing evidence");
        b.States[guid].Frames += 100; b.States[guid].SwappedFrames += 100; s.Refresh();
        b.States[guid].HostProcess++; s.Refresh(); check(!s.Presentation.Success, "Audio host restart invalidates prior evidence");
        b.States[guid].Frames = 0; b.States[guid].SwappedFrames = 0; s.Refresh(); check(!s.Presentation.Success, "Counter reset invalidates prior evidence");
        b.Alive = false; b.States[guid].Frames += 100; b.States[guid].SwappedFrames += 100; s.Refresh();
        check(!s.Presentation.Success, "A stale or dead host PID cannot establish success");

        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend; b.NoProcessing = true;
        s.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult(); s.Refresh();
        check(s.Presentation.Code == "test-failed" && s.Observation.TestedChannels == 0, "Test with no expected processing remains failed after refresh");
        b.NoProcessing = false; s.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult();
        b.States[s.Selected.Guid].Loads++;
        s.TestAsync(1, CancellationToken.None).GetAwaiter().GetResult();
        check(s.Observation.TestedChannels == 3 && !s.Observation.HearingConfirmed, "Two processed tones enable hearing confirmation without automatically claiming it");
        s.Observation.ConfirmHearing(); check(s.Observation.HearingConfirmed, "Hearing result is recorded only by explicit confirmation");
        DateTime confirmedTest = s.Observation.TestUtc;
        s.Clock = delegate { return confirmedTest.AddSeconds(9); }; s.Refresh();
        check(s.Presentation.Code == "idle" && s.Presentation.Title == "目前沒有新音訊" && !s.Presentation.Success &&
            s.Observation.HearingConfirmed && s.Observation.TestedChannels == 3 && s.Observation.TestUtc == confirmedTest,
            "Nine seconds of silence preserves both completed tests and explicit hearing confirmation without claiming live processing");
        b.States[s.Selected.Guid].Frames += 100; b.States[s.Selected.Guid].SwappedFrames += 100; s.Refresh();
        check(s.Presentation.Code == "processing" && s.Observation.HearingConfirmed && !s.Presentation.Detail.Contains("請用左右測試"),
            "Resuming audio retains confirmation and does not ask for another test");
        s.ToggleAsync(false).GetAwaiter().GetResult(); check(s.Observation.TestedChannels == 0 && !s.Observation.HearingConfirmed, "Changing the switch invalidates prior direction confirmation");
        s.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult(); check(s.Observation.TestFailure == null && s.Message.Contains("左耳"), "Off-mode tone verifies pass-through and uses the correct ear instruction");
        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend; b.CancelPlay = true;
        s.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult(); check(s.Message.Contains("取消") && s.Observation.TestedChannels == 0, "Cancelled playback is never reported as a passed test");

        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend; s.SetFollow(true); id = s.Selected.Id; original = s.Selected;
        var gate = new TaskCompletionSource<int>(); b.DuringPlay = delegate { return gate.Task; };
        Task operation = s.TestAsync(0, CancellationToken.None);
        original.IsDefault = false; other.IsDefault = true; b.Devices.Add(other); s.Refresh();
        check(s.Busy && s.Selected.Id == id, "Pending operation keeps its original endpoint when the default changes");
        gate.SetResult(0); operation.GetAwaiter().GetResult();
        check(b.Calls.Single().StartsWith("play:" + id) && s.Selected.Id == other.Id && !s.Presentation.Success && s.Observation.TestedChannels == 0, "Completed result is not transferred to the next default endpoint");
        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend; id = s.Selected.Id; b.Devices.Clear();
        s.ToggleAsync(false).GetAwaiter().GetResult(); check(b.Calls.Count == 0 && s.MessageError, "Disconnect before execution stops the command without retargeting");
        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend;
        s.GlobalAsync("remove", "outdated-scope").GetAwaiter().GetResult();
        check(b.Calls.Count == 0 && s.MessageError && s.Scope.HasChanges, "Changed confirmation scope blocks the global command");
        s.GlobalAsync("off", s.Scope.Signature).GetAwaiter().GetResult();
        check(!s.State.Enabled && s.Scope.HasChanges, "Global off retains installation and journals");
        s.GlobalAsync("remove", s.Scope.Signature).GetAwaiter().GetResult(); check(!s.Scope.HasChanges && !s.State.Attached, "Simulated remove re-reads the resulting scope");

        if (Application.Current == null) new Application();
        var switchWindow = new MainWindow(Scenarios.Create("off"), true);
        try
        {
            var toggle = (ToggleButton)switchWindow.View.FindName("SwapSwitch"); toggle.ApplyTemplate();
            var peer = new ToggleButtonAutomationPeer(toggle);
            var provider = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
            var thumb = (FrameworkElement)toggle.Template.FindName("Thumb", toggle);
            check(peer.GetName() == "左右互換" && provider.ToggleState == ToggleState.Off && thumb.HorizontalAlignment == HorizontalAlignment.Left,
                "Switch exposes a stable accessible function name, off state, and left thumb");
            provider.Toggle();
            check(provider.ToggleState == ToggleState.On && thumb.HorizontalAlignment == HorizontalAlignment.Right && switchWindow.Session.State.Enabled,
                "Automation toggle updates the actual device setting, accessible state, and thumb together");
            provider.Toggle();
            check(provider.ToggleState == ToggleState.Off && !switchWindow.Session.State.Enabled,
                "Automation toggle returns the device to off");
            UiTypography.Apply(switchWindow.View.Resources, 2.25);
            var evidence = (TextBlock)switchWindow.View.FindName("TestEvidence");
            check(Math.Abs(evidence.FontSize - 29.25) < .01 && Math.Abs(toggle.FontSize - 31.5) < .01,
                "System text scaling reaches explanatory text and switch labels");
            UiTypography.Apply(switchWindow.View.Resources, 1);
            check(evidence.FontSize == 13 && toggle.FontSize == 14, "Text size can return to normal without cumulative scaling");
        }
        finally { switchWindow.View.Close(); }
        s = Scenarios.Create("confirmed-idle"); b = (SimulationBackend)s.Backend;
        b.States[s.Selected.Guid].HostProcess++; s.Refresh();
        check(s.Presentation.Code == "waiting" && !s.Observation.HearingConfirmed && s.Observation.TestedChannels == 0,
            "A new host invalidates even a previously confirmed idle result");
        s = Scenarios.Create("confirmed-idle"); b = (SimulationBackend)s.Backend; b.Devices.Clear(); s.Refresh();
        check(s.Presentation.Code == "offline" && !s.Observation.HearingConfirmed, "Physical endpoint loss invalidates previous hearing evidence");
        s = Scenarios.Create("processing"); b = (SimulationBackend)s.Backend; s.Message = "互換設定已開啟，請測試方向。";
        b.Devices.Clear(); s.Refresh();
        check(s.Presentation.Code == "offline" && !s.Presentation.CanTest && s.Message == null,
            "Disconnect removes a stale success instruction instead of asking to use disabled test buttons");
        foreach (string scenario in Scenarios.Names)
        foreach (int width in new[] { 584, 664 })
        {
            var window = new MainWindow(Scenarios.Create(scenario), true);
            try
            {
                var content = (FrameworkElement)window.View.Content;
                content.Measure(new Size(width, 600)); content.Arrange(new Rect(0, 0, width, 600)); content.UpdateLayout();
                var test = (Button)window.View.FindName("TestRight");
                var bounds = test.TransformToAncestor(content).TransformBounds(new Rect(test.RenderSize));
                check(bounds.Bottom <= 600 && bounds.Right <= width && bounds.Top >= 0, scenario + ": primary test visible at " + width + " x 600");
                check(((Button)window.View.FindName("SetupButton")).Visibility != Visibility.Visible || ((System.Windows.Controls.Primitives.ToggleButton)window.View.FindName("SwapSwitch")).Visibility != Visibility.Visible,
                    scenario + ": setup and daily switch never compete");
            }
            finally { window.View.Close(); }
        }
        foreach (string scenario in new[] { "setup", "confirmed-idle", "core-error", "long-name" })
        {
            var window = new MainWindow(Scenarios.Create(scenario), true);
            try
            {
                UiTypography.Apply(window.View.Resources, 2.25);
                var content = (FrameworkElement)window.View.Content;
                content.Measure(new Size(584, 600)); content.Arrange(new Rect(0, 0, 584, 600)); content.UpdateLayout();
                var scroll = (ScrollViewer)window.View.FindName("PageScroll");
                var advanced = (Button)window.View.FindName("Advanced");
                scroll.ScrollToBottom(); content.UpdateLayout();
                var bounds = advanced.TransformToAncestor(content).TransformBounds(new Rect(advanced.RenderSize));
                check(scroll.ScrollableWidth == 0 && bounds.Bottom <= 600 && bounds.Right <= 584 && bounds.Top >= 0,
                    scenario + ": 225 percent text reflows with reachable bottom controls and no horizontal scrolling");
            }
            finally { window.View.Close(); }
        }
    }
}
