using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// Turns the player window into a transparent, always-on-top, click-through overlay covering the
    /// primary screen, and puts an icon with a menu in the task tray. Windows player builds only;
    /// in the editor everything here does nothing (Active stays false).
    /// </summary>
    /// <summary>One display, in virtual-screen pixels (origin = top-left of the primary monitor).</summary>
    public struct BettaMonitor
    {
        public string name;
        public RectInt rect;
        public bool primary;
    }

    public static class BettaWindow
    {
        public const int CmdSettings = 1, CmdFeed = 2, CmdAdd = 3, CmdMouse = 4, CmdQuit = 5, CmdActiveAbove = 6, CmdStartup = 7;

        /// <summary>The overlay window is set up (player build on Windows).</summary>
        public static bool Active { get; private set; }
        /// <summary>Last command chosen from the tray menu (0 = none). Read and clear it.</summary>
        public static int PendingCommand;
        // menu state shown in the tray menu
        public static bool SettingsOpen, MouseReact = true;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // ------------------------------------------------------------------ Win32
        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int left, right, top, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc cb, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int max);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hWnd, IntPtr tpm);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr LoadIcon(IntPtr inst, IntPtr name);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(int msg, ref NOTIFYICONDATA data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetModuleFileName(IntPtr module, StringBuilder buf, int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }
        delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFOEX info);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        // registry (start with Windows)
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegOpenKeyEx(UIntPtr hKey, string subKey, int options, int sam, out IntPtr result);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegSetValueEx(IntPtr hKey, string name, int reserved, int type, string data, int cbData);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegQueryValueEx(IntPtr hKey, string name, IntPtr reserved, out int type, StringBuilder data, ref int cbData);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegDeleteValue(IntPtr hKey, string name);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr hKey);
        static readonly UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "BettaDesktop";
        const int KEY_READ = 0x20019, KEY_WRITE = 0x20006, REG_SZ = 1;

        static readonly System.Collections.Generic.List<BettaMonitor> monitorList = new System.Collections.Generic.List<BettaMonitor>();
        [AOT.MonoPInvokeCallback(typeof(MonitorEnumProc))]
        static bool MonitorCb(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data)
        {
            MONITORINFOEX mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            mi.szDevice = "";
            if (GetMonitorInfo(hMon, ref mi))
            {
                RECT r = mi.rcMonitor;
                monitorList.Add(new BettaMonitor
                {
                    name = mi.szDevice,
                    rect = new RectInt(r.left, r.top, r.right - r.left, r.bottom - r.top),
                    primary = (mi.dwFlags & 1) != 0
                });
            }
            return true;
        }

        static void UsePhysicalPixels()
        {
            // per-monitor DPI awareness v2 for this thread, so every monitor is measured in real pixels
            try { SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch { }
        }

        /// <summary>All displays, left to right.</summary>
        public static BettaMonitor[] GetMonitors()
        {
            UsePhysicalPixels();
            monitorList.Clear();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorCb, IntPtr.Zero);
            if (monitorList.Count == 0)
                monitorList.Add(new BettaMonitor { name = "primary", rect = new RectInt(0, 0, GetSystemMetrics(0), GetSystemMetrics(1)), primary = true });
            monitorList.Sort((a, b) => a.rect.x.CompareTo(b.rect.x));
            return monitorList.ToArray();
        }

        const int GWL_STYLE = -16, GWL_EXSTYLE = -20, GWLP_WNDPROC = -4;
        const long WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L;
        const long WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8;
        const uint SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40, SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2), HWND_TOP = IntPtr.Zero;
        const uint SWP_NOZORDER = 0x4;

        // "active window in front": the overlay sits just below the foreground window instead of on top of everything
        static bool activeAbove, zDirty = true;
        static IntPtr lastFg;
        static float zTimer;
        static IntPtr ZInsert { get { return activeAbove ? IntPtr.Zero : HWND_TOPMOST; } }
        static uint ZFlag { get { return activeAbove ? SWP_NOZORDER : 0u; } }
        const uint WM_NULL = 0, WM_APP = 0x8000, WM_TRAY = WM_APP + 77;
        const int WM_LBUTTONUP = 0x202, WM_RBUTTONUP = 0x205;
        const uint MF_STRING = 0, MF_CHECKED = 8, MF_SEPARATOR = 0x800;
        const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2, TPM_BOTTOMALIGN = 0x20;

        static IntPtr hwnd, oldProc, icon;
        static WndProcDelegate procDelegate;   // kept alive so it is never garbage collected
        static bool clickThrough, trayAdded, iconOwned;
        static int winX, winY, winW, winH;

        static IntPtr foundWindow;
        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        static bool EnumCb(IntPtr h, IntPtr lp)
        {
            StringBuilder sb = new StringBuilder(64);
            GetClassName(h, sb, 64);
            if (sb.ToString() == "UnityWndClass") { foundWindow = h; return false; }
            return true;
        }

        static IntPtr FindPlayerWindow()
        {
            foundWindow = IntPtr.Zero;
            EnumThreadWindows(GetCurrentThreadId(), EnumCb, IntPtr.Zero);
            if (foundWindow == IntPtr.Zero) foundWindow = GetActiveWindow();
            return foundWindow;
        }

        /// <summary>Call once: StartCoroutine(BettaWindow.Setup(area)). area = virtual-screen pixels to cover.</summary>
        public static IEnumerator Setup(RectInt area)
        {
            UsePhysicalPixels();
            winX = area.x; winY = area.y; winW = Mathf.Max(64, area.width); winH = Mathf.Max(64, area.height);
            Screen.SetResolution(winW, winH, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            hwnd = FindPlayerWindow();
            if (hwnd == IntPtr.Zero) { Debug.LogWarning("Betta: ウィンドウが見つかりません"); yield break; }

            ShowWindow(hwnd, 0);   // hide while changing styles so the taskbar button goes away
            clickThrough = true;
            ApplyOverlayStyle();
            ShowWindow(hwnd, 4);   // SW_SHOWNOACTIVATE

            // tray icon + menu messages
            procDelegate = WndProc;
            oldProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(procDelegate));
            AddTrayIcon();
            Active = true;
            FixStartupPath();
            ApplyExStyle();
            zDirty = true;
            // Unity may have clamped the size to one monitor: set it once more now that the window is a popup
            yield return null;
            SetWindowPos(hwnd, ZInsert, winX, winY, winW, winH, SWP_NOACTIVATE | ZFlag);
            zDirty = true;
        }

        /// <summary>Move / resize the overlay to cover another area (after choosing other monitors).</summary>
        public static IEnumerator Place(RectInt area)
        {
            if (!Active) yield break;
            UsePhysicalPixels();
            winX = area.x; winY = area.y; winW = Mathf.Max(64, area.width); winH = Mathf.Max(64, area.height);
            Screen.SetResolution(winW, winH, FullScreenMode.Windowed);
            yield return null;
            yield return null;
            // changing the resolution makes Unity rebuild its window: make it a see-through overlay again
            ApplyOverlayStyle();
            yield return null;
            SetWindowPos(hwnd, ZInsert, winX, winY, winW, winH, SWP_NOACTIVATE | ZFlag);
            zDirty = true;
        }

        static void ApplyOverlayStyle()
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(WS_POPUP | WS_VISIBLE));
            ApplyExStyle();
            MARGINS m = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);
            SetWindowPos(hwnd, ZInsert, winX, winY, winW, winH, SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE | ZFlag);
            zDirty = true;
        }

        /// <summary>Where the window actually is (for the diagnostics file).</summary>
        public static string Describe()
        {
            RECT r;
            GetWindowRect(hwnd, out r);
            return "window " + r.left + "," + r.top + " " + (r.right - r.left) + "x" + (r.bottom - r.top)
                 + " / wanted " + winX + "," + winY + " " + winW + "x" + winH;
        }

        static void ApplyExStyle()
        {
            long ex = WS_EX_LAYERED | WS_EX_TOOLWINDOW | (activeAbove ? 0 : WS_EX_TOPMOST);
            if (clickThrough) ex |= WS_EX_TRANSPARENT;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        }

        /// <summary>true: mouse clicks go through to the windows below. false: this window takes them.</summary>
        public static void SetClickThrough(bool on)
        {
            if (!Active || on == clickThrough) return;
            clickThrough = on;
            ApplyExStyle();
            SetWindowPos(hwnd, ZInsert, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | ZFlag);
        }

        /// <summary>on: the active window stays in front of the fish. off: the fish swim over everything.</summary>
        public static void SetActiveAbove(bool on)
        {
            if (on == activeAbove) return;
            activeAbove = on;
            if (!Active) return;
            ApplyExStyle();
            zDirty = true;
        }

        public static bool ActiveAbove { get { return activeAbove; } }

        /// <summary>Call every frame: keeps the overlay right below the foreground window (when that mode is on).</summary>
        public static void UpdateZOrder(float dt)
        {
            if (!Active) return;
            const uint keep = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;
            if (!activeAbove)
            {
                if (zDirty) { SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, keep); zDirty = false; }
                return;
            }
            zTimer -= dt;
            IntPtr fg = GetForegroundWindow();
            if (fg == lastFg && !zDirty && zTimer > 0f) return;
            lastFg = fg; zDirty = false; zTimer = 0.5f;   // also re-check twice a second
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, keep);
            // our own window, the desktop or the taskbar active: stay above the other windows
            IntPtr after = (fg == IntPtr.Zero || fg == hwnd || IsShell(fg)) ? HWND_TOP : fg;
            SetWindowPos(hwnd, after, 0, 0, 0, 0, keep);
        }

        static bool IsShell(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(64);
            GetClassName(h, sb, 64);
            string c = sb.ToString();
            return c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd";
        }

        // ------------------------------------------------------------------ start with Windows
        static string ExePath()
        {
            StringBuilder sb = new StringBuilder(1024);
            GetModuleFileName(IntPtr.Zero, sb, sb.Capacity);
            return sb.ToString();
        }

        static string ReadRun()
        {
            IntPtr k;
            if (RegOpenKeyEx(HKEY_CURRENT_USER, RunKey, 0, KEY_READ, out k) != 0) return null;
            try
            {
                int type, size = 2048;
                StringBuilder sb = new StringBuilder(1024);
                return RegQueryValueEx(k, RunName, IntPtr.Zero, out type, sb, ref size) == 0 ? sb.ToString() : null;
            }
            finally { RegCloseKey(k); }
        }

        /// <summary>Is the app registered to start when you sign in to Windows?</summary>
        public static bool StartupEnabled() { return !string.IsNullOrEmpty(ReadRun()); }

        public static void SetStartup(bool on)
        {
            IntPtr k;
            if (RegOpenKeyEx(HKEY_CURRENT_USER, RunKey, 0, KEY_READ | KEY_WRITE, out k) != 0) return;
            try
            {
                if (on)
                {
                    string v = "\"" + ExePath() + "\"";
                    RegSetValueEx(k, RunName, 0, REG_SZ, v, (v.Length + 1) * 2);
                }
                else RegDeleteValue(k, RunName);
            }
            finally { RegCloseKey(k); }
        }

        /// <summary>If start-up is on but the exe was moved, point the entry at the current exe.</summary>
        public static void FixStartupPath()
        {
            string v = ReadRun();
            if (!string.IsNullOrEmpty(v) && v.Trim('"') != ExePath()) SetStartup(true);
        }

        /// <summary>Mouse position in Unity screen coordinates (origin bottom-left). false if off this screen.</summary>
        public static bool GetCursor(out Vector2 p)
        {
            POINT pt;
            if (!GetCursorPos(out pt)) { p = Vector2.zero; return false; }
            int x = pt.x - winX, y = pt.y - winY;
            p = new Vector2(x * Screen.width / (float)Mathf.Max(1, winW), Screen.height - y * Screen.height / (float)Mathf.Max(1, winH));
            return x >= 0 && y >= 0 && x < winW && y < winH;
        }

        static void AddTrayIcon()
        {
            StringBuilder sb = new StringBuilder(1024);
            GetModuleFileName(IntPtr.Zero, sb, sb.Capacity);
            // the small icon (tray size, 16px at 100%) is sharper than shrinking the large one
            IntPtr[] small = new IntPtr[1];
            icon = ExtractIconEx(sb.ToString(), 0, null, small, 1) > 0 ? small[0] : IntPtr.Zero;
            iconOwned = icon != IntPtr.Zero;
            if (!iconOwned) icon = LoadIcon(IntPtr.Zero, new IntPtr(32512));   // IDI_APPLICATION
            NOTIFYICONDATA d = NewData();
            d.uFlags = 1 | 2 | 4;   // NIF_MESSAGE | NIF_ICON | NIF_TIP
            d.uCallbackMessage = (int)WM_TRAY;
            d.hIcon = icon;
            d.szTip = "ベタ v" + BettaDesktop.Version + "（右クリックでメニュー）";
            trayAdded = Shell_NotifyIcon(0, ref d);   // NIM_ADD
        }

        static NOTIFYICONDATA NewData()
        {
            NOTIFYICONDATA d = new NOTIFYICONDATA();
            d.cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
            d.hWnd = hwnd;
            d.uID = 1;
            d.szTip = ""; d.szInfo = ""; d.szInfoTitle = "";
            return d;
        }

        [AOT.MonoPInvokeCallback(typeof(WndProcDelegate))]
        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == 0x02E0) return IntPtr.Zero;   // WM_DPICHANGED: keep our size when spanning monitors
            if (msg == WM_TRAY)
            {
                int ev = (int)(lParam.ToInt64() & 0xFFFF);
                if (ev == WM_RBUTTONUP || ev == WM_LBUTTONUP) ShowMenu();
                return IntPtr.Zero;
            }
            return CallWindowProc(oldProc, hWnd, msg, wParam, lParam);
        }

        static void ShowMenu()
        {
            IntPtr menu = CreatePopupMenu();
            AppendMenu(menu, MF_STRING, new UIntPtr((uint)CmdSettings), SettingsOpen ? "設定を閉じる" : "設定を開く");
            AppendMenu(menu, MF_STRING, new UIntPtr((uint)CmdFeed), "エサをあげる");
            AppendMenu(menu, MF_STRING, new UIntPtr((uint)CmdAdd), "ベタを増やす（おまかせ）");
            AppendMenu(menu, MF_STRING | (MouseReact ? MF_CHECKED : 0), new UIntPtr((uint)CmdMouse), "マウスに反応する");
            AppendMenu(menu, MF_STRING | (activeAbove ? MF_CHECKED : 0), new UIntPtr((uint)CmdActiveAbove), "アクティブなウィンドウを前に出す");
            AppendMenu(menu, MF_STRING | (StartupEnabled() ? MF_CHECKED : 0), new UIntPtr((uint)CmdStartup), "Windowsの起動時に起動する");
            AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenu(menu, MF_STRING, new UIntPtr((uint)CmdQuit), "終了");
            POINT pt;
            GetCursorPos(out pt);
            SetForegroundWindow(hwnd);   // so the menu closes when clicking elsewhere
            int cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, pt.x, pt.y, hwnd, IntPtr.Zero);
            PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            DestroyMenu(menu);
            if (cmd > 0) PendingCommand = cmd;
        }

        public static void Shutdown()
        {
            if (trayAdded)
            {
                NOTIFYICONDATA d = NewData();
                Shell_NotifyIcon(2, ref d);   // NIM_DELETE
                trayAdded = false;
            }
            if (oldProc != IntPtr.Zero && hwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, oldProc);
                oldProc = IntPtr.Zero;
            }
            if (iconOwned && icon != IntPtr.Zero) { DestroyIcon(icon); icon = IntPtr.Zero; }
            Active = false;
        }
#else
        // ------------------------------------------------------------------ editor / other platforms
        public static IEnumerator Setup(RectInt area) { yield break; }
        public static IEnumerator Place(RectInt area) { yield break; }
        public static string Describe() { return "editor"; }
        static bool activeAbove;
        public static void SetActiveAbove(bool on) { activeAbove = on; }
        public static bool ActiveAbove { get { return activeAbove; } }
        public static void UpdateZOrder(float dt) { }
        public static bool StartupEnabled() { return false; }
        public static void SetStartup(bool on) { }
        public static BettaMonitor[] GetMonitors()
        {
            return new[] { new BettaMonitor { name = "editor", rect = new RectInt(0, 0, Screen.width, Screen.height), primary = true } };
        }
        public static void SetClickThrough(bool on) { }
        public static bool GetCursor(out Vector2 p)
        {
            p = BettaInput.MousePosition();
            return p.x >= 0f && p.y >= 0f && p.x < Screen.width && p.y < Screen.height;
        }
        public static void Shutdown() { }
#endif
    }

    /// <summary>Mouse / keyboard through whichever input system the project uses.</summary>
    public static class BettaInput
    {
        public static Vector2 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.position.ReadValue() : new Vector2(-1f, -1f);
#else
            return Input.mousePosition;
#endif
        }

        public static bool LeftDown()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        public static bool RightDown()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }

        public static bool EscDown()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
