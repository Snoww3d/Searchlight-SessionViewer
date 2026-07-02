using Microsoft.UI.Xaml;
using Microsoft.Win32;

namespace Searchlight.Services;

/// <summary>
/// Reads the current Windows apps theme (dark vs light) so the window can follow
/// the system setting instead of defaulting to a blinding white titlebar.
/// </summary>
internal static class SystemThemeHelper
{
    // ASSUMPTION: Windows exposes the apps theme at
    // HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize
    // with AppsUseLightTheme (DWORD): 0 = dark, 1 = light. This is the same key
    // File Explorer / Settings toggle writes and is stable across Win10/Win11.
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>
    /// Returns <see cref="ElementTheme.Dark"/> or <see cref="ElementTheme.Light"/>
    /// based on the current OS "app mode" setting. Defaults to Light if the value
    /// cannot be read.
    /// </summary>
    public static ElementTheme GetAppsTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            object? raw = key?.GetValue(AppsUseLightThemeValue);
            if (raw is int appsUseLight)
            {
                return appsUseLight == 0 ? ElementTheme.Dark : ElementTheme.Light;
            }
        }
        catch
        {
            // Registry read is best-effort; fall through to the default below.
        }

        return ElementTheme.Light;
    }
}
