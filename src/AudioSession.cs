using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelFlip
{
    public sealed class OperationResult
    {
        public string DeviceId { get; internal set; }
        public string DeviceName { get; internal set; }
        public LocalizedText Operation { get; internal set; }
        public LocalizedText Error { get; internal set; }
        public bool Failed { get; internal set; }
        public string ErrorText { get { return L10n.T("未完成的操作：{0}\n原裝置：{1}\n{2}", Operation,
            DeviceName ?? L10n.T("所有裝置"), Error); } }
    }
    public sealed class AudioSession
    {
        public readonly IAudioBackend Backend;
        public readonly DevicePreference Preference;
        public readonly ProcessingObservation Observation = new ProcessingObservation();
        public Func<DateTime> Clock = delegate { return DateTime.UtcNow; };
        public List<OutputDevice> Devices = new List<OutputDevice>();
        public OutputDevice Selected;
        public EngineStatus State = new EngineStatus();
        public SetupScope Scope = new SetupScope();
        public string ReadError, ScopeError, EnumerationError;
        public OperationResult LastOperation { get; private set; }
        private LocalizedText message, errorDetail;
        public string Message { get { return LocalizedText.Render(message); } set { message = value; } }
        public string ErrorDetail { get { return LocalizedText.Render(errorDetail); } set { errorDetail = value; } }
        public void SetMessage(LocalizedText value) { message = value; }
        public void SetErrorDetail(LocalizedText value) { errorDetail = value; }
        public bool MessageError, Busy;
        public event Action Changed;
        public UiState Presentation { get { return UiState.Evaluate(Selected, State, ReadError, Observation, Clock()); } }
        public AudioSession(IAudioBackend backend, DevicePreference preference) { Backend = backend; Preference = preference; }
        public void Notify() { if (Changed != null) Changed(); }
        public void Refresh()
        {
            if (Busy) return;
            string previousId = Selected == null ? null : Selected.Id;
            bool previouslyOffline = Selected != null && Selected.Offline;
            ReadError = null; ScopeError = null;
            try
            {
                Devices = Backend.Enumerate(); Selected = Preference.Resolve(Devices);
                EnumerationError = String.Join("\n", Backend.EnumerationDiagnostics);
                if (Selected != null && Selected.Offline) Devices.Insert(0, Selected);
                // A successful instruction to test is stale once its endpoint disconnects.
                // Keep failure details, and do not hide later global-operation results while offline.
                if (Selected != null && Selected.Offline && !previouslyOffline && !MessageError) Message = null;
                if (previousId != (Selected == null ? null : Selected.Id))
                {
                    Observation.Reset();
                    if (!MessageError)
                    {
                        Message = null; ErrorDetail = null;
                        if (Preference.FollowDefault && previousId != null && Selected != null) message = L10n.M("操作目標已跟隨系統預設改為「{0}」。", Selected.Name);
                    }
                }
                State = Selected == null || Selected.Guid == null ? new EngineStatus() : Backend.Read(Selected.Guid);
                Observation.Sample(Selected == null ? null : Selected.Id, State,
                    Selected != null && !Selected.Offline && Selected.Channels >= 2 && !Selected.EnhancementsDisabled,
                    Backend.HostAlive(State.HostProcess), Clock());
            }
            catch (Exception ex) { ReadError = ex.Message; State = new EngineStatus(); Observation.Reset(); }
            try { Scope = Backend.Scope(); }
            catch (Exception ex) { Scope = new SetupScope(); ScopeError = ex.Message; }
            Notify();
        }
        public void Select(OutputDevice device)
        {
            if (Busy || device == null) return;
            Preference.Id = device.Id; Preference.EndpointGuid = device.Guid; Preference.Name = device.Name; Preference.FollowDefault = false; Refresh();
        }
        public void SetFollow(bool follow)
        {
            if (Busy) return;
            if (!follow && Selected != null) { Preference.Id = Selected.Id; Preference.EndpointGuid = Selected.Guid; Preference.Name = Selected.Name; }
            Preference.FollowDefault = follow; Refresh();
        }
        private OutputDevice RequireDevice(string id)
        {
            var device = Backend.Enumerate().FirstOrDefault(d => d.Id == id && !d.Offline);
            if (device == null) throw new IOException(L10n.T("原目標裝置已離線，這次操作已停止。請重新連接後再試。"));
            if (device.Channels < 2 || device.FormatError != null) throw new IOException(L10n.T("原目標裝置目前無法使用左右聲道操作。"));
            return device;
        }
        private async Task Run(string targetId, LocalizedText progress, Func<Task<LocalizedText>> action)
        {
            if (Busy) return;
            var target = Devices.FirstOrDefault(d => d.Id == targetId);
            LastOperation = new OperationResult { DeviceId = targetId, DeviceName = target == null ? targetId : target.Name, Operation = progress };
            Busy = true; message = progress; MessageError = false; ErrorDetail = null; Notify();
            try { message = await action(); }
            catch (OperationCanceledException) { message = L10n.M("已取消這次操作。"); }
            catch (Exception ex) { message = L10n.M("操作未完成，請檢查下方狀態或錯誤詳情。"); ErrorDetail = ex.Message; MessageError = true; }
            finally
            {
                LastOperation.Failed = MessageError; LastOperation.Error = errorDetail;
                Busy = false; Refresh();
                if (!MessageError && targetId != null && (Selected == null || Selected.Id != targetId)) { message = L10n.M("操作目標已變更，請查看目前裝置的狀態。"); ErrorDetail = null; Notify(); }
            }
        }
        public Task SetupAsync(string targetId, bool allowHostChange)
        {
            return Run(targetId, L10n.M("正在設定此裝置，電腦音訊會短暫中斷…"), async delegate
            {
                var device = RequireDevice(targetId); Observation.Reset();
                await Backend.Attach(device.Guid, allowHostChange);
                if (!Backend.Read(device.Guid).Attached) throw new IOException(L10n.T("尚未確認裝置設定完成。"));
                return L10n.M("裝置已設定。請播放左右測試音，確認實際耳機方向。");
            });
        }
        public Task ToggleAsync(bool enabled)
        {
            if (Selected == null) return Task.FromResult(0);
            string id = Selected.Id;
            return Run(id, L10n.M("正在切換這個裝置的互換設定…"), async delegate
            {
                var device = RequireDevice(id); var before = Backend.Read(device.Guid);
                if (!before.Attached) throw new IOException(L10n.T("此裝置尚未設定，請先使用「設定此裝置」。"));
                if (enabled && (before.Error != 0 || device.EnhancementsDisabled)) throw new IOException(L10n.T("請先處理目前的音訊錯誤或開啟音效強化。"));
                await Backend.Toggle(device.Guid, enabled); Observation.Reset();
                if (Backend.Read(device.Guid).Enabled != enabled) throw new IOException(L10n.T("無法確認設定已儲存。"));
                return enabled ? L10n.M("互換設定已開啟，請測試方向。") : L10n.M("互換設定已關閉。");
            });
        }
        public Task TestAsync(int channel, CancellationToken cancellation)
        {
            if (Selected == null) return Task.FromResult(0);
            string id = Selected.Id;
            return Run(id, L10n.M("正在播放來源{0}聲道…", channel == 0 ? L10n.M("左") : L10n.M("右")), async delegate
            {
                try
                {
                    var device = RequireDevice(id); var before = Backend.Read(device.Guid);
                    await Backend.Play(id, channel, cancellation);
                    cancellation.ThrowIfCancellationRequested(); RequireDevice(id);
                    var after = Backend.Read(device.Guid);
                    Observation.Sample(id, before, true, Backend.HostAlive(before.HostProcess), Clock());
                    Observation.Sample(id, after, true, Backend.HostAlive(after.HostProcess), Clock());
                    bool passed = !before.Attached || (after.Attached && before.Enabled == after.Enabled && after.Error == 0 &&
                        after.Frames > before.Frames && Backend.HostAlive(after.HostProcess) &&
                        (before.Enabled ? after.SwappedFrames > before.SwappedFrames : after.SwappedFrames == before.SwappedFrames));
                    var failure = L10n.M("本次測試未偵測到預期的核心處理。請重試，或到進階設定檢查音訊服務。");
                    Observation.RecordTest(channel, passed, failure, Clock());
                    if (!passed)
                    {
                        MessageError = true;
                        errorDetail = L10n.M("測試處理計數：{0} → {1}\n互換計數：{2} → {3}\n音訊宿主 PID：{4}\n核心錯誤：0x{5}",
                            before.Frames, after.Frames, before.SwappedFrames, after.SwappedFrames, after.HostProcess, after.Error.ToString("X8"));
                        return L10n.M("測試未通過，可展開錯誤詳情。");
                    }
                    var ear = ((channel == 0) != (before.Attached && before.Enabled)) ? L10n.M("左") : L10n.M("右");
                    return L10n.M("來源{0}聲道播放完成，預期從{1}耳聽到。", channel == 0 ? L10n.M("左") : L10n.M("右"), ear);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Observation.RecordTest(channel, false, L10n.M("測試未完成，請檢查裝置連線後重試。"), Clock()); throw new IOException(ex.Message, ex); }
            });
        }
        public Task GlobalAsync(string action, string signature)
        {
            return Run(null, action == "off" ? L10n.M("正在關閉所有已設定裝置的互換…") : L10n.M("正在處理系統設定，電腦音訊可能短暫中斷…"), async delegate
            {
                if (Backend.Scope().Signature != signature) throw new InvalidOperationException(L10n.T("影響範圍已改變，請重新開啟進階設定檢視清單。"));
                Observation.Reset(); return await Backend.Global(action, signature);
            });
        }
    }
}
