using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class AppDiscoveryService {
    public Task<IReadOnlyList<AppEntry>> DiscoverAsync(UserSettings settings) {
        return Task.Run(() => Discover(settings));
    }

    private IReadOnlyList<AppEntry> Discover(UserSettings settings) {
        var entries = new List<AppEntry>();

        if (settings.SearchStartMenu) {
            entries.AddRange(DiscoverShortcuts(GetStartMenuDirectories(), SourceType.StartMenu));
        }

        if (settings.SearchDesktop) {
            entries.AddRange(DiscoverShortcuts(GetDesktopDirectories(), SourceType.Desktop));
        }

        if (settings.SearchUwp) {
            entries.AddRange(DiscoverUwpApps());
        }

        if (settings.CustomFolders.Count > 0) {
            entries.AddRange(DiscoverExecutables(settings.CustomFolders));
        }

        return Deduplicate(entries);
    }

    private static IEnumerable<AppEntry> DiscoverShortcuts(IEnumerable<string> directories, SourceType sourceType) {
        foreach (var directory in directories.Where(Directory.Exists)) {
            foreach (var shortcutPath in EnumerateFilesSafe(directory, "*.lnk")) {
                var displayName = Path.GetFileNameWithoutExtension(shortcutPath);
                if (ShouldIgnore(displayName)) {
                    continue;
                }

                var resolved = ResolveShortcut(shortcutPath);
                var resolvedTarget = resolved.TargetPath;
                var executableName = string.IsNullOrWhiteSpace(resolvedTarget)
                    ? string.Empty
                    : Path.GetFileName(resolvedTarget);

                yield return CreateEntry(
                    displayName,
                    executableName,
                    LaunchKind.Shortcut,
                    shortcutPath,
                    resolvedTarget,
                    resolved.Arguments,
                    shortcutPath,
                    sourceType);
            }

            foreach (var exePath in EnumerateFilesSafe(directory, "*.exe")) {
                var displayName = Path.GetFileNameWithoutExtension(exePath);
                if (ShouldIgnore(displayName)) {
                    continue;
                }

                yield return CreateEntry(
                    displayName,
                    Path.GetFileName(exePath),
                    LaunchKind.Executable,
                    exePath,
                    exePath,
                    string.Empty,
                    exePath,
                    sourceType);
            }
        }
    }

    private static IEnumerable<AppEntry> DiscoverExecutables(IEnumerable<string> directories) {
        foreach (var directory in directories.Where(Directory.Exists)) {
            foreach (var executablePath in EnumerateFilesSafe(directory, "*.exe")) {
                var displayName = Path.GetFileNameWithoutExtension(executablePath);
                if (ShouldIgnore(displayName)) {
                    continue;
                }

                yield return CreateEntry(
                    displayName,
                    Path.GetFileName(executablePath),
                    LaunchKind.Executable,
                    executablePath,
                    executablePath,
                    string.Empty,
                    executablePath,
                    SourceType.CustomFolder);
            }
        }
    }

    private static IEnumerable<AppEntry> DiscoverUwpApps() {
        object? shellObject = null;
        object? folderObject = null;

        try {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) {
                yield break;
            }

            shellObject = Activator.CreateInstance(shellType);
            if (shellObject is null) {
                yield break;
            }

            dynamic shell = shellObject;
            folderObject = shell.NameSpace("shell:AppsFolder");
            if (folderObject is null) {
                yield break;
            }

            dynamic folder = folderObject;
            foreach (var rawItem in (IEnumerable)folder.Items()) {
                dynamic item = rawItem;
                var displayName = Convert.ToString(item.Name) ?? string.Empty;
                if (ShouldIgnore(displayName)) {
                    continue;
                }

                var appUserModelId = Convert.ToString(item.Path) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(appUserModelId)) {
                    continue;
                }

                yield return CreateEntry(
                    displayName,
                    displayName,
                    LaunchKind.Uwp,
                    appUserModelId,
                    appUserModelId,
                    string.Empty,
                    "shell:AppsFolder",
                    SourceType.Uwp);
            }
        }
        finally {
            ReleaseComObject(folderObject);
            ReleaseComObject(shellObject);
        }
    }

    private static AppEntry CreateEntry(
        string displayName,
        string executableName,
        LaunchKind launchKind,
        string launchTarget,
        string resolvedTarget,
        string arguments,
        string sourcePath,
        SourceType sourceType) {
        return new AppEntry {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = displayName.Trim(),
            ExecutableName = executableName,
            LaunchKind = launchKind,
            LaunchTarget = launchTarget,
            ResolvedTarget = resolvedTarget,
            Arguments = arguments,
            SourcePath = sourcePath,
            SourceType = sourceType,
            LastIndexedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<AppEntry> Deduplicate(IEnumerable<AppEntry> entries) {
        var byTarget = entries
            .GroupBy(BuildDeduplicateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => GetSourcePriority(item.SourceType))
                .ThenBy(item => item.DisplayName.Length)
                .First());

        return byTarget
            .GroupBy(entry => NormalizeForKey(entry.DisplayName), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => GetSourcePriority(item.SourceType))
                .ThenBy(item => item.DisplayName.Length)
                .First())
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string BuildDeduplicateKey(AppEntry entry) {
        if (entry.LaunchKind == LaunchKind.Uwp) {
            return $"uwp::{entry.LaunchTarget.Trim().ToLowerInvariant()}";
        }

        var target = string.IsNullOrWhiteSpace(entry.ResolvedTarget)
            ? entry.LaunchTarget
            : entry.ResolvedTarget;

        if (!string.IsNullOrWhiteSpace(target)) {
            return $"path::{target.Trim().ToLowerInvariant()}";
        }

        return $"name::{NormalizeForKey(entry.DisplayName)}";
    }

    private static int GetSourcePriority(SourceType sourceType) {
        return sourceType switch {
            SourceType.StartMenu => 4,
            SourceType.Uwp => 3,
            SourceType.Desktop => 2,
            SourceType.CustomFolder => 1,
            _ => 0
        };
    }

    private static string NormalizeForKey(string text) {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text) {
            if (char.IsLetterOrDigit(character)) {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static bool ShouldIgnore(string displayName) {
        if (string.IsNullOrWhiteSpace(displayName)) {
            return true;
        }

        var lowered = displayName.ToLowerInvariant();
        return lowered.Contains("uninstall") ||
               lowered.Contains("helper") ||
               lowered.Contains("crash") ||
               lowered.Contains("report") ||
               lowered.Contains("update") ||
               lowered.Contains("setup") ||
               lowered.Contains("repair");
    }

    private static IEnumerable<string> GetStartMenuDirectories() {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs");
    }

    private static IEnumerable<string> GetDesktopDirectories() {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootDirectory, string searchPattern) {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);

        while (pendingDirectories.Count > 0) {
            var currentDirectory = pendingDirectories.Pop();
            IEnumerable<string> childDirectories = [];
            IEnumerable<string> files = [];

            try {
                childDirectories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch {
            }

            try {
                files = Directory.EnumerateFiles(currentDirectory, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch {
            }

            foreach (var file in files) {
                yield return file;
            }

            foreach (var childDirectory in childDirectories) {
                pendingDirectories.Push(childDirectory);
            }
        }
    }

    private static (string TargetPath, string Arguments) ResolveShortcut(string shortcutPath) {
        object? shellObject = null;
        object? shortcutObject = null;

        try {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) {
                return (string.Empty, string.Empty);
            }

            shellObject = Activator.CreateInstance(shellType);
            if (shellObject is null) {
                return (string.Empty, string.Empty);
            }

            dynamic shell = shellObject;
            shortcutObject = shell.CreateShortcut(shortcutPath);
            if (shortcutObject is null) {
                return (string.Empty, string.Empty);
            }

            dynamic shortcut = shortcutObject;
            var targetPath = Convert.ToString(shortcut.TargetPath) ?? string.Empty;
            var arguments = Convert.ToString(shortcut.Arguments) ?? string.Empty;
            return (targetPath, arguments);
        }
        catch {
            return (string.Empty, string.Empty);
        }
        finally {
            ReleaseComObject(shortcutObject);
            ReleaseComObject(shellObject);
        }
    }

    private static void ReleaseComObject(object? comObject) {
        if (comObject is null || !Marshal.IsComObject(comObject)) {
            return;
        }

        try {
            Marshal.FinalReleaseComObject(comObject);
        }
        catch {
        }
    }
}
