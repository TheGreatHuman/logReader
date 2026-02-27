using System.Windows;
using LogVision.Services;

namespace LogVision;

public partial class App : Application
{
    public static ThemeService ThemeService { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Initialize();
    }
}
