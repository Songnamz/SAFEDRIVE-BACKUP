using System.Windows.Media.Imaging;

namespace SafeDriveBackup;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        Icon = SafeDriveBackup.Services.TrayIconService.CreateShieldImageSource(System.Drawing.Color.FromArgb(0, 120, 212), 256);
    }
}
