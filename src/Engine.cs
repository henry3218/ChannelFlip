using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Win32;

namespace ChannelFlip
{
    public sealed class EngineStatus
    {
        public bool Attached;
        public bool Enabled;
        public int Loads;
        public long Frames;
        public long SwappedFrames;
        public int Channels;
        public int Error;
        public int HostProcess;
    }

    public static class Engine
    {
        public const string Clsid = "{f81b4c35-7458-4c1e-b820-770a914ed436}";
        public const string RegistryPath = @"SOFTWARE\ChannelFlip";
        public const string AudioPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Audio";
        public const string ProtectionValue = "DisableProtectedAudioDG";
        public const string FxPrefix = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},";
        public const string ModesPrefix = "{d3993a3f-99c2-4402-b5ec-a92a0367664b},";
        public const string DisableEnhancements = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5";
        public const int StateMagic = 0x50464c43;
        public static string SharedDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ChannelFlip"); } }
        public static string InstalledDll { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ChannelFlip", "2.0", "ChannelFlipApo.dll"); } }
        private static RegistryKey Machine() { return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64); }
        public static string GuidText(string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid)) throw new ArgumentException(L10n.T("音訊裝置識別碼無效。"));
            return guid.ToString("B").ToLowerInvariant();
        }
        public static string StatePath(string id) { return Path.Combine(SharedDirectory, "State", GuidText(id) + ".bin"); }
        private static string FxPath(string id) { return AudioDevices.RenderRegistry + GuidText(id) + @"\FxProperties"; }

        public static EngineStatus Read(string id)
        {
            id = GuidText(id);
            var status = new EngineStatus();
            using (var machine = Machine())
            using (var fx = machine.OpenSubKey(FxPath(id)))
            {
                if (fx != null) status.Attached = new[] { 2, 6, 7 }.Any(slot => String.Equals(fx.GetValue(FxPrefix + slot) as string, Clsid, StringComparison.OrdinalIgnoreCase));
            }
            string path = StatePath(id);
            if (!File.Exists(path)) return status;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var map = OpenStateMap(file, MemoryMappedFileAccess.Read))
            using (var view = map.CreateViewAccessor(0, 4096, MemoryMappedFileAccess.Read))
            {
                if (view.ReadInt32(0) != StateMagic || view.ReadInt32(4) != 1) throw new InvalidDataException(L10n.T("音訊核心狀態檔無效，請在進階設定移除所有裝置的設定，再重新設定裝置。"));
                status.Enabled = view.ReadInt32(8) != 0;
                status.Channels = view.ReadInt32(16);
                status.Loads = view.ReadInt32(20);
                status.Frames = view.ReadInt64(24);
                status.SwappedFrames = view.ReadInt64(32);
                status.Error = view.ReadInt32(40);
                status.HostProcess = view.ReadInt32(44);
            }
            return status;
        }

        public static void SetEnabled(string id, bool enabled)
        {
            SetStateFile(StatePath(id), enabled);
        }
        public static void SetStateFile(string path, bool enabled)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            using (var map = OpenStateMap(file, MemoryMappedFileAccess.ReadWrite))
            using (var view = map.CreateViewAccessor(0, 4096, MemoryMappedFileAccess.ReadWrite))
            {
                if (view.ReadInt32(0) != StateMagic || view.ReadInt32(4) != 1) throw new InvalidDataException(L10n.T("音訊核心狀態檔無效。"));
                view.Write(8, enabled ? 1 : 0);
                view.Flush();
            }
        }
        public static void CreateStateFile(string path)
        {
            using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            using (var writer = new BinaryWriter(file))
            {
                file.SetLength(4096); writer.Write(StateMagic); writer.Write(1); writer.Write(0); writer.Flush(); file.Flush(true);
            }
        }
        private static MemoryMappedFile OpenStateMap(FileStream file, MemoryMappedFileAccess access)
        {
            if (file.Length < 4096) throw new InvalidDataException(L10n.T("音訊核心狀態檔不完整。"));
            return MemoryMappedFile.CreateFromFile(file, null, 4096, access, null, HandleInheritability.None, true);
        }
        public static string[] KnownDevices()
        {
            using (var machine = Machine())
            using (var devices = machine.OpenSubKey(RegistryPath + @"\Devices")) return devices == null ? new string[0] : devices.GetSubKeyNames();
        }
        public static SetupScope ReadScope()
        {
            var devices = new List<SetupDevice>(); var edits = new List<RegistryEdit>();
            var fingerprint = new StringBuilder(); bool hostSettings, registered;
            using (var machine = Machine())
            {
                foreach (string id in KnownDevices().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                using (var key = machine.OpenSubKey(RegistryPath + @"\Devices\" + id))
                {
                    if (key == null) throw new IOException(L10n.T("裝置設定清單已改變，請重試。"));
                    string journal = key.GetValue("Journal") as string;
                    devices.Add(new SetupDevice { Id = id, Name = key.GetValue("DeviceName") as string ?? id });
                    fingerprint.Append(id).Append('\n').Append(journal).Append('\n');
                    if (journal != null) edits.AddRange(RegistryEdit.Deserialize(journal));
                }
                using (var key = machine.OpenSubKey(RegistryPath))
                {
                    string journal = key == null ? null : key.GetValue("ProtectionJournal") as string;
                    hostSettings = journal != null; fingerprint.Append(journal);
                    if (journal != null) edits.AddRange(RegistryEdit.Deserialize(journal));
                }
                using (var key = machine.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + Clsid)) registered = key != null;
            }
            fingerprint.Append(registered);
            using (var sha = SHA256.Create()) return new SetupScope { Devices = devices.ToArray(), Changes = edits.ToArray(),
                HostSettings = hostSettings, CoreRegistered = registered,
                Signature = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToString()))).Replace("-", "").ToLowerInvariant() };
        }
        public static SetupScope CheckScope(string expected)
        {
            var scope = ReadScope();
            if (expected != null && scope.Signature != expected) throw new InvalidOperationException(L10n.T("影響範圍已改變。請重新開啟進階設定，檢視裝置清單後再執行。"));
            return scope;
        }
        public static void DisableAll() { DisableAll(null); }
        public static void DisableAll(string expected)
        {
            var scope = CheckScope(expected);
            var errors = new List<string>();
            foreach (string id in scope.Devices.Select(d => d.Id))
            {
                try { SetEnabled(id, false); if (Read(id).Enabled) throw new IOException(L10n.T("無法確認互換已關閉。")); }
                catch (Exception ex) { errors.Add(id + ": " + ex.Message); }
            }
            if (errors.Count > 0) throw new IOException(String.Join(Environment.NewLine, errors));
        }
        public static bool NeedsAudioHostPermission()
        {
            using (var machine = Machine())
            using (var audio = machine.OpenSubKey(AudioPath)) return audio == null || Convert.ToInt32(audio.GetValue(ProtectionValue, 0)) != 1;
        }

        public static async Task Attach(string id, bool allowAudioHostChange)
        {
            int code = await RunAdmin("--attach " + GuidText(id) + (allowAudioHostChange ? " --allow-audio-host-change" : ""));
            if (code != 0) throw new IOException("Unexpected setup exit code: " + code);
        }
        public static async Task Remove(string expected)
        {
            CheckScope(expected);
            int code = await RunAdmin("--remove --scope " + expected);
            if (code != 0) throw new IOException("Unexpected setup exit code: " + code);
        }
        public static async Task Restart()
        {
            int code = await RunAdmin("--restart-audio");
            if (code != 0) throw new IOException("Unexpected setup exit code: " + code);
        }
        internal static string SetupResultPath(string operationId)
        { return Path.Combine(SharedDirectory, "SetupResults", Guid.ParseExact(operationId, "N").ToString("N") + ".txt"); }
        private static Exception SetupError(int code, string operationId)
        {
            string log = SetupResultPath(operationId);
            string detail = File.Exists(log) ? File.ReadAllText(log) : L10n.T("請檢查 Windows 管理員授權。");
            return new InvalidOperationException(L10n.T("音訊設定未完成（{0}）：{1}", code, detail));
        }
        private static async Task<int> RunAdmin(string arguments)
        {
            Process child;
            string operationId = Guid.NewGuid().ToString("N");
            try
            {
                child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--language " + L10n.Language + " --operation-id " + operationId + " " + arguments)
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) throw new OperationCanceledException(L10n.T("已取消 Windows 管理員授權，設定未變更。"));
                throw;
            }
            if (child == null) throw new IOException(L10n.T("無法啟動系統設定。"));
            using (child)
            {
                await Task.Run(delegate { child.WaitForExit(); });
                if (child.ExitCode != 0) throw SetupError(child.ExitCode, operationId);
                return child.ExitCode;
            }
        }
        public static bool IsAdministrator()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void AttachAsAdmin(string id, bool allowAudioHostChange)
        { SystemConfiguration.Run(delegate { AttachCore(id, allowAudioHostChange); }); }
        private static void AttachCore(string id, bool allowAudioHostChange)
        {
            if (!IsAdministrator()) throw new UnauthorizedAccessException(L10n.T("需要 Windows 管理員授權。"));
            id = GuidText(id);
            var device = AudioDevices.Enumerate().FirstOrDefault(x => x.Guid == id);
            if (device == null || device.Channels < 2) throw new InvalidOperationException(L10n.T("找不到可用的立體聲輸出裝置。"));
            if (Read(id).Attached) { SetEnabled(id, true); return; }
            if (NeedsAudioHostPermission() && !allowAudioHostChange)
                throw new InvalidOperationException(L10n.T("自製核心未經 Microsoft WHQL 簽署，需先在主視窗同意音訊宿主設定變更。"));

            using (var machine = Machine())
            using (var fx = machine.OpenSubKey(FxPath(id)))
            {
                if (fx == null) throw new NotSupportedException(L10n.T("此裝置未提供系統音效設定，尚不支援直接接入。"));
                if (fx.GetValue(FxPrefix + 14) != null) throw new NotSupportedException(L10n.T("這個裝置使用多重音效鏈，目前版本尚不支援安全接入。"));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(InstalledDll));
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.Native.dll"))
            {
                if (resource == null) throw new InvalidDataException(L10n.T("程式缺少內建音訊核心，請重新編譯。"));
                byte[] bytes = new byte[resource.Length];
                int offset = 0;
                while (offset < bytes.Length) { int n = resource.Read(bytes, offset, bytes.Length - offset); if (n == 0) throw new EndOfStreamException(); offset += n; }
                if (!File.Exists(InstalledDll)) File.WriteAllBytes(InstalledDll, bytes);
                else if (!File.ReadAllBytes(InstalledDll).SequenceEqual(bytes))
                {
                    if (KnownDevices().Length != 0) throw new InvalidOperationException(L10n.T("已存在不同版本的音訊核心。請先在進階設定移除所有裝置的設定，再設定新版。"));
                    string staged = InstalledDll + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { File.WriteAllBytes(staged, bytes); File.Replace(staged, InstalledDll, null); }
                    finally { if (File.Exists(staged)) File.Delete(staged); }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(StatePath(id)));
            using (var machine = Machine())
            using (var saved = machine.OpenSubKey(RegistryPath + @"\Devices\" + id))
                if (saved != null && saved.GetValue("Journal") != null) throw new InvalidOperationException(L10n.T("找到未完成的設定備份。請先在進階設定使用「移除所有裝置的設定」還原。"));
            CreateStateFile(StatePath(id));
            var permissions = File.GetAccessControl(StatePath(id));
            permissions.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Read | FileSystemRights.Write, AccessControlType.Allow));
            permissions.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null), FileSystemRights.Read | FileSystemRights.Write, AccessControlType.Allow));
            File.SetAccessControl(StatePath(id), permissions);

            var changes = new List<RegistryEdit>();
            using (var machine = Machine())
            using (var fx = machine.OpenSubKey(FxPath(id)))
            {
                int slot = fx.GetValue(FxPrefix + 5) == null && fx.GetValue(FxPrefix + 6) == null && fx.GetValue(FxPrefix + 2) != null ? 2 : 6;
                string child = fx.GetValue(FxPrefix + slot) as string ?? "";
                changes.Add(RegistryEdit.Capture(FxPath(id), FxPrefix + slot, Clsid, RegistryValueKind.String));
                if (slot == 6 && fx.GetValue(ModesPrefix + slot) == null)
                    changes.Add(RegistryEdit.Capture(FxPath(id), ModesPrefix + slot, new[] { "{C18E2F7E-933D-4965-B7D1-1EEF228D2AF3}" }, RegistryValueKind.MultiString));
                if (Convert.ToInt32(fx.GetValue(DisableEnhancements, 0)) != 0)
                    changes.Add(RegistryEdit.Capture(FxPath(id), DisableEnhancements, 0, RegistryValueKind.DWord));
                using (var registry = machine.CreateSubKey(RegistryPath + @"\Devices\" + id))
                {
                    // Save recovery information before the first audio-related mutation.
                    if (registry.GetValue("Journal") != null) throw new InvalidOperationException(L10n.T("找到未完成的設定備份。請先在進階設定使用「移除所有裝置的設定」還原。"));
                    registry.SetValue("Journal", RegistryEdit.Serialize(changes));
                    registry.SetValue("StatePath", StatePath(id));
                    registry.SetValue("ChildClsid", child);
                    registry.SetValue("DeviceName", device.Name);
                    registry.Flush();
                }
            }
            try
            {
                RegisterCore();
                using (var machine = Machine())
                using (var registry = machine.CreateSubKey(RegistryPath))
                {
                    if (registry.GetValue("ProtectionJournal") == null)
                        registry.SetValue("ProtectionJournal", RegistryEdit.Serialize(new[] { RegistryEdit.Capture(AudioPath, ProtectionValue, 1, RegistryValueKind.DWord) }));
                    registry.Flush();
                }
                if (NeedsAudioHostPermission()) RegistryEdit.Capture(AudioPath, ProtectionValue, 1, RegistryValueKind.DWord).Apply();
                foreach (var change in changes) change.Apply();
                SetEnabled(id, true);
                RestartAudioService();
                TestTone.Play(device.Id, 0, System.Threading.CancellationToken.None);
                TestTone.Play(device.Id, 1, System.Threading.CancellationToken.None);
                var verified = Read(id);
                if (verified.Error != 0 || verified.SwappedFrames == 0 || verified.HostProcess == 0)
                    throw new InvalidOperationException(L10n.T("Windows 未成功載入或執行音訊核心，已嘗試還原原本設定。核心錯誤：0x") + verified.Error.ToString("X8"));
            }
            catch
            {
                try { RemoveDeviceAsAdmin(id); RestoreGlobalIfUnused(); RestartAudioService(); } catch (Exception rollback) { Program.Log(rollback); }
                throw;
            }
        }

        private static void RegisterCore()
        {
            using (var machine = Machine())
            {
                using (var clsid = machine.CreateSubKey(@"SOFTWARE\Classes\CLSID\" + Clsid + @"\InprocServer32"))
                { clsid.SetValue("", InstalledDll); clsid.SetValue("ThreadingModel", "Both"); }
                using (var apo = machine.CreateSubKey(@"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\" + Clsid))
                {
                    apo.SetValue("FriendlyName", "Channel Flip native stereo swap");
                    apo.SetValue("Copyright", "Channel Flip 2026");
                    foreach (string name in new[] { "MinInputConnections", "MaxInputConnections", "MinOutputConnections", "MaxOutputConnections", "NumAPOInterfaces" }) apo.SetValue(name, 1, RegistryValueKind.DWord);
                    apo.SetValue("MajorVersion", 2, RegistryValueKind.DWord); apo.SetValue("MinorVersion", 0, RegistryValueKind.DWord);
                    apo.SetValue("Flags", 14, RegistryValueKind.DWord); apo.SetValue("MaxInstances", -1, RegistryValueKind.DWord);
                    apo.SetValue("APOInterface0", "{FD7F2B29-24D0-4B5C-B177-592C39F9CA10}");
                }
            }
        }
        private static void RemoveDeviceAsAdmin(string id)
        {
            string journal;
            using (var machine = Machine())
            using (var key = machine.OpenSubKey(RegistryPath + @"\Devices\" + GuidText(id))) journal = key == null ? null : key.GetValue("Journal") as string;
            if (journal == null) return;
            try { if (File.Exists(StatePath(id))) SetEnabled(id, false); }
            catch (Exception ex) { Program.Log(ex); } // Registry recovery still works with a damaged control file.
            foreach (var edit in RegistryEdit.Deserialize(journal).AsEnumerable().Reverse()) edit.RestoreIfOwned();
            using (var machine = Machine()) machine.DeleteSubKeyTree(RegistryPath + @"\Devices\" + GuidText(id), false);
        }
        private static void RestoreGlobalIfUnused()
        {
            if (KnownDevices().Length != 0) return;
            using (var machine = Machine())
            {
                using (var key = machine.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        string journal = key.GetValue("ProtectionJournal") as string;
                        if (journal != null)
                        {
                            foreach (var edit in RegistryEdit.Deserialize(journal)) edit.RestoreIfOwned();
                            key.DeleteValue("ProtectionJournal", false);
                        }
                    }
                }
                machine.DeleteSubKeyTree(@"SOFTWARE\Classes\CLSID\" + Clsid, false);
                machine.DeleteSubKeyTree(@"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\" + Clsid, false);
            }
        }
        public static void RemoveAsAdmin() { RemoveAsAdmin(null); }
        public static void RemoveAsAdmin(string expected)
        { SystemConfiguration.Run(delegate { RemoveCore(expected); }); }
        private static void RemoveCore(string expected)
        {
            if (!IsAdministrator()) throw new UnauthorizedAccessException();
            var scope = CheckScope(expected);
            foreach (string id in scope.Devices.Select(d => d.Id)) RemoveDeviceAsAdmin(id);
            RestoreGlobalIfUnused();
            RestartAudioService();
        }
        public static void RestartAudioService()
        { SystemConfiguration.Run(RestartAudioCore); }
        private static void RestartAudioCore()
        {
            if (!IsAdministrator()) throw new UnauthorizedAccessException();
            using (var service = new ServiceController("Audiosrv"))
            {
                var dependents = new List<ServiceController>();
                var allDependents = service.DependentServices;
                foreach (var dependent in allDependents)
                    if (dependent.Status == ServiceControllerStatus.Running) dependents.Add(dependent);
                try
                {
                    if (service.Status != ServiceControllerStatus.Stopped) { service.Stop(); service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(25)); }
                    service.Start(); service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(25));
                }
                finally
                {
                    try
                    {
                        foreach (var dependent in dependents)
                        {
                            dependent.Refresh();
                            if (dependent.Status == ServiceControllerStatus.Stopped) dependent.Start();
                        }
                    }
                    // Stop() also uses its cached DependentServices instances.
                    finally { foreach (var dependent in allDependents) dependent.Dispose(); }
                }
            }
        }
    }

    public sealed class RegistryEdit
    {
        public string Path;
        public string Name;
        public bool Existed;
        public RegistryValueKind BeforeKind;
        public object Before;
        public RegistryValueKind AfterKind;
        public object After;
        public static RegistryEdit Capture(string path, string name, object after, RegistryValueKind kind)
        {
            var edit = new RegistryEdit { Path = path, Name = name, After = after, AfterKind = kind };
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = machine.OpenSubKey(path))
            {
                edit.Existed = key != null && key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase);
                if (edit.Existed) { edit.BeforeKind = key.GetValueKind(name); edit.Before = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
            }
            return edit;
        }
        public void Apply() { RegistryAccess.Write(Path, delegate(RegistryKey key) { key.SetValue(Name, After, AfterKind); }); }
        public bool MatchesBefore() { return Matches(Before, BeforeKind, Existed); }
        public bool MatchesAfter() { return Matches(After, AfterKind, true); }
        private bool Matches(object value, RegistryValueKind kind, bool exists)
        {
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = machine.OpenSubKey(Path))
            {
                bool present = key != null && key.GetValueNames().Contains(Name, StringComparer.OrdinalIgnoreCase);
                return !exists ? !present : present && key.GetValueKind(Name) == kind && ValueText(key.GetValue(Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)) == ValueText(value);
            }
        }
        public void RestoreIfOwned()
        {
            // An unapplied edit must be recoverable without write access to the key.
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = machine.OpenSubKey(Path))
            {
                if (key == null || ValueText(key.GetValue(Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)) != ValueText(After) || key.GetValueKind(Name) != AfterKind) return;
            }
            RegistryAccess.Write(Path, delegate(RegistryKey key)
            {
                object current = key.GetValue(Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (ValueText(current) != ValueText(After) || key.GetValueKind(Name) != AfterKind) return;
                if (Existed) key.SetValue(Name, Before, BeforeKind); else key.DeleteValue(Name, false);
            });
        }
        private static XElement Encode(string tag, object value, RegistryValueKind kind)
        {
            var element = new XElement(tag, new XAttribute("kind", (int)kind));
            if (kind == RegistryValueKind.MultiString) foreach (string text in (string[])value) element.Add(new XElement("s", text));
            else if (kind == RegistryValueKind.Binary) element.Value = Convert.ToBase64String((byte[])value);
            else element.Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            return element;
        }
        private static object Decode(XElement element)
        {
            var kind = (RegistryValueKind)(int)element.Attribute("kind");
            if (kind == RegistryValueKind.MultiString) return element.Elements("s").Select(x => x.Value).ToArray();
            if (kind == RegistryValueKind.Binary) return Convert.FromBase64String(element.Value);
            if (kind == RegistryValueKind.DWord) return Int32.Parse(element.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (kind == RegistryValueKind.QWord) return Int64.Parse(element.Value, System.Globalization.CultureInfo.InvariantCulture);
            return element.Value;
        }
        private static string ValueText(object value)
        {
            if (value == null) return "<missing>";
            if (value is string[]) return "multi:" + String.Join("\0", (string[])value);
            if (value is byte[]) return "binary:" + Convert.ToBase64String((byte[])value);
            return value.GetType().FullName + ":" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        public static string Serialize(IEnumerable<RegistryEdit> edits)
        {
            return new XElement("changes", edits.Select(e => new XElement("edit", new XAttribute("path", e.Path), new XAttribute("name", e.Name),
                new XAttribute("existed", e.Existed), e.Existed ? Encode("before", e.Before, e.BeforeKind) : null, Encode("after", e.After, e.AfterKind)))).ToString(SaveOptions.DisableFormatting);
        }
        public static List<RegistryEdit> Deserialize(string xml)
        {
            return XElement.Parse(xml).Elements("edit").Select(e => new RegistryEdit {
                Path = (string)e.Attribute("path"), Name = (string)e.Attribute("name"), Existed = (bool)e.Attribute("existed"),
                BeforeKind = e.Element("before") == null ? RegistryValueKind.None : (RegistryValueKind)(int)e.Element("before").Attribute("kind"),
                Before = e.Element("before") == null ? null : Decode(e.Element("before")),
                AfterKind = (RegistryValueKind)(int)e.Element("after").Attribute("kind"), After = Decode(e.Element("after"))
            }).ToList();
        }
    }

    internal static class RegistryAccess
    {
        public static void RecoverPending() { PermissionRecovery.ForMachine().RecoverAll(); }
        public static void Write(string path, Action<RegistryKey> write)
        {
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                RegistryKey writable = null;
                try { writable = machine.OpenSubKey(path, true); }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
                if (writable != null) { using (writable) { write(writable); writable.Flush(); } return; }
            }
            PermissionRecovery.ForMachine().Write(path, write);
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct TokenPrivileges { public uint Count; public long Luid; public uint Attributes; }
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValue(string system, string name, out long luid);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr token, bool disable, ref TokenPrivileges privileges, uint size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        internal static void EnablePrivilege(string name)
        {
            IntPtr token;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x28, out token)) throw new Win32Exception();
            try
            {
                long luid; if (!LookupPrivilegeValue(null, name, out luid)) throw new Win32Exception();
                var privilege = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                if (!AdjustTokenPrivileges(token, false, ref privilege, 0, IntPtr.Zero, IntPtr.Zero) || Marshal.GetLastWin32Error() != 0) throw new Win32Exception();
            }
            finally { CloseHandle(token); }
        }
    }
}
