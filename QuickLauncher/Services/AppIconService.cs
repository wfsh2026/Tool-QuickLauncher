using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class AppIconService {
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiPidl = 0x000000008;

    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? GetIcon(AppEntry entry) {
        var cacheKey = BuildCacheKey(entry);
        return _cache.GetOrAdd(cacheKey, _ => ResolveIcon(entry));
    }

    private static string BuildCacheKey(AppEntry entry) {
        return entry.LaunchKind switch {
            LaunchKind.Uwp => $"uwp::{entry.LaunchTarget}",
            LaunchKind.Shortcut when !string.IsNullOrWhiteSpace(entry.ResolvedTarget) => $"shortcut::{entry.ResolvedTarget}",
            _ => $"file::{entry.LaunchTarget}"
        };
    }

    private static ImageSource? ResolveIcon(AppEntry entry) {
        return entry.LaunchKind switch {
            LaunchKind.Uwp => GetUwpIcon(entry.LaunchTarget),
            LaunchKind.Shortcut => GetFileIcon(string.IsNullOrWhiteSpace(entry.ResolvedTarget) ? entry.LaunchTarget : entry.ResolvedTarget),
            LaunchKind.Executable => GetFileIcon(entry.LaunchTarget),
            _ => null
        };
    }

    private static ImageSource? GetFileIcon(string path) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            return null;
        }

        var result = NativeShellMethods.SHGetFileInfo(
            path,
            0,
            out var fileInfo,
            (uint)Marshal.SizeOf<NativeShellMethods.ShFileInfo>(),
            ShgfiIcon | ShgfiSmallIcon);

        if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero) {
            return null;
        }

        try {
            return CreateBitmapSource(fileInfo.hIcon);
        }
        finally {
            NativeShellMethods.DestroyIcon(fileInfo.hIcon);
        }
    }

    private static ImageSource? GetUwpIcon(string appUserModelId) {
        if (string.IsNullOrWhiteSpace(appUserModelId)) {
            return null;
        }

        var shellPath = $"shell:AppsFolder\\{appUserModelId}";
        var parseResult = NativeShellMethods.SHParseDisplayName(shellPath, IntPtr.Zero, out var pidl, 0, out _);
        if (parseResult != 0 || pidl == IntPtr.Zero) {
            return null;
        }

        try {
            var result = NativeShellMethods.SHGetFileInfo(
                pidl,
                0,
                out var fileInfo,
                (uint)Marshal.SizeOf<NativeShellMethods.ShFileInfo>(),
                ShgfiPidl | ShgfiIcon | ShgfiSmallIcon);

            if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero) {
                return null;
            }

            try {
                return CreateBitmapSource(fileInfo.hIcon);
            }
            finally {
                NativeShellMethods.DestroyIcon(fileInfo.hIcon);
            }
        }
        finally {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    private static ImageSource CreateBitmapSource(IntPtr iconHandle) {
        var image = Imaging.CreateBitmapSourceFromHIcon(
            iconHandle,
            System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(32, 32));
        image.Freeze();
        return image;
    }

    private static class NativeShellMethods {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(
            string path,
            uint fileAttributes,
            out ShFileInfo fileInfo,
            uint cbFileInfo,
            uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(
            IntPtr pidl,
            uint fileAttributes,
            out ShFileInfo fileInfo,
            uint cbFileInfo,
            uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName(
            string name,
            IntPtr bindingContext,
            out IntPtr pidl,
            uint sfgaoIn,
            out uint psfgaoOut);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr iconHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ShFileInfo {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
    }
}
