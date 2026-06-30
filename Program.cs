using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace ResolutionToggle
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new TrayApplicationContext());
        }
    }

    [Flags]
    internal enum HotkeyModifiers
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }

    public class AppSettings
    {
        public int ResAWidth { get; set; }
        public int ResAHeight { get; set; }
        public int ResBWidth { get; set; }
        public int ResBHeight { get; set; }
        public int HotkeyModsValue { get; set; }
        public int HotkeyKeyValue { get; set; }

        public static AppSettings Load()
        {
            string path = Path.Combine(Application.StartupPath, "settings.xml");
            if (File.Exists(path))
            {
                try
                {
                    var xs = new XmlSerializer(typeof(AppSettings));
                    using (var sr = new StreamReader(path))
                        return (AppSettings)xs.Deserialize(sr);
                }
                catch
                {
                }
            }
            return GetDefaults();
        }

        public static AppSettings GetDefaults()
        {
            return new AppSettings
            {
                ResAWidth = 2880,
                ResAHeight = 1800,
                ResBWidth = 2560,
                ResBHeight = 1440,
                HotkeyModsValue = (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt),
                HotkeyKeyValue = (int)Keys.P
            };
        }

        public void Save()
        {
            string path = Path.Combine(Application.StartupPath, "settings.xml");
            var xs = new XmlSerializer(typeof(AppSettings));
            using (var sw = new StreamWriter(path))
                xs.Serialize(sw, this);
        }
    }

    internal class TrayApplicationContext : ApplicationContext
    {
        private AppSettings settings;
        private HotkeyModifiers hotkeyMods;
        private Keys hotkeyKey;
        private Resolution ResA;
        private Resolution ResB;

        private readonly NotifyIcon trayIcon;
        private readonly ToolStripMenuItem itemA;
        private readonly ToolStripMenuItem itemB;
        private readonly HotkeyWindow hotkeyWindow;
        private ToolStripMenuItem toggleItem;
        private ToolStripMenuItem startupItem;

        public TrayApplicationContext()
        {
            settings = AppSettings.Load();
            hotkeyMods = (HotkeyModifiers)settings.HotkeyModsValue;
            hotkeyKey = (Keys)settings.HotkeyKeyValue;
            ResA = new Resolution(settings.ResAWidth, settings.ResAHeight, Label(settings.ResAWidth, settings.ResAHeight));
            ResB = new Resolution(settings.ResBWidth, settings.ResBHeight, Label(settings.ResBWidth, settings.ResBHeight));

            var menu = new ContextMenuStrip();

            toggleItem = new ToolStripMenuItem("Toggle Resolution (" + HotkeyLabel() + ")");
            toggleItem.Click += (s, e) => ToggleResolution();
            menu.Items.Add(toggleItem);

            menu.Items.Add(new ToolStripSeparator());

            itemA = new ToolStripMenuItem(ResA.Label);
            itemA.Click += (s, e) => SetResolution(ResA);
            menu.Items.Add(itemA);

            itemB = new ToolStripMenuItem(ResB.Label);
            itemB.Click += (s, e) => SetResolution(ResB);
            menu.Items.Add(itemB);

            menu.Items.Add(new ToolStripSeparator());

            var changeHotkeyItem = new ToolStripMenuItem("Change hotkey...");
            changeHotkeyItem.Click += (s, e) => ChangeHotkey();
            menu.Items.Add(changeHotkeyItem);

            var res1Item = new ToolStripMenuItem("Resolution 1...");
            res1Item.Click += (s, e) => ChangeResolutionA();
            menu.Items.Add(res1Item);

            var res2Item = new ToolStripMenuItem("Resolution 2...");
            res2Item.Click += (s, e) => ChangeResolutionB();
            menu.Items.Add(res2Item);

            menu.Items.Add(new ToolStripSeparator());

            startupItem = new ToolStripMenuItem("Run on startup");
            startupItem.Click += (s, e) => ToggleStartup();
            startupItem.Checked = IsStartupEnabled();
            menu.Items.Add(startupItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApp();
            menu.Items.Add(exitItem);

            trayIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "Resolution Toggle (" + HotkeyLabel() + ")",
                ContextMenuStrip = menu,
                Visible = true
            };

            hotkeyWindow = new HotkeyWindow();
            hotkeyWindow.HotkeyPressed += (s, e) => ToggleResolution();
            hotkeyWindow.RegisterHotkey(1, hotkeyMods, hotkeyKey);

            UpdateCheckedState();
        }

        private static string Label(int w, int h)
        {
            return w + " x " + h;
        }

        private static Point GetTrayPosition(Size formSize)
        {
            var screen = Screen.FromPoint(Cursor.Position);
            var wa = screen.WorkingArea;
            int x = Cursor.Position.X - formSize.Width / 2;
            int y = Cursor.Position.Y - formSize.Height / 2;
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - formSize.Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - formSize.Height));
            return new Point(x, y);
        }

        private void ChangeHotkey()
        {
            using (var form = new HotkeyPickerForm(hotkeyMods, hotkeyKey))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = GetTrayPosition(form.Size);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    hotkeyMods = form.SelectedModifiers;
                    hotkeyKey = form.SelectedKey;
                    settings.HotkeyModsValue = (int)hotkeyMods;
                    settings.HotkeyKeyValue = (int)hotkeyKey;
                    settings.Save();

                    hotkeyWindow.UnregisterHotkey(1);
                    hotkeyWindow.RegisterHotkey(1, hotkeyMods, hotkeyKey);

                    toggleItem.Text = "Toggle Resolution (" + HotkeyLabel() + ")";
                    trayIcon.Text = "Resolution Toggle (" + HotkeyLabel() + ")";
                }
            }
        }

        private void ChangeResolutionA()
        {
            using (var form = new ResolutionPickerForm(ResA.Width, ResA.Height))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = GetTrayPosition(form.Size);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    settings.ResAWidth = form.SelectedWidth;
                    settings.ResAHeight = form.SelectedHeight;
                    settings.Save();
                    ResA = new Resolution(form.SelectedWidth, form.SelectedHeight, Label(form.SelectedWidth, form.SelectedHeight));
                    itemA.Text = ResA.Label;
                    UpdateCheckedState();
                }
            }
        }

        private void ChangeResolutionB()
        {
            using (var form = new ResolutionPickerForm(ResB.Width, ResB.Height))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = GetTrayPosition(form.Size);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    settings.ResBWidth = form.SelectedWidth;
                    settings.ResBHeight = form.SelectedHeight;
                    settings.Save();
                    ResB = new Resolution(form.SelectedWidth, form.SelectedHeight, Label(form.SelectedWidth, form.SelectedHeight));
                    itemB.Text = ResB.Label;
                    UpdateCheckedState();
                }
            }
        }

        private string HotkeyLabel()
        {
            string mods = "";
            if ((hotkeyMods & HotkeyModifiers.Control) != 0) mods += "Ctrl+";
            if ((hotkeyMods & HotkeyModifiers.Alt) != 0) mods += "Alt+";
            if ((hotkeyMods & HotkeyModifiers.Shift) != 0) mods += "Shift+";
            if ((hotkeyMods & HotkeyModifiers.Win) != 0) mods += "Win+";
            return mods + hotkeyKey;
        }

        private Icon LoadIcon()
        {
            string iconPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
            return SystemIcons.Application;
        }

        private void ToggleResolution()
        {
            var current = DisplaySettings.GetCurrent();
            if (current.dmPelsWidth == ResA.Width && current.dmPelsHeight == ResA.Height)
                SetResolution(ResB);
            else
                SetResolution(ResA);
        }

        private void SetResolution(Resolution res)
        {
            DisplaySettings.SetResolution(res.Width, res.Height);
            UpdateCheckedState();
        }

        private void UpdateCheckedState()
        {
            var current = DisplaySettings.GetCurrent();
            itemA.Checked = (current.dmPelsWidth == ResA.Width && current.dmPelsHeight == ResA.Height);
            itemB.Checked = (current.dmPelsWidth == ResB.Width && current.dmPelsHeight == ResB.Height);
        }

        private bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                return key != null && key.GetValue("ResolutionToggle") != null;
            }
        }

        private void ToggleStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;
                if (IsStartupEnabled())
                {
                    key.DeleteValue("ResolutionToggle", false);
                    startupItem.Checked = false;
                }
                else
                {
                    key.SetValue("ResolutionToggle", Application.ExecutablePath);
                    startupItem.Checked = true;
                }
            }
        }

        private void ExitApp()
        {
            hotkeyWindow.UnregisterHotkey(1);
            hotkeyWindow.DestroyHandle();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }
    }

    internal struct Resolution
    {
        public readonly int Width;
        public readonly int Height;
        public readonly string Label;

        public Resolution(int width, int height, string label)
        {
            Width = width;
            Height = height;
            Label = label;
        }
    }

    internal class HotkeyWindow : NativeWindow
    {
        private const int WM_HOTKEY = 0x0312;

        public event EventHandler HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        public bool RegisterHotkey(int id, HotkeyModifiers modifiers, Keys key)
        {
            return NativeMethods.RegisterHotKey(Handle, id, (uint)modifiers, (uint)key);
        }

        public void UnregisterHotkey(int id)
        {
            NativeMethods.UnregisterHotKey(Handle, id);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                if (HotkeyPressed != null)
                    HotkeyPressed(this, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal class HotkeyPickerForm : Form
    {
        public HotkeyModifiers SelectedModifiers { get; private set; }
        public Keys SelectedKey { get; private set; }

        private readonly TextBox txtHotkey;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public HotkeyPickerForm(HotkeyModifiers currentMods, Keys currentKey)
        {
            Text = "Change Hotkey";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 130);

            var lbl = new Label
            {
                Text = "Press a key combination:",
                Location = new Point(12, 12),
                Size = new Size(276, 20)
            };

            txtHotkey = new TextBox
            {
                Location = new Point(12, 38),
                Size = new Size(276, 24),
                ReadOnly = true,
                TabStop = false
            };

            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(126, 75),
                Size = new Size(75, 26),
                DialogResult = DialogResult.OK,
                Enabled = false
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(213, 75),
                Size = new Size(75, 26),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(lbl);
            Controls.Add(txtHotkey);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            SelectedModifiers = currentMods;
            SelectedKey = currentKey;
            UpdateDisplay();

            if (SelectedKey != Keys.None)
                btnOk.Enabled = true;

            KeyPreview = true;
            KeyDown += HotkeyPickerForm_KeyDown;
        }

        private void HotkeyPickerForm_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                return;

            HotkeyModifiers mods = HotkeyModifiers.None;
            if (e.Control) mods |= HotkeyModifiers.Control;
            if (e.Alt) mods |= HotkeyModifiers.Alt;
            if (e.Shift) mods |= HotkeyModifiers.Shift;
            if ((GetAsyncKeyState(0x5B) & 0x8000) != 0)
                mods |= HotkeyModifiers.Win;

            SelectedModifiers = mods;
            SelectedKey = key;
            UpdateDisplay();
            btnOk.Enabled = true;
        }

        private void UpdateDisplay()
        {
            if (SelectedKey == Keys.None)
            {
                txtHotkey.Text = "";
                return;
            }
            string mods = "";
            if ((SelectedModifiers & HotkeyModifiers.Control) != 0) mods += "Ctrl + ";
            if ((SelectedModifiers & HotkeyModifiers.Alt) != 0) mods += "Alt + ";
            if ((SelectedModifiers & HotkeyModifiers.Shift) != 0) mods += "Shift + ";
            if ((SelectedModifiers & HotkeyModifiers.Win) != 0) mods += "Win + ";
            txtHotkey.Text = mods + SelectedKey;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }

    internal class ResolutionPickerForm : Form
    {
        public int SelectedWidth { get; private set; }
        public int SelectedHeight { get; private set; }

        private readonly ComboBox combo;
        private readonly List<ResolutionMode> modes;

        public ResolutionPickerForm(int currentWidth, int currentHeight)
        {
            Text = "Select Resolution";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 130);

            var lbl = new Label
            {
                Text = "Choose a resolution:",
                Location = new Point(12, 12),
                Size = new Size(276, 20)
            };

            combo = new ComboBox
            {
                Location = new Point(12, 38),
                Size = new Size(276, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            modes = DisplaySettings.GetAvailableResolutions();
            int selectIdx = -1;
            for (int i = 0; i < modes.Count; i++)
            {
                var m = modes[i];
                string label = m.Width + " x " + m.Height;
                combo.Items.Add(label);
                if (m.Width == currentWidth && m.Height == currentHeight)
                    selectIdx = i;
            }
            if (selectIdx >= 0)
                combo.SelectedIndex = selectIdx;

            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(126, 75),
                Size = new Size(75, 26),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) =>
            {
                if (combo.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select a resolution.");
                    DialogResult = DialogResult.None;
                    return;
                }
                var mode = modes[combo.SelectedIndex];
                SelectedWidth = mode.Width;
                SelectedHeight = mode.Height;
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(213, 75),
                Size = new Size(75, 26),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(lbl);
            Controls.Add(combo);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }

    internal class ResolutionMode
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    internal static class DisplaySettings
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll")]
        public static extern int EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DM_PELSWIDTH = 0x80000;
        private const int DM_PELSHEIGHT = 0x100000;

        public static DEVMODE GetCurrent()
        {
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(dm);
            EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm);
            return dm;
        }

        public static int SetResolution(int width, int height)
        {
            DEVMODE dm = GetCurrent();
            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;
            return ChangeDisplaySettings(ref dm, 0);
        }

        public static List<ResolutionMode> GetAvailableResolutions()
        {
            var seen = new HashSet<string>();
            var modes = new List<ResolutionMode>();
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(dm);
            int modeNum = 0;
            while (EnumDisplaySettings(null, modeNum, ref dm) != 0)
            {
                string key = dm.dmPelsWidth + "x" + dm.dmPelsHeight;
                if (seen.Add(key))
                {
                    modes.Add(new ResolutionMode { Width = dm.dmPelsWidth, Height = dm.dmPelsHeight });
                }
                modeNum++;
            }
            modes.Sort((a, b) => (a.Width * a.Height).CompareTo(b.Width * b.Height));
            return modes;
        }
    }
}
