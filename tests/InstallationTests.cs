using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ChannelFlip;

public static class InstallationTests
{
    public static void Run(Action<bool, string> check, string directory)
    {
        var app = new Version(2, 4, 0, 0);
        byte[] core = { 1, 2, 3 };
        check(!InstallationStatus.Evaluate(false, core, null, false, null, app).NeedsUpdate, "An unconfigured computer has no installation to update");
        var current = InstallationStatus.Evaluate(true, core, new byte[] { 1, 2, 3 }, true, "2.4.0", app);
        check(!current.NeedsUpdate && !current.Newer, "A matching core and Apps entry need no update; a three-part version equals the assembly version");
        var legacy = InstallationStatus.Evaluate(true, core, new byte[] { 1, 2, 3 }, false, null, app);
        check(legacy.NeedsUpdate && !legacy.InterruptsAudio && legacy.InstalledVersion == null, "An earlier install without an Apps entry is completed without interrupting audio");
        var outdated = InstallationStatus.Evaluate(true, core, new byte[] { 9 }, true, "2.3.0", app);
        check(outdated.NeedsUpdate && outdated.InterruptsAudio, "A different installed core needs an update that restarts audio");
        var missing = InstallationStatus.Evaluate(true, core, null, true, "2.4.0", app);
        check(missing.NeedsUpdate && missing.CoreMissing && missing.InterruptsAudio, "A deleted core file is reinstalled");
        var newer = InstallationStatus.Evaluate(true, core, new byte[] { 9 }, true, "2.5.0", app);
        check(newer.Newer && !newer.NeedsUpdate, "An older app never offers to downgrade a newer installed core");
        var unreadable = InstallationStatus.Evaluate(true, core, core.ToArray(), true, "not a version", app);
        check(!unreadable.Newer && unreadable.InstalledVersion == null && !unreadable.NeedsUpdate, "An unreadable recorded version is treated as unrecorded");

        string folder = Path.Combine(directory, "installation-files"), nested = Path.Combine(folder, "empty");
        Directory.CreateDirectory(nested);
        string free = Path.Combine(folder, "free.tmp"), held = Path.Combine(folder, "held.tmp"), absent = Path.Combine(folder, "absent.tmp");
        File.WriteAllText(free, "x"); File.WriteAllText(held, "x");
        using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var pending = InstallationFiles.Delete(new[] { free, held, absent, free }, 2, TimeSpan.FromMilliseconds(10));
            check(!File.Exists(free) && pending.Count == 1 && pending[0] == held, "Deletion removes free files, ignores missing ones and reports files still in use");
            check(InstallationFiles.DeleteEmptyDirectories(new[] { nested, folder }).Single() == folder && !Directory.Exists(nested),
                "Empty folders are deleted child first; a folder with a file in use is kept for deletion at restart");
        }
        check(InstallationFiles.Delete(new[] { held }, 1, TimeSpan.Zero).Count == 0 && !File.Exists(held), "A released file is deleted");
        check(InstallationFiles.DeleteEmptyDirectories(new[] { folder, Path.Combine(directory, "missing-folder") }).Count == 0 && !Directory.Exists(folder),
            "An emptied folder is deleted and a missing folder is ignored");

        var s = Scenarios.Create("reset");
        check(s.Presentation.Code == "reset" && s.Presentation.Warning && s.Presentation.CanSetup && s.Presentation.Action == "重新設定此裝置" && s.Scope.HasChanges,
            "A setup journal without an attachment says Windows removed it and offers to set it up again");
        s.SetupAsync(s.Selected.Id, true).GetAwaiter().GetResult();
        check(s.State.Attached && s.Presentation.Code != "reset" && !s.MessageError, "Setting up again reattaches a device that Windows reset");

        s = Scenarios.Create("update"); var b = (SimulationBackend)s.Backend;
        check(s.Presentation.Code == "update" && s.Presentation.CanUpdate && s.Presentation.Warning && s.Presentation.UpdateAction == "更新音訊核心",
            "A different installed core offers an update that restarts audio");
        check(s.State.Attached && s.Presentation.Swap && s.Presentation.CanToggle, "The update notice keeps the configured switch usable");
        s.UpdateInstallationAsync().GetAwaiter().GetResult();
        check(b.Calls.Single() == "update:" + s.Selected.Guid && !s.Presentation.CanUpdate && s.Presentation.Code != "update" && !s.MessageError,
            "Updating passes the connected device for the tone check and clears the notice");
        s = Scenarios.Create("update"); b = (SimulationBackend)s.Backend; b.Devices.Clear(); s.Refresh();
        check(s.Presentation.Code == "offline" && !s.Presentation.CanUpdate, "A disconnected device shows its connection state before the update notice");
        s.UpdateInstallationAsync().GetAwaiter().GetResult();
        check(b.Calls.Single() == "update:" && !s.MessageError, "An update without a connected device skips the tone check instead of targeting another device");

        s = Scenarios.Create("waiting"); b = (SimulationBackend)s.Backend;
        b.Install = new InstallationStatus { Configured = true, Registered = false }; s.Refresh();
        check(s.Presentation.Code == "update" && !s.Presentation.Warning && s.Presentation.UpdateAction == "加入應用程式清單",
            "An install without an Apps entry is offered without an audio warning");
        b.Install = new InstallationStatus { Configured = true, CoreDiffers = true, Registered = true, Newer = true }; s.Refresh();
        check(!s.Presentation.CanUpdate && s.Presentation.Code == "waiting", "A newer installed core is left alone");
        s = Scenarios.Create("update"); s.GlobalAsync("remove", s.Scope.Signature).GetAwaiter().GetResult();
        check(!s.Installation.NeedsUpdate && !s.Scope.HasChanges, "Removing every device setting leaves nothing to update");

        if (Application.Current == null) new Application();
        foreach (var expected in new[] { Tuple.Create("update", true, "更新音訊核心"), Tuple.Create("waiting", false, (string)null) })
        {
            var window = new MainWindow(Scenarios.Create(expected.Item1), true);
            try
            {
                var button = (Button)window.View.FindName("UpdateButton");
                check((button.Visibility == Visibility.Visible) == expected.Item2 && (!expected.Item2 || ((string)button.Content == expected.Item3 && button.IsEnabled)),
                    expected.Item1 + ": update button appears only when an update is available");
            }
            finally { window.View.Close(); }
        }
        var resetWindow = new MainWindow(Scenarios.Create("reset"), true);
        try { check((string)((Button)resetWindow.View.FindName("SetupButton")).Content == "重新設定此裝置", "reset: the setup button says it sets the device up again"); }
        finally { resetWindow.View.Close(); }
    }
}
