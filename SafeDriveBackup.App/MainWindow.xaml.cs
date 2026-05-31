using System.Windows.Media.Imaging;

namespace SafeDriveBackup;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        Icon = new BitmapImage(new Uri("pack://application:,,,/SafeDrive.ico"));
    }
}
