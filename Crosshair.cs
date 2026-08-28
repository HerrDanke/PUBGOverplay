using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PubgCrosshair
{
    public class OverlayForm : Form
    {
        private bool showDots = false;
        private bool showMapName = false;
        private int currentMap = 0;
        private Timer mapNameTimer;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool[] mapEnabled;
        private ToolStripMenuItem[] mapMenuItems;
        private ToolStripMenuItem toggleMenuItem;   // 显示/隐藏标记菜单项（动态文字）

        // ===== 快捷键配置 =====
        private HotkeyConfig hotkeys;
        private Dictionary<Keys, Action> keyActions = new Dictionary<Keys, Action>();
        private bool settingsOpen = false;          // 设置窗口打开期间钩子暂停消费

        // ===== 地图数据 =====
        private class MapData
        {
            public string Name;      // 中文名
            public float ImgW, ImgH; // 图片像素尺寸
            public int[,] Dots;      // 像素坐标 [i,0]=x [i,1]=y
            public byte[] DotTypes;  // null=全部红色, 0=红(密室), 1=紫(撬棍房), 2=绿(熊洞)
        }

        private MapData[] maps = new MapData[] {
            new MapData {
                Name = "艾伦格", ImgW = 1026, ImgH = 1022,
                Dots = new int[,] {
                    { 643, 84 }, { 173, 229 }, { 518, 248 }, { 820, 261 },
                    { 326, 278 }, { 686, 431 }, { 186, 445 }, { 378, 472 },
                    { 585, 556 }, { 848, 616 }, { 344, 648 }, { 158, 695 },
                    { 553, 744 }, { 415, 843 }, { 712, 846 },
                }
            },
            new MapData {
                Name = "米拉玛", ImgW = 1028, ImgH = 1026,
                Dots = new int[,] {
                    { 410, 136 }, { 581, 181 }, { 226, 210 }, { 792, 244 },
                    { 353, 315 }, { 646, 402 }, { 177, 415 }, { 484, 490 },
                    { 778, 535 }, { 337, 626 }, { 555, 654 }, { 166, 668 },
                    { 659, 791 }, { 408, 837 }, { 177, 910 },
                }
            },
            new MapData {
                Name = "泰戈", ImgW = 1027, ImgH = 1026,
                Dots = new int[,] {
                    { 176, 150 }, { 324, 170 }, { 608, 217 }, { 450, 249 },
                    { 869, 262 }, { 159, 340 }, { 893, 424 }, { 129, 429 },
                    { 760, 487 }, { 557, 625 }, { 123, 659 }, { 806, 700 },
                    { 622, 807 }, { 305, 811 }, { 800, 904 },
                }
            },
            new MapData {
                Name = "维寒迪", ImgW = 1025, ImgH = 1021,
                Dots = new int[,] {
                    {387,161}, {682,166}, {359,171}, {178,175}, {663,176},
                    {347,199}, {763,219}, {456,274}, {788,310}, {656,331},
                    {405,367}, {719,368}, {240,387}, {518,404}, {569,447},
                    {593,449}, {670,460}, {176,483}, {836,487}, {862,488},
                    {224,561}, {859,582}, {128,619}, {761,621}, {593,623},
                    {368,666}, {505,693}, {301,708}, {829,735}, {765,739},
                    {785,740}, {660,742}, {402,756}, {478,765}, {667,779},
                    {475,817}, {498,822}, {542,844}, {452,873},
                },
                DotTypes = new byte[] {
                    1,0,2,1,2,
                    0,2,1,0,1,
                    1,2,1,0,1,
                    2,1,0,2,0,
                    1,1,1,1,0,
                    1,1,0,1,0,
                    2,2,1,2,1,
                    2,0,1,1,
                }
            },
            new MapData {
                Name = "帕拉莫", ImgW = 1024, ImgH = 1023,
                Dots = new int[,] {
                    { 407, 324 }, { 284, 366 }, { 639, 502 }, { 837, 587 },
                    { 619, 616 }, { 444, 646 }, { 132, 650 }, { 530, 823 },
                }
            },
            new MapData {
                Name = "帝斯顿", ImgW = 1027, ImgH = 1025,
                Dots = new int[,] {
                    {349,68}, {653,113}, {237,181}, {246,228}, {843,237},
                    {639,248}, {394,340}, {569,345}, {924,397}, {218,427},
                    {413,453}, {574,514}, {778,529}, {829,532}, {480,572},
                    {207,573}, {847,573}, {804,587}, {290,592}, {758,595},
                    {833,622}, {475,716}, {145,792}, {717,807},
                }
            },
            new MapData {
                Name = "荣都", ImgW = 1025, ImgH = 1026,
                Dots = new int[,] {
                    { 731, 116 }, { 383, 119 }, { 186, 173 }, { 634, 261 },
                    { 477, 336 }, { 887, 347 }, { 180, 410 }, { 834, 540 },
                    { 588, 555 }, { 183, 605 }, { 380, 649 }, { 717, 767 },
                    { 159, 820 }, { 427, 882 }, { 622, 930 },
                }
            },
        };

        // ===== 低层键盘钩子 =====
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private IntPtr hookId = IntPtr.Zero;
        private LowLevelKeyboardProc hookProc;

        // ===== 分层窗口透明度控制 =====
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private const uint LWA_ALPHA = 0x2;
        private const uint LWA_COLORKEY = 0x1;

        public OverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            this.Bounds = screen;
            this.Location = new Point(0, 0);

            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.AllowTransparency = true;
            this.DoubleBuffered = true;

            hotkeys = HotkeyConfig.Load();
            RebuildKeyActions();

            this.Icon = DotIcon.Create(32); // 窗口图标与托盘红点同源

            SetupTrayIcon();

            mapNameTimer = new Timer();
            mapNameTimer.Interval = 1000;
            mapNameTimer.Tick += (s, e) => {
                showMapName = false;
                mapNameTimer.Stop();
                this.Invalidate();
            };

            InstallHook();

            SetLayeredWindowAttributes(this.Handle, 0, 0, LWA_ALPHA | LWA_COLORKEY);
            showDots = false;
        }

        // ============================================================
        // 低层键盘钩子
        // ============================================================
        private void InstallHook()
        {
            hookProc = HookCallback;
            using (Process p = Process.GetCurrentProcess())
            using (ProcessModule m = p.MainModule)
            {
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc,
                    GetModuleHandle(m.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                // 设置窗口打开期间不消费任何按键，避免监听「按新键」时误触发
                if (settingsOpen) return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

                Keys key = (Keys)Marshal.ReadInt32(lParam);
                Action action;
                if (keyActions.TryGetValue(key, out action))
                {
                    action();
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // ============================================================
        // 切换地图
        // ============================================================
        private void SwitchMap(int delta)
        {
            int newMap = currentMap;
            do
            {
                newMap = (newMap + delta + maps.Length) % maps.Length;
            } while (!mapEnabled[newMap] && newMap != currentMap);

            if (newMap == currentMap) return; // 没有可切换的启用地图
            currentMap = newMap;

            // 如果当前是显示状态，刷新画面并显示地图名
            if (showDots)
            {
                showMapName = true;
                mapNameTimer.Stop();
                mapNameTimer.Start();
                trayIcon.Text = maps[currentMap].Name + " - 已显示";
                this.Invalidate();
            }
        }

        // ============================================================
        // 快捷键
        // ============================================================
        private void RebuildKeyActions()
        {
            keyActions.Clear();
            foreach (HotkeyAction act in hotkeys.Actions)
            {
                Action fn = null;
                if (act.Name == "Toggle") fn = this.Toggle;
                else if (act.Name == "PrevMap") fn = () => this.SwitchMap(-1);
                else if (act.Name == "NextMap") fn = () => this.SwitchMap(1);
                if (fn == null) continue;
                foreach (Keys k in act.KeysList)
                {
                    if (k != Keys.None) keyActions[k] = fn;
                }
            }
        }

        // 打开设置窗口（模态）；保存成功后立即重建键映射使其生效
        private void OpenSettings()
        {
            settingsOpen = true;
            try
            {
                using (SettingsForm f = new SettingsForm(hotkeys))
                {
                    f.StartPosition = FormStartPosition.CenterScreen;
                    if (f.ShowDialog() == DialogResult.OK)
                    {
                        RebuildKeyActions();
                        this.Invalidate(); // 外观修改后立即重绘
                    }
                }
            }
            finally
            {
                settingsOpen = false;
            }
        }

        // ============================================================
        // 托盘图标
        // ============================================================
        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayMenu = new ContextMenuStrip();
            trayMenu.Renderer = new CustomMenuRenderer(); // 深色扁平主题
            trayMenu.ShowImageMargin = false;             // 无图标菜单，去掉左侧图文边距

            // 地图池子菜单
            mapEnabled = new bool[maps.Length];
            mapMenuItems = new ToolStripMenuItem[maps.Length];
            ToolStripMenuItem mapPoolItem = new ToolStripMenuItem("地图池");
            for (int i = 0; i < maps.Length; i++)
            {
                mapEnabled[i] = true;
                int idx = i;
                mapMenuItems[i] = new ToolStripMenuItem(maps[i].Name);
                mapMenuItems[i].Checked = true;
                mapMenuItems[i].Click += (s, e) => {
                    mapEnabled[idx] = !mapEnabled[idx];
                    mapMenuItems[idx].Checked = mapEnabled[idx];
                    // 当前显示的地图被取消勾选时自动跳转
                    if (showDots && idx == currentMap && !mapEnabled[idx])
                    {
                        int next = currentMap;
                        do
                        {
                            next = (next + 1) % maps.Length;
                        } while (!mapEnabled[next] && next != currentMap);
                        if (next != currentMap)
                        {
                            currentMap = next;
                            showMapName = true;
                            mapNameTimer.Stop();
                            mapNameTimer.Start();
                            trayIcon.Text = maps[currentMap].Name + " - 已显示";
                            this.Invalidate();
                        }
                    }
                };
                mapPoolItem.DropDownItems.Add(mapMenuItems[i]);
            }
            // 点击地图池内的选项不关闭子菜单，方便连续勾选
            mapPoolItem.DropDown.Closing += (s, e) => {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                    e.Cancel = true;
            };
            trayMenu.Items.Add(mapPoolItem);

            toggleMenuItem = new ToolStripMenuItem("显示标记", null, (s, e) => { this.Toggle(); });
            trayMenu.Items.Add(toggleMenuItem);

            trayMenu.Items.Add("设置", null, (s, e) => { this.OpenSettings(); });
            trayMenu.Items.Add("退出", null, (s, e) => { Application.Exit(); });

            trayIcon.Icon = DotIcon.Create(16);

            trayIcon.Text = "PUBG 标记 - 已隐藏 (按 ` 显示)";
            trayIcon.Visible = true;

            // 右键菜单（原生 NotifyIcon 弹出，自动消失）
            trayMenu.Opening += (s, e) => {
                toggleMenuItem.Text = showDots ? "隐藏标记" : "显示标记";
            };
            trayIcon.ContextMenuStrip = trayMenu;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20;
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            int sw = this.ClientSize.Width;
            int sh = this.ClientSize.Height;

            MapData map = maps[currentMap];

            // 计算图片在屏幕上的显示区域（居中、等比缩放）
            float scale = Math.Min(sw / map.ImgW, sh / map.ImgH);
            float offX = (sw - map.ImgW * scale) / 2f;
            float offY = (sh - map.ImgH * scale) / 2f;

            // 画标记点
            if (showDots)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                for (int i = 0; i < map.Dots.GetLength(0); i++)
                {
                    Color c = MarkerRenderer.PickColor(hotkeys.NormalColor, hotkeys.RoomColor,
                        hotkeys.CrowbarColor, hotkeys.BearColor, map.DotTypes, i);
                    using (SolidBrush brush = new SolidBrush(c))
                    {
                        float px = offX + map.Dots[i, 0] * scale;
                        float py = offY + map.Dots[i, 1] * scale;
                        MarkerRenderer.Draw(g, brush, px, py, hotkeys.MarkerSize, hotkeys.Shape);
                    }
                }
            }

            // 画地图名称提示
            if (showMapName)
            {
                using (Font font = new Font("Microsoft YaHei", 28, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(map.Name, font);
                    float bw = textSize.Width + 60;
                    float bh = textSize.Height + 30;
                    float bx = (sw - bw) / 2;
                    float by = (sh - bh) / 2;

                    using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                    {
                        g.FillRectangle(bgBrush, bx, by, bw, bh);
                    }
                    using (Pen borderPen = new Pen(Color.FromArgb(80, 80, 80), 1))
                    {
                        g.DrawRectangle(borderPen, bx, by, bw, bh);
                    }
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
                    {
                        g.TextRenderingHint = TextRenderingHint.AntiAlias;
                        g.DrawString(map.Name, font, textBrush,
                            bx + (bw - textSize.Width) / 2,
                            by + (bh - textSize.Height) / 2);
                    }
                }
            }
        }

        public void Toggle()
        {
            showDots = !showDots;
            if (showDots)
            {
                SetLayeredWindowAttributes(this.Handle, 0, 255, LWA_ALPHA | LWA_COLORKEY);
                this.TopMost = true;
                this.BringToFront();
                trayIcon.Text = maps[currentMap].Name + " - 已显示";

                showMapName = true;
                mapNameTimer.Stop();
                mapNameTimer.Start();
            }
            else
            {
                SetLayeredWindowAttributes(this.Handle, 0, 0, LWA_ALPHA | LWA_COLORKEY);
                showMapName = false;
                mapNameTimer.Stop();
                trayIcon.Text = "PUBG 标记 - 已隐藏";
            }
            this.Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (hookId != IntPtr.Zero)
                UnhookWindowsHookEx(hookId);
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnFormClosed(e);
        }
    }

    // ============================================================
    // 键名转换（Keys ↔ 配置文本 / 显示文本）
    // ============================================================
    public static class HotkeyNames
    {
        // 特殊键的友好显示名（默认 KeysConverter 输出不够友好，如 Oemtilde→"OEM 3"）
        private static readonly Dictionary<Keys, string> friendlyNames = new Dictionary<Keys, string>
        {
            { Keys.Oemtilde, "`" },
            { Keys.OemOpenBrackets, "[" },
            { Keys.OemCloseBrackets, "]" },
            { Keys.OemPipe, "\\" },
            { Keys.OemMinus, "-" },
            { Keys.Oemplus, "=" },
            { Keys.OemSemicolon, ";" },
            { Keys.OemQuotes, "'" },
            { Keys.Oemcomma, "," },
            { Keys.OemPeriod, "." },
            { Keys.OemQuestion, "/" },
            { Keys.Back, "Backspace" },
            { Keys.Return, "Enter" },
            { Keys.Left, "←" },
            { Keys.Right, "→" },
            { Keys.Up, "↑" },
            { Keys.Down, "↓" },
        };

        private static readonly KeysConverter converter = new KeysConverter();

        // 写入配置文件的键名（Keys 枚举名，无修饰位）
        public static string ToConfigName(Keys key)
        {
            return ((Keys)((int)key & (int)Keys.KeyCode)).ToString();
        }

        // 界面显示名
        public static string ToDisplayName(Keys key)
        {
            string friendly;
            if (friendlyNames.TryGetValue(key, out friendly)) return friendly;
            string s = converter.ConvertToString(key);
            return string.IsNullOrEmpty(s) ? key.ToString() : s;
        }

        // 从配置键名解析；非法返回 Keys.None
        public static Keys FromConfigName(string name)
        {
            Keys k;
            if (Enum.TryParse<Keys>(name.Trim(), true, out k))
                return (Keys)((int)k & (int)Keys.KeyCode);
            return Keys.None;
        }

        // 键列表显示文本，如 "` 或 F2"
        public static string JoinDisplay(IEnumerable<Keys> keys)
        {
            return string.Join(" 或 ", keys.Select(k => ToDisplayName(k)).ToArray());
        }
    }

    // ============================================================
    // 标记形状
    // ============================================================
    public enum MarkerShape
    {
        Circle,    // 圆形（默认）
        Square,    // 方形
        Triangle,  // 三角形
        Diamond,   // 菱形
    }

    // ============================================================
    // 快捷键动作（纯数据）
    // ============================================================
    public class HotkeyAction
    {
        public string Name;          // 动作标识（ini 行键名）
        public string DisplayName;   // 界面显示名
        public Keys[] DefaultKeys;   // 出厂默认键
        public List<Keys> KeysList;  // 当前键（可变）

        public HotkeyAction(string name, string displayName, params Keys[] defaultKeys)
        {
            Name = name;
            DisplayName = displayName;
            DefaultKeys = defaultKeys;
            KeysList = new List<Keys>(defaultKeys);
        }
    }

    // ============================================================
    // 快捷键配置读写（exe 同目录 hotkeys.ini）
    // ============================================================
    public class HotkeyConfig
    {
        public const string FileName = "hotkeys.ini";
        public HotkeyAction[] Actions;

        // ===== 标记外观 =====
        public Color NormalColor;    // 常规色（无类型区分的地图）
        public Color RoomColor;      // 密室
        public Color CrowbarColor;   // 撬棍房
        public Color BearColor;      // 熊洞
        public int MarkerSize;       // 标记直径（4~20 px）
        public MarkerShape Shape;    // 标记形状

        public HotkeyConfig()
        {
            Actions = new HotkeyAction[] {
                new HotkeyAction("Toggle",  "显示/隐藏标记", Keys.Oemtilde, Keys.F2),
                new HotkeyAction("PrevMap", "切换到上一张地图", Keys.Left),
                new HotkeyAction("NextMap", "切换到下一张地图", Keys.Right),
            };

            NormalColor = Color.FromArgb(255, 255, 40, 40);
            RoomColor = Color.FromArgb(255, 255, 40, 40);
            CrowbarColor = Color.FromArgb(255, 160, 40, 200);
            BearColor = Color.FromArgb(255, 40, 200, 40);
            MarkerSize = 8;
            Shape = MarkerShape.Circle;
        }

        // 加载配置；文件缺失/损坏时逐项回退默认值（首次运行不落盘）
        public static HotkeyConfig Load()
        {
            HotkeyConfig cfg = new HotkeyConfig();
            try
            {
                string path = Path.Combine(Application.StartupPath, FileName);
                if (!File.Exists(path)) return cfg;

                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string name = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (name.StartsWith("Appearance.", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyAppearance(cfg, name, value);
                        continue;
                    }

                    HotkeyAction act = cfg.FindAction(name);
                    if (act == null) continue; // 未知动作名：忽略该行

                    List<Keys> keys = ParseKeyList(value);
                    if (keys.Count > 0) act.KeysList = keys; // 全非法键名：保持默认
                }
            }
            catch { /* 读取异常：保持默认 */ }
            return cfg;
        }

        public void Save()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (HotkeyAction act in Actions)
                    lines.Add(act.Name + "=" + string.Join(",", act.KeysList.Select(k => HotkeyNames.ToConfigName(k)).ToArray()));

                lines.Add("Appearance.Normal=" + ColorToConfig(NormalColor));
                lines.Add("Appearance.Room=" + ColorToConfig(RoomColor));
                lines.Add("Appearance.Crowbar=" + ColorToConfig(CrowbarColor));
                lines.Add("Appearance.Bear=" + ColorToConfig(BearColor));
                lines.Add("Appearance.Size=" + MarkerSize);
                lines.Add("Appearance.Shape=" + Shape);

                File.WriteAllLines(Path.Combine(Application.StartupPath, FileName), lines.ToArray());
            }
            catch { /* 写入失败不阻断程序 */ }
        }

        public void RestoreDefaults()
        {
            foreach (HotkeyAction act in Actions)
                act.KeysList = new List<Keys>(act.DefaultKeys);

            NormalColor = Color.FromArgb(255, 255, 40, 40);
            RoomColor = Color.FromArgb(255, 255, 40, 40);
            CrowbarColor = Color.FromArgb(255, 160, 40, 200);
            BearColor = Color.FromArgb(255, 40, 200, 40);
            MarkerSize = 8;
            Shape = MarkerShape.Circle;
        }

        // ===== 外观配置解析（缺失/非法逐项回退默认）=====
        private static void ApplyAppearance(HotkeyConfig cfg, string name, string value)
        {
            if (name.Equals("Appearance.Normal", StringComparison.OrdinalIgnoreCase))
                cfg.NormalColor = ParseColor(value, cfg.NormalColor);
            else if (name.Equals("Appearance.Room", StringComparison.OrdinalIgnoreCase))
                cfg.RoomColor = ParseColor(value, cfg.RoomColor);
            else if (name.Equals("Appearance.Crowbar", StringComparison.OrdinalIgnoreCase))
                cfg.CrowbarColor = ParseColor(value, cfg.CrowbarColor);
            else if (name.Equals("Appearance.Bear", StringComparison.OrdinalIgnoreCase))
                cfg.BearColor = ParseColor(value, cfg.BearColor);
            else if (name.Equals("Appearance.Size", StringComparison.OrdinalIgnoreCase))
            {
                int size;
                if (int.TryParse(value, out size))
                    cfg.MarkerSize = Math.Max(4, Math.Min(20, size));
            }
            else if (name.Equals("Appearance.Shape", StringComparison.OrdinalIgnoreCase))
            {
                MarkerShape sh;
                if (Enum.TryParse<MarkerShape>(value, true, out sh))
                    cfg.Shape = sh;
            }
        }

        // "R,G,B" → Color；非法返回 fallback
        public static Color ParseColor(string value, Color fallback)
        {
            string[] parts = value.Split(',');
            int r, g, b;
            if (parts.Length == 3 &&
                int.TryParse(parts[0].Trim(), out r) &&
                int.TryParse(parts[1].Trim(), out g) &&
                int.TryParse(parts[2].Trim(), out b) &&
                r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255)
                return Color.FromArgb(255, r, g, b);
            return fallback;
        }

        public static string ColorToConfig(Color c)
        {
            return c.R + "," + c.G + "," + c.B;
        }

        private HotkeyAction FindAction(string name)
        {
            foreach (HotkeyAction act in Actions)
                if (string.Equals(act.Name, name, StringComparison.OrdinalIgnoreCase))
                    return act;
            return null;
        }

        private static List<Keys> ParseKeyList(string value)
        {
            List<Keys> list = new List<Keys>();
            foreach (string part in value.Split(','))
            {
                Keys k = HotkeyNames.FromConfigName(part);
                if (k != Keys.None) list.Add(k);
            }
            return list;
        }
    }

    // ============================================================
    // 标记绘制（类型取色 + 形状渲染）
    // ============================================================
    public static class MarkerRenderer
    {
        // 按类型取色：无类型地图用常规色；type 0/1/2 分别用密室/撬棍房/熊洞色
        public static Color PickColor(Color normal, Color room, Color crowbar, Color bear, byte[] types, int index)
        {
            if (types == null) return normal;
            int type = (index < types.Length) ? types[index] : 0;
            switch (type)
            {
                case 1: return crowbar;
                case 2: return bear;
                default: return room;
            }
        }

        // 以 (cx, cy) 为中心绘制 size×size 的标记（形状外接包围盒一致）
        public static void Draw(Graphics g, SolidBrush brush, float cx, float cy, float size, MarkerShape shape)
        {
            float half = size / 2f;
            switch (shape)
            {
                case MarkerShape.Square:
                    g.FillRectangle(brush, cx - half, cy - half, size, size);
                    break;
                case MarkerShape.Triangle:
                    g.FillPolygon(brush, new PointF[] {
                        new PointF(cx, cy - half),        // 顶点（上）
                        new PointF(cx - half, cy + half), // 左下
                        new PointF(cx + half, cy + half), // 右下
                    });
                    break;
                case MarkerShape.Diamond:
                    g.FillPolygon(brush, new PointF[] {
                        new PointF(cx, cy - half), // 上
                        new PointF(cx + half, cy), // 右
                        new PointF(cx, cy + half), // 下
                        new PointF(cx - half, cy), // 左
                    });
                    break;
                default: // Circle
                    g.FillEllipse(brush, cx - half, cy - half, size, size);
                    break;
            }
        }
    }

    // ============================================================
    // 红点图标生成（托盘 / 窗口 / exe 图标同源）
    // ============================================================
    public static class DotIcon
    {
        // 按尺寸绘制红点（透明背景 + (255,60,60) 圆点），返回 Icon（由控件负责销毁）
        public static Icon Create(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    int d = (int)Math.Round(size * 0.625); // 圆点直径 ≈ 62.5%，与托盘 10/16 一致
                    int o = (size - d) / 2;
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 255, 60, 60)))
                    {
                        g.FillEllipse(brush, o, o, d, d);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    // ============================================================
    // 现代化浅色苹果风 UI 主题（颜色/字体令牌 + 样式助手）
    // ============================================================
    public static class UiTheme
    {
        // 色彩令牌（浅色苹果风，明亮基底 + 苹果蓝强调）
        public static readonly Color Bg      = Color.FromArgb(245, 245, 247); // #F5F5F7 窗口/菜单基底
        public static readonly Color Surface = Color.FromArgb(255, 255, 255); // #FFFFFF 白面（键帽/按钮/预览）
        public static readonly Color Elevated = Color.FromArgb(232, 232, 237); // #E8E8ED 提升面（hover/选中）
        public static readonly Color Line     = Color.FromArgb(209, 209, 214); // #D1D1D6 分隔/细边框
        public static readonly Color Text     = Color.FromArgb(29, 29, 31);    // #1D1D1F 主文本（近黑）
        public static readonly Color TextDim  = Color.FromArgb(134, 134, 139); // #86868B 次文本（灰）
        public static readonly Color Accent   = Color.FromArgb(0, 122, 255);   // #007AFF 苹果蓝
        public static readonly Color AccentPressed = Color.FromArgb(10, 92, 214); // #0A5CD6 苹果蓝按下态
        // 标记三色（外观页色块）
        public static readonly Color MarkerRed    = Color.FromArgb(255, 40, 40);
        public static readonly Color MarkerPurple = Color.FromArgb(160, 40, 200);
        public static readonly Color MarkerGreen  = Color.FromArgb(40, 200, 40);

        // 字体角色：界面微软雅黑 / 数据等宽 Consolas
        public static Font UiFont()
        {
            return new Font("Microsoft YaHei", 9F);
        }

        public static Font MonoFont()
        {
            return new Font("Consolas", 9F);
        }

        // 扁平浅色按钮；primary=true 为苹果蓝主操作
        public static void StyleButton(Button b, bool primary = false)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.BorderSize = 1;
            if (primary)
            {
                b.FlatAppearance.MouseOverBackColor = AccentPressed;
                b.FlatAppearance.MouseDownBackColor = AccentPressed;
                b.BackColor = Accent;
                b.ForeColor = Color.White;
            }
            else
            {
                b.FlatAppearance.MouseOverBackColor = Elevated;
                b.FlatAppearance.MouseDownBackColor = Elevated;
                b.BackColor = Surface;
                b.ForeColor = Text;
            }
        }
    }

    // ============================================================
    // 托盘菜单浅色渲染器（扁平浅色 + hover 蓝点指示 + 勾选蓝点）
    // ============================================================
    public class CustomMenuRenderer : ToolStripProfessionalRenderer
    {
        public CustomMenuRenderer()
            : base(new UiColorTable())
        {
        }

        private class UiColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return UiTheme.Bg; } }
            public override Color ImageMarginGradientBegin   { get { return UiTheme.Bg; } }
            public override Color ImageMarginGradientMiddle  { get { return UiTheme.Bg; } }
            public override Color ImageMarginGradientEnd     { get { return UiTheme.Bg; } }
            public override Color MenuBorder                 { get { return UiTheme.Line; } }
            public override Color MenuItemBorder             { get { return UiTheme.Line; } }
            public override Color MenuItemSelected           { get { return UiTheme.Elevated; } }
            public override Color MenuItemSelectedGradientBegin { get { return UiTheme.Elevated; } }
            public override Color MenuItemSelectedGradientEnd   { get { return UiTheme.Elevated; } }
            public override Color SeparatorDark              { get { return UiTheme.Line; } }
            public override Color SeparatorLight             { get { return UiTheme.Bg; } }
        }

        // 文字始终使用主题主文本
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? UiTheme.Text : UiTheme.TextDim;
            base.OnRenderItemText(e);
        }

        // hover 高亮 + 左侧品牌红点（勾选态非 hover 时以红点呈现）
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
            using (SolidBrush bg = new SolidBrush(e.Item.Selected ? UiTheme.Elevated : UiTheme.Bg))
            {
                e.Graphics.FillRectangle(bg, rc);
            }
            ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
            if (e.Item.Selected)
            {
                int dot = 6;
                int y = rc.Y + (rc.Height - dot) / 2;
                using (SolidBrush b = new SolidBrush(UiTheme.Accent))
                {
                    e.Graphics.FillEllipse(b, 8, y, dot, dot);
                }
            }
            else if (mi != null && mi.Checked)
            {
                int dot = 6;
                int y = rc.Y + (rc.Height - dot) / 2;
                using (SolidBrush b = new SolidBrush(UiTheme.Accent))
                {
                    e.Graphics.FillEllipse(b, 8, y, dot, dot);
                }
            }
        }
    }

    // ============================================================
    // 快捷键设置窗口
    // ============================================================
    public class SettingsForm : Form
    {
        private HotkeyConfig config;      // 宿主配置引用（确定时才写回）
        private HotkeyAction[] edits;     // 快捷键可编辑副本（取消/关闭不影响原配置）
        private Label[] keyLabels;
        private Button[] modifyButtons;
        private Label statusLabel;
        private int listeningIndex = -1;  // 正在监听的动作索引，-1 表示未监听

        // ===== 标记外观编辑副本 =====
        private static readonly string[] colorNames = new string[] { "常规色", "密室", "撬棍房", "熊洞" };
        private Color[] editColors;       // 顺序与 colorNames 一致
        private int editSize;
        private MarkerShape editShape;
        private Button[] colorButtons;
        private Label[] rgbLabels;
        private TrackBar sizeTrack;
        private Label sizeLabel;
        private RadioButton[] shapeRadios;
        private Panel previewPanel;

        public SettingsForm(HotkeyConfig config)
        {
            this.config = config;

            // 深拷贝当前配置作为可编辑副本
            edits = new HotkeyAction[config.Actions.Length];
            for (int i = 0; i < edits.Length; i++)
            {
                HotkeyAction src = config.Actions[i];
                edits[i] = new HotkeyAction(src.Name, src.DisplayName, src.DefaultKeys);
                edits[i].KeysList = new List<Keys>(src.KeysList);
            }
            editColors = new Color[] { config.NormalColor, config.RoomColor, config.CrowbarColor, config.BearColor };
            editSize = config.MarkerSize;
            editShape = config.Shape;

            this.Text = "● 设置";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.TopMost = true; // 主叠加层为置顶窗口，设置窗需同步置顶
            this.KeyPreview = true;
            this.ClientSize = new Size(430, 358);
            this.BackColor = UiTheme.Bg;   // 浅色苹果主题
            this.ForeColor = UiTheme.Text;
            this.Font = UiTheme.UiFont();

            // ===== 两个页签 =====
            TabControl tabs = new TabControl();
            tabs.Location = new Point(10, 10);
            tabs.Size = new Size(410, 300);
            tabs.BackColor = UiTheme.Bg;
            tabs.ForeColor = UiTheme.Text;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += TabsDrawItem; // 深色页签条
            TabPage hotkeyPage = new TabPage("快捷键");
            TabPage appearancePage = new TabPage("标记外观");
            hotkeyPage.BackColor = UiTheme.Bg;
            appearancePage.BackColor = UiTheme.Bg;
            tabs.TabPages.Add(hotkeyPage);
            tabs.TabPages.Add(appearancePage);
            tabs.SelectedIndexChanged += (s, e) => StopListening(); // 切页终止监听
            this.Controls.Add(tabs);

            BuildHotkeyPage(hotkeyPage);
            BuildAppearancePage(appearancePage);

            Button restoreBtn = new Button();
            restoreBtn.Text = "恢复默认";
            restoreBtn.Location = new Point(20, 318);
            restoreBtn.Size = new Size(90, 30);
            UiTheme.StyleButton(restoreBtn);
            restoreBtn.Click += (s, e) => { StopListening(); RestoreDefaults(); };
            this.Controls.Add(restoreBtn);

            Button okBtn = new Button();
            okBtn.Text = "确定";
            okBtn.Location = new Point(250, 318);
            okBtn.Size = new Size(80, 30);
            UiTheme.StyleButton(okBtn, primary: true); // 主操作：品牌红
            okBtn.Click += (s, e) => SaveAndClose();
            this.Controls.Add(okBtn);

            Button cancelBtn = new Button();
            cancelBtn.Text = "取消";
            cancelBtn.Location = new Point(340, 318);
            cancelBtn.Size = new Size(80, 30);
            UiTheme.StyleButton(cancelBtn);
            cancelBtn.Click += (s, e) => { StopListening(); this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(cancelBtn);
        }

        // ===== 深色页签条：未选中 Surface+TextDim，选中 Elevated+Text+底部品牌红指示 =====
        private static void TabsDrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = (TabControl)sender;
            Rectangle rc = tc.GetTabRect(e.Index);
            bool selected = e.Index == tc.SelectedIndex;
            using (SolidBrush bg = new SolidBrush(selected ? UiTheme.Elevated : UiTheme.Surface))
            {
                e.Graphics.FillRectangle(bg, rc);
            }
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
                {
                    e.Graphics.FillRectangle(accent, rc.X, rc.Bottom - 2, rc.Width, 2);
                }
            }
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, rc,
                selected ? UiTheme.Text : UiTheme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ===== 快捷键页 =====
        private void BuildHotkeyPage(TabPage page)
        {
            keyLabels = new Label[edits.Length];
            modifyButtons = new Button[edits.Length];

            for (int i = 0; i < edits.Length; i++)
            {
                int y = 14 + i * 42;

                Label nameLabel = new Label();
                nameLabel.Text = edits[i].DisplayName;
                nameLabel.Location = new Point(14, y + 4);
                nameLabel.Size = new Size(150, 22);
                nameLabel.TextAlign = ContentAlignment.MiddleLeft;
                nameLabel.ForeColor = UiTheme.Text;
                page.Controls.Add(nameLabel);

                // 键帽样式：深色表面 + 等宽字体
                Label keyLabel = new Label();
                keyLabel.Text = HotkeyNames.JoinDisplay(edits[i].KeysList);
                keyLabel.Location = new Point(176, y);
                keyLabel.Size = new Size(128, 26);
                keyLabel.TextAlign = ContentAlignment.MiddleCenter;
                keyLabel.BorderStyle = BorderStyle.FixedSingle;
                keyLabel.BackColor = UiTheme.Surface;
                keyLabel.ForeColor = UiTheme.Text;
                keyLabel.Font = UiTheme.MonoFont();
                page.Controls.Add(keyLabel);
                keyLabels[i] = keyLabel;

                int idx = i;
                Button modifyBtn = new Button();
                modifyBtn.Text = "修改";
                modifyBtn.Location = new Point(314, y);
                modifyBtn.Size = new Size(80, 26);
                UiTheme.StyleButton(modifyBtn);
                modifyBtn.Click += (s, e) => StartListening(idx);
                page.Controls.Add(modifyBtn);
                modifyButtons[i] = modifyBtn;
            }

            statusLabel = new Label();
            statusLabel.Text = "点击「修改」后按新键绑定，按 Esc 取消；绑定后立即生效。";
            statusLabel.Location = new Point(14, 150);
            statusLabel.Size = new Size(380, 22);
            statusLabel.ForeColor = UiTheme.TextDim;
            page.Controls.Add(statusLabel);
        }

        // ===== 标记外观页 =====
        private void BuildAppearancePage(TabPage page)
        {
            page.AutoScroll = true; // 内容超出时兜底滚动
            colorButtons = new Button[colorNames.Length];
            rgbLabels = new Label[colorNames.Length];

            for (int i = 0; i < colorNames.Length; i++)
            {
                int y = 10 + i * 34;
                int idx = i;

                Label nameLabel = new Label();
                nameLabel.Text = colorNames[i];
                nameLabel.Location = new Point(16, y + 4);
                nameLabel.Size = new Size(70, 22);
                nameLabel.ForeColor = UiTheme.Text;
                page.Controls.Add(nameLabel);

                Button colorBtn = new Button();
                colorBtn.Text = "";
                colorBtn.BackColor = editColors[i];
                colorBtn.FlatStyle = FlatStyle.Flat;
                colorBtn.FlatAppearance.BorderColor = UiTheme.Line;
                colorBtn.FlatAppearance.BorderSize = 1;
                colorBtn.FlatAppearance.MouseOverBackColor = UiTheme.Elevated;
                colorBtn.Location = new Point(92, y);
                colorBtn.Size = new Size(56, 26);
                colorBtn.Click += (s, e) => ChooseColor(idx);
                page.Controls.Add(colorBtn);
                colorButtons[i] = colorBtn;

                Label rgbLabel = new Label();
                rgbLabel.Text = ColorText(editColors[i]);
                rgbLabel.Location = new Point(154, y + 4);
                rgbLabel.Size = new Size(130, 22);
                rgbLabel.ForeColor = UiTheme.TextDim;
                rgbLabel.Font = UiTheme.MonoFont(); // 数据等宽
                page.Controls.Add(rgbLabel);
                rgbLabels[i] = rgbLabel;
            }

            // 大小（直径 4~20）
            Label sizeName = new Label();
            sizeName.Text = "大小";
            sizeName.Location = new Point(16, 152);
            sizeName.Size = new Size(50, 22);
            sizeName.ForeColor = UiTheme.Text;
            page.Controls.Add(sizeName);

            sizeTrack = new TrackBar();
            sizeTrack.Minimum = 4;
            sizeTrack.Maximum = 20;
            sizeTrack.Value = editSize;
            sizeTrack.TickFrequency = 2;
            sizeTrack.BackColor = UiTheme.Bg; // TrackBar 系统绘制，统一浅色背景
            sizeTrack.Location = new Point(70, 146);
            sizeTrack.Size = new Size(180, 30);
            sizeTrack.Scroll += (s, e) => {
                editSize = sizeTrack.Value;
                sizeLabel.Text = editSize + " px";
                previewPanel.Invalidate();
            };
            page.Controls.Add(sizeTrack);

            sizeLabel = new Label();
            sizeLabel.Text = editSize + " px";
            sizeLabel.Location = new Point(256, 152);
            sizeLabel.Size = new Size(60, 22);
            sizeLabel.ForeColor = UiTheme.Text;
            sizeLabel.Font = UiTheme.MonoFont(); // 数据等宽
            page.Controls.Add(sizeLabel);

            // 形状
            Label shapeName = new Label();
            shapeName.Text = "形状";
            shapeName.Location = new Point(16, 190);
            shapeName.Size = new Size(50, 22);
            shapeName.ForeColor = UiTheme.Text;
            page.Controls.Add(shapeName);

            string[] shapeNames = new string[] { "圆形", "方形", "三角形", "菱形" };
            shapeRadios = new RadioButton[shapeNames.Length];
            int rx = 70;
            for (int i = 0; i < shapeRadios.Length; i++)
            {
                int s = i;
                RadioButton rb = new RadioButton();
                rb.Text = shapeNames[i];
                rb.Location = new Point(rx, 190);
                rb.Size = new Size(78, 24);
                rb.ForeColor = UiTheme.Text;
                rb.BackColor = UiTheme.Bg;
                rb.Checked = ((int)editShape) == i;
                rb.CheckedChanged += (sender, e2) => {
                    if (((RadioButton)sender).Checked)
                    {
                        editShape = (MarkerShape)s;
                        previewPanel.Invalidate();
                    }
                };
                page.Controls.Add(rb);
                shapeRadios[i] = rb;
                rx += 78;
            }

            // 预览
            Label previewName = new Label();
            previewName.Text = "预览";
            previewName.Location = new Point(16, 222);
            previewName.Size = new Size(50, 22);
            previewName.ForeColor = UiTheme.Text;
            page.Controls.Add(previewName);

            previewPanel = new Panel();
            previewPanel.Location = new Point(70, 216);
            previewPanel.Size = new Size(324, 40);
            previewPanel.BackColor = UiTheme.Surface;
            previewPanel.Paint += PreviewPaint; // 边框在 PreviewPaint 绘制
            page.Controls.Add(previewPanel);
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (Pen pen = new Pen(UiTheme.Line)) // 细边框
            {
                g.DrawRectangle(pen, 0, 0, previewPanel.Width - 1, previewPanel.Height - 1);
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using (SolidBrush brush = new SolidBrush(editColors[0])) // 以常规色预览
            {
                float size = Math.Max(editSize * 2.0f, 12f); // 放大便于观察，最小 12
                MarkerRenderer.Draw(g, brush, previewPanel.Width / 2f, 20f, size, editShape);
            }
        }

        private static string ColorText(Color c)
        {
            return c.R + "," + c.G + "," + c.B;
        }

        private void ChooseColor(int index)
        {
            StopListening();
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.Color = editColors[index];
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    editColors[index] = Color.FromArgb(255, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                    colorButtons[index].BackColor = editColors[index];
                    rgbLabels[index].Text = ColorText(editColors[index]);
                    previewPanel.Invalidate();
                }
            }
        }

        // ===== 按键监听 =====
        private void StartListening(int index)
        {
            if (listeningIndex == index) return;
            StopListening();
            listeningIndex = index;
            this.ActiveControl = null; // 让焦点回到窗体，避免空格/回车触发按钮
            modifyButtons[index].Text = "请按键…";
            statusLabel.Text = "请按键…，按 Esc 取消";
        }

        private void StopListening()
        {
            if (listeningIndex >= 0)
            {
                listeningIndex = -1;
                statusLabel.Text = "点击「修改」后按新键绑定，按 Esc 取消；绑定后立即生效。";
                RefreshLabels();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (listeningIndex >= 0)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    // Esc 取消本次修改，而不是绑定
                    StopListening();
                }
                else
                {
                    // 整体替换：按下一次键即替换该动作的全部绑定
                    edits[listeningIndex].KeysList = new List<Keys> { (Keys)((int)e.KeyCode & (int)Keys.KeyCode) };
                    StopListening();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        private void RefreshLabels()
        {
            for (int i = 0; i < edits.Length; i++)
            {
                keyLabels[i].Text = HotkeyNames.JoinDisplay(edits[i].KeysList);
                modifyButtons[i].Text = "修改";
            }
        }

        // ===== 恢复默认（快捷键 + 外观）=====
        private void RestoreDefaults()
        {
            foreach (HotkeyAction act in edits)
                act.KeysList = new List<Keys>(act.DefaultKeys);
            RefreshLabels();

            editColors = new Color[] {
                Color.FromArgb(255, 255, 40, 40),
                Color.FromArgb(255, 255, 40, 40),
                Color.FromArgb(255, 160, 40, 200),
                Color.FromArgb(255, 40, 200, 40),
            };
            editSize = 8;
            editShape = MarkerShape.Circle;

            sizeTrack.Value = editSize;
            sizeLabel.Text = editSize + " px";
            for (int i = 0; i < editColors.Length; i++)
            {
                colorButtons[i].BackColor = editColors[i];
                rgbLabels[i].Text = ColorText(editColors[i]);
            }
            for (int i = 0; i < shapeRadios.Length; i++)
                shapeRadios[i].Checked = ((int)editShape) == i;
            previewPanel.Invalidate();
        }

        // ===== 保存（快捷键冲突检测 + 外观写回）=====
        private void SaveAndClose()
        {
            StopListening();

            // 全局查重（同一动作内重复键自动去重，跨动作重复报冲突）
            Dictionary<Keys, HotkeyAction> seen = new Dictionary<Keys, HotkeyAction>();
            foreach (HotkeyAction act in edits)
            {
                foreach (Keys k in act.KeysList)
                {
                    HotkeyAction other;
                    if (seen.TryGetValue(k, out other) && other != act)
                    {
                        MessageBox.Show(this,
                            "按键冲突：'" + HotkeyNames.ToDisplayName(k) + "' 已绑定到「" + other.DisplayName +
                            "」，无法再绑定到「" + act.DisplayName + "」。请为两个动作设置不同按键。",
                            "快捷键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return; // 不保存、不关窗，各动作保持原绑定
                    }
                    seen[k] = act;
                }
            }

            // 写回快捷键
            for (int i = 0; i < edits.Length; i++)
            {
                config.Actions[i].KeysList = new List<Keys>(edits[i].KeysList.Distinct());
            }
            // 写回外观
            config.NormalColor = editColors[0];
            config.RoomColor = editColors[1];
            config.CrowbarColor = editColors[2];
            config.BearColor = editColors[3];
            config.MarkerSize = editSize;
            config.Shape = editShape;
            config.Save();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OverlayForm());
        }
    }
}
