using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Plotarium.Application;
using Plotarium.Domain;
using PuppeteerSharp;
namespace Plotarium.Infrastructure;

// One owned browser per admitted job: higher startup cost, simpler isolation and reliable kill ownership.
public sealed class ChromiumRenderer(BrowserSettings settings, AssetCatalog assets) : IChartRenderer
{
    private static readonly string Script = ReadScript();
    private const string Origin = "https://plotarium.invalid/";
    private const string Html = "<!doctype html><html><head><meta charset='utf-8'><title>Plotarium</title><style>html,body{margin:0;padding:0;overflow:hidden}#chart{position:relative;overflow:hidden}*{box-sizing:border-box}</style></head><body><div id='chart'></div></body></html>";
    // No network connections, frames or workers. Only local intercepted engine modules and inline styles.
    private const string Csp = "default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; img-src data: blob:; font-src 'none'; connect-src 'none'; frame-src 'none'; worker-src 'none'; base-uri 'none'; form-action 'none'";
    public async Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(settings.ExecutablePath) || !File.Exists(settings.ExecutablePath))
            throw new RenderFailure("browser_missing", "A configured Chromium executable is required.", 503);
        var launcher = new Launcher();
        IBrowser? browser = null;
        Task<IBrowser>? launching = null;
        try
        {
            // No --no-sandbox, downloads, custom user flags or remote debugging connections.
            var launchOptions = new LaunchOptions
            {
                ExecutablePath = settings.ExecutablePath, Headless = true,
                Timeout = settings.LaunchTimeoutMs, ProtocolTimeout = settings.ProtocolTimeoutMs,
                Args = ["--disable-background-networking", "--disable-component-update", "--no-first-run"]
            };
            launchOptions.Env["Plotarium__ApiKey"] = "";
            launching = launcher.LaunchAsync(launchOptions);
            browser = await launching.WaitAsync(cancellationToken);
            var page = await browser.NewPageAsync().WaitAsync(cancellationToken);
            page.DefaultTimeout = settings.ProtocolTimeoutMs;
            page.DefaultNavigationTimeout = settings.ProtocolTimeoutMs;
            await page.SetViewportAsync(new ViewPortOptions { Width=request.Width, Height=request.Height, DeviceScaleFactor=request.Scale }).WaitAsync(cancellationToken);
            page.AddRequestInterceptor(async resource =>
            {
                try
                {
                    if (resource.Method != HttpMethod.Get || !Uri.TryCreate(resource.Url, UriKind.Absolute, out var uri)) { await resource.AbortAsync(); return; }
                    if (resource.Url == Origin)
                    {
                        await resource.RespondAsync(new ResponseData { Status=HttpStatusCode.OK,ContentType="text/html; charset=utf-8",Body=Html,
                            Headers=new Dictionary<string,object>{{"Content-Security-Policy",Csp},{"Cache-Control","no-store"}} });
                        return;
                    }
                    if (assets.TryRead(uri,out var bytes))
                    {
                        await resource.RespondAsync(new ResponseData { Status=HttpStatusCode.OK,ContentType="text/javascript; charset=utf-8",BodyData=bytes! });
                        return;
                    }
                    await resource.AbortAsync();
                }
                catch (PuppeteerException) { /* The owned page may already have been killed by its deadline. Never continue an unknown request. */ }
            });
            await page.SetRequestInterceptionAsync(true).WaitAsync(cancellationToken);
            await page.GoToAsync(Origin).WaitAsync(cancellationToken);
            // Pass data as a JSON argument, not executable interpolation or raw HTML.
            var payload = JsonSerializer.Serialize(request,Contract.Json);
            var svg = await page.EvaluateFunctionAsync<string>(Script,payload).WaitAsync(cancellationToken);
            byte[] bytes;
            if (request.Format == "svg") bytes = Encoding.UTF8.GetBytes(svg);
            else if (request.Format == "pdf")
                bytes = await page.PdfDataAsync(new PdfOptions { Width=$"{request.Width}px", Height=$"{request.Height}px",PrintBackground=true,
                    PageRanges="1",PreferCSSPageSize=false,Timeout=settings.ProtocolTimeoutMs }).WaitAsync(cancellationToken);
            else
            {
                var type = request.Format switch {"png"=>ScreenshotType.Png,"jpeg"=>ScreenshotType.Jpeg,"webp"=>ScreenshotType.Webp,
                    _=>throw new RenderFailure("format","Unsupported raster format.")};
                bytes = await page.ScreenshotDataAsync(new ScreenshotOptions {Type=type,FullPage=false,CaptureBeyondViewport=false,
                    OmitBackground=request.Background=="transparent",Quality=type==ScreenshotType.Png?null:90}).WaitAsync(cancellationToken);
            }
            var format = Contract.Format(request.Format);
            return new(bytes,format.Mime,format.Extension);
        }
        catch (OperationCanceledException) { throw; }
        catch (RenderFailure) { throw; }
        catch (PuppeteerException) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
        catch (PuppeteerException) { throw new RenderFailure("render_failed","Engine or browser rejected the render request."); }
        catch (System.ComponentModel.Win32Exception) { throw new RenderFailure("browser_start","Unable to start the configured browser.",503); }
        finally
        {
            // Do not leave an abandoned LaunchAsync running after cancellation.
            if (browser is null && launching is not null)
            {
                try { browser = await launching.WaitAsync(TimeSpan.FromMilliseconds(settings.LaunchTimeoutMs+1000)); }
                catch(Exception ex) when (ex is PuppeteerException or TimeoutException or OperationCanceledException or System.ComponentModel.Win32Exception) { }
            }
            if (browser is not null)
            {
                try { await browser.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
                catch(Exception ex) when (ex is PuppeteerException or TimeoutException or ObjectDisposedException) { }
            }
            // Kill only this job's process tree; never kill shared/system browsers.
            var process = launcher.Process?.Process;
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree:true); }
                catch(Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
            launcher.Process?.Dispose();
        }
    }
    private static string ReadScript()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Plotarium.Infrastructure.renderer.js")
            ?? throw new InvalidOperationException("Renderer script is missing.");
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
}
