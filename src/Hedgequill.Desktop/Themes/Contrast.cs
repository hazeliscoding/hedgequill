using Avalonia.Platform;
using Avalonia.Styling;
using Hedgequill.Core.Projects;

namespace Hedgequill.Desktop.Themes;

/// <summary>
/// High contrast, as the prototype defines it: solid borders and stronger text, over the light or dark theme. Hedgequill uses it
/// when the system asks for more contrast, as Windows' contrast themes and macOS's Increase Contrast do.
/// </summary>
public static class Contrast
{
    public static readonly ThemeVariant LightHigh = new("LightHighContrast", ThemeVariant.Light);

    public static readonly ThemeVariant DarkHigh = new("DarkHighContrast", ThemeVariant.Dark);

    /// <summary>The theme for View › Appearance and the system's settings: Match system follows the system's light or dark.</summary>
    public static ThemeVariant For(Appearance appearance, PlatformColorValues? system)
    {
        if (system?.ContrastPreference != ColorContrastPreference.High)
        {
            return appearance switch
            {
                Appearance.Light => ThemeVariant.Light,
                Appearance.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        var dark = appearance == Appearance.Dark || (appearance == Appearance.System && system.ThemeVariant == PlatformThemeVariant.Dark);
        return dark ? DarkHigh : LightHigh;
    }
}
