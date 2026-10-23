using System.Diagnostics;
using System.Globalization;
using Ploteus.Domain;

namespace Ploteus.Infrastructure;

internal sealed class ChromiumProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _profile;
    private readonly Task _drainOutput;
    private readonly Task _drainError;
    public Uri Endpoint { get; private set; } = null!;

    private ChromiumProcess(Process process, string profile)
    {
        _process = process; _profile = profile;
        _drainOutput = DrainAsync(process.StandardOutput);
        _drainError = DrainAsync(process.StandardError);
    }

    public static async Task<ChromiumProcess> LaunchAsync(BrowserSettings settings, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(settings.ExecutablePath) || !File.Exists(settings.ExecutablePath))
            throw new RenderFailure("browser_missing", "Configure an absolute Ploteus__Browser__ExecutablePath.", 503);
        var profile = Directory.CreateTempSubdirectory("ploteus-").FullName;
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(profile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var start = new ProcessStartInfo(settings.ExecutablePath)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        // Do not pass service credentials to the browser child.
        var allowedEnvironment = new[] { "PATH", "SystemRoot", "WINDIR", "LANG", "LC_ALL", "FONTCONFIG_PATH", "FONTCONFIG_FILE" };
        var retained = allowedEnvironment.Select(k => (Key:k, Value:Environment.GetEnvironmentVariable(k))).Where(x => x.Value is not null).ToArray();
        start.Environment.Clear();
        foreach (var item in retained) start.Environment[item.Key] = item.Value;
        start.Environment["HOME"] = profile;
        start.Environment["TMPDIR"] = profile;
        start.Environment["TMP"] = profile;
        start.Environment["TEMP"] = profile;
        foreach (var argument in new[] {
            "--headless=new", "--remote-debugging-address=127.0.0.1", "--remote-debugging-port=0", "--user-data-dir=" + profile,
            "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-component-update",
            "--disable-sync", "--metrics-recording-only", "--password-store=basic", "--use-mock-keychain", "--hide-scrollbars",
            "--disable-features=Translate,MediaRouter,OptimizationHints", "about:blank" }) start.ArgumentList.Add(argument);
        // GPU-less CI may opt into software WebGL only in an explicitly isolated development environment.
        if (settings.EnableSoftwareWebGl)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.Ordinal))
                throw new RenderFailure("browser_software_gl", "Software WebGL is restricted to Development.", 503);
            start.ArgumentList.Add("--enable-unsafe-swiftshader");
            start.ArgumentList.Add("--use-angle=swiftshader");
            start.ArgumentList.Add("--enable-webgl");
        }
        // No --no-sandbox, no ignore-certificate-errors, and no automatic browser download.
        if (settings.DisableSandbox)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.Ordinal))
                throw new RenderFailure("browser_sandbox", "DisableSandbox is allowed only in Development.", 503);
            start.ArgumentList.Add("--no-sandbox");
        }
        Process? process = null;
        ChromiumProcess? owned = null;
        try
        {
            process = Process.Start(start) ?? throw new IOException("Chromium process did not start.");
            owned = new ChromiumProcess(process, profile);
            var activePort = Path.Combine(profile, "DevToolsActivePort");
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new RenderFailure("browser_start", "Chromium exited before initialization. Check browser sandbox and operating-system dependencies.", 503);
                if (File.Exists(activePort))
                {
                    var lines = await File.ReadAllLinesAsync(activePort, token);
                    if (lines.Length >= 2 && int.TryParse(lines[0], NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535 && lines[1].StartsWith("/devtools/browser/", StringComparison.Ordinal))
                    {
                        owned.Endpoint = new Uri($"ws://127.0.0.1:{port}{lines[1]}");
                        return owned;
                    }
                }
                await Task.Delay(25, token);
            }
        }
        catch
        {
            if (owned is not null) await owned.DisposeAsync();
            else { process?.Dispose(); Directory.Delete(profile, recursive:true); }
            throw;
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[2048];
        while (await reader.ReadAsync(buffer) != 0) { }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree:true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(_drainOutput, _drainError).WaitAsync(deadline.Token);
        }
        finally
        {
            _process.Dispose();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try { if (Directory.Exists(_profile)) Directory.Delete(_profile, recursive:true); break; }
                catch (IOException) when (attempt < 2) { await Task.Delay(50); }
            }
        }
    }
}
