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
        public bool HostSettings, CoreRegistered;
        public bool HasChanges { get { return Devices.Length != 0 || HostSettings || CoreRegistered; } }
    }
    public interface IAudioBackend
    {
        List<OutputDevice> Enumerate();
        EngineStatus Read(string id);
        SetupScope Scope();
        bool HostAlive(int pid);
        bool NeedsHostChange();
        Task Attach(string id, bool allowHostChange);
        Task Toggle(string id, bool enabled);
        Task Play(string id, int channel, CancellationToken cancellation);
        Task<string> Global(string action, string scope);
    }
    public sealed class WindowsAudioBackend : IAudioBackend
    {
        public List<OutputDevice> Enumerate() { return AudioDevices.Enumerate(); }
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
        public Task Toggle(string id, bool enabled) { return Task.Run(delegate { Engine.SetEnabled(id, enabled); }); }
        public Task Play(string id, int channel, CancellationToken cancellation) { return Task.Run(delegate { TestTone.Play(id, channel, cancellation); }); }
        public async Task<string> Global(string action, string expected)
        {
            var scope = Engine.CheckScope(expected);
            if (action == "restart") { await Engine.Restart(); return "電腦音訊服務已重新啟動，請重新測試方向。"; }
            if (action == "off")
            {
                await Task.Run(delegate { Engine.DisableAll(expected); });
                return "已確認所有 " + scope.Devices.Length + " 個裝置的互換已關閉；核心與系統設定保留。";
            }
            if (action != "remove") throw new ArgumentException("未知的系統操作。");
            var owned = scope.Changes.Where(e => e.MatchesAfter()).ToArray();
            var external = scope.Changes.Where(e => !e.MatchesAfter() && !e.MatchesBefore()).ToArray();
            await Engine.Remove(expected);
            if (Engine.ReadScope().HasChanges) throw new IOException("移除尚未完成，仍有本程式的設定。請檢查進階設定中的剩餘清單。");
            int unrestored = owned.Count(e => !e.MatchesBefore());
            if (unrestored > 0) throw new IOException("本程式已解除登記，但有 " + unrestored + " 項設定未能確認還原，可能在操作期間被修改。請保留診斷資訊。");
            return "已移除所有裝置的設定，並確認本程式管理的設定已還原。" +
                (external.Length > 0 ? "另保留了 " + external.Length + " 項外部修改。" : "") + " 電腦音訊服務已重新啟動。";
        }
    }

    // This backend is also used by automated UI scenarios. It never reaches Windows audio,
    // the registry, preferences, or the real test-tone player.
    public sealed class SimulationBackend : IAudioBackend
    {
        public List<OutputDevice> Devices = new List<OutputDevice>();
        public Dictionary<string, EngineStatus> States = new Dictionary<string, EngineStatus>();
        public SetupScope Setup = new SetupScope { Signature = "simulation" };
        public List<string> Calls = new List<string>();
        public bool Alive = true, NoProcessing, CancelPlay;
        public string ReadFailure;
        public Func<Task> DuringPlay;
        public List<OutputDevice> Enumerate() { return Devices.ToList(); }
        public EngineStatus Read(string id)
        {
            if (ReadFailure != null) throw new IOException(ReadFailure);
            EngineStatus s; if (!States.TryGetValue(id, out s)) return new EngineStatus();
            return new EngineStatus { Attached = s.Attached, Enabled = s.Enabled, Loads = s.Loads, Frames = s.Frames,
                SwappedFrames = s.SwappedFrames, Channels = s.Channels, HostProcess = s.HostProcess, Error = s.Error };
        }
        public SetupScope Scope() { return Setup; }
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
            if (CancelPlay) throw new OperationCanceledException("已取消測試。");
            cancellation.ThrowIfCancellationRequested();
            var device = Devices.FirstOrDefault(d => d.Id == id);
            if (device == null) throw new IOException("測試期間裝置已離線。");
            EngineStatus s;
            if (!NoProcessing && States.TryGetValue(device.Guid, out s)) { s.Frames += 48000; if (s.Enabled) s.SwappedFrames += 48000; }
        }
        public Task<string> Global(string action, string signature)
        {
            if (signature != Setup.Signature) throw new InvalidOperationException("影響範圍已改變。");
            Calls.Add(action);
            if (action == "remove") { States.Clear(); Setup = new SetupScope { Signature = "removed" }; }
            if (action == "off") foreach (var state in States.Values) state.Enabled = false;
            return Task.FromResult("模擬操作完成：" + action);
        }
    }
}
