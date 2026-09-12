using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;

namespace ChannelFlip
{
    // Each record is flushed before the mutation it authorizes. A terminated process
    // leaves enough information to restore permissions without inferring ownership.
    internal sealed class PermissionRecovery
    {
        private const AccessControlSections Sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
        private readonly string rootPath;
        private readonly SecurityIdentifier journalOwner;
        private readonly Action privileges;
        internal Action<string> Checkpoint;
        internal PermissionRecovery(string path, SecurityIdentifier owner, Action enablePrivileges)
        { rootPath = path; journalOwner = owner; privileges = enablePrivileges; }
        internal static PermissionRecovery ForMachine()
        {
            return new PermissionRecovery(Engine.RegistryPath + @"\PermissionRecovery",
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), delegate
                { RegistryAccess.EnablePrivilege("SeTakeOwnershipPrivilege"); RegistryAccess.EnablePrivilege("SeRestorePrivilege"); });
        }
        private RegistryKey OpenJournal(RegistryKey machine)
        {
            var security = new RegistrySecurity();
            security.SetAccessRuleProtection(true, false); security.SetOwner(journalOwner);
            foreach (var sid in new[] { journalOwner, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                security.AddAccessRule(new RegistryAccessRule(sid, RegistryRights.FullControl, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
            var key = machine.CreateSubKey(rootPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.None, security);
            const AccessControlSections compare = AccessControlSections.Owner | AccessControlSections.Access;
            if (key.GetAccessControl(compare).GetSecurityDescriptorSddlForm(compare) != security.GetSecurityDescriptorSddlForm(compare))
            { key.Dispose(); throw new UnauthorizedAccessException(L10n.T("權限復原紀錄的存取權不符，已停止系統操作。")); }
            return key;
        }
        private static RegistrySecurity Security(string sddl)
        { var result = new RegistrySecurity(); result.SetSecurityDescriptorSddlForm(sddl, Sections); return result; }
        private static string ReadSecurity(RegistryKey machine, string path)
        {
            using (var key = machine.OpenSubKey(path, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadPermissions))
            {
                if (key == null) throw new IOException(L10n.T("登錄機碼不存在：") + path);
                return key.GetAccessControl(Sections).GetSecurityDescriptorSddlForm(Sections);
            }
        }
        private static void Save(RegistryKey journal, string id, XElement record, string phase)
        {
            record.SetAttributeValue("phase", phase);
            // One registry value is the atomic record; there are no partly populated subkeys.
            journal.SetValue(id, record.ToString(SaveOptions.DisableFormatting), RegistryValueKind.String);
            journal.Flush();
        }
        private void Mark(RegistryKey journal, string id, XElement record, string phase)
        { Save(journal, id, record, phase); if (Checkpoint != null) Checkpoint(phase); }
        private static void AssertKnown(RegistryKey machine, XElement record)
        {
            string current = ReadSecurity(machine, (string)record.Attribute("path"));
            string phase = (string)record.Attribute("phase");
            bool known = current == (string)record.Element("original");
            if (phase != "Prepared") known |= current == (string)record.Element("owned");
            if (phase != "Prepared" && phase != "OwnerIntent" && phase != "OwnerChanged") known |= current == (string)record.Element("writable");
            if (!known) throw new IOException(L10n.T("偵測到外部權限變更，已停止復原並保留紀錄：{0}", (string)record.Attribute("path")));
        }
        private void Restore(RegistryKey machine, RegistryKey journal, string id, XElement record)
        {
            AssertKnown(machine, record);
            Save(journal, id, record, "RestoreIntent");
            string path = (string)record.Attribute("path"), original = (string)record.Element("original");
            if (ReadSecurity(machine, path) != original)
            {
                using (var key = machine.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.TakeOwnership | RegistryRights.ReadPermissions))
                {
                    AssertKnown(machine, record);
                    key.SetAccessControl(Security(original)); key.Flush();
                }
            }
            if (ReadSecurity(machine, path) != original) throw new IOException(L10n.T("無法確認原始權限已還原，已保留復原紀錄：{0}", path));
            Mark(journal, id, record, "Restored");
            journal.DeleteValue(id); journal.Flush();
        }
        internal void RecoverAll()
        {
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (var existing = machine.OpenSubKey(rootPath)) if (existing == null) return;
                using (var journal = OpenJournal(machine))
                {
                    foreach (string id in journal.GetValueNames())
                    {
                        Guid transaction;
                        if (!Guid.TryParseExact(id, "N", out transaction)) throw new InvalidDataException("Invalid permission transaction ID.");
                        var record = XElement.Parse((string)journal.GetValue(id));
                        string path = (string)record.Attribute("path"), phase = (string)record.Attribute("phase");
                        if ((int?)record.Attribute("version") != 1 || String.IsNullOrEmpty(path) || path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) ||
                            !new[] { "Prepared", "OwnerIntent", "OwnerChanged", "AccessIntent", "AccessChanged", "WriteIntent", "ValueWritten", "RestoreIntent", "Restored" }.Contains(phase))
                            throw new InvalidDataException("Invalid permission recovery record.");
                        // Detect interference before acquiring any target permissions.
                        AssertKnown(machine, record); privileges(); Restore(machine, journal, id, record);
                    }
                }
            }
        }
        internal void Write(string path, Action<RegistryKey> write)
        {
            privileges();
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var journal = OpenJournal(machine))
            {
                string original = ReadSecurity(machine, path);
                var owned = Security(original); owned.SetOwner(WindowsIdentity.GetCurrent().User);
                var writable = Security(owned.GetSecurityDescriptorSddlForm(Sections));
                writable.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User, RegistryRights.FullControl, AccessControlType.Allow));
                var record = new XElement("permissions", new XAttribute("version", 1), new XAttribute("path", path),
                    new XElement("original", original), new XElement("owned", owned.GetSecurityDescriptorSddlForm(Sections)),
                    new XElement("writable", writable.GetSecurityDescriptorSddlForm(Sections)));
                string id = Guid.NewGuid().ToString("N");
                Mark(journal, id, record, "Prepared");
                try
                {
                    AssertKnown(machine, record); Mark(journal, id, record, "OwnerIntent");
                    using (var key = machine.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
                    { var owner = new RegistrySecurity(); owner.SetOwner(WindowsIdentity.GetCurrent().User); key.SetAccessControl(owner); key.Flush(); }
                    if (Checkpoint != null) Checkpoint("OwnerApplied");
                    Mark(journal, id, record, "OwnerChanged");
                    AssertKnown(machine, record); Mark(journal, id, record, "AccessIntent");
                    using (var key = machine.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                    { var access = new RegistrySecurity(); access.SetSecurityDescriptorSddlForm(writable.GetSecurityDescriptorSddlForm(Sections), AccessControlSections.Access); key.SetAccessControl(access); key.Flush(); }
                    if (Checkpoint != null) Checkpoint("AccessApplied");
                    Mark(journal, id, record, "AccessChanged");
                    AssertKnown(machine, record); Mark(journal, id, record, "WriteIntent");
                    using (var key = machine.OpenSubKey(path, true)) { write(key); key.Flush(); }
                    if (Checkpoint != null) Checkpoint("ValueApplied");
                    Mark(journal, id, record, "ValueWritten");
                }
                finally { Restore(machine, journal, id, record); }
            }
        }
    }
}
