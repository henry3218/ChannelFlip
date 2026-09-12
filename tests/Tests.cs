using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using ChannelFlip;

public static class Tests
{
    private static int passed, failed;
    private static void Check(bool ok, string name) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (ok) passed++; else failed++; }
    private static void Throws(Action action, string name) { try { action(); Check(false, name); } catch { Check(true, name); } }
    private static bool Equal(object a, object b)
    {
        if (a is string[] && b is string[]) return ((string[])a).SequenceEqual((string[])b);
        if (a is byte[] && b is byte[]) return ((byte[])a).SequenceEqual((byte[])b);
        return Object.Equals(a, b);
    }
    [DllImport("advapi32.dll")] private static extern int RegOverridePredefKey(IntPtr root, IntPtr key);
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "--reliability-child") return RegistryReliabilityTests.Child(args);
        string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
        try
        {
            Check(Engine.GuidText("FB240FA0-1D7B-479B-9442-00A7412EAA11") == "{fb240fa0-1d7b-479b-9442-00a7412eaa11}", "Endpoint GUID normalized");
            Throws(delegate { Engine.GuidText("{fb240fa0-1d7b-479b-9442-00a7412eaa11}\\Other"); }, "Endpoint path injection rejected");
            string state = Path.Combine(directory, "state.bin");
            Engine.CreateStateFile(state); byte[] bytes = File.ReadAllBytes(state);
            Check(bytes.Length == 4096 && BitConverter.ToInt32(bytes, 0) == Engine.StateMagic && BitConverter.ToInt32(bytes, 4) == 1, "State layout agrees with native core contract");
            Check(bytes.Skip(8).All(b => b == 0), "Fresh state defaults to unchanged audio and zero counters");
            using (var file = new FileStream(state, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            using (var map = MemoryMappedFile.CreateFromFile(file, null, 4096, MemoryMappedFileAccess.ReadWrite, null, HandleInheritability.None, true))
            using (var view = map.CreateViewAccessor())
            {
                view.Write(24, 987654321L); view.Write(32, 1234567L);
                Engine.SetStateFile(state, true); Check(view.ReadInt32(8) == 1, "Independent mapping observes enable immediately");
                Engine.SetStateFile(state, false); Check(view.ReadInt32(8) == 0, "Independent mapping observes disable immediately");
                Check(view.ReadInt64(24) == 987654321L && view.ReadInt64(32) == 1234567L, "Toggling preserves native processing counters");
                view.Write(0, 0); Throws(delegate { Engine.SetStateFile(state, true); }, "Invalid state header fails closed");
                Check(view.ReadInt32(8) == 0, "Invalid state cannot enable processing");
            }
            File.WriteAllBytes(state, new byte[6]); Throws(delegate { Engine.SetStateFile(state, true); }, "Truncated state rejected");
            Check(new FileInfo(state).Length == 6, "Rejected state is not silently resized");
            Engine.CreateStateFile(state);
            using (var held = new FileStream(state, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Throws(delegate { Engine.SetStateFile(state, true); }, "Exclusive file owner blocks update");

            // Registry transactions run against a private HKCU tree. The override is
            // process-local; real endpoint registrations and system settings are untouched.
            RegistryEdit.Deserialize("<changes />"); // Initialize the framework XML hash seed before isolating HKLM.
            string privatePath = @"Software\ChannelFlip.ManagedTests." + Guid.NewGuid().ToString("N");
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                using (var root = user.CreateSubKey(privatePath))
                {
                    if (RegOverridePredefKey(new IntPtr(unchecked((int)0x80000002)), root.Handle.DangerousGetHandle()) != 0) throw new IOException("Cannot isolate registry test process");
                    try
                    {
                        using (var key = root.CreateSubKey("JournalTest"))
                        {
                            object[] originals = { "耳機 <before> & ", new[] { "第一", "", "third" }, new byte[] { 0, 1, 255 }, -23, 12345678901234L, "%USERPROFILE%\\literal" };
                            object[] replacements = { "changed", new[] { "new" }, new byte[] { 9, 8 }, 42, 12L, "%WINDIR%" };
                            RegistryValueKind[] kinds = { RegistryValueKind.String, RegistryValueKind.MultiString, RegistryValueKind.Binary, RegistryValueKind.DWord, RegistryValueKind.QWord, RegistryValueKind.ExpandString };
                            for (int i = 0; i < kinds.Length; i++)
                            {
                                string name = "value" + i; key.SetValue(name, originals[i], kinds[i]);
                                var edit = RegistryEdit.Capture("JournalTest", name, replacements[i], kinds[i]);
                                edit = RegistryEdit.Deserialize(RegistryEdit.Serialize(new[] { edit })).Single();
                                Check(edit.Existed && edit.BeforeKind == kinds[i] && Equal(edit.Before, originals[i]), "Journal round-trips original " + kinds[i]);
                                edit.Apply(); Check(Equal(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), replacements[i]), "Apply " + kinds[i]);
                                Check(edit.MatchesAfter() && !edit.MatchesBefore(), "Recovery audit identifies owned " + kinds[i]);
                                edit.RestoreIfOwned(); Check(key.GetValueKind(name) == kinds[i] && Equal(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), originals[i]), "Exact restore " + kinds[i]);
                                Check(edit.MatchesBefore() && !edit.MatchesAfter(), "Recovery audit confirms restored " + kinds[i]);
                                edit.RestoreIfOwned(); Check(Equal(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), originals[i]), "Repeated restore preserves " + kinds[i]);
                            }
                            var absent = RegistryEdit.Capture("JournalTest", "NewValue", 1, RegistryValueKind.DWord);
                            Check(!absent.Existed, "Missing value recorded as missing");absent.Apply();absent.RestoreIfOwned();
                            Check(!key.GetValueNames().Contains("NewValue"), "Previously absent value removed on restore");
                            key.SetValue("Conflict", "before");var conflict = RegistryEdit.Capture("JournalTest", "Conflict", "ours", RegistryValueKind.String);
                            conflict.Apply();key.SetValue("Conflict", "changed externally");conflict.RestoreIfOwned();
                            Check((string)key.GetValue("Conflict") == "changed externally", "External registry changes are preserved");
                            conflict.Apply();key.SetValue("Conflict", "ours", RegistryValueKind.ExpandString);conflict.RestoreIfOwned();
                            Check(key.GetValueKind("Conflict") == RegistryValueKind.ExpandString, "External type-only changes are preserved");
                            var unapplied = RegistryEdit.Capture("JournalTest", "NeverApplied", 2, RegistryValueKind.DWord);unapplied.RestoreIfOwned();
                            Check(!key.GetValueNames().Contains("NeverApplied"), "Partial transaction rollback skips unapplied change");
                        }
                        using (var key = root.CreateSubKey("ReadOnlyRecovery"))
                        {
                            key.SetValue("Value", "original");
                            var unapplied = RegistryEdit.Capture("ReadOnlyRecovery", "Value", "ours", RegistryValueKind.String);
                            var originalSecurity = key.GetAccessControl(AccessControlSections.Access);
                            var restrictedSecurity = new RegistrySecurity();
                            restrictedSecurity.SetSecurityDescriptorBinaryForm(originalSecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                            restrictedSecurity.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User, RegistryRights.SetValue | RegistryRights.CreateSubKey, AccessControlType.Deny));
                            try
                            {
                                key.SetAccessControl(restrictedSecurity);
                                Throws(delegate { using (var denied = root.OpenSubKey("ReadOnlyRecovery", true)) { } }, "Protected test key denies writable access");
                                unapplied.RestoreIfOwned();
                                Check((string)key.GetValue("Value") == "original", "Unapplied rollback succeeds with read-only registry access");
                            }
                            finally
                            {
                                var restoredSecurity = new RegistrySecurity();
                                restoredSecurity.SetSecurityDescriptorBinaryForm(originalSecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                                key.SetAccessControl(restoredSecurity);
                            }
                        }
                        var emptyScope = Engine.ReadScope();
                        Check(!emptyScope.HasChanges && emptyScope.Devices.Length == 0, "Empty scope has no global changes");
                        string sampleId = "{11111111-1111-1111-1111-111111111111}";
                        using (var scopeKey = root.CreateSubKey(Engine.RegistryPath + @"\Devices\" + sampleId))
                        {
                            scopeKey.SetValue("DeviceName", "已離線的耳機"); scopeKey.SetValue("Journal", "<changes />");
                        }
                        var deviceScope = Engine.ReadScope();
                        Check(deviceScope.Devices.Single().Name == "已離線的耳機" && deviceScope.HasChanges, "Recovery scope includes journaled devices without enumerating hardware");
                        Check(Engine.CheckScope(deviceScope.Signature).Signature == deviceScope.Signature, "Unchanged reviewed scope is accepted");
                        using (var scopeKey = root.CreateSubKey(Engine.RegistryPath)) scopeKey.SetValue("ProtectionJournal", "<changes />");
                        Throws(delegate { Engine.CheckScope(deviceScope.Signature); }, "Changed system scope is rejected before mutation");
                        root.DeleteSubKeyTree(Engine.RegistryPath + @"\Devices");
                        Check(Engine.ReadScope().HasChanges && Engine.ReadScope().Devices.Length == 0, "Orphaned global journal still exposes recovery");
                    }
                    finally { RegOverridePredefKey(new IntPtr(unchecked((int)0x80000002)), IntPtr.Zero); }
                }
                user.DeleteSubKeyTree(privatePath);
            }
            foreach (int channels in new[] { 2, 6, 8 })
            foreach (int target in new[] { 0, 1 })
            {
                byte[] sound = TestTone.Generate(48000, channels, 32, true, target, 0, 4800, 4800);
                double energy = 0, peak = 0;bool othersSilent = true;
                for (int frame = 0; frame < 4800; frame++) for (int channel = 0; channel < channels; channel++)
                {
                    double sample = BitConverter.ToSingle(sound, (frame * channels + channel) * 4);
                    if (channel == target) { energy += sample * sample; peak = Math.Max(peak, Math.Abs(sample)); } else if (sample != 0) othersSilent = false;
                }
                Check(energy > 1 && peak <= 0.080001 && othersSilent, channels + " channels: test tone only on source " + target + " at modest level");
                Check(BitConverter.ToSingle(sound, target * 4) == 0 && BitConverter.ToSingle(sound, ((4800 - 1) * channels + target) * 4) == 0, "Tone fades to zero at both endpoints");
            }
            foreach (int bits in new[] { 16, 24, 32 })
            {
                var full = TestTone.Generate(48000, 2, bits, false, 1, 0, 4800, 4800);
                var a = TestTone.Generate(48000, 2, bits, false, 1, 0, 1111, 4800);
                var b = TestTone.Generate(48000, 2, bits, false, 1, 1111, 3689, 4800);
                Check(a.Concat(b).SequenceEqual(full), "PCM " + bits + " continuous across render buffers");
                int count = bits / 8;
                Check(Enumerable.Range(0, 4800).All(f => Enumerable.Range(0, count).All(i => full[f * 2 * count + i] == 0)), "PCM " + bits + " other channel silent");
            }
            Throws(delegate { TestTone.Generate(48000, 1, 32, true, 0, 0, 100, 100); }, "Mono cannot claim left/right test");
            UiTests.Run(Check, directory);
            ReliabilityTests.Run(Check, directory);
            RegistryReliabilityTests.Run(Check);
            LocalizationTests.Run(Check, directory);
        }
        catch (Exception ex) { Console.WriteLine(ex); failed++; }
        Console.WriteLine("RESULT: " + passed + " passed; " + failed + " failed");return failed == 0 ? 0 : 1;
    }
}
