using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Hedgequill.Tests.Guards;

namespace Hedgequill.Tests.Desktop;

// Every text and focus color pair the UI uses must pass WCAG 2.2 AA, in both themes.
public partial class ContrastTests
{
    private static readonly string[] Text = ["TextPrimary", "TextSecondary", "TextTertiary", "AccentText"];
    private static readonly string[] Surfaces = ["BgWindow", "BgPage", "BgRaised", "BgInset", "BgInput"];

    // (foreground, background, minimum ratio): 4.5 for text (SC 1.4.3), 3 for focus indicators (SC 1.4.11).
    // MarkerFaded is left out on purpose: faded Markdown markers recede, and the text they wrap stays full contrast.
    private static readonly (string Fg, string Bg, double Min)[] Pairs =
    [
        .. Text.SelectMany(fg => Surfaces.Select(bg => (fg, bg, 4.5))),
        .. new[] { "Accent", "AccentHover", "AccentPressed" }.Select(bg => ("TextOnAccent", bg, 4.5)),
        .. new[] { "ButtonBackground", "ButtonBackgroundPointerOver", "ButtonBackgroundPressed" }.Select(bg => ("TextPrimary", bg, 4.5)),
        .. Surfaces.Select(bg => ("FocusRing", bg, 3.0)),
    ];

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    // The prototype's tertiary text and dark focus color, before the AA fixes in docs/brand/tokens.md.
    private const string Prototype = """
        <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <ResourceDictionary.ThemeDictionaries>
            <ResourceDictionary x:Key="Light">
              <SolidColorBrush x:Key="BgWindow" Color="#F0EDE6" />
              <SolidColorBrush x:Key="BgPage" Color="#FBF8F1" />
              <SolidColorBrush x:Key="TextTertiary" Color="#7D756D" />
              <SolidColorBrush x:Key="Accent" Color="#6E2633" />
              <StaticResource x:Key="FocusRing" ResourceKey="Accent" />
            </ResourceDictionary>
            <ResourceDictionary x:Key="Dark">
              <SolidColorBrush x:Key="BgWindow" Color="#1A1917" />
              <SolidColorBrush x:Key="BgPage" Color="#201F1D" />
              <SolidColorBrush x:Key="TextTertiary" Color="#8A837A" />
              <SolidColorBrush x:Key="Accent" Color="#A14A5C" />
              <StaticResource x:Key="FocusRing" ResourceKey="Accent" />
            </ResourceDictionary>
          </ResourceDictionary.ThemeDictionaries>
        </ResourceDictionary>
        """;

    [Fact]
    public void Contrast_measures_the_known_extremes()
    {
        Assert.Equal(21, Contrast((0, 0, 0), (255, 255, 255)), 10);
        Assert.Equal(1, Contrast((9, 9, 9), (9, 9, 9)), 10);
    }

    [Fact]
    public void Resolve_follows_StaticResource_aliases_and_rejects_unknown_tokens()
    {
        var theme = new Dictionary<string, string> { ["A"] = "#FFFFFF", ["B"] = "{StaticResource A}", ["C"] = "{StaticResource Missing}" };
        Assert.Equal((255, 255, 255), Resolve(theme, "B"));
        Assert.Throws<KeyNotFoundException>(() => Resolve(theme, "C"));
    }

    [Fact]
    public void The_check_catches_the_prototypes_failures()
    {
        (string, string, double)[] pairs = [("TextTertiary", "BgWindow", 4.5), ("TextTertiary", "BgPage", 4.5), ("FocusRing", "BgPage", 3.0)];
        Assert.Equal(
            ["Light TextTertiary BgWindow 3.87", "Light TextTertiary BgPage 4.26", "Dark TextTertiary BgPage 4.39", "Dark FocusRing BgPage 2.86"],
            Failures(ParseThemes(Prototype), pairs).Select(f => $"{f.Theme} {f.Fg} {f.Bg} {f.Ratio.ToString(CultureInfo.InvariantCulture)}"));
    }

    [Fact]
    public void The_app_tokens_pass_in_both_themes()
    {
        var themes = ParseThemes(Repo.Read("src/Hedgequill.Desktop/Themes/Tokens.axaml"));
        Assert.Equal(["Light", "Dark"], themes.Keys);
        Assert.Empty(Failures(themes, Pairs));
    }

    // Each theme dictionary's colors by key: a brush's hex color, or an alias written as {StaticResource Key}.
    private static Dictionary<string, Dictionary<string, string>> ParseThemes(string xaml) =>
        XDocument.Parse(xaml).Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary" && e.Attribute(X + "Key") is not null)
            .ToDictionary(
                theme => theme.Attribute(X + "Key")!.Value,
                theme => theme.Elements()
                    .Where(e => e.Name.LocalName is "SolidColorBrush" or "StaticResource")
                    .ToDictionary(
                        e => e.Attribute(X + "Key")!.Value,
                        e => e.Name.LocalName == "SolidColorBrush" ? e.Attribute("Color")!.Value : $"{{StaticResource {e.Attribute("ResourceKey")!.Value}}}"));

    private static (int R, int G, int B) Resolve(IReadOnlyDictionary<string, string> theme, string key)
    {
        if (!theme.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"unknown token {key}");
        }

        if (Alias().Match(value) is { Success: true } alias)
        {
            return Resolve(theme, alias.Groups[1].Value);
        }

        var hex = Hex().Match(value);
        Assert.True(hex.Success, $"cannot resolve color \"{value}\" of {key}");
        int Channel(int i) => int.Parse(hex.Groups[1].Value.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (Channel(0), Channel(2), Channel(4));
    }

    private static double Luminance((int R, int G, int B) rgb)
    {
        static double Linear(int c)
        {
            var s = c / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(rgb.R)) + (0.7152 * Linear(rgb.G)) + (0.0722 * Linear(rgb.B));
    }

    private static double Contrast((int, int, int) a, (int, int, int) b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static IEnumerable<(string Theme, string Fg, string Bg, double Ratio, double Min)> Failures(
        Dictionary<string, Dictionary<string, string>> themes, IEnumerable<(string Fg, string Bg, double Min)> pairs) =>
        from theme in themes
        from pair in pairs
        let ratio = Contrast(Resolve(theme.Value, pair.Fg), Resolve(theme.Value, pair.Bg))
        where ratio < pair.Min
        select (theme.Key, pair.Fg, pair.Bg, Math.Floor(ratio * 100) / 100, pair.Min);

    [GeneratedRegex(@"^\{StaticResource (\w+)\}$")]
    private static partial Regex Alias();

    [GeneratedRegex("^#([0-9A-Fa-f]{6})$")]
    private static partial Regex Hex();
}
