using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Heartbeat.Agent.Utils
{
    public static class ActiveWindowHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        // WinEventHook 相关
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WM_QUIT = 0x0012;

        private const int OBJID_WINDOW = 0;
        private const int CHILDID_SELF = 0;

        private static WinEventDelegate? _winEventDelegate;
        private static IntPtr _foregroundHook;
        private static IntPtr _minimizeStartHook;
        private static IntPtr _minimizeEndHook;
        private static IntPtr _nameChangeHook;
        private static uint _messageLoopThreadId;

        /// <summary>
        /// 前台窗口切换时触发，参数为新的前台窗口采样（进程名 + 标题，可能为 None）
        /// </summary>
        public static event Action<ForegroundWindow>? ForegroundWindowChanged;

        /// <summary>
        /// 获取当前前台窗口采样（进程名 + 标题）
        /// </summary>
        public static ForegroundWindow GetForegroundWindowSample()
        {
            IntPtr hwnd = GetForegroundWindow();
            return SampleWindow(hwnd);
        }

        /// <summary>给定窗口句柄采样（进程名 + 标题）。</summary>
        private static ForegroundWindow SampleWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return ForegroundWindow.None;
            return new ForegroundWindow(GetProcessNameFromHwnd(hwnd), GetWindowTitle(hwnd));
        }

        /// <summary>
        /// 启动事件钩子，监听前台窗口切换。必须在专用线程上调用（内部运行消息循环）。
        /// </summary>
        public static void StartHook()
        {
            _messageLoopThreadId = GetCurrentThreadId();

            // 必须保持委托引用，防止 GC 回收
            _winEventDelegate = new WinEventDelegate(OnWinEvent);

            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT);

            _minimizeStartHook = SetWinEventHook(
                EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZESTART,
                IntPtr.Zero, _winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT);

            _minimizeEndHook = SetWinEventHook(
                EVENT_SYSTEM_MINIMIZEEND, EVENT_SYSTEM_MINIMIZEEND,
                IntPtr.Zero, _winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT);

            // 标题变化（同一前台窗口内切 tab / 改标题）。详见 ADR-015。
            // 此事件对任意窗口高频触发，回调里严格过滤到"当前前台窗口 + 窗口对象本身"。
            _nameChangeHook = SetWinEventHook(
                EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE,
                IntPtr.Zero, _winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT);

            // 运行消息循环（阻塞当前线程）
            int ret;
            while ((ret = GetMessage(out MSG msg, IntPtr.Zero, 0, 0)) != 0)
            {
                if (ret == -1) break;
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            // 清理
            if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
            if (_minimizeStartHook != IntPtr.Zero) UnhookWinEvent(_minimizeStartHook);
            if (_minimizeEndHook != IntPtr.Zero) UnhookWinEvent(_minimizeEndHook);
            if (_nameChangeHook != IntPtr.Zero) UnhookWinEvent(_nameChangeHook);
            _foregroundHook = IntPtr.Zero;
            _minimizeStartHook = IntPtr.Zero;
            _minimizeEndHook = IntPtr.Zero;
            _nameChangeHook = IntPtr.Zero;
        }

        /// <summary>
        /// 停止事件钩子，退出消息循环
        /// </summary>
        public static void StopHook()
        {
            if (_messageLoopThreadId != 0)
            {
                PostThreadMessage(_messageLoopThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private static void OnWinEvent(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            IntPtr fg = GetForegroundWindow();

            if (eventType == EVENT_OBJECT_NAMECHANGE)
            {
                // NAMECHANGE 对任意窗口/子控件高频触发。只关心"当前前台窗口本身"的标题变化：
                // 过滤掉子控件（idObject/idChild）和非前台窗口，否则引入大量噪声与开销。
                if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return;
                if (hwnd != fg) return;
            }

            // FOREGROUND / MINIMIZE / 通过过滤的 NAMECHANGE：以当前前台窗口采样后上报。
            ForegroundWindowChanged?.Invoke(SampleWindow(fg));
        }

        private static string? GetWindowTitle(IntPtr hWnd)
        {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0) return null;
            var sb = new System.Text.StringBuilder(len + 1);
            int copied = GetWindowText(hWnd, sb, sb.Capacity);
            if (copied <= 0) return null;
            return sb.ToString();
        }

        // 进程名单条缓存：同一前台窗口在一次"停留"内 hwnd 不变，
        // 高频 NAMECHANGE（标题抖动）时命中缓存，省去 Process.GetProcessById 开销。
        private static IntPtr _cachedHwnd;
        private static string? _cachedProcessName;

        private static string? GetProcessNameFromHwnd(IntPtr hWnd)
        {
            if (hWnd == _cachedHwnd) return _cachedProcessName;

            GetWindowThreadProcessId(hWnd, out uint pid);
            string? name;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
            }
            catch
            {
                name = null;
            }

            _cachedHwnd = hWnd;
            _cachedProcessName = name;
            return name;
        }
    }
}
