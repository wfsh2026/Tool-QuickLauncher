using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickLauncher;

public static class Program {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MbOk = 0x00000000;
    private const uint MbIconError = 0x00000010;

    [STAThread]
    public static void Main() {
        try {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex) {
            var message = BuildCrashReport(ex);
            WriteCrashLog(message);
            MessageBoxW(IntPtr.Zero, message, "QuickLauncher - 启动失败", MbOk | MbIconError);
        }
    }

    private static string BuildCrashReport(Exception ex) {
        var info = new StringBuilder();
        info.AppendLine("QuickLauncher 启动时发生致命错误:");
        info.AppendLine();
        info.AppendLine($"异常类型: {ex.GetType().FullName}");
        info.AppendLine($"异常信息: {ex.Message}");

        var inner = ex.InnerException;
        var depth = 0;
        while (inner is not null && depth < 5) {
            info.AppendLine();
            info.AppendLine($"内部异常 [{depth + 1}]: {inner.GetType().FullName}");
            info.AppendLine($"内部信息: {inner.Message}");
            inner = inner.InnerException;
            depth++;
        }

        info.AppendLine();
        info.AppendLine("--- 环境信息 ---");
        try { info.AppendLine($"操作系统: {RuntimeInformation.OSDescription}"); } catch { }
        try { info.AppendLine($"架构: {RuntimeInformation.OSArchitecture}"); } catch { }
        try { info.AppendLine($"运行时: {RuntimeInformation.FrameworkDescription}"); } catch { }
        try { info.AppendLine($"进程路径: {Environment.ProcessPath}"); } catch { }
        try { info.AppendLine($"工作目录: {Environment.CurrentDirectory}"); } catch { }
        try { info.AppendLine($"AppData: {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}"); } catch { }
        try { info.AppendLine($"Temp: {Path.GetTempPath()}"); } catch { }

        info.AppendLine();
        info.AppendLine("--- 堆栈跟踪 ---");
        info.AppendLine(ex.StackTrace);

        return info.ToString();
    }

    private static void WriteCrashLog(string content) {
        try {
            var exePath = Environment.ProcessPath;
            var logDir = string.IsNullOrEmpty(exePath)
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
            var logPath = Path.Combine(logDir, "QuickLauncher_crash.log");
            File.WriteAllText(logPath, content, Encoding.UTF8);
        }
        catch {
            try {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                File.WriteAllText(Path.Combine(desktop, "QuickLauncher_crash.log"), content, Encoding.UTF8);
            }
            catch { }
        }
    }
}
