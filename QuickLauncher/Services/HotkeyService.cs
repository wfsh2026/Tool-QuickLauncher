using System.Windows.Interop;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class HotkeyService : IDisposable {
    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;
    private readonly int _hotkeyId = 1001;
    private HotkeyGesture? _registeredGesture;

    public event EventHandler? Activated;

    public HotkeyService(IntPtr windowHandle) {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("无法创建 HwndSource。");
        _source.AddHook(WndProc);
    }

    public bool Register(HotkeyGesture gesture) {
        if (!gesture.IsValid()) {
            return false;
        }

        var previous = _registeredGesture?.Clone();
        UnregisterCurrent();

        if (NativeMethods.RegisterHotKey(_windowHandle, _hotkeyId, gesture.ToNativeModifiers(), gesture.ToVirtualKey())) {
            _registeredGesture = gesture.Clone();
            return true;
        }

        if (previous is not null &&
            NativeMethods.RegisterHotKey(_windowHandle, _hotkeyId, previous.ToNativeModifiers(), previous.ToVirtualKey())) {
            _registeredGesture = previous;
        }

        return false;
    }

    public void Dispose() {
        UnregisterCurrent();
        _source.RemoveHook(WndProc);
    }

    private void UnregisterCurrent() {
        if (_registeredGesture is null) {
            return;
        }

        NativeMethods.UnregisterHotKey(_windowHandle, _hotkeyId);
        _registeredGesture = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == _hotkeyId) {
            Activated?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
