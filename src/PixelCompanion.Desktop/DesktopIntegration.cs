using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using PixelCompanion.Core.Models;

namespace PixelCompanion.Desktop;

public interface IDesktopIntegration
{
    IReadOnlyList<MovementSurface> GetWindowSurfaces(IReadOnlyCollection<string> excludedProcesses);
    bool IsForegroundFullScreen();
    void SetClickThrough(nint petWindowHandle, bool enabled);
    bool IsClickThroughHotKeyPressed();
    bool SetAutoStart(bool enabled);
}

public sealed class SafeDesktopIntegration : IDesktopIntegration
{
    public IReadOnlyList<MovementSurface> GetWindowSurfaces(IReadOnlyCollection<string> excludedProcesses) => [];
    public bool IsForegroundFullScreen() => false;
    public void SetClickThrough(nint petWindowHandle, bool enabled) { }
    public bool IsClickThroughHotKeyPressed() => false;
    public bool SetAutoStart(bool enabled) => false;
}

public sealed class WindowsDesktopIntegration : IDesktopIntegration
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExToolWindow = 0x80;
    private const long WsExNoActivate = 0x08000000;
    private const uint GwOwner = 4;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkP = 0x50;
    private bool _hotKeyWasDown;

    public IReadOnlyList<MovementSurface> GetWindowSurfaces(IReadOnlyCollection<string> excludedProcesses)
    {
        if (!OperatingSystem.IsWindows()) return [];

        var excluded = excludedProcesses
            .Select(NormalizeProcessName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ownProcessId = (uint)Environment.ProcessId;
        var surfaces = new List<MovementSurface>();
        _ = EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window) || GetWindow(window, GwOwner) != nint.Zero)
                return true;

            var extendedStyle = GetWindowLongPtr(window, GwlExStyle).ToInt64();
            if ((extendedStyle & (WsExToolWindow | WsExNoActivate)) != 0)
                return true;

            GetWindowThreadProcessId(window, out var processId);
            if (processId == ownProcessId)
                return true;

            var className = ReadClassName(window);
            if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW")
                return true;

            var title = ReadWindowTitle(window);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            var processName = ReadProcessName(processId);
            if (excluded.Contains(NormalizeProcessName(processName)) ||
                processName.StartsWith("PixelCompanion", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!TryGetWindowBounds(window, out var bounds) || bounds.Width < 220 || bounds.Height < 120)
                return true;

            surfaces.Add(new MovementSurface(
                $"window:{window.ToInt64():X}",
                MovementSurfaceKind.WindowTop,
                bounds,
                window.ToInt64(),
                processName));
            return true;
        }, nint.Zero);

        return surfaces;
    }

    public bool IsForegroundFullScreen()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var window = GetForegroundWindow();
        if (window == nint.Zero || !IsWindowVisible(window) || IsIconic(window))
            return false;

        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == (uint)Environment.ProcessId)
            return false;

        if (!TryGetWindowBounds(window, out var bounds))
            return false;

        var monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == nint.Zero || !GetMonitorInfo(monitor, ref info))
            return false;

        const double tolerance = 3;
        return Math.Abs(bounds.X - info.Monitor.Left) <= tolerance &&
               Math.Abs(bounds.Y - info.Monitor.Top) <= tolerance &&
               Math.Abs(bounds.Right - info.Monitor.Right) <= tolerance &&
               Math.Abs(bounds.Bottom - info.Monitor.Bottom) <= tolerance;
    }

    public void SetClickThrough(nint petWindowHandle, bool enabled)
    {
        if (!OperatingSystem.IsWindows() || petWindowHandle == nint.Zero) return;
        var style = GetWindowLongPtr(petWindowHandle, GwlExStyle).ToInt64();
        var next = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        if (next != style) _ = SetWindowLongPtr(petWindowHandle, GwlExStyle, new nint(next));
    }

    public bool IsClickThroughHotKeyPressed()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var down = IsKeyDown(VkControl) && IsKeyDown(VkMenu) && IsKeyDown(VkP);
        var pressed = down && !_hotKeyWasDown;
        _hotKeyWasDown = down;
        return pressed;
    }

    public bool SetAutoStart(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (key is null) return false;
            if (enabled)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable)) return false;
                key.SetValue("PixelCompanion", $"\"{executable}\"");
            }
            else
            {
                key.DeleteValue("PixelCompanion", false);
            }
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool TryGetWindowBounds(nint window, out DesktopRect bounds)
    {
        if (DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out var rect, Marshal.SizeOf<NativeRect>()) != 0 &&
            !GetWindowRect(window, out rect))
        {
            bounds = default;
            return false;
        }

        bounds = new DesktopRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return bounds.IsValid;
    }

    private static string ReadWindowTitle(nint window)
    {
        var length = GetWindowTextLength(window);
        if (length <= 0) return "";
        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(nint window)
    {
        var builder = new StringBuilder(256);
        _ = GetClassName(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadProcessName(uint processId)
    {
        try { return Process.GetProcessById((int)processId).ProcessName; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ""; }
    }

    private static string NormalizeProcessName(string name) =>
        Path.GetFileNameWithoutExtension(name.Trim());

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder className, int maximum);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr64(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint window, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(nint window, int index, int value);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int size);

    private static nint GetWindowLongPtr(nint window, int index) =>
        nint.Size == 8 ? GetWindowLongPtr64(window, index) : new nint(GetWindowLong32(window, index));

    private static nint SetWindowLongPtr(nint window, int index, nint value) =>
        nint.Size == 8 ? SetWindowLongPtr64(window, index, value) : new nint(SetWindowLong32(window, index, value.ToInt32()));
}
