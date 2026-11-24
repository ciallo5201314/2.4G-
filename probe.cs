// atk_probe.cs —— 探测 ATK/VXE 373B:1216 2.4G 接收器电量 (纯 Win32 API, 无第三方依赖)
// 协议: usage page 0xFF05, Report ID 0x08, 请求 "08 7D 72 02 00 01 07 01 00...00" (64B)
//       回复校验 resp[1]==0x72 && resp[5]==0x07, 电量 = resp[7]
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

class AtkProbe
{
    // ---- P/Invoke 外置声明区 ----
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTAPI { public int X; public int Y; }
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINTAPI lpPoint);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    // ---- P/Invoke ----
    static readonly Guid HID_GUID = new Guid(0x4d1e55b2, 0xf16f, 0x11cf, 0x88, 0xcb, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);

    const uint DIGCF_PRESENT = 0x02;
    const uint DIGCF_DEVICEINTERFACE = 0x10;
    const uint GENERIC_READ = 0x80000000;
    const uint GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 0x1;
    const uint FILE_SHARE_WRITE = 0x2;
    const uint OPEN_EXISTING = 3;
    const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    const uint INVALID_HANDLE = 0xFFFFFFFF;
    const uint ERROR_IO_PENDING = 997;
    const uint WAIT_OBJECT_0 = 0;
    const uint WAIT_TIMEOUT = 0x102;

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int cbSize;
        public short DevicePath; // 实际是 WCHAR[1], 手动取地址读字符串
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

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);
    [DllImport("setupapi.dll", SetLastError = true)]
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
    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, uint BufferLength);

    const uint HIDP_STATUS_SUCCESS = 0x00110000;

    [DllImport("hid.dll", SetLastError = true)]
    static extern void HidD_GetHidGuid(out Guid HidGuid);
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_SetOutputReport(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);

    // ---- 枚举 ----
    static List<string> EnumerateHidPaths()
    {
        var paths = new List<string>();
        Guid cls;
        HidD_GetHidGuid(out cls);
        IntPtr devs = SetupDiGetClassDevs(ref cls, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (devs == new IntPtr(-1) || devs == IntPtr.Zero)
        {
            Console.WriteLine("  SetupDiGetClassDevs 失败 err=" + Marshal.GetLastWin32Error());
            return paths;
        }
        try
        {
            uint idx = 0;
            int total = 0;
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
                    Marshal.WriteInt32(buf, 8); // cbSize = 8 (64 位)
                    uint ignored = 0;
                    if (SetupDiGetDeviceInterfaceDetail(devs, ref iface, buf, need, out ignored, IntPtr.Zero))
                    {
                        // dump 实测: DevicePath 紧跟 cbSize (偏移 4) 开始
                        string path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                        total++;
                        if (!string.IsNullOrEmpty(path)) paths.Add(path);
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            Console.WriteLine("  枚举到 " + total + " 个 HID 接口");
        }
        finally { SetupDiDestroyDeviceInfoList(devs); }
        return paths;
    }

    // ---- 探测接口属性 ----
    static bool Probe(string path, out ushort vid, out ushort pid, out ushort usagePage, out ushort inLen, out ushort outLen)
    {
        vid = pid = usagePage = inLen = outLen = 0;
        IntPtr h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == new IntPtr(-1) || h == IntPtr.Zero) return false;
        try
        {
            HIDD_ATTRIBUTES attrs;
            if (!HidD_GetAttributes(h, out attrs)) return false;
            vid = attrs.VendorID; pid = attrs.ProductID;
            IntPtr pp;
            if (!HidD_GetPreparsedData(h, out pp)) return true; // 属性拿到即可
            try
            {
                HIDP_CAPS caps;
                if (HidP_GetCaps(pp, out caps) == HIDP_STATUS_SUCCESS)
                {
                    usagePage = caps.UsagePage;
                    inLen = caps.InputReportByteLength;
                    outLen = caps.OutputReportByteLength;
                }
            }
            finally { HidD_FreePreparsedData(pp); }
            return true;
        }
        finally { CloseHandle(h); }
    }

    // ---- 发送请求读电量 ----
    static int? QueryBattery(string path)
    {
        IntPtr h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == new IntPtr(-1) || h == IntPtr.Zero)
        {
            Console.WriteLine("  写句柄打开失败 err=" + Marshal.GetLastWin32Error());
            return null;
        }
        IntPtr hRead = IntPtr.Zero, hEv = IntPtr.Zero;
        try
        {
            // 独立异步读句柄 (避免同句柄阻塞读+写崩溃)
            hRead = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (hRead == new IntPtr(-1) || hRead == IntPtr.Zero)
            {
                Console.WriteLine("  读句柄打开失败 err=" + Marshal.GetLastWin32Error());
                return null;
            }
            hEv = CreateEvent(IntPtr.Zero, true, true, null);

            int? last = null;
            // 先丢弃旧报文 (drain), 避免读到历史数据
            try
            {
                byte[] dbuf = new byte[64];
                uint dgot;
                var dov = new OVERLAPPED();
                dov.hEvent = hEv;
                ResetEvent(hEv);
                if (ReadFile(hRead, dbuf, 64, out dgot, ref dov))
                {
                    // 有旧数据, 丢弃
                }
                else
                {
                    uint derr = (uint)Marshal.GetLastWin32Error();
                    if (derr == ERROR_IO_PENDING)
                    {
                        if (WaitForSingleObject(hEv, 50) == WAIT_OBJECT_0)
                        {
                            GetOverlappedResult(hRead, ref dov, out dgot, false);
                            // 丢弃
                        }
                        else CancelIoEx(hRead, ref dov);
                    }
                }
            }
            catch { }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                // 请求报文: 08 7D 72 02 00 01 07 01 + 补 0 到 64 字节
                byte[] req = new byte[64];
                req[0] = 0x08; req[1] = 0x7D; req[2] = 0x72; req[3] = 0x02;
                req[4] = 0x00; req[5] = 0x01; req[6] = 0x07; req[7] = 0x01;
                // 方式A: HidD_SetOutputReport (README 推荐, 首字节=Report ID 0x08)
                bool sent = HidD_SetOutputReport(h, req, 64);
                if (!sent)
                {
                    // 方式B: WriteFile 兜底
                    uint written;
                    if (!WriteFile(h, req, 64, out written, IntPtr.Zero))
                    {
                        Console.WriteLine("  发送失败(err=" + Marshal.GetLastWin32Error() + ") 第" + (attempt + 1) + "次");
                        return last;
                    }
                }

                // 异步读, 超时 500ms
                byte[] buf = new byte[64];
                var ov = new OVERLAPPED();
                ov.hEvent = hEv;
                ResetEvent(hEv);
                uint got;
                bool ok = ReadFile(hRead, buf, 64, out got, ref ov);
                if (!ok)
                {
                    uint err = (uint)Marshal.GetLastWin32Error();
                    if (err != ERROR_IO_PENDING)
                    {
                        Console.WriteLine("  读启动失败 err=" + err);
                        return last;
                    }
                    uint r = WaitForSingleObject(hEv, 500);
                    if (r == WAIT_OBJECT_0)
                    {
                        if (!GetOverlappedResult(hRead, ref ov, out got, false))
                        {
                            Console.WriteLine("  读结果获取失败 (第" + (attempt + 1) + "次, 设备可能休眠)");
                            last = null; continue;
                        }
                    }
                    else
                    {
                        CancelIoEx(hRead, ref ov);
                        // 超时: 多半是鼠标休眠, 提示动一下
                        if (attempt == 0) Console.WriteLine("  读超时 (设备可能在休眠, 动一下鼠标再试)");
                        last = null; continue;
                    }
                }
                Console.Write("  回复 (" + got + "B): " + Hex(buf, (int)Math.Min(got, 16)));
                if (got >= 8 && buf[1] == 0x72 && buf[5] == 0x07)
                {
                    int lvl = buf[7];
                    Console.WriteLine("  => 电量=" + lvl + "%");
                    return lvl;
                }
                Console.WriteLine("  (校验不符 resp[1]=" + buf[1].ToString("X2") + " resp[5]=" + buf[5].ToString("X2") + ")");
                last = null;
            }
            return last;
        }
        finally
        {
            if (hEv != IntPtr.Zero) CloseHandle(hEv);
            if (hRead != IntPtr.Zero && hRead != new IntPtr(-1)) CloseHandle(hRead);
            CloseHandle(h);
        }
    }

    static string Hex(byte[] b, int n)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.Append(b[i].ToString("X2")).Append(' ');
        return sb.ToString();
    }

    // ---- 监听输入 ----
    static void MonitorInput(string path, int ms)
    {
        IntPtr h = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (h == new IntPtr(-1) || h == IntPtr.Zero)
        {
            Console.WriteLine("  监听句柄失败 err=" + Marshal.GetLastWin32Error());
            return;
        }
        IntPtr hEv = CreateEvent(IntPtr.Zero, true, false, null);
        int gotCount = 0;
        long t0 = Environment.TickCount;
        try
        {
            while (Environment.TickCount - t0 < ms)
            {
                byte[] buf = new byte[64];
                var ov = new OVERLAPPED();
                ov.hEvent = hEv;
                ResetEvent(hEv);
                uint got;
                if (!ReadFile(h, buf, 64, out got, ref ov))
                {
                    uint err = (uint)Marshal.GetLastWin32Error();
                    if (err != ERROR_IO_PENDING) break;
                    int wm = Math.Max(50, ms - (int)(Environment.TickCount - t0));
                    if (WaitForSingleObject(hEv, (uint)wm) == WAIT_OBJECT_0)
                    {
                        if (GetOverlappedResult(h, ref ov, out got, false))
                        {
                            gotCount++;
                            if (gotCount <= 5) Console.WriteLine("  输入 " + got + "B: " + Hex(buf, (int)Math.Min(got, 16)));
                        }
                    }
                    else CancelIoEx(h, ref ov);
                }
                else
                {
                    gotCount++;
                    if (gotCount <= 5) Console.WriteLine("  输入 " + got + "B: " + Hex(buf, (int)Math.Min(got, 16)));
                }
            }
            Console.WriteLine("  监听结束: 共收到 " + gotCount + " 条输入");
        }
        finally
        {
            if (hEv != IntPtr.Zero) CloseHandle(hEv);
            if (h != IntPtr.Zero && h != new IntPtr(-1)) CloseHandle(h);
        }
    }

    static int Main()
    {
        Console.WriteLine("== ATK 373B:1216 电量探测 ==");
        // 唤醒鼠标: 移动 1px (外设休眠时收不到回复)
        POINTAPI pt;
        GetCursorPos(out pt);
        SetCursorPos(pt.X + 1, pt.Y);
        Console.WriteLine("已尝试移动鼠标唤醒 (休眠时请手动动一下)");
        int found = 0, probed = 0;
        string target = null;
        foreach (string p in EnumerateHidPaths())
        {
            ushort vid, pid, up, il, ol;
            bool ok = Probe(p, out vid, out pid, out up, out il, out ol);
            if (!ok)
            {
                Console.WriteLine("  探测失败: " + p);
                continue;
            }
            probed++;
            Console.WriteLine("  VID=0x{0:X4} PID=0x{1:X4} UP=0x{2:X4} In={3} Out={4}", vid, pid, up, il, ol);
            if (vid == 0x373B && pid == 0x1216)
            {
                found++;
                if (up == 0xFF05 && ol == 64) target = p;
            }
        }
        Console.WriteLine("  可探测 " + probed + " 个, 373B:1216 共 " + found + " 个");
        if (target == null)
        {
            Console.WriteLine("没找到 373B:1216 + UsagePage 0xFF05 + Out=64 的接口");
            return 1;
        }
        Console.WriteLine("目标接口: " + target);
        // 监听模式: 连读 5 秒, 看鼠标是否有流量 (有=活跃, 无=休眠)
        Console.WriteLine("监听输入 5 秒 (请移动鼠标)...");
        MonitorInput(target, 5000);
        int? lvl = QueryBattery(target);
        Console.WriteLine(lvl.HasValue ? "结果: " + lvl.Value + "%" : "结果: 读不到");
        return lvl.HasValue ? 0 : 2;
    }
}