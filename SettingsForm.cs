// SettingsForm.cs —— 设置界面(左侧分类导航): 连接 / 设备 / 设置 三页
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

public class HidInfo
{
    public string Path;
    public ushort Vid;
    public ushort Pid;
    public ushort UsagePage;
    public ushort Usage;
    public ushort InLen;
    public ushort OutLen;
    public ushort FeatLen;
    public string Product = "";
}

public class ProtoSpec
{
    public string Name;
    public ushort Vid;
    public ushort UsagePage;
    public byte Rid;
    public byte[] Payload;
    public int Length;
    public int LevelOffset;
    public int WaitMs;
    public int Scale = 1;
    public int LevelMax = -1;   // -1 = 无上限
    public string Mode;         // "feature" / "output"
    public int[] ExpectIdx;
    public byte[] ExpectVal;
    public int Retries;
}

public class SettingsForm : Form
{
    ListBox lbNav;
    List<Control> pages = new List<Control>();

    // 连接页
    ListView lv;
    List<HidInfo> devices = new List<HidInfo>();
    Button btnRefresh, btnConnect;

    // 设备页
    Label lbDevName, lbDevInfo, lbBattery, lbBatteryInfo;

    // 设置页
    CheckBox chkAutostart;
    NumericUpDown nudMinutes;
    ComboBox cmbDisplay;

    Label lbStatus;

    public SettingsForm()
    {
        Text = "无线设备电量 - 设置";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Microsoft YaHei UI", 9f);
        ClientSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        // ---------- 左侧分类导航 ----------
        lbNav = new ListBox();
        lbNav.Location = new Point(12, 12);
        lbNav.Size = new Size(120, 430);
        lbNav.BorderStyle = BorderStyle.FixedSingle;
        lbNav.Font = new Font("Microsoft YaHei UI", 11f);
        lbNav.Items.Add("连  接");
        lbNav.Items.Add("设  备");
        lbNav.Items.Add("设  置");
        lbNav.SelectedIndexChanged += (s, e) => SwitchPage(lbNav.SelectedIndex);
        Controls.Add(lbNav);

        // ---------- 右侧页面区 ----------
        pages.Add(BuildConnectPage());
        pages.Add(BuildDevicePage());
        pages.Add(BuildSettingsPage());
        foreach (var p in pages)
        {
            p.Location = new Point(140, 12);
            p.Size = new Size(548, 430);
            Controls.Add(p);
        }

        lbStatus = new Label();
        lbStatus.Location = new Point(140, 456);
        lbStatus.Size = new Size(548, 30);
        lbStatus.ForeColor = Color.DimGray;
        Controls.Add(lbStatus);

        lbNav.SelectedIndex = 0;
        RefreshList();
        ApplyTheme();
    }

    // ================= 跟随系统深浅主题 =================
    static bool IsDarkMode()
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

    void ApplyTheme()
    {
        ApplyRec(this, IsDarkMode());
    }

    static void ApplyRec(Control c, bool dark)
    {
        if (c is Button)
        {
            var b = (Button)c;
            if (dark)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Color.FromArgb(0x55, 0x55, 0x58);
                b.BackColor = Color.FromArgb(0x33, 0x33, 0x37);
                b.ForeColor = Color.FromArgb(0xE8, 0xE8, 0xE8);
            }
            else
            {
                b.FlatStyle = FlatStyle.Standard;
                b.UseVisualStyleBackColor = true;
            }
        }
        else if (c is ListView || c is ListBox || c is ComboBox || c is NumericUpDown || c is TextBox)
        {
            if (dark)
            {
                c.BackColor = Color.FromArgb(0x2B, 0x2B, 0x2B);
                c.ForeColor = Color.FromArgb(0xE8, 0xE8, 0xE8);
            }
            else
            {
                c.BackColor = SystemColors.Window;
                c.ForeColor = SystemColors.WindowText;
            }
        }
        else if (c is CheckBox)
        {
            c.BackColor = dark ? Color.Transparent : SystemColors.Control;
            c.ForeColor = dark ? Color.FromArgb(0xE0, 0xE0, 0xE0) : SystemColors.ControlText;
        }
        else if (c is Label)
        {
            c.BackColor = Color.Transparent;
            c.ForeColor = dark ? Color.FromArgb(0xD8, 0xD8, 0xD8) : SystemColors.ControlText;
        }
        else
        {
            if (dark)
            {
                c.BackColor = Color.FromArgb(0x20, 0x20, 0x20);
                c.ForeColor = Color.FromArgb(0xE0, 0xE0, 0xE0);
            }
            else
            {
                c.BackColor = SystemColors.Control;
                c.ForeColor = SystemColors.ControlText;
            }
        }
        foreach (Control ch in c.Controls) ApplyRec(ch, dark);
    }

    // ================= ① 连接页 =================
    Control BuildConnectPage()
    {
        var panel = new Panel();

        var lbHint = new Label();
        lbHint.Location = new Point(8, 6);
        lbHint.Size = new Size(530, 20);
        lbHint.ForeColor = Color.Gray;
        lbHint.Text = "选择已连接的 2.4G 接收器, 一键连接即可读取电量";
        panel.Controls.Add(lbHint);

        lv = new ListView();
        lv.Location = new Point(8, 30);
        lv.Size = new Size(532, 320);
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.GridLines = true;
        lv.Columns.Add("设备名称", 150);
        lv.Columns.Add("VID:PID", 78);
        lv.Columns.Add("UsagePage", 72);
        lv.Columns.Add("接口", 74);
        lv.Columns.Add("协议", 56);
        lv.Columns.Add("路径", 90);
        panel.Controls.Add(lv);

        btnRefresh = new Button();
        btnRefresh.Text = "刷新列表";
        btnRefresh.Location = new Point(8, 358);
        btnRefresh.Size = new Size(90, 32);
        btnRefresh.Click += (s, e) => RefreshList();
        panel.Controls.Add(btnRefresh);

        btnConnect = new Button();
        btnConnect.Text = "一键连接选中设备";
        btnConnect.Location = new Point(106, 358);
        btnConnect.Size = new Size(150, 32);
        btnConnect.Click += (s, e) => ConnectSelected();
        panel.Controls.Add(btnConnect);

        var btnRemove = new Button();
        btnRemove.Text = "移除显示中的设备";
        btnRemove.Location = new Point(264, 358);
        btnRemove.Size = new Size(160, 32);
        btnRemove.Click += (s, e) => RemoveDisplayDevice();
        panel.Controls.Add(btnRemove);

        return panel;
    }

    // ================= ② 设备页 (已连接设备+电量) =================
    Control BuildDevicePage()
    {
        var panel = new Panel();

        var lbT1 = new Label();
        lbT1.Location = new Point(8, 12);
        lbT1.Size = new Size(520, 18);
        lbT1.ForeColor = Color.Gray;
        lbT1.Text = "当前连接设备";
        panel.Controls.Add(lbT1);

        lbDevName = new Label();
        lbDevName.Location = new Point(8, 34);
        lbDevName.Size = new Size(520, 26);
        lbDevName.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
        panel.Controls.Add(lbDevName);

        lbDevInfo = new Label();
        lbDevInfo.Location = new Point(8, 66);
        lbDevInfo.Size = new Size(520, 20);
        lbDevInfo.ForeColor = Color.DimGray;
        panel.Controls.Add(lbDevInfo);

        var lbT2 = new Label();
        lbT2.Location = new Point(8, 104);
        lbT2.Size = new Size(520, 18);
        lbT2.ForeColor = Color.Gray;
        lbT2.Text = "当前电量";
        panel.Controls.Add(lbT2);

        lbBattery = new Label();
        lbBattery.Location = new Point(8, 124);
        lbBattery.Size = new Size(200, 80);
        lbBattery.Font = new Font("Microsoft YaHei UI", 30f, FontStyle.Bold);
        lbBattery.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(lbBattery);

        lbBatteryInfo = new Label();
        lbBatteryInfo.Location = new Point(220, 134);
        lbBatteryInfo.Size = new Size(310, 90);
        lbBatteryInfo.ForeColor = Color.DimGray;
        panel.Controls.Add(lbBatteryInfo);

        var lbT3 = new Label();
        lbT3.Location = new Point(8, 220);
        lbT3.Size = new Size(520, 18);
        lbT3.ForeColor = Color.Gray;
        lbT3.Text = "说明";
        panel.Controls.Add(lbT3);

        var lbNote = new Label();
        lbNote.Location = new Point(8, 242);
        lbNote.Size = new Size(520, 120);
        lbNote.ForeColor = Color.Gray;
        lbNote.Text = "· 电量由托盘程序按设定间隔自动刷新\r\n· 显示 \"?\" 表示设备休眠或无响应, 动一下鼠标即可唤醒\r\n· 连接新设备请切到「连接」页选择后一键连接";
        panel.Controls.Add(lbNote);

        return panel;
    }

    // ================= ③ 设置页 =================
    Control BuildSettingsPage()
    {
        var panel = new Panel();

        chkAutostart = new CheckBox();
        chkAutostart.Text = "开机自动启动";
        chkAutostart.Location = new Point(14, 24);
        chkAutostart.Size = new Size(200, 26);
        chkAutostart.Font = new Font("Microsoft YaHei UI", 10.5f);
        chkAutostart.Checked = AtkBatteryApp.Instance != null && AtkBatteryApp.Instance.IsAutostartOn();
        chkAutostart.CheckedChanged += (s, e) =>
        {
            if (AtkBatteryApp.Instance != null) AtkBatteryApp.Instance.SetAutostart(chkAutostart.Checked);
            lbStatus.Text = chkAutostart.Checked ? "已开启开机自启" : "已关闭开机自启";
        };
        panel.Controls.Add(chkAutostart);

        var lbMin = new Label();
        lbMin.Text = "刷新时间 (分钟):";
        lbMin.Location = new Point(18, 78);
        lbMin.Size = new Size(120, 24);
        panel.Controls.Add(lbMin);

        nudMinutes = new NumericUpDown();
        nudMinutes.Location = new Point(146, 74);
        nudMinutes.Size = new Size(64, 26);
        nudMinutes.Minimum = 1;
        nudMinutes.Maximum = 60;
        nudMinutes.Value = AtkBatteryApp.GetRefreshMinutes();
        nudMinutes.ValueChanged += (s, e) =>
        {
            AtkBatteryApp.SetRefreshMinutes((int)nudMinutes.Value);
            lbStatus.Text = "刷新间隔已改为 " + nudMinutes.Value + " 分钟";
        };
        panel.Controls.Add(nudMinutes);

        var lbDisp = new Label();
        lbDisp.Text = "电池显示设备:";
        lbDisp.Location = new Point(18, 122);
        lbDisp.Size = new Size(120, 24);
        panel.Controls.Add(lbDisp);

        cmbDisplay = new ComboBox();
        cmbDisplay.Location = new Point(146, 118);
        cmbDisplay.Size = new Size(280, 26);
        cmbDisplay.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDisplay.SelectedIndexChanged += CmbDisplayChanged;
        panel.Controls.Add(cmbDisplay);

        var lbTip = new Label();
        lbTip.Location = new Point(18, 170);
        lbTip.Size = new Size(500, 80);
        lbTip.ForeColor = Color.Gray;
        lbTip.Text = "更改立即生效。\r\n· 电池显示设备: 任务栏电池图标显示哪台设备的电量\r\n· 悬停托盘可同时查看所有设备的电量\r\n· 刷新时间控制所有设备多久更新一次";
        panel.Controls.Add(lbTip);

        return panel;
    }

    // ================= 页面切换 =================
    void SwitchPage(int idx)
    {
        if (idx < 0 || idx >= pages.Count) return;
        for (int i = 0; i < pages.Count; i++) pages[i].Visible = (i == idx);
        if (idx == 1) UpdateDevicePage();
        if (idx == 2) UpdateDisplayCombo();
    }

    void RefreshList()
    {
        devices = AtkBatteryApp.EnumerateDevices();
        lv.Items.Clear();
        foreach (var d in devices)
        {
            if (d.Vid == 0 && d.Pid == 0) continue;
            var spec = AtkBatteryApp.FindSpec(d.Vid, d.UsagePage);
            var it = new ListViewItem(d.Product.Length > 0 ? d.Product : d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4"));
            it.SubItems.Add(d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4"));
            it.SubItems.Add(d.UsagePage.ToString("X4"));
            it.SubItems.Add("In" + d.InLen + "/Out" + d.OutLen + "/Feat" + d.FeatLen);
            it.SubItems.Add(spec != null ? spec.Name : "-");
            it.SubItems.Add(d.Path);
            it.Tag = d;
            lv.Items.Add(it);
        }
        lbStatus.Text = "发现 " + devices.Count + " 个 HID 接口";
    }

    void ConnectSelected()
    {
        if (lv.SelectedItems.Count == 0) { lbStatus.Text = "请先在列表中选择一个设备"; return; }
        var d = (HidInfo)lv.SelectedItems[0].Tag;
        lbStatus.Text = "正在连接 " + d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4") + " ...";
        btnConnect.Enabled = false;
        var dd = d;
        Thread th = new Thread(() =>
        {
            string err = "";
            int lvl = AtkBatteryApp.ConnectAndRead(dd, out err);
            this.Invoke((Action)(() =>
            {
                btnConnect.Enabled = true;
                if (lvl >= 0)
                {
                    lbStatus.Text = "连接成功! 电量 " + lvl + "%";
                    UpdateDevicePage();
                }
                else
                {
                    lbStatus.Text = "连接失败: " + err;
                }
            }));
        });
        th.IsBackground = true;
        th.Start();
    }

    void RemoveDisplayDevice()
    {
        var d = AtkBatteryApp.DisplayDevice();
        if (d == null) { lbStatus.Text = "没有设备可移除"; return; }
        AtkBatteryApp.RemoveDevice(d);
        UpdateDevicePage();
        RefreshList();
        lbStatus.Text = "已移除设备, 剩余 " + AtkBatteryApp.GetDevices().Count + " 台";
    }

    void UpdateDevicePage()
    {
        var d = AtkBatteryApp.DisplayDevice();
        if (d == null)
        {
            lbDevName.Text = "未连接任何设备";
            lbDevInfo.Text = "到「连接」页选择设备一键连接, 或插上 ATK 373B:1216 接收器自动添加";
            lbBattery.Text = "?";
            lbBattery.ForeColor = Color.DimGray;
            lbBatteryInfo.Text = "尚未读取到电量";
            UpdateDisplayCombo();
            return;
        }
        string name = d.Product != "" ? d.Product : d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4");
        lbDevName.Text = name;
        lbDevInfo.Text = "VID:PID " + d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4")
            + "    协议: " + d.Proto
            + "    共 " + AtkBatteryApp.GetDevices().Count + " 台设备";
        int lvl = d.Level;
        if (lvl >= 0)
        {
            lbBattery.Text = lvl + "%";
            lbBattery.ForeColor = lvl >= 60 ? Color.FromArgb(0x5D, 0xDC, 0x8A) : (lvl >= 30 ? Color.FromArgb(0xF4, 0xC4, 0x51) : Color.FromArgb(0xFF, 0x6B, 0x6B));
            lbBatteryInfo.Text = "最近读取: " + lvl + "%\r\n状态: 正常";
        }
        else
        {
            lbBattery.Text = "?";
            lbBattery.ForeColor = Color.DimGray;
            lbBatteryInfo.Text = "尚未读取到电量" + (d.Err.Length > 0 ? "\r\n上次错误: " + d.Err : "");
        }
        UpdateDisplayCombo();
    }

    void UpdateDisplayCombo()
    {
        int sel = AtkBatteryApp.GetDisplayIdx();
        var devices = AtkBatteryApp.GetDevices();
        if (cmbDisplay.Items.Count != devices.Count)
        {
            cmbDisplay.Items.Clear();
            foreach (var e in devices)
            {
                string nm = e.Product != "" ? e.Product : e.Vid.ToString("X4") + ":" + e.Pid.ToString("X4");
                cmbDisplay.Items.Add(nm + "  (" + e.Vid.ToString("X4") + ":" + e.Pid.ToString("X4") + ")");
            }
            if (cmbDisplay.Items.Count > 0) cmbDisplay.SelectedIndex = -1;
        }
        if (cmbDisplay.SelectedIndex != sel)
        {
            cmbDisplay.SelectedIndexChanged -= CmbDisplayChanged;
            if (sel >= 0 && sel < cmbDisplay.Items.Count) cmbDisplay.SelectedIndex = sel;
            cmbDisplay.SelectedIndexChanged += CmbDisplayChanged;
        }
    }

    void CmbDisplayChanged(object sender, EventArgs e)
    {
        if (cmbDisplay.SelectedIndex >= 0)
        {
            AtkBatteryApp.SetDisplayIdx(cmbDisplay.SelectedIndex);
            lbStatus.Text = "任务栏电池已切换到: " + cmbDisplay.Text;
            UpdateDevicePage();
        }
    }
}