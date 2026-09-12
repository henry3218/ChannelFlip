using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ChannelFlip.Tests")]

namespace ChannelFlip
{
    // A failed write remains pending. Only the caller's next update retries it.
    internal sealed class PreferenceWriter
    {
        private string preferenceKey;
        private bool saving;
        public string Error { get; private set; }
        public void Save(DevicePreference preference, Action<DevicePreference> write)
        {
            if (saving) return;
            var snapshot = new DevicePreference { Id = preference.Id, EndpointGuid = preference.EndpointGuid,
                Name = preference.Name, Language = preference.Language, FollowDefault = preference.FollowDefault };
            string key = snapshot.ToXml().ToString();
            if (preferenceKey == key) return;
            saving = true;
            try { write(snapshot); preferenceKey = key; Error = null; }
            catch (Exception ex) { Error = ex.Message; }
            finally { saving = false; }
        }
    }
}
