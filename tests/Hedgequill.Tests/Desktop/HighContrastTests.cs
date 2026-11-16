using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Hedgequill.Core.Projects;
using Hedgequill.Desktop.Themes;
using Hedgequill.Tests.Core;
using static Hedgequill.Tests.Desktop.Ui;

namespace Hedgequill.Tests.Desktop;

/// <summary>Windows' contrast themes and macOS's Increase Contrast switch Hedgequill to the prototype's high contrast.</summary>
public sealed class HighContrastTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public HighContrastTests() => _temp.Write("stories/Tides/Chapter 1/Fog.md", "Invented fog rolled in.");

    [Theory]
    [InlineData(Appearance.System, PlatformThemeVariant.Light, ColorContrastPreference.NoPreference, "Default")]
    [InlineData(Appearance.Dark, PlatformThemeVariant.Light, ColorContrastPreference.NoPreference, "Dark")]
    [InlineData(Appearance.System, PlatformThemeVariant.Light, ColorContrastPreference.High, "LightHighContrast")]
    [InlineData(Appearance.System, PlatformThemeVariant.Dark, ColorContrastPreference.High, "DarkHighContrast")]
    [InlineData(Appearance.Light, PlatformThemeVariant.Dark, ColorContrastPreference.High, "LightHighContrast")]
    [InlineData(Appearance.Dark, PlatformThemeVariant.Light, ColorContrastPreference.High, "DarkHighContrast")]
    public void The_systems_contrast_setting_picks_high_contrast_over_the_chosen_appearance(Appearance appearance, PlatformThemeVariant system, ColorContrastPreference contrast, string theme) =>
        Assert.Equal(theme, Contrast.For(appearance, new PlatformColorValues { ThemeVariant = system, ContrastPreference = contrast }).Key.ToString());

    [Theory]
    [InlineData("LightHighContrast", "#fff0ede6", "#ff55534f")]
    [InlineData("DarkHighContrast", "#ff1a1917", "#ffb0b1b8")]
    public Task The_workspace_renders_in_high_contrast(string theme, string window, string tertiary)
    {
        var variant = theme.StartsWith("Dark", StringComparison.Ordinal) ? Contrast.DarkHigh : Contrast.LightHigh;
        return OnWindow(variant, _temp["settings"], main =>
        {
            Assert.True(main.Open(_temp["stories/Tides"]));
            Settle();
            Assert.Equal(variant, main.ActualThemeVariant);

            // Inherited from the light or dark theme, and changed by high contrast.
            Assert.Equal(window, Token(main, "BgWindow", variant).ToString());
            Assert.Equal(tertiary, Token(main, "TextTertiary", variant).ToString());
            var caption = Visible<TextBlock>(main.Workspace!).First(text => text.Classes.Contains("caption"));
            Assert.Equal(tertiary, ((ISolidColorBrush)caption.Foreground!).Color.ToString());

            using var frame = Render(main);
            Assert.Equal(window, Pixel(frame, new Avalonia.Point(4, 400)).ToString());
        });
    }

    public void Dispose() => _temp.Dispose();
}
