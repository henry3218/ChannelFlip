using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;

[assembly: AssemblyTitle("左右聲道互換")]
[assembly: AssemblyDescription("獨立音訊核心 / Channel Flip")]
[assembly: AssemblyVersion("2.1.1.0")]
[assembly: AssemblyFileVersion("2.1.1.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.6.2")]

namespace ChannelFlip
{
    public static class Program
    {
        public static string DataDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChannelFlip"); } }
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
                AppContext.SetSwitch("Switch.UseLegacyAccessibilityFeatures", false);
                AppContext.SetSwitch("Switch.UseLegacyAccessibilityFeatures.2", false);
                AppContext.SetSwitch("Switch.UseLegacyAccessibilityFeatures.3", false);
                if (args.Length > 0)
                {
                    if (args[0] == "--diagnose" && args.Length == 2) { File.WriteAllText(args[1], Diagnose(), new UTF8Encoding(true)); return 0; }
                    if (args[0] == "--licenses" && args.Length == 2)
                    {
                        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.Notices"))
                        using (var reader = new StreamReader(resource))
                        using (var license = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.License"))
                        using (var licenseReader = new StreamReader(license))
                            File.WriteAllText(args[1], licenseReader.ReadToEnd() + Environment.NewLine + Environment.NewLine + reader.ReadToEnd(), new UTF8Encoding(true));
                        return 0;
                    }
                    if (args[0] == "--render-preview" && args.Length >= 2 && args.Length <= 8)
                    {
                        int width = args.Length > 3 ? Int32.Parse(args[3]) : 660;
                        int height = args.Length > 4 ? Int32.Parse(args[4]) : 720;
                        int dpi = args.Length > 5 ? Int32.Parse(args[5]) : 96;
                        int textPercent = args.Length > 7 ? Int32.Parse(args[7]) : 100;
                        if (width < 320 || width > 2000 || height < 300 || height > 2000 || dpi < 96 || dpi > 288) return 64;
                        new Application(); new MainWindow(Scenarios.Create(args.Length > 2 ? args[2] : "waiting"), true)
                            .RenderPreview(args[1], width, height, dpi, args.Length > 6 && args[6] == "contrast", textPercent / 100.0); return 0;
                    }
                    if (args[0] == "--simulate" && args.Length == 2)
                    { var app = new Application(); app.Run(new MainWindow(Scenarios.Create(args[1]), true).View); return 0; }
                    if (args[0] == "--attach" && (args.Length == 2 || args.Length == 3 && args[2] == "--allow-audio-host-change"))
                    { Engine.AttachAsAdmin(args[1], args.Length == 3); return 0; }
                    if (args[0] == "--remove" && args.Length == 1) { Engine.RemoveAsAdmin(); return 0; }
                    if (args[0] == "--remove" && args.Length == 3 && args[1] == "--scope") { Engine.RemoveAsAdmin(args[2]); return 0; }
                    if (args[0] == "--restart-audio" && args.Length == 1) { Engine.RestartAudioService(); return 0; }
                    if (args[0] == "--off" && args.Length == 1) { Engine.DisableAll(); return 0; }
                    return 64;
                }
                bool created;
                using (var mutex = new Mutex(true, @"Local\ChannelFlip.MainWindow.v2", out created))
                {
                    if (!created)
                    {
                        foreach (Process other in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
                        {
                            if (other.Id != Process.GetCurrentProcess().Id && other.MainWindowHandle != IntPtr.Zero)
                            { ShowWindow(other.MainWindowHandle, 9); SetForegroundWindow(other.MainWindowHandle); }
                            other.Dispose();
                        }
                        return 0;
                    }
                    var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                    application.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                    {
                        Log(e.Exception); MessageBox.Show(e.Exception.Message, "左右聲道互換", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true;
                    };
                    application.Run(new MainWindow(false).View);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log(ex);
                if (args.Length > 0 && new[] { "--attach", "--remove", "--restart-audio" }.Contains(args[0]))
                {
                    try { Directory.CreateDirectory(Engine.SharedDirectory); File.WriteAllText(Path.Combine(Engine.SharedDirectory, "setup-error.txt"), ex.ToString(), new UTF8Encoding(true)); } catch { }
                }
                if (args.Length == 0) MessageBox.Show(ex.Message, "無法啟動左右聲道互換", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
        public static string Diagnose()
        {
            var text = new StringBuilder();
            text.AppendLine("Channel Flip " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + " standalone diagnostic (read-only)");
            text.AppendLine("UTC: " + DateTime.UtcNow.ToString("o"));
            text.AppendLine("64-bit process: " + Environment.Is64BitProcess);
            using (var core = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.Native.dll"))
                text.AppendLine("Embedded native core: " + (core == null ? "MISSING" : core.Length + " bytes"));
            text.AppendLine("Audio host setting consent required: " + Engine.NeedsAudioHostPermission());
            foreach (var device in AudioDevices.Enumerate())
            {
                var state = Engine.Read(device.Guid);
                text.AppendLine(device.DisplayName + " | " + device.Id);
                text.AppendLine("  Channels=" + device.Channels + " Attached=" + state.Attached + " Enabled=" + state.Enabled + " EnhancementsDisabled=" + device.EnhancementsDisabled);
                text.AppendLine("  Loads=" + state.Loads + " Frames=" + state.Frames + " Swapped=" + state.SwappedFrames + " HostPID=" + state.HostProcess + " Error=0x" + state.Error.ToString("X8"));
                if (device.FormatError != null) text.AppendLine("  Format error: " + device.FormatError);
            }
            return text.ToString();
        }
        public static void Log(Exception error)
        {
            try { Directory.CreateDirectory(DataDirectory); File.AppendAllText(Path.Combine(DataDirectory, "error.log"), DateTime.UtcNow.ToString("o") + " " + error + Environment.NewLine); } catch { }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
    }
}
