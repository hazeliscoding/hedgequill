namespace Hedgequill.Core;

// A deliberate leak: CI must fail on this, and the pull request is closed without merging.
internal static class Leak
{
    internal static readonly HttpClient Client = new();
}
