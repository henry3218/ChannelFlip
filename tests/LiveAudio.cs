using System;
using System.Linq;
using System.Threading;
using ChannelFlip;

public static class LiveAudio
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
    private static void PlayBoth(string id)
    {
        TestTone.Play(id, 0, CancellationToken.None);
        Console.WriteLine("PASS: Left source rendered through shared WASAPI.");
        Thread.Sleep(350);
        TestTone.Play(id, 1, CancellationToken.None);
        Console.WriteLine("PASS: Right source rendered through shared WASAPI.");
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1 || args.Length > 2 || args.Length == 2 && args[1] != "--verify-installed") return 64;
            var device = AudioDevices.Enumerate().Single(x => x.Id == args[0]);
            Console.WriteLine("Testing output: " + device.DisplayName);
            if (args.Length == 1) PlayBoth(device.Id);
            else
            {
                var original = Engine.Read(device.Guid);
                Check(original.Attached, "Core is already attached; this test never installs it.");
                try
                {
                    Engine.SetEnabled(device.Guid, false);
                    Thread.Sleep(250);
                    var before = Engine.Read(device.Guid);
                    PlayBoth(device.Id);
                    var after = Engine.Read(device.Guid);
                    Check(after.Frames > before.Frames && after.SwappedFrames == before.SwappedFrames && after.Error == 0, "Disabled core processes audio without swapping samples.");
                    Engine.SetEnabled(device.Guid, true);
                    Thread.Sleep(250);
                    before = Engine.Read(device.Guid);
                    PlayBoth(device.Id);
                    after = Engine.Read(device.Guid);
                    Check(after.SwappedFrames > before.SwappedFrames && after.Error == 0, "Enabled core swaps real Windows audio.");
                    using (var host = System.Diagnostics.Process.GetProcessById(after.HostProcess))
                        Check(host.ProcessName.Equals("audiodg", StringComparison.OrdinalIgnoreCase), "Processing runs inside the Windows audio host.");
                    Check(new WindowsAudioBackend().HostAlive(after.HostProcess), "UI backend recognizes AudioDG without requesting a privileged process handle.");
                    Console.WriteLine("Frames=" + after.Frames + " Swapped=" + after.SwappedFrames + " HostPID=" + after.HostProcess);
                }
                finally { Engine.SetEnabled(device.Guid, original.Enabled); }
            }
            Console.WriteLine("Physical left/right direction still requires listening through the headphones.");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
