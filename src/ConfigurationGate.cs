using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ChannelFlip
{
    internal sealed class ConfigurationGate : IDisposable
    {
        private Mutex mutex;
        private bool acquired;
        internal const string MachineName = @"Global\ChannelFlip.SystemConfiguration.v1";
        internal ConfigurationGate(string name, SecurityIdentifier owner, TimeSpan timeout)
        {
            var security = new MutexSecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(owner);
            security.AddAccessRule(new MutexAccessRule(owner, MutexRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), MutexRights.FullControl, AccessControlType.Allow));
            bool created;
            mutex = new Mutex(false, name, out created, security);
            try
            {
                // Refuse an object pre-created with a weaker DACL or a different owner.
                const AccessControlSections sections = AccessControlSections.Owner | AccessControlSections.Access;
                if (mutex.GetAccessControl().GetSecurityDescriptorSddlForm(sections) != security.GetSecurityDescriptorSddlForm(sections))
                    throw new UnauthorizedAccessException(L10n.T("系統設定鎖的權限不符，已停止操作。請重新啟動 Windows 後再試。"));
                try { acquired = mutex.WaitOne(timeout); }
                catch (AbandonedMutexException) { acquired = true; } // Recovery runs before any new mutation.
                if (!acquired) throw new TimeoutException(L10n.T("另一個程序仍在修改音訊設定，請稍後再試。"));
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (mutex == null) return;
            if (acquired) { mutex.ReleaseMutex(); acquired = false; }
            mutex.Dispose(); mutex = null;
        }
    }

    internal static class SystemConfiguration
    {
        [ThreadStatic] private static bool held;
        internal static void Run(Action action)
        {
            if (!Engine.IsAdministrator()) throw new UnauthorizedAccessException(L10n.T("需要 Windows 管理員授權。"));
            if (held) { action(); return; }
            using (new ConfigurationGate(ConfigurationGate.MachineName, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), TimeSpan.FromSeconds(90)))
            {
                held = true;
                try { RegistryAccess.RecoverPending(); action(); }
                finally { held = false; }
            }
        }
    }
}
