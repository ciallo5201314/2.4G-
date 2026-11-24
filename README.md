# 无线设备电量 (2.4G Battery Viewer)

Windows 托盘小工具:通过 HID 读取 2.4G 无线鼠标/设备的实时电量,托盘图标以电池样式显示,支持多设备、设置界面、开机自启。

## 功能

- 托盘电池图标:绿色按电量从下往上填充(≥60% 绿 / 30-59% 黄 / <30% 红,未知显示 "?")
- 悬停提示:列出所有已连接设备的电量
- 一键连接:自动枚举 HID 接口,匹配内置协议读取电量
- 设置界面(托盘右键 → 设置):三个分类页
  - **连接**:列出所有 2.4G HID 设备,一键连接/移除
  - **设备**:当前显示设备的名称、VID:PID、协议、电量
  - **设置**:开机自启、刷新间隔(1-60 分钟)、托盘电池显示哪台设备
- 跟随 Windows 深浅色主题(菜单、设置窗口、系统控件)
- 单实例运行,常驻托盘,占用极小(每轮刷新约 15ms CPU,内存 ~18MB)

## 内置协议 (battery.py 协议表对应)

| 品牌 | VID | UsagePage | 模式 | 说明 |
|------|-----|-----------|------|------|
| ATK | 0x373B | 0xFF05 | Output | 64B 报文 `08 7D 72 02 00 01 07 01`, 电量=resp[7], 校验 resp[1]==0x72 && resp[5]==0x07 |
| ATK / VXE | 0x373B/0x3554 | 0xFF02 | Output | 17B 报文, 电量=resp[6] |
| Endgame | 0x3367 | 0xFF01 | Feature | 0xA1, 电量=resp[16], 需先连读一次唤醒 |
| VAXEE | 0x3057 | 0xFF05 | Feature | 0x0E, 电量=resp[5]×5 |
| Nordic | 0x1915 | 0xFFA0 | Feature | 0x05, 电量=resp[9] |

可在 `atk_battery.cs` 的 `BuiltinProtocols()` 中自行添加新设备协议。

## 编译

需要 .NET Framework 4.8(Windows 自带 csc):

```bat
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" ^
  /nologo /target:winexe /win32icon:app.ico ^
  /out:无线设备电量.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  atk_battery.cs SettingsForm.cs
```

## 文件

- `atk_battery.cs` — 主程序(托盘、HID 枚举、协议读取、配置)
- `SettingsForm.cs` — 设置界面(三页分类导航、跟随系统主题)
- `gen_icon.cs` — 应用图标(app.ico)生成器
- `probe.cs` — 协议探测工具(HID 枚举 + 手动读数,调试用)
- `app.ico` — 应用图标

## 配置存储

注册表 `HKCU\Software\ATKBattery`:

- `Dev0..DevN` — 设备列表 `vid,pid,usagepage,proto,product`
- `DisplayIdx` — 托盘电池显示哪台设备
- `Minutes` — 刷新间隔(分钟)

开机自启注册表 `HKCU\...\Run` 值 `无线设备电量`。