using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChannelFlip;

public static class ReliabilityTests
{
    public static void Run(Action<bool, string> check, string directory)
    {
        var writer = new PreferenceWriter();
        var preference = new DevicePreference { Id = "opaque endpoint", EndpointGuid = "{aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb}", Name = "Test headphones", Language = "en" };
        int attempts = 0;
        string folder = Path.Combine(directory, "retry-preferences");
        Action<DevicePreference> save = null;
        save = p => { attempts++; writer.Save(preference, save); if (attempts == 1) throw new IOException("injected write failure"); p.Save(folder); };
        writer.Save(preference, save);
        check(attempts == 1 && writer.Error == "injected write failure", "F05 failed save does not recurse or count as persisted");
        writer.Save(preference, save);
        check(attempts == 2 && writer.Error == null && DevicePreference.Load(folder).Id == preference.Id, "F05 next update retries and persists recovered storage");
        writer.Save(preference, save);
        check(attempts == 2, "F05 successful preference is not redundantly written");
        preference.Language = "zh-TW"; writer.Save(preference, save);
        check(attempts == 3 && DevicePreference.Load(folder).Language == "zh-TW", "F05 subsequent preference changes still save");

        var snapshot = new AudioDeviceSnapshot();
        snapshot.Read(3, i => { if (i == 1) throw new IOException("property access denied"); return new OutputDevice { Id = "opaque-" + i, Guid = preference.EndpointGuid }; });
        check(snapshot.Devices.Count == 2 && snapshot.Devices[1].Id == "opaque-2" && snapshot.Diagnostics.Single().Contains("property access denied"),
            "F04 one endpoint exception preserves other endpoints and its diagnostic");
        var active = new[] { new OutputDevice { Id = "opaque endpoint", Guid = preference.EndpointGuid, Name = "Test headphones", IsDefault = true } };
        var legacy = new DevicePreference { Id = "opaque endpoint" };
        check(legacy.Resolve(new OutputDevice[0]).Guid == null, "F04 legacy offline preference does not guess a GUID from an endpoint ID");
        legacy.Resolve(active); legacy.Save(folder);
        check(DevicePreference.Load(folder).Resolve(new OutputDevice[0]).Guid == preference.EndpointGuid, "F04 property GUID survives offline state and restart");

        var session = Scenarios.Create("waiting"); var backend = (SimulationBackend)session.Backend;
        var original = session.Selected;
        var next = new OutputDevice { Id = "opaque-next", Guid = "{22222222-2222-2222-2222-222222222222}", Name = "Other speakers", Channels = 2 };
        backend.Devices.Add(next); session.SetFollow(true);
        backend.DuringPlay = delegate { original.IsDefault = false; next.IsDefault = true; throw new IOException("original device failed"); };
        session.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult();
        check(session.Selected.Id == next.Id && session.MessageError && session.ErrorDetail == "original device failed", "F06 default switch during failure preserves the error");
        check(session.LastOperation.DeviceId == original.Id && session.LastOperation.DeviceName == original.Name && session.LastOperation.Failed,
            "F06 operation result records original device independently of current selection");
        session.Refresh(); session.Select(original);
        check(session.MessageError && session.LastOperation.ErrorText.Contains("original device failed"), "F06 later refresh and manual selection retain the failure");
        L10n.SetLanguage("en");
        check(session.LastOperation.ErrorText.Contains("Original device:") && session.LastOperation.ErrorText.Contains(original.Name), "F06 retained operation context translates without losing origin");
        L10n.SetLanguage("zh-TW");
    }
}
