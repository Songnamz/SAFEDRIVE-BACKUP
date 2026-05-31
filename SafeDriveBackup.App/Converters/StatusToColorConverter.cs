using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SafeDriveBackup.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace SafeDriveBackup.Converters;

[ValueConversion(typeof(BackupStatusEnum), typeof(Brush))]
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BackupStatusEnum status)
        {
            return status switch
            {
                BackupStatusEnum.Protected     => new SolidColorBrush(Color.FromRgb(0, 184, 148)),
                BackupStatusEnum.BackingUp     => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                BackupStatusEnum.Warning       => new SolidColorBrush(Color.FromRgb(253, 203, 110)),
                BackupStatusEnum.Error         => new SolidColorBrush(Color.FromRgb(214, 48, 49)),
                BackupStatusEnum.Paused        => new SolidColorBrush(Color.FromRgb(99, 110, 114)),
                BackupStatusEnum.NotConfigured => new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(BackupStatusEnum), typeof(string))]
public class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BackupStatusEnum status)
        {
            return status switch
            {
                BackupStatusEnum.Protected     => "Protected",
                BackupStatusEnum.BackingUp     => "Backing up…",
                BackupStatusEnum.Warning       => "Warning",
                BackupStatusEnum.Error         => "Error",
                BackupStatusEnum.Paused        => "Paused",
                BackupStatusEnum.NotConfigured => "Not selected",
                _ => "Unknown"
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
