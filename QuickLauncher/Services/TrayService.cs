using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace QuickLauncher.Services;

public sealed class TrayService : IDisposable {
    private readonly Forms.NotifyIcon _notifyIcon;

    public event EventHandler? ShowRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayService() {
        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("显示搜索框", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        contextMenu.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon {
            Text = "QuickLauncher",
            Icon = CreateTrayIcon(),
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon CreateTrayIcon() {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        // Background: rounded square with dark blue
        using var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bgPath = CreateRoundedRect(2, 2, size - 4, size - 4, 14);
        graphics.FillPath(bgBrush, bgPath);

        // Lightning bolt icon (represents "quick launch")
        var bolt = new PointF[] {
            new(36, 8),   // top right
            new(22, 30),  // middle left
            new(32, 30),  // middle center-left
            new(28, 56),  // bottom left
            new(42, 28),  // middle right
            new(32, 28),  // middle center-right
        };

        using var boltBrush = new LinearGradientBrush(
            new Point(28, 8), new Point(36, 56),
            Color.FromArgb(96, 165, 250),   // #60A5FA
            Color.FromArgb(59, 130, 246));  // #3B82F6
        graphics.FillPolygon(boltBrush, bolt);

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static GraphicsPath CreateRoundedRect(float x, float y, float w, float h, float r) {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}
