namespace Plotarium.Infrastructure;
public sealed class BrowserSettings
{
    public string ExecutablePath { get; init; } = "";
    public string AssetsDirectory { get; init; } = "assets";
    public bool DisableSandbox { get; init; } = false;
    public bool EnableSoftwareWebGl { get; init; } = false;
    public int LaunchTimeoutMs { get; init; } = 10_000;
    public int ProtocolTimeoutMs { get; init; } = 15_000;
}
