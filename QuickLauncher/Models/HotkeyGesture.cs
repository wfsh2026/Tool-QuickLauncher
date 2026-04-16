using System.Windows.Input;
using QuickLauncher.Infrastructure;

namespace QuickLauncher.Models;

public sealed class HotkeyGesture {
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Windows { get; set; }
    public Key Key { get; set; } = Key.Space;

    public static HotkeyGesture CreateDefault() {
        return new HotkeyGesture {
            Alt = true,
            Key = Key.Space
        };
    }

    public HotkeyGesture Clone() {
        return new HotkeyGesture {
            Control = Control,
            Alt = Alt,
            Shift = Shift,
            Windows = Windows,
            Key = Key
        };
    }

    public uint ToNativeModifiers() {
        uint modifiers = 0;

        if (Alt) {
            modifiers |= NativeMethods.ModAlt;
        }

        if (Control) {
            modifiers |= NativeMethods.ModControl;
        }

        if (Shift) {
            modifiers |= NativeMethods.ModShift;
        }

        if (Windows) {
            modifiers |= NativeMethods.ModWin;
        }

        return modifiers;
    }

    public uint ToVirtualKey() {
        return (uint)KeyInterop.VirtualKeyFromKey(Key);
    }

    public string GetDisplayText() {
        var parts = new List<string>();

        if (Control) {
            parts.Add("Ctrl");
        }

        if (Alt) {
            parts.Add("Alt");
        }

        if (Shift) {
            parts.Add("Shift");
        }

        if (Windows) {
            parts.Add("Win");
        }

        parts.Add(Key.ToString());
        return string.Join(" + ", parts);
    }

    public bool IsValid() {
        return (Control || Alt || Shift || Windows) && Key != Key.None;
    }
}
