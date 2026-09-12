using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace ChannelFlip
{
    public sealed class OutputDevice
    {
        public string Id;
        public string Guid;
        public string Name;
        public bool IsDefault;
        public int Channels;
        public bool ApoAttached;
        public bool EnhancementsDisabled;
        public string FormatError;
        public bool Offline;
        public string DisplayName { get { return Name + (Offline ? L10n.T("  ·  已離線") : IsDefault ? L10n.T("  ·  系統預設") : ""); } }
        public override string ToString() { return DisplayName; }
    }

    public sealed class AudioDeviceSnapshot
    {
        public readonly List<OutputDevice> Devices = new List<OutputDevice>();
        public readonly List<string> Diagnostics = new List<string>();
        internal void Read(uint count, Func<uint, OutputDevice> read)
        {
            for (uint i = 0; i < count; i++)
                try { Devices.Add(read(i)); }
                catch (Exception ex) { Diagnostics.Add(L10n.T("無法讀取音訊裝置 {0}：{1}", i + 1, ex.Message)); }
        }
    }

    public static class AudioDevices
    {
        public const string RenderRegistry = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\";
        public static List<OutputDevice> Enumerate()
        {
            var snapshot = ReadSnapshot();
            foreach (string error in snapshot.Diagnostics) Program.Log(new System.IO.IOException(error));
            return snapshot.Devices;
        }
        public static AudioDeviceSnapshot ReadSnapshot()
        {
            var snapshot = new AudioDeviceSnapshot();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            IMMDevice defaultDevice = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                string defaultId = null;
                try { if (enumerator.GetDefaultAudioEndpoint(0, 1, out defaultDevice) >= 0) Check(defaultDevice.GetId(out defaultId)); }
                catch (Exception ex) { snapshot.Diagnostics.Add(L10n.T("無法讀取預設音訊裝置：{0}", ex.Message)); }
                Check(enumerator.EnumAudioEndpoints(0, 1, out collection));
                uint count;
                Check(collection.GetCount(out count));
                snapshot.Read(count, delegate(uint i)
                {
                    IMMDevice endpoint = null;
                    IPropertyStore properties = null;
                    try
                    {
                        Check(collection.Item(i, out endpoint));
                        string id;
                        Check(endpoint.GetId(out id));
                        Check(endpoint.OpenPropertyStore(0, out properties));
                        string guid = Engine.GuidText(PropertyString(properties, new PropertyKey("1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 4)));
                        var device = new OutputDevice { Id = id, Guid = guid, Name = id, IsDefault = id == defaultId };
                        try { device.Name = PropertyString(properties, new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0", 14)) ?? id; }
                        catch (Exception ex) { snapshot.Diagnostics.Add(L10n.T("無法讀取音訊裝置 {0}：{1}", id, ex.Message)); }
                        using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                        {
                            using (var fx = machine.OpenSubKey(RenderRegistry + guid + @"\FxProperties"))
                            {
                                if (fx != null)
                                {
                                    foreach (int slot in new[] { 2, 6, 7, 14, 15 })
                                    {
                                        object value = fx.GetValue("{d04e05a6-594b-4fb6-a80d-01af5eed7d1d}," + slot);
                                        var values = value as string[] ?? new[] { value as string ?? "" };
                                        if (values.Any(x => String.Equals(x, Engine.Clsid, StringComparison.OrdinalIgnoreCase))) device.ApoAttached = true;
                                    }
                                    device.EnhancementsDisabled = Convert.ToInt32(fx.GetValue("{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5", 0)) != 0;
                                }
                            }
                        }
                        try
                        {
                            IAudioClient client = Activate(endpoint);
                            IntPtr format = IntPtr.Zero;
                            try { Check(client.GetMixFormat(out format)); device.Channels = (ushort)Marshal.ReadInt16(format, 2); }
                            finally { if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format); Release(client); }
                        }
                        catch (Exception ex) { device.FormatError = ex.Message; }
                        return device;
                    }
                    finally { Release(properties); Release(endpoint); }
                });
            }
            finally { Release(defaultDevice); Release(collection); Release(enumerator); }
            snapshot.Devices.Sort((a, b) => a.IsDefault != b.IsDefault ? (a.IsDefault ? -1 : 1) : String.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            return snapshot;
        }

        internal static string PropertyString(IPropertyStore store, PropertyKey key)
        {
            PropVariant value = new PropVariant();
            try
            {
                Check(store.GetValue(ref key, out value));
                if (value.Type == 0) return null;
                if (value.Type != 31) throw new InvalidOperationException("Expected VT_LPWSTR for endpoint property " + key.Id);
                return Marshal.PtrToStringUni(value.Pointer);
            }
            finally { PropVariantClear(ref value); }
        }
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);

        internal static IAudioClient Activate(IMMDevice endpoint)
        {
            Guid iid = typeof(IAudioClient).GUID;
            object value;
            Check(endpoint.Activate(ref iid, 23, IntPtr.Zero, out value));
            return (IAudioClient)value;
        }
        internal static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
        internal static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    }

    public static class TestTone
    {
        public static byte[] Generate(int sampleRate, int channels, int bits, bool floatingPoint, int sourceChannel, int startFrame, int frames, int totalFrames)
        {
            if (sourceChannel < 0 || sourceChannel >= channels || channels < 2) throw new InvalidOperationException(L10n.T("裝置目前不是立體聲，無法測試左右聲道。"));
            if ((floatingPoint && bits != 32) || (!floatingPoint && bits != 16 && bits != 24 && bits != 32)) throw new NotSupportedException(L10n.T("測試音不支援此裝置的音訊格式。"));
            int bytesPerSample = bits / 8;
            byte[] result = new byte[frames * channels * bytesPerSample];
            int fade = Math.Max(1, sampleRate / 60);
            for (int i = 0; i < frames; i++)
            {
                int frame = startFrame + i;
                double envelope = Math.Max(0, Math.Min(1.0, Math.Min((double)frame / fade, (double)(totalFrames - 1 - frame) / fade)));
                double sample = Math.Sin(2 * Math.PI * (sourceChannel == 0 ? 660 : 880) * frame / sampleRate) * 0.08 * envelope;
                int offset = (i * channels + sourceChannel) * bytesPerSample;
                if (floatingPoint) Buffer.BlockCopy(BitConverter.GetBytes((float)sample), 0, result, offset, 4);
                else
                {
                    long scale = (1L << (bits - 1)) - 1;
                    int value = (int)Math.Round(sample * scale);
                    for (int b = 0; b < bytesPerSample; b++) result[offset + b] = (byte)(value >> (8 * b));
                }
            }
            return result;
        }

        public static void Play(string endpointId, int sourceChannel, CancellationToken cancel)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice endpoint = null;
            IAudioClient client = null;
            IAudioRenderClient render = null;
            IntPtr format = IntPtr.Zero;
            bool started = false;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                AudioDevices.Check(enumerator.GetDevice(endpointId, out endpoint));
                client = AudioDevices.Activate(endpoint);
                AudioDevices.Check(client.GetMixFormat(out format));
                int tag = (ushort)Marshal.ReadInt16(format, 0);
                int channels = (ushort)Marshal.ReadInt16(format, 2);
                int rate = Marshal.ReadInt32(format, 4);
                int bits = (ushort)Marshal.ReadInt16(format, 14);
                if (tag == 65534) tag = Marshal.ReadInt32(format, 24);
                if (tag != 1 && tag != 3) throw new NotSupportedException(L10n.T("測試音不支援此裝置的音訊格式。"));
                AudioDevices.Check(client.Initialize(0, 0, 1000000, 0, format, IntPtr.Zero));
                uint capacity;
                AudioDevices.Check(client.GetBufferSize(out capacity));
                Guid iid = typeof(IAudioRenderClient).GUID;
                object service;
                AudioDevices.Check(client.GetService(ref iid, out service));
                render = (IAudioRenderClient)service;
                int total = (int)(rate * 0.8);
                int written = 0;
                var clock = Stopwatch.StartNew();
                while (true)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException(L10n.T("播放測試音逾時；請確認耳機仍保持連線。"));
                    uint padding;
                    AudioDevices.Check(client.GetCurrentPadding(out padding));
                    if (written == total && padding == 0) break;
                    int count = Math.Min((int)(capacity - padding), total - written);
                    if (count > 0)
                    {
                        byte[] data = Generate(rate, channels, bits, tag == 3, sourceChannel, written, count, total);
                        IntPtr buffer;
                        AudioDevices.Check(render.GetBuffer((uint)count, out buffer));
                        try { Marshal.Copy(data, 0, buffer, data.Length); }
                        finally { AudioDevices.Check(render.ReleaseBuffer((uint)count, 0)); }
                        written += count;
                    }
                    if (!started) { AudioDevices.Check(client.Start()); started = true; }
                    Thread.Sleep(8);
                }
            }
            finally
            {
                if (started && client != null) client.Stop();
                AudioDevices.Release(render);
                AudioDevices.Release(client);
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                AudioDevices.Release(endpoint);
                AudioDevices.Release(enumerator);
            }
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object value);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid Format;
        public uint Id;
        public PropertyKey(string format, uint id) { Format = new Guid(format); Id = id; }
    }
    // PROPVARIANT contains a 16-byte union on x64 (including counted arrays).
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint flags, long duration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }
    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }
}
