using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelFlip
{
    public sealed class SetupDevice { public string Id, Name; }
    public sealed class SetupScope
    {
        public string Signature;
        public SetupDevice[] Devices = new SetupDevice[0];
        public RegistryEdit[] Changes = new RegistryEdit[0];
        public bool HostSettings, CoreRegistered, Installed;
        public bool HasChanges { get { return Devices.Length != 0 || HostSettings || CoreRegistered || Installed; } }
    }
    public interface IAudioBackend
    {
        List<OutputDevice> Enumerate();
        string[] EnumerationDiagnostics { get; }
        EngineStatus Read(string id);
        SetupScope Scope();
        bool HostAlive(int pid);
        bool NeedsHostChange();
        Task Attach(string id, bool allowHostChange);
        Task Toggle(string id, bool enabled);
        Task Play(string id, int channel, CancellationToken cancellation);
        Task<LocalizedText> Global(string action, string scope);
        InstallationStatus Installation();
        Task UpdateInstallation(string id);
    }
    public sealed class WindowsAudioBackend : IAudioBackend
    {
        public string[] EnumerationDiagnostics { get; private set; }
        public WindowsAudioBackend() { EnumerationDiagnostics = new string[0]; }
        public List<OutputDevice> Enumerate()
        {
            var snapshot = AudioDevices.ReadSnapshot(); EnumerationDiagnostics = snapshot.Diagnostics.ToArray(); return snapshot.Devices;
        }
        public EngineStatus Read(string id) { return Engine.Read(id); }
        public SetupScope Scope() { return Engine.ReadScope(); }
        public bool NeedsHostChange() { return Engine.NeedsAudioHostPermission(); }
        public bool HostAlive(int pid)
        {
            if (pid <= 0) return false;
            // GetProcessById/ProcessName use the process table. HasExited requests a process
            // handle and is denied for AudioDG even when this ordinary user's APO is processing.
            try { using (var process = Process.GetProcessById(pid)) return String.Equals(process.ProcessName, "audiodg", StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
        public Task Attach(string id, bool allowHostChange) { return Engine.Attach(id, allowHostChange); }
        public InstallationStatus Installation() { return Engine.ReadInstallation(); }
        public Task UpdateInstallation(string id) { return Engine.UpdateInstallation(id); }
        public Task Toggle(string id, bool enabled) { return Task.Run(delegate { Engine.SetEnabled(id, enabled); }); }
        public Task Play(string id, int channel, CancellationToken cancellation) { return Task.Run(delegate { TestTone.Play(id, channel, cancellation); }); }
        public async Task<LocalizedText> Global(string action, string expected)
        {
            var scope = Engine.CheckScope(expected);
            if (action == "restart") { await Engine.Restart(); return L10n.M("電腦音訊服務已重新啟動，請重新測試方向。"); }
            if (action == "off")
            {
                await Task.Run(delegate { Engine.DisableAll(expected); });
                return L10n.M("已確認所有 {0} 個裝置的互換已關閉；核心與系統設定保留。", scope.Devices.Length);
            }
            if (action != "remove") throw new ArgumentException(L10n.T("未知的系統操作。"));
            var owned = scope.Changes.Where(e => e.MatchesAfter()).ToArray();
            var external = scope.Changes.Where(e => !e.MatchesAfter() && !e.MatchesBefore()).ToArray();
            await Engine.Remove(expected);
            if (Engine.ReadScope().HasChanges) throw new IOException(L10n.T("解除安裝尚未完成，仍有本程式的設定。請重新開啟進階設定，檢查剩餘的項目。"));
            int unrestored = owned.Count(e => !e.MatchesBefore());
            if (unrestored > 0) throw new IOException(L10n.T("本程式已解除登記，但有 {0} 項設定未能確認還原，可能在操作期間被修改。請保留診斷資訊。", unrestored));
            return L10n.M("已解除安裝 Channel Flip，並確認本程式管理的設定都已還原。{0} 電腦音訊服務已重新啟動。{1}",
                external.Length > 0 ? (object)L10n.M("另保留了 {0} 項外部修改。", external.Length) : "",
                Directory.Exists(Engine.InstallDirectory) ? (object)L10n.M("部分檔案仍在使用中，會在下次重新啟動 Windows 時刪除。") : "");
        }
    }

    // This backend is also used by automated UI scenarios. It never reaches Windows audio,
    // the registry, preferences, or the real test-tone player.
    public sealed class SimulationBackend : IAudioBackend
    {
        public string[] EnumerationDiagnostics { get { return new string[0]; } }
        public List<OutputDevice> Devices = new List<OutputDevice>();
        public Dictionary<string, EngineStatus> States = new Dictionary<string, EngineStatus>();
        public SetupScope Setup = new SetupScope { Signature = "simulation" };
        public InstallationStatus Install = new InstallationStatus();
        public List<string> Calls = new List<string>();
        public bool Alive = true, NoProcessing, CancelPlay;
        public string ReadFailure;
        public Func<Task> DuringPlay;
        public List<OutputDevice> Enumerate() { return Devices.ToList(); }
        public EngineStatus Read(string id)
        {
            if (ReadFailure != null) throw new IOException(ReadFailure);
            EngineStatus s; if (!States.TryGetValue(id, out s)) return new EngineStatus();
            return new EngineStatus { Attached = s.Attached, Known = s.Known, Enabled = s.Enabled, Loads = s.Loads, Frames = s.Frames,
                SwappedFrames = s.SwappedFrames, Channels = s.Channels, HostProcess = s.HostProcess, Error = s.Error };
        }
        public SetupScope Scope() { return Setup; }
        public InstallationStatus Installation() { return Install; }
        public Task UpdateInstallation(string id)
        {
            Calls.Add("update:" + id); Install = new InstallationStatus { Configured = true, Registered = true };
            return Task.FromResult(0);
        }
        public bool HostAlive(int pid) { return Alive && pid > 0; }
        public bool NeedsHostChange() { return true; }
        public Task Attach(string id, bool consent)
        {
            Calls.Add("attach:" + id); States[id] = new EngineStatus { Attached = true, Enabled = true, HostProcess = 123, Loads = 1 };
            return Task.FromResult(0);
        }
        public Task Toggle(string id, bool enabled) { Calls.Add("toggle:" + id); States[id].Enabled = enabled; return Task.FromResult(0); }
        public async Task Play(string id, int channel, CancellationToken cancellation)
        {
            Calls.Add("play:" + id + ":" + channel);
            if (DuringPlay != null) await DuringPlay();
            if (CancelPlay) throw new OperationCanceledException(L10n.T("已取消測試。"));
            cancellation.ThrowIfCancellationRequested();
            var device = Devices.FirstOrDefault(d => d.Id == id);
            if (device == null) throw new IOException(L10n.T("測試期間裝置已離線。"));
            EngineStatus s;
            if (!NoProcessing && States.TryGetValue(device.Guid, out s)) { s.Frames += 48000; if (s.Enabled) s.SwappedFrames += 48000; }
        }
        public Task<LocalizedText> Global(string action, string signature)
        {
            if (signature != Setup.Signature) throw new InvalidOperationException(L10n.T("影響範圍已改變。"));
            Calls.Add(action);
            if (action == "remove") { States.Clear(); Setup = new SetupScope { Signature = "removed" }; Install = new InstallationStatus(); }
            if (action == "off") foreach (var state in States.Values) state.Enabled = false;
            return Task.FromResult(L10n.M("模擬操作完成：{0}", action));
        }
    }
}
