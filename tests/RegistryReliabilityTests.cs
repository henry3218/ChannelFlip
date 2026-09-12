using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
using ChannelFlip;

public static class RegistryReliabilityTests
{
    private const string Prefix = @"Software\ChannelFlip.ReliabilityTests.";
    private const string Journal = @"SOFTWARE\ChannelFlip\PermissionRecovery";
    private const AccessControlSections Sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
    [DllImport("advapi32.dll")] private static extern int RegOverridePredefKey(IntPtr root, IntPtr key);
    private static SecurityIdentifier User { get { return WindowsIdentity.GetCurrent().User; } }
    private static PermissionRecovery Recovery() { return new PermissionRecovery(Journal, User, delegate { }); }
    private static string MutexName(string id) { return @"Global\ChannelFlip.ReliabilityTests." + id; }
    private static void AllowCleanup(RegistryKey root)
    {
        using (var key = root.OpenSubKey("Target", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
        {
            if (key == null) return;
            var access = new RegistrySecurity(); access.SetAccessRuleProtection(true, false);
            access.AddAccessRule(new RegistryAccessRule(User, RegistryRights.FullControl, AccessControlType.Allow));
            key.SetAccessControl(access);
        }
    }
    private static Process Start(string id, string action, string detail)
    {
        var info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--reliability-child " + id + " " + action + " " + detail)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        return Process.Start(info);
    }
    private static int Finish(Process child)
    {
        using (child)
        {
            if (!child.WaitForExit(20000)) { child.Kill(); throw new TimeoutException("Isolated registry child timed out."); }
            string output = child.StandardOutput.ReadToEnd() + child.StandardError.ReadToEnd();
            if (!String.IsNullOrEmpty(output)) Console.WriteLine(output);
            return child.ExitCode;
        }
    }
    public static int Child(string[] args)
    {
        try
        {
            string id = Guid.ParseExact(args[1], "N").ToString("N");
            L10n.SetLanguage("en"); RegistryEdit.Deserialize("<changes />");
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (var root = user.OpenSubKey(Prefix + id, true))
            {
                if (root == null || RegOverridePredefKey(new IntPtr(unchecked((int)0x80000002)), root.Handle.DangerousGetHandle()) != 0)
                    throw new IOException("Missing isolated test registry.");
                try
                {
                    using (new ConfigurationGate(MutexName(id), User, TimeSpan.FromSeconds(10)))
                    {
                        var recovery = Recovery(); recovery.RecoverAll();
                        if (args[2] == "recover") return 0;
                        if (args[2] == "crash")
                        {
                            recovery.Checkpoint = phase => { if (phase == args[3]) Process.GetCurrentProcess().Kill(); };
                            recovery.Write("Target", key => key.SetValue("value", "ours"));
                            return 7; // The requested interruption must have happened.
                        }
                        using (var key = root.OpenSubKey("Concurrent", true))
                        {
                            if (args[2] == "attach")
                            {
                                // The same capture/apply primitives as setup, read only after the gate.
                                if (key.GetValue("journal") == null)
                                {
                                    var edit = RegistryEdit.Capture("Concurrent", "value", "ours", RegistryValueKind.String);
                                    Thread.Sleep(150); // Make unguarded overlap reproducible.
                                    key.SetValue("journal", RegistryEdit.Serialize(new[] { edit })); key.Flush(); edit.Apply();
                                }
                            }
                            else if (args[2] == "remove")
                            {
                                var xml = key.GetValue("journal") as string;
                                if (xml != null) { foreach (var edit in RegistryEdit.Deserialize(xml)) edit.RestoreIfOwned(); key.DeleteValue("journal"); key.Flush(); }
                            }
                        }
                    }
                }
                finally { RegOverridePredefKey(new IntPtr(unchecked((int)0x80000002)), IntPtr.Zero); }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
    }
    public static void Run(Action<bool, string> check)
    {
        string id = Guid.NewGuid().ToString("N");
        using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
        {
            try
            {
                using (var root = user.CreateSubKey(Prefix + id))
                {
                    using (var key = root.CreateSubKey("Concurrent")) key.SetValue("value", "original");
                    var a = Start(id, "attach", "none"); var b = Start(id, "attach", "none");
                    check(Finish(a) == 0 && Finish(b) == 0, "F01 two setup processes finish under the machine-wide gate");
                    using (var key = root.OpenSubKey("Concurrent"))
                        check((string)RegistryEdit.Deserialize((string)key.GetValue("journal")).Single().Before == "original", "F01 concurrent setup never replaces the original backup with our value");
                    a = Start(id, "attach", "none"); b = Start(id, "remove", "none");
                    check(Finish(a) == 0 && Finish(b) == 0, "F01 setup and removal serialize");
                    check(Finish(Start(id, "remove", "none")) == 0, "F01 final removal completes");
                    using (var key = root.OpenSubKey("Concurrent")) check((string)key.GetValue("value") == "original" && key.GetValue("journal") == null, "F01 overlap preserves exact original and removes its journal");

                    foreach (string phase in new[] { "OwnerApplied", "AccessApplied", "ValueApplied", "ValueWritten" })
                    {
                        AllowCleanup(root);
                        root.DeleteSubKeyTree("Target", false);
                        string original;
                        using (var key = root.CreateSubKey("Target"))
                        {
                            key.SetValue("value", "original");
                            var restricted = new RegistrySecurity(); restricted.SetAccessRuleProtection(true, false);
                            restricted.SetOwner(Engine.IsAdministrator() ? new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) : User);
                            restricted.AddAccessRule(new RegistryAccessRule(User, RegistryRights.ReadKey | RegistryRights.ChangePermissions | RegistryRights.TakeOwnership, AccessControlType.Allow));
                            key.SetAccessControl(restricted); original = key.GetAccessControl(Sections).GetSecurityDescriptorSddlForm(Sections);
                        }
                        check(Finish(Start(id, "crash", phase)) != 0, "F02 process is forcibly terminated at " + phase);
                        using (var journal = root.OpenSubKey(Journal)) check(journal != null && journal.GetValueNames().Length == 1, "F02 durable recovery record exists after " + phase);
                        check(Finish(Start(id, "recover", "none")) == 0, "F02 next process recovers " + phase);
                        using (var key = root.OpenSubKey("Target"))
                        {
                            check(key.GetAccessControl(Sections).GetSecurityDescriptorSddlForm(Sections) == original, "F02 exact owner, group and DACL restored after " + phase);
                            check((string)key.GetValue("value") == (phase.StartsWith("Value") ? "ours" : "original"), "F02 ACL recovery preserves value state for the independent setting journal at " + phase);
                        }
                        using (var journal = root.OpenSubKey(Journal)) check(journal.GetValueNames().Length == 0, "F02 verified recovery clears its record at " + phase);
                    }
                    check(Finish(Start(id, "crash", "AccessApplied")) != 0, "F02 create pending transaction for interference test");
                    string external;
                    using (var key = root.OpenSubKey("Target", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadPermissions))
                    {
                        var acl = key.GetAccessControl(Sections);
                        acl.AddAccessRule(new RegistryAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), RegistryRights.ReadKey, AccessControlType.Allow));
                        key.SetAccessControl(acl); external = key.GetAccessControl(Sections).GetSecurityDescriptorSddlForm(Sections);
                    }
                    check(Finish(Start(id, "recover", "none")) == 2, "F02 external permission edit stops recovery");
                    using (var key = root.OpenSubKey("Target")) check(key.GetAccessControl(Sections).GetSecurityDescriptorSddlForm(Sections) == external, "F02 external DACL is preserved");
                    using (var journal = root.OpenSubKey(Journal)) check(journal.GetValueNames().Length == 1, "F02 conflicting transaction is retained for diagnosis");
                }
            }
            finally
            {
                // This exact, fixed-prefix HKCU subtree belongs only to this test run.
                using (var root = user.OpenSubKey(Prefix + id, true)) if (root != null) AllowCleanup(root);
                user.DeleteSubKeyTree(Prefix + id, false);
            }
        }
    }
}
