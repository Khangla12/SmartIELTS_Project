using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartIELTS.WpfApp.Services;

public class WindowsKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_SNAPSHOT = 0x2C; // Print Screen
    private const int VK_F4 = 0x73;
    private const int VK_CONTROL = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public bool IsEnabled { get; set; } = true;
    public event Action<string>? OnHotkeyBlocked;

    public WindowsKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId == IntPtr.Zero)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule?.ModuleName), 0);
        }
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsEnabled)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vk = hookStruct.vkCode;
                bool isAltPressed = (hookStruct.flags & 0x20) != 0;
                bool isCtrlPressed = (GetKeyState(VK_CONTROL) & 0x8000) != 0;

                string? blockedKey = null;

                // 1. Windows Key (Start menu)
                if (vk == VK_LWIN || vk == VK_RWIN)
                {
                    blockedKey = "Windows Key (Start Menu)";
                }
                // 2. Alt + Tab (Task switcher)
                else if (vk == VK_TAB && isAltPressed)
                {
                    blockedKey = "Alt + Tab (Chuyển cửa sổ)";
                }
                // 3. Alt + Esc or Ctrl + Esc
                else if (vk == VK_ESCAPE && (isAltPressed || isCtrlPressed))
                {
                    blockedKey = "Alt/Ctrl + Escape";
                }
                // 4. PrintScreen (Chụp màn hình đề thi)
                else if (vk == VK_SNAPSHOT)
                {
                    blockedKey = "PrintScreen (Chụp màn hình)";
                }
                // 5. Alt + F4 (Đóng ứng dụng)
                else if (vk == VK_F4 && isAltPressed)
                {
                    blockedKey = "Alt + F4 (Đóng ứng dụng trái phép)";
                }

                if (blockedKey != null)
                {
                    OnHotkeyBlocked?.Invoke(blockedKey);
                    return (IntPtr)1; // Chặn phím không cho hệ điều hành xử lý
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
        GC.SuppressFinalize(this);
    }
}
