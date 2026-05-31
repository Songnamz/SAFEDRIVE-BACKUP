using System.Drawing;
using System.Windows.Forms;

namespace SafeDriveBackup.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _tray;
    private ToolStripMenuItem? _pauseItem;
    private ToolStripMenuItem? _resumeItem;
    private bool _disposed;

    private readonly Action _openWindow;
    private readonly Action _backupNow;
    private readonly Action _pause;
    private readonly Action _resume;
    private readonly Action _viewLogs;
    private readonly Action _exit;

    public TrayIconService(
        Action openWindow, Action backupNow,
        Action pause, Action resume,
        Action viewLogs, Action exit)
    {
        _openWindow = openWindow;
        _backupNow = backupNow;
        _pause = pause;
        _resume = resume;
        _viewLogs = viewLogs;
        _exit = exit;
    }

    public void Initialize()
    {
        _tray = new NotifyIcon
        {
            Icon = CreateShieldIcon(Color.FromArgb(0, 120, 212)),
            Text = "SafeDrive Backup - Starting…",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => _openWindow();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(new ToolStripLabel("SafeDrive Backup")
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212)
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open SafeDrive",    null, (_, _) => _openWindow());
        menu.Items.Add("Backup Now",        null, (_, _) => _backupNow());
        menu.Items.Add(new ToolStripSeparator());

        _pauseItem  = new ToolStripMenuItem("Pause Backup");
        _resumeItem = new ToolStripMenuItem("Resume Backup") { Enabled = false };

        _pauseItem.Click  += (_, _) => { _pause();  UpdatePauseState(paused: true); };
        _resumeItem.Click += (_, _) => { _resume(); UpdatePauseState(paused: false); };

        menu.Items.Add(_pauseItem);
        menu.Items.Add(_resumeItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("View Logs", null, (_, _) => _viewLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",      null, (_, _) => _exit());

        return menu;
    }

    private void UpdatePauseState(bool paused)
    {
        if (_pauseItem  != null) _pauseItem.Enabled  = !paused;
        if (_resumeItem != null) _resumeItem.Enabled =  paused;
    }

    public void SetStatus(string statusText, bool error = false, bool warning = false, bool paused = false)
    {
        if (_tray == null) return;

        Color color;
        if (error)        color = Color.FromArgb(214, 48, 49);   // red
        else if (warning) color = Color.FromArgb(253, 203, 110); // yellow
        else if (paused)  color = Color.FromArgb(108, 117, 125); // grey
        else              color = Color.FromArgb(0, 184, 148);   // green

        _tray.Icon = CreateShieldIcon(color);
        SetTooltip($"SafeDrive Backup - {statusText}");
        UpdatePauseState(paused);
    }

    public void SetTooltip(string text)
    {
        if (_tray == null) return;
        _tray.Text = text.Length > 63 ? text[..63] : text;
    }

    public void ShowBalloon(string title, string message,
        ToolTipIcon icon = ToolTipIcon.Info)
    {
        _tray?.ShowBalloonTip(4000, title, message, icon);
    }

    public static System.Windows.Media.ImageSource CreateShieldImageSource(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        DrawShield(bmp, color);
        var hBitmap = bmp.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally { DeleteObject(hBitmap); }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static void DrawShield(Bitmap bmp, Color color)
    {
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var shield = new System.Drawing.Drawing2D.GraphicsPath();
        shield.AddPolygon(new PointF[]
        {
            new(16, 2), new(29, 7), new(29, 17),
            new(16, 30), new(3, 17), new(3, 7)
        });

        using var fill = new SolidBrush(color);
        using var outline = new Pen(Color.FromArgb(180, 255, 255, 255), 1f);
        g.FillPath(fill, shield);
        g.DrawPath(outline, shield);

        using var check = new Pen(Color.White, 2.5f)
            { StartCap = System.Drawing.Drawing2D.LineCap.Round,
              EndCap   = System.Drawing.Drawing2D.LineCap.Round };
        g.DrawLines(check, new PointF[] { new(10, 16), new(14, 21), new(22, 11) });
    }

    private static Icon CreateShieldIcon(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        DrawShield(bmp, color);
        var hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var clone = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return clone;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
    }
}
