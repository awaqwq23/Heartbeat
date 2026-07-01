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
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WM_QUIT = 0x0012;

        private static WinEventDelegate? _winEventDelegate;
        private static IntPtr _foregroundHook;
        private static IntPtr _minimizeStartHook;
        private static IntPtr _minimizeEndHook;
        private static uint _messageLoopThreadId;

        /// <summary>
        /// 前台窗口切换时触发，参数为新的前台窗口采样（进程名 + 标题，可能为 None）
        /// </summary>
        public static event Action<ForegroundWindow>? ForegroundWindowChanged;

        /// <summary>
        /// 获取当前前台窗口采样（进程名 + 标题）
        /// </summary>
        public static ForegroundWindow GetForegroundWindow_()
        {
            IntPtr hwnd = GetForegroundWindow();
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
            _foregroundHook = IntPtr.Zero;
            _minimizeStartHook = IntPtr.Zero;
            _minimizeEndHook = IntPtr.Zero;
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
            // 对于所有事件，都重新获取当前前台窗口，确保准确
            ForegroundWindowChanged?.Invoke(GetForegroundWindow_());
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

        private static string? GetProcessNameFromHwnd(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                return null;
            }
        }
    }
}
