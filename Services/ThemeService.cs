using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using ThemeMode = LogVision.Models.ThemeMode;

namespace LogVision.Services;

public class ThemeService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogVision");

    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    private ResourceDictionary? _currentThemeDictionary;

    public ThemeMode CurrentMode { get; private set; } = ThemeMode.Dark;

    /// <summary>
    /// The resolved theme (Dark or Light). When CurrentMode is Auto, this reflects the system theme.
    /// </summary>
    public ThemeMode EffectiveTheme =>
        CurrentMode == ThemeMode.Auto ? GetSystemTheme() : CurrentMode;

    public event Action? ThemeChanged;

    public void Initialize()
    {
        LoadPreference();
        ApplyTheme(CurrentMode);
        StartSystemThemeMonitoring();
    }

    public void ApplyTheme(ThemeMode mode)
    {
        CurrentMode = mode;

        var effective = EffectiveTheme;
        var uri = effective == ThemeMode.Light
            ? new Uri("Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Themes/DarkTheme.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = uri };

        var mergedDicts = Application.Current.Resources.MergedDictionaries;

        if (_currentThemeDictionary != null)
        {
            mergedDicts.Remove(_currentThemeDictionary);
        }

        mergedDicts.Add(newDict);
        _currentThemeDictionary = newDict;

        SavePreference();
        ThemeChanged?.Invoke();
    }

    public static ThemeMode GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intVal)
                return intVal == 1 ? ThemeMode.Light : ThemeMode.Dark;
        }
        catch
        {
            // Fall back to dark if registry read fails
        }

        return ThemeMode.Dark;
    }

    private void StartSystemThemeMonitoring()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && CurrentMode == ThemeMode.Auto)
            {
                Application.Current.Dispatcher.Invoke(() => ApplyTheme(ThemeMode.Auto));
            }
        };
    }

    private void LoadPreference()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;

            var json = File.ReadAllText(SettingsFile);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("theme", out var themeProp))
            {
                if (Enum.TryParse<ThemeMode>(themeProp.GetString(), true, out var mode))
                {
                    CurrentMode = mode;
                }
            }
        }
        catch
        {
            // Use default on any error
        }
    }

    private void SavePreference()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(new { theme = CurrentMode.ToString() });
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Best effort
        }
    }
}
