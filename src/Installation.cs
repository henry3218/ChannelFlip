using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ChannelFlip
{
    public sealed class InstallationStatus
    {
        public bool Configured, CoreMissing, CoreDiffers, Registered, Newer;
        public string InstalledVersion;
        // A newer installation is never downgraded; an unconfigured computer has nothing to update.
        public bool NeedsUpdate { get { return Configured && !Newer && (CoreMissing || CoreDiffers || !Registered); } }
        public bool InterruptsAudio { get { return CoreMissing || CoreDiffers; } }
        public static InstallationStatus Evaluate(bool configured, byte[] embedded, byte[] installed, bool registered, string recordedVersion, Version appVersion)
        {
            Version recorded;
            bool known = Version.TryParse(recordedVersion, out recorded);
            return new InstallationStatus { Configured = configured, CoreMissing = installed == null,
                CoreDiffers = installed != null && !installed.SequenceEqual(embedded), Registered = registered, InstalledVersion = known ? recordedVersion : null,
                Newer = known && Release(recorded) > Release(appVersion) };
        }
        // Assembly versions carry a fourth field; recorded versions have three.
        private static Version Release(Version value) { return new Version(value.Major, value.Minor, Math.Max(0, value.Build)); }
    }

    public static class InstallationFiles
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool MoveFileEx(string existing, string replacement, int flags);
        // Windows audio can hold a just-unregistered core briefly after its service restarts.
        public static List<string> Delete(IEnumerable<string> files, int attempts, TimeSpan delay)
        {
            var remaining = files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (int attempt = 0; remaining.Count != 0 && attempt < attempts; attempt++)
            {
                if (attempt != 0) Thread.Sleep(delay);
                remaining = remaining.Where(path =>
                {
                    try { File.Delete(path); return File.Exists(path); }
                    catch (IOException) { return true; }
                    catch (UnauthorizedAccessException) { return true; }
                }).ToList();
            }
            return remaining;
        }
        public static List<string> DeleteEmptyDirectories(IEnumerable<string> directories)
        {
            var remaining = new List<string>();
            foreach (string directory in directories.Where(Directory.Exists))
            {
                try { Directory.Delete(directory, false); }
                catch (IOException) { remaining.Add(directory); }
                catch (UnauthorizedAccessException) { remaining.Add(directory); }
            }
            return remaining;
        }
        // A running EXE or loaded DLL can be renamed but not deleted. Renaming it before scheduling the deletion
        // frees its name, so a reinstall before the restart keeps its new file.
        public static string MoveAside(string path)
        {
            string target = path + "." + Guid.NewGuid().ToString("N") + ".delete";
            try { File.Move(path, target); return target; }
            catch (IOException) { return path; }
            catch (UnauthorizedAccessException) { return path; }
        }
        public static void DeleteAtRestart(string path)
        {
            const int DelayUntilReboot = 4;
            if (!MoveFileEx(path, null, DelayUntilReboot)) throw new Win32Exception();
        }
    }
}
