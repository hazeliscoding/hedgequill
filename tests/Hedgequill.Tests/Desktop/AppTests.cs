using Hedgequill.Desktop;

namespace Hedgequill.Tests.Desktop;

public class AppTests
{
    // avares:// resource URIs and the published executable both carry this name.
    [Fact]
    public void The_desktop_assembly_is_named_Hedgequill() =>
        Assert.Equal("Hedgequill", typeof(App).Assembly.GetName().Name);
}
