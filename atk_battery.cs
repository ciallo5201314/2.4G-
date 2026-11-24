// atk_battery.cs —— 2.4G 无线鼠标电量托盘小软件 (支持自由添加设备)
// 内置协议: ATK/VXE (373B:1216 FF05 协议2 / FF02 协议1), VXE 3554, Endgame 3367,
//           VAXEE 3057, Nordic 1915。设置界面可枚举已连接 2.4G 接收器并一键连接。
// 纯 Win32 API + WinForms 托盘, 无需第三方依赖
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class AtkBatteryApp : Form
{
    // ---- 常量 ----
    const uint DIGCF_PRESENT = 0x02;
    const uint DIGCF_DEVICEINTERFACE = 0x10;
    const uint GENERIC_READ = 0x80000000;
    const uint GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 0x1;
    const uint FILE_SHARE_WRITE = 0x2;
    const uint OPEN_EXISTING = 3;
    const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    const uint ERROR_IO_PENDING = 997;
    const uint WAIT_OBJECT_0 = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid", SetLastError = true)]
    static extern void HidD_GetHidGuid(out Guid Guid);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, ref OVERLAPPED lpOverlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetOverlappedResult(IntPtr hFile, ref OVERLAPPED lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CancelIoEx(IntPtr hFile, ref OVERLAPPED lpOverlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ResetEvent(IntPtr hEvent);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, out HIDD_ATTRIBUTES Attributes);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);
    [DllImport("hid.dll", SetLastError = true)]
    static extern uint HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_SetOutputReport(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_SetFeature(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetFeature(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);
    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, int BufferLength);

    const uint HIDP_STATUS_SUCCESS = 0x00110000;

    // ---- 多设备配置 (注册表持久化) ----
    static readonly string CFG_KEY = @"Software\ATKBattery";
    static int CfgMinutes = 1;
    static int CfgDisplayIdx = 0;

    public class DeviceEntry
    {
        public int Vid, Pid, UsagePage;
        public string Proto = "", Product = "";
        public int Level = -1;
        public string Err = "";
        public string Key { get { return Vid.ToString("X4") + ":" + Pid.ToString("X4") + ":" + UsagePage.ToString("X4"); } }
    }

    static List<DeviceEntry> Devices = new List<DeviceEntry>();

    public static List<DeviceEntry> GetDevices() { return Devices; }

    public static DeviceEntry DisplayDevice()
    {
        if (Devices.Count == 0) return null;
        if (CfgDisplayIdx < 0 || CfgDisplayIdx >= Devices.Count) CfgDisplayIdx = 0;
        return Devices[CfgDisplayIdx];
    }

    public static int GetDisplayIdx() { return CfgDisplayIdx; }

    public static void SetDisplayIdx(int idx)
    {
        if (idx < 0 || idx >= Devices.Count) return;
        CfgDisplayIdx = idx;
        SaveDevices(null);
        if (Instance != null) Instance.RefreshNow();
    }

    public static int LastLevel
    {
        get { var d = DisplayDevice(); return d == null ? -1 : d.Level; }
    }
    public static string LastErr
    {
        get { var d = DisplayDevice(); return d == null ? "" : d.Err; }
    }

    public static int GetRefreshMinutes()
    {
        return CfgMinutes;
    }

    public static void SetRefreshMinutes(int minutes)
    {
        if (minutes < 1) minutes = 1;
        if (minutes > 60) minutes = 60;
        CfgMinutes = minutes;
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CFG_KEY))
            {
                key.SetValue("Minutes", minutes);
            }
        }
        catch { }
        if (Instance != null) Instance.ApplyInterval();
    }

    // ---- 成员 ----
    NotifyIcon tray;
    ContextMenuStrip menu;
    ToolStripMenuItem miRefresh, miSettings, miExit;
    System.Windows.Forms.Timer timer;
    bool autostart = false;
    SettingsForm settingsWin = null;

    static readonly string AUTOSTART_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static readonly string AUTOSTART_NAME = "无线设备电量";

    public static string CurrentDeviceLabel()
    {
        var d = DisplayDevice();
        if (d == null) return "未连接 (添加设备自动连接默认 ATK)";
        return d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4") + " [" + d.Proto + "]";
    }

    public static string CurrentDeviceName()
    {
        var d = DisplayDevice();
        if (d == null) return CurrentDeviceLabel();
        return d.Product != "" ? d.Product : CurrentDeviceLabel();
    }

    static void LoadConfig()
    {
        Devices.Clear();
        CfgDisplayIdx = 0;
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CFG_KEY))
            {
                if (key == null) return;
                CfgMinutes = Convert.ToInt32(key.GetValue("Minutes", 1));
                if (CfgMinutes < 1) CfgMinutes = 1;
                if (CfgMinutes > 60) CfgMinutes = 60;
            }
        }
        catch { }
        // 设备列表: Dev0..DevN 字符串 "vid,pid,usagepage,proto,product"
        for (int i = 0; ; i++)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CFG_KEY))
                {
                    if (key == null) break;
                    object v = key.GetValue("Dev" + i);
                    if (v == null) break;
                    string[] parts = v.ToString().Split(',');
                    if (parts.Length < 5) { break; }
                    var e = new DeviceEntry();
                    e.Vid = Convert.ToInt32(parts[0]);
                    e.Pid = Convert.ToInt32(parts[1]);
                    e.UsagePage = Convert.ToInt32(parts[2]);
                    e.Proto = parts[3];
                    e.Product = parts[4];
                    Devices.Add(e);
                }
            }
            catch { break; }
        }
        // 兼容旧版单设备配置: 无 Dev0 但有 Vid 键 → 迁移
        try
        {
            if (Devices.Count == 0)
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CFG_KEY))
                {
                    if (key != null && Convert.ToInt32(key.GetValue("Vid", 0)) != 0)
                    {
                        var e = new DeviceEntry();
                        e.Vid = Convert.ToInt32(key.GetValue("Vid", 0));
                        e.Pid = Convert.ToInt32(key.GetValue("Pid", 0));
                        e.UsagePage = Convert.ToInt32(key.GetValue("UsagePage", 0));
                        e.Proto = Convert.ToString(key.GetValue("Proto", ""));
                        e.Product = Convert.ToString(key.GetValue("Product", ""));
                        Devices.Add(e);
                        SaveDevices(e);
                    }
                }
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CFG_KEY))
                    {
                        key.DeleteValue("Vid", false);
                        key.DeleteValue("Pid", false);
                        key.DeleteValue("UsagePage", false);
                        key.DeleteValue("Proto", false);
                        key.DeleteValue("Product", false);
                    }
                }
                catch { }
            }
        }
        catch { }
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CFG_KEY))
            {
                if (key != null) CfgDisplayIdx = Convert.ToInt32(key.GetValue("DisplayIdx", 0));
                if (CfgDisplayIdx < 0) CfgDisplayIdx = 0;
                if (Devices.Count > 0 && CfgDisplayIdx >= Devices.Count) CfgDisplayIdx = 0;
            }
        }
        catch { }
    }

    static void SaveDevices(DeviceEntry dontTouchLevels)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CFG_KEY))
            {
                for (int i = 0; i < Devices.Count; i++)
                {
                    var e = Devices[i];
                    key.SetValue("Dev" + i, e.Vid + "," + e.Pid + "," + e.UsagePage + "," + e.Proto + "," + e.Product);
                }
                key.SetValue("DisplayIdx", CfgDisplayIdx);
            }
        }
        catch { }
    }

    // 添加/更新设备配置 (已存在同 VID:PID:UsagePage 则更新协议), 返回条目
    public static DeviceEntry AddDevice(int vid, int pid, int up, string proto, string product)
    {
        foreach (var e in Devices)
        {
            if (e.Vid == vid && e.Pid == pid && e.UsagePage == up)
            {
                e.Proto = proto;
                e.Product = product == null ? "" : product;
                SaveDevices(e);
                return e;
            }
        }
        var n = new DeviceEntry();
        n.Vid = vid; n.Pid = pid; n.UsagePage = up; n.Proto = proto;
        n.Product = product == null ? "" : product;
        Devices.Add(n);
        SaveDevices(n);
        return n;
    }

    public static void RemoveDevice(DeviceEntry e)
    {
        Devices.Remove(e);
        if (CfgDisplayIdx >= Devices.Count) CfgDisplayIdx = Devices.Count - 1;
        if (CfgDisplayIdx < 0) CfgDisplayIdx = 0;
        SaveDevices(null);
        if (Instance != null) Instance.RefreshNow();
    }

    public static void ClearConfig()
    {
        Devices.Clear();
        CfgDisplayIdx = 0;
        SaveDevices(null);
        if (Instance != null) Instance.RefreshNow();
    }

    public static AtkBatteryApp Instance = null;

    // ================= 设备枚举 =================
    public static List<HidInfo> EnumerateDevices()
    {
        var list = new List<HidInfo>();
        Guid cls;
        HidD_GetHidGuid(out cls);
        IntPtr devs = SetupDiGetClassDevs(ref cls, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (devs == new IntPtr(-1) || devs == IntPtr.Zero) return list;
        try
        {
            uint idx = 0;
            while (true)
            {
                var iface = new SP_DEVICE_INTERFACE_DATA();
                iface.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                if (!SetupDiEnumDeviceInterfaces(devs, IntPtr.Zero, ref cls, idx, ref iface)) break;
                idx++;
                uint need = 0;
                SetupDiGetDeviceInterfaceDetail(devs, ref iface, IntPtr.Zero, 0, out need, IntPtr.Zero);
                if (need == 0) continue;
                IntPtr buf = Marshal.AllocHGlobal((int)need);
                try
                {
                    Marshal.WriteInt32(buf, 8);
                    uint ignored = 0;
                    if (!SetupDiGetDeviceInterfaceDetail(devs, ref iface, buf, need, out ignored, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                    if (string.IsNullOrEmpty(path)) continue;
                    IntPtr h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (h == new IntPtr(-1) || h == IntPtr.Zero) continue;
                    try
                    {
                        HIDD_ATTRIBUTES attrs;
                        if (!HidD_GetAttributes(h, out attrs)) continue;
                        var info = new HidInfo();
                        info.Path = path;
                        info.Vid = attrs.VendorID;
                        info.Pid = attrs.ProductID;
                        StringBuilder sb = new StringBuilder(256);
                        if (HidD_GetProductString(h, sb, 256)) info.Product = sb.ToString();
                        IntPtr pp;
                        if (HidD_GetPreparsedData(h, out pp))
                        {
                            try
                            {
                                HIDP_CAPS caps;
                                if (HidP_GetCaps(pp, out caps) == HIDP_STATUS_SUCCESS)
                                {
                                    info.UsagePage = caps.UsagePage;
                                    info.Usage = caps.Usage;
                                    info.InLen = caps.InputReportByteLength;
                                    info.OutLen = caps.OutputReportByteLength;
                                    info.FeatLen = caps.FeatureReportByteLength;
                                }
                            }
                            finally { HidD_FreePreparsedData(pp); }
                        }
                        list.Add(info);
                    }
                    finally { CloseHandle(h); }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(devs); }
        return list;
    }

    // ================= 内置协议表 =================
    static List<ProtoSpec> BuiltinProtocols()
    {
        var list = new List<ProtoSpec>();
        // ATK / VXE (373B) 协议2: FF05, Rid 08, 64B output
        list.Add(new ProtoSpec { Name = "ATK协议2", Vid = 0x373B, UsagePage = 0xFF05, Rid = 0x08,
            Payload = new byte[] { 0x7D, 0x72, 0x02, 0x00, 0x01, 0x07, 0x01 }, Length = 64,
            LevelOffset = 7, WaitMs = 100, Scale = 1, LevelMax = 100, Mode = "output",
            ExpectIdx = new int[] { 1, 5 }, ExpectVal = new byte[] { 0x72, 0x07 }, Retries = 3 });
        // ATK / VXE (373B) 协议1: FF02, Rid 08, 17B output
        list.Add(new ProtoSpec { Name = "ATK协议1", Vid = 0x373B, UsagePage = 0xFF02, Rid = 0x08,
            Payload = BuildPayload(new byte[] { 0x04 }, 15, 0x49), Length = 17,
            LevelOffset = 6, WaitMs = 100, Scale = 1, LevelMax = 100, Mode = "output",
            ExpectIdx = new int[] { }, ExpectVal = new byte[] { }, Retries = 3 });
        // VXE / VGN (3554) 协议1: FF02
        list.Add(new ProtoSpec { Name = "VXE协议1", Vid = 0x3554, UsagePage = 0xFF02, Rid = 0x08,
            Payload = BuildPayload(new byte[] { 0x04 }, 15, 0x49), Length = 17,
            LevelOffset = 6, WaitMs = 100, Scale = 1, LevelMax = 100, Mode = "output",
            ExpectIdx = new int[] { }, ExpectVal = new byte[] { }, Retries = 3 });
        // Endgame (3367): FF01, Rid A1, 64B feature, 连读两次第一次唤醒
        list.Add(new ProtoSpec { Name = "Endgame", Vid = 0x3367, UsagePage = 0xFF01, Rid = 0xA1,
            Payload = new byte[] { 0xB4 }, Length = 64,
            LevelOffset = 16, WaitMs = 350, Scale = 1, LevelMax = -1, Mode = "feature",
            ExpectIdx = new int[] { 1 }, ExpectVal = new byte[] { 0x01 }, Retries = 3 });
        // VAXEE (3057): FF05, Rid 0E, 64B feature, scale 5
        list.Add(new ProtoSpec { Name = "VAXEE", Vid = 0x3057, UsagePage = 0xFF05, Rid = 0x0E,
            Payload = new byte[] { 0xA5, 0x0B, 0x01, 0x01 }, Length = 64,
            LevelOffset = 5, WaitMs = 120, Scale = 5, LevelMax = 20, Mode = "feature",
            ExpectIdx = new int[] { }, ExpectVal = new byte[] { }, Retries = 2 });
        // Nordic (1915): FFA0, Rid 05, 32B feature
        list.Add(new ProtoSpec { Name = "Nordic", Vid = 0x1915, UsagePage = 0xFFA0, Rid = 0x05,
            Payload = new byte[] { 0x15, 0x00, 0x00, 0x01 }, Length = 32,
            LevelOffset = 9, WaitMs = 90, Scale = 1, LevelMax = -1, Mode = "feature",
            ExpectIdx = new int[] { }, ExpectVal = new byte[] { }, Retries = 2 });
        return list;
    }

    static byte[] BuildPayload(byte[] head, int zeros, byte tail)
    {
        byte[] p = new byte[head.Length + zeros + 1];
        Array.Copy(head, p, head.Length);
        p[p.Length - 1] = tail;
        return p;
    }

    public static ProtoSpec FindSpec(int vid, int usagePage)
    {
        foreach (var s in BuiltinProtocols())
        {
            if (s.Vid == vid && s.UsagePage == usagePage) return s;
        }
        return null;
    }

    static ProtoSpec FindSpecByName(string name)
    {
        foreach (var s in BuiltinProtocols())
        {
            if (s.Name == name) return s;
        }
        return null;
    }

    // ================= 连接 + 读电量 =================
    // 用指定接口 + 协议读电量, 返回 -1 失败
    static int ReadLevelBySpec(string path, ProtoSpec spec, out string err)
    {
        err = "";
        if (spec == null) { err = "无匹配协议"; return -1; }
        IntPtr h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == new IntPtr(-1) || h == IntPtr.Zero)
        {
            err = "打开失败 (err=" + Marshal.GetLastWin32Error() + ")";
            return -1;
        }
        IntPtr hRead = IntPtr.Zero, hEv = IntPtr.Zero;
        try
        {
            bool isFeature = spec.Mode == "feature";
            if (!isFeature)
            {
                hRead = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                if (hRead == new IntPtr(-1) || hRead == IntPtr.Zero)
                {
                    err = "读句柄失败 (err=" + Marshal.GetLastWin32Error() + ")";
                    return -1;
                }
                hEv = CreateEvent(IntPtr.Zero, true, true, null);
            }
            byte[] req = new byte[spec.Length];
            req[0] = spec.Rid;
            for (int i = 0; i < spec.Payload.Length && i + 1 < spec.Length; i++) req[i + 1] = spec.Payload[i];
            int attempts = spec.Retries < 1 ? 1 : spec.Retries + 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                bool sent = false;
                if (isFeature)
                {
                    sent = HidD_SetFeature(h, req, (uint)spec.Length);
                }
                else
                {
                    sent = HidD_SetOutputReport(h, req, (uint)spec.Length);
                    if (!sent)
                    {
                        uint written;
                        sent = WriteFile(h, req, (uint)spec.Length, out written, IntPtr.Zero);
                    }
                }
                if (!sent)
                {
                    err = "发送失败 (err=" + Marshal.GetLastWin32Error() + ")";
                    continue;
                }
                System.Threading.Thread.Sleep(spec.WaitMs);
                byte[] buf = new byte[spec.Length + 2];
                uint got = 0;
                bool gotData = false;
                if (isFeature)
                {
                    buf[0] = spec.Rid;
                    if (HidD_GetFeature(h, buf, (uint)spec.Length))
                    {
                        got = (uint)spec.Length;
                        gotData = true;
                    }
                }
                else
                {
                    var ov = new OVERLAPPED();
                    ov.hEvent = hEv;
                    ResetEvent(hEv);
                    bool ok = ReadFile(hRead, buf, (uint)spec.Length, out got, ref ov);
                    if (!ok)
                    {
                        uint errc = (uint)Marshal.GetLastWin32Error();
                        if (errc != ERROR_IO_PENDING) { err = "读失败 (" + errc + ")"; continue; }
                        if (WaitForSingleObject(hEv, 400) != WAIT_OBJECT_0)
                        {
                            CancelIoEx(hRead, ref ov);
                            continue;
                        }
                        gotData = GetOverlappedResult(hRead, ref ov, out got, false);
                    }
                    else gotData = true;
                }
                if (!gotData || got < spec.LevelOffset + 1) continue;
                // expect 校验
                bool match = true;
                for (int i = 0; i < spec.ExpectIdx.Length; i++)
                {
                    if (buf[spec.ExpectIdx[i]] != spec.ExpectVal[i]) { match = false; break; }
                }
                if (!match) continue;
                int raw = buf[spec.LevelOffset];
                if (spec.LevelMax >= 0 && raw > spec.LevelMax) continue;
                int lvl = raw * spec.Scale;
                if (lvl > 100) lvl = 100;
                return lvl;
            }
            err = "读超时 (设备休眠或无响应)";
            return -1;
        }
        finally
        {
            if (hEv != IntPtr.Zero) CloseHandle(hEv);
            if (hRead != IntPtr.Zero && hRead != new IntPtr(-1)) CloseHandle(hRead);
            CloseHandle(h);
        }
    }

    // 一键连接: 按设备 vid+usagePage 匹配协议, 读电量, 成功加入设备列表并设为显示设备
    public static int ConnectAndRead(HidInfo d, out string err)
    {
        err = "";
        ProtoSpec spec = FindSpec(d.Vid, d.UsagePage);
        if (spec == null)
        {
            err = "该设备无内置协议 (VID=" + d.Vid.ToString("X4") + " UP=" + d.UsagePage.ToString("X4") + ")";
            return -1;
        }
        int lvl = ReadLevelBySpec(d.Path, spec, out err);
        if (lvl >= 0)
        {
            DeviceEntry e = AddDevice(d.Vid, d.Pid, d.UsagePage, spec.Name, d.Product);
            int idx = Devices.IndexOf(e);
            if (idx >= 0) SetDisplayIdx(idx);
            if (Instance != null) Instance.RefreshNow();
        }
        return lvl;
    }

    // 按配置条目找设备路径 (用于托盘轮询)
    static string FindPathFor(DeviceEntry e, out ProtoSpec spec)
    {
        spec = null;
        var list = EnumerateDevices();
        foreach (var d in list)
        {
            if (d.Vid == e.Vid && d.Pid == e.Pid && d.UsagePage == e.UsagePage)
            {
                spec = FindSpecByName(e.Proto);
                if (spec == null) spec = FindSpec(d.Vid, d.UsagePage);
                return d.Path;
            }
        }
        return null;
    }

    // ================= 托盘刷新 (遍历全部设备) =================
    void RefreshNow()
    {
        Thread th = new Thread(() =>
        {
            // 设备列表为空: 自动添加默认 ATK 373B:1216
            if (Devices.Count == 0)
            {
                foreach (var d in EnumerateDevices())
                {
                    if (d.Vid == 0x373B && d.Pid == 0x1216)
                    {
                        ProtoSpec spec = FindSpec(d.Vid, d.UsagePage);
                        if (spec != null)
                        {
                            AddDevice(d.Vid, d.Pid, d.UsagePage, spec.Name, d.Product);
                            break;
                        }
                    }
                }
            }
            foreach (var e in Devices)
            {
                ProtoSpec spec;
                string path = FindPathFor(e, out spec);
                string err = "";
                if (path == null || spec == null)
                {
                    err = "未找到设备 (重新插拔接收器)";
                    e.Level = -1;
                    e.Err = err;
                }
                else
                {
                    e.Level = ReadLevelBySpec(path, spec, out err);
                    e.Err = err;
                }
            }
            this.Invoke((Action)(() => ApplyAll()));
        });
        th.IsBackground = true;
        th.Start();
    }

    static string BrandName(int vid)
    {
        switch (vid)
        {
            case 0x373B: return "ATK";
            case 0x3554: return "VXE";
            case 0x3367: return "Endgame";
            case 0x3057: return "VAXEE";
            case 0x1915: return "Nordic";
            default: return "2.4G";
        }
    }

    void ApplyAll()
    {
        var d = DisplayDevice();
        int lvl = d == null ? -1 : d.Level;
        // 悬停提示: 每设备一行 "名称: 电量%"
        string tip = "";
        foreach (var e in Devices)
        {
            string name = e.Product != "" ? e.Product : BrandName(e.Vid) + " 鼠标";
            if (name.Length > 12) name = name.Substring(0, 12);
            tip += name + ": " + (e.Level >= 0 ? e.Level + "%" : "?") + "\n";
        }
        tip = tip.TrimEnd('\n');
        if (tip.Length > 60) tip = tip.Substring(0, 60);
        tray.Text = tip.Length > 0 ? tip : "无线设备电量";
        tray.Icon = MakeIcon(lvl, true);
    }

    // 圆角菜单渲染器: 按系统深浅色模式取自定义色板 (SystemColors 在 Win11 深色下不跟随)
    class RoundedMenuRenderer : ToolStripProfessionalRenderer
    {
        bool dark;
        public RoundedMenuRenderer(bool dark) { this.dark = dark; }

        Color Bg { get { return dark ? Color.FromArgb(0x28, 0x28, 0x28) : SystemColors.Menu; } }
        Color Txt { get { return dark ? Color.FromArgb(0xE8, 0xE8, 0xE8) : SystemColors.MenuText; } }
        Color SelBg { get { return dark ? Color.FromArgb(0x3D, 0x3D, 0x46) : SystemColors.MenuHighlight; } }
        Color Bdr { get { return dark ? Color.FromArgb(0x4C, 0x4C, 0x52) : SystemColors.ControlDark; } }
        Color Sep { get { return dark ? Color.FromArgb(0x3A, 0x3A, 0x40) : SystemColors.ControlDark; } }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Rectangle r = e.AffectedBounds;
            using (SolidBrush b = new SolidBrush(Bg))
            {
                e.Graphics.FillRectangle(b, r);             // 四角先铺菜单底色
            }
            using (GraphicsPath p = RoundedRect(r, 10))
            using (SolidBrush b = new SolidBrush(Bg))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(b, p);                  // 圆角内再画一遍, 覆盖尖角
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = new Rectangle(1, 1, e.AffectedBounds.Width - 2, e.AffectedBounds.Height - 2);
            using (GraphicsPath p = RoundedRect(r, 10))
            using (Pen pen = new Pen(Bdr))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, p);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Txt;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Item.ForeColor = Txt;
            if (e.Item.Selected && e.Item.Enabled)
            {
                using (SolidBrush b = new SolidBrush(SelBg))
                {
                    e.Graphics.FillRectangle(b, new Rectangle(1, 1, e.Item.Width - 2, e.Item.Height - 2));
                }
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen p = new Pen(Sep))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(p, 12, y, e.Item.Width - 12, y);
            }
        }
    }

    static bool IsDarkModeNow()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key == null) return false;
                var v = key.GetValue("AppsUseLightTheme");
                return v != null && v is int && (int)v == 0;
            }
        }
        catch { return false; }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    static void RoundMenuCorners(ContextMenuStrip m)
    {
        IntPtr r = CreateRoundRectRgn(0, 0, m.Width + 1, m.Height + 1, 10, 10);
        if (r != IntPtr.Zero)
        {
            SetWindowRgn(m.Handle, r, true);
        }
    }

    // ---- 构造 ----
    public AtkBatteryApp(bool openSettingsAfterShown = false)
    {
        if (openSettingsAfterShown) Shown += (s, e) => OpenSettings();
        Instance = this;
        LoadConfig();
        Text = "无线设备电量";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;

        tray = new NotifyIcon();
        tray.Icon = MakeIcon(50, false);
        tray.Text = "无线设备电量: 刷新中...";
        tray.Visible = true;
        tray.DoubleClick += (s, e) => RefreshNow();

        menu = new ContextMenuStrip();
        menu.ShowImageMargin = false;
        miRefresh = new ToolStripMenuItem("立即刷新", null, (s, e) => RefreshNow());
        miSettings = new ToolStripMenuItem("设置", null, (s, e) => OpenSettings());
        miExit = new ToolStripMenuItem("退出", null, (s, e) => { tray.Visible = false; Application.Exit(); });
        menu.Items.Add(miRefresh);
        menu.Items.Add(miSettings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(miExit);
        tray.ContextMenuStrip = menu;
        menu.Renderer = new RoundedMenuRenderer(IsDarkModeNow());
        menu.DropShadowEnabled = false;
        menu.VisibleChanged += (s, e) => { if (menu.Visible) RoundMenuCorners(menu); };

        timer = new System.Windows.Forms.Timer();
        ApplyInterval();
        timer.Tick += (s, e) => RefreshNow();
        timer.Start();

        autostart = CheckAutostart();
        RefreshNow();
    }

    void ApplyInterval()
    {
        timer.Stop();
        timer.Interval = CfgMinutes * 60000;
        timer.Start();
    }

    public void OpenSettings()
    {
        if (settingsWin != null && !settingsWin.IsDisposed)
        {
            settingsWin.Activate();
            return;
        }
        settingsWin = new SettingsForm();
        settingsWin.Show();
    }

    bool CheckAutostart()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AUTOSTART_KEY))
            {
                if (key == null) return false;
                var v = key.GetValue(AUTOSTART_NAME);
                return v != null && v.ToString() == Application.ExecutablePath;
            }
        }
        catch { return false; }
    }

    public bool IsAutostartOn()
    {
        return autostart;
    }

    public void SetAutostart(bool on)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AUTOSTART_KEY))
            {
                key.DeleteValue("ATK鼠标电量", false);
                if (!on) key.DeleteValue(AUTOSTART_NAME, false);
                else key.SetValue(AUTOSTART_NAME, Application.ExecutablePath);
            }
        }
        catch { }
        autostart = on;
    }

    void ToggleAutostart()
    {
        SetAutostart(!autostart);
    }

    // ---- 画图标: 竖放电池, 绿色电量从下往上填充, 其余透明 ----
    static Icon MakeIcon(int level, bool final)
    {
        int size = 64;
        Bitmap bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            Rectangle body = new Rectangle(14, 10, 36, 50);
            Rectangle cap = new Rectangle(26, 2, 12, 8);
            int innerH = body.Height - 8;
            int fillH = level < 0 ? 0 : (int)Math.Round(innerH * level / 100.0);
            if (fillH < 2 && level > 0) fillH = 2;
            int fillTop = body.Bottom - 4 - fillH;
            using (Pen pen = new Pen(Color.White, 4f))
            using (GraphicsPath path = RoundedRect(body, 6))
            {
                g.DrawPath(pen, path);
            }
            using (SolidBrush cb = new SolidBrush(Color.White))
            {
                g.FillRectangle(cb, cap);
            }
            if (fillH > 0)
            {
                Rectangle fillRect = new Rectangle(body.X + 4, fillTop, body.Width - 8, fillH);
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(93, 220, 138)))
                using (GraphicsPath fp = RoundedRect(fillRect, 3))
                {
                    g.FillPath(fb, fp);
                }
            }
        }
        IntPtr hbmColor = bmp.GetHbitmap();
        IntPtr hbmMask = CreateBitmap(1, 1, 1, 1, new byte[] { 0 });
        try
        {
            ICONINFO ii = new ICONINFO();
            ii.fIcon = true;
            ii.hbmColor = hbmColor;
            ii.hbmMask = hbmMask;
            IntPtr hIcon = CreateIconIndirect(ref ii);
            if (hIcon == IntPtr.Zero) hIcon = bmp.GetHicon();
            Icon ico = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            return ico;
        }
        finally
        {
            DeleteObject(hbmColor);
            DeleteObject(hbmMask);
            bmp.Dispose();
        }
    }

    static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        GraphicsPath p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO
    {
        public bool fIcon;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateIconIndirect(ref ICONINFO iconInfo);
    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, byte[] lpvBits);
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    // Win11 深色模式: 让系统级控件(表头/滚动条/下拉等)跟随深色
    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int SetPreferredAppMode(int mode);   // 1 = AllowDark
    [DllImport("uxtheme.dll", EntryPoint = "#136", CharSet = CharSet.Unicode)]
    static extern void FlushMenuThemes();

    [STAThread]
    static void Main(string[] args)
    {
        bool created;
        using (var mutex = new Mutex(true, "ATK_Battery_373B1216", out created))
        {
            if (!created)
            {
                if (args.Length > 0 && args[0] == "--settings")
                {
                    // 已运行实例: 发送消息打开设置? 简化: 只提示
                    MessageBox.Show("无线设备电量已在运行 (托盘区)", "无线设备电量", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            if (IsDarkModeNow())
            {
                try { SetPreferredAppMode(1); FlushMenuThemes(); } catch { }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AtkBatteryApp app = new AtkBatteryApp(args.Length > 0 && args[0] == "--settings");
            Application.Run(app);
        }
    }
}