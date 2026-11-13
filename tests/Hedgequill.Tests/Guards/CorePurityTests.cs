using System.Xml.Linq;

namespace Hedgequill.Tests.Guards;

// Hedgequill.Core holds the project model, saving and AO3 HTML, so it can be tested without a window. It may use files
// and special-purpose libraries, but never the UI or the updater.
public sealed class CorePurityTests
{
    private const string Core = "src/Hedgequill.Core";

    // As in NetworkGuardTests, comments and strings count.
    private static readonly Ban[] Ui =
    [
        new("Avalonia", @"\bAvalonia\b"),
        new("System.Windows", @"\bSystem\.Windows\b"),
        new("Velopack", @"\bVelopack\b"),
    ];

    private static bool IsUiOrUpdater(string name) =>
        name.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Velopack", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Hedgequill.Desktop", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<Hit> Scan(string file, string text) => SourceScan.Find(file, text, Ui);

    internal static IEnumerable<string> BannedReferences(string project) =>
        XDocument.Parse(project).Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference" or "Reference")
            .Select(element => element.Attribute("Include")?.Value ?? "")
            .Where(IsUiOrUpdater);

    internal static IEnumerable<string> BannedRestores(string assetsJson) =>
        Repo.Libraries(assetsJson).Select(library => library.Name).Where(IsUiOrUpdater);

    [Fact]
    public void The_core_project_references_no_UI_or_updater() =>
        Assert.Empty(BannedReferences(Repo.Read($"{Core}/Hedgequill.Core.csproj")));

    // Directory.Build.props, Directory.Packages.props or a package's own dependencies could still bring one in.
    [Fact]
    public void The_core_restores_no_UI_or_updater() =>
        Assert.Empty(BannedRestores(Repo.Assets(Core)));

    [Fact]
    public void No_core_file_names_the_UI_or_updater() =>
        Assert.Empty(Repo.SourceFiles(Core, ".cs").SelectMany(file => Scan(file, Repo.Read(file))).Select(hit => hit.ToString()));

    [Theory]
    [InlineData("using Avalonia.Controls;", "Avalonia")]
    [InlineData("var brush = new global::Avalonia.Media.SolidColorBrush();", "Avalonia")]
    [InlineData("using System.Windows.Input;", "System.Windows")]
    [InlineData("using Velopack;", "Velopack")]
    public void Flags_each_UI_or_updater_spelling(string code, string ban) =>
        Assert.Contains(ban, Scan($"{Core}/Leak.cs", code).Select(hit => hit.Name));

    [Fact]
    public void Lets_core_code_through() =>
        Assert.Empty(Scan($"{Core}/Projects/Saving.cs", """
            namespace Hedgequill.Core.Projects;

            // Write a temporary file, then replace, so a crash never leaves half a scene.
            internal static class Saving
            {
                public static void Write(string path, string text)
                {
                    var temporary = path + ".tmp";
                    File.WriteAllText(temporary, text);
                    File.Move(temporary, path, overwrite: true);
                }
            }
            """));

    [Theory]
    [InlineData("""<ItemGroup><PackageReference Include="Avalonia" /></ItemGroup>""", "Avalonia")]
    [InlineData("""<ItemGroup><PackageReference Include="Velopack" /></ItemGroup>""", "Velopack")]
    [InlineData("""<ItemGroup><ProjectReference Include="..\Hedgequill.Desktop\Hedgequill.Desktop.csproj" /></ItemGroup>""", @"..\Hedgequill.Desktop\Hedgequill.Desktop.csproj")]
    public void Flags_a_UI_or_updater_reference_in_the_core_project(string items, string reference) =>
        Assert.Equal([reference], BannedReferences($"""<Project Sdk="Microsoft.NET.Sdk">{items}<ItemGroup><PackageReference Include="Markdig" /></ItemGroup></Project>"""));

    [Fact]
    public void Flags_a_UI_or_updater_package_in_the_core_restore()
    {
        const string assets = """
            {
              "libraries": {
                "Microsoft.NET.ILLink.Tasks/10.0.15": { "type": "package" },
                "Markdig/1.0.0": { "type": "package" },
                "Avalonia.Base/12.1.6": { "type": "package" },
                "Velopack/0.0.1": { "type": "package" }
              }
            }
            """;

        Assert.Equal(["Avalonia.Base", "Velopack"], BannedRestores(assets));
    }
}
