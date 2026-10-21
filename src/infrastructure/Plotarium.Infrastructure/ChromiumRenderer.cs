using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Plotarium.Domain;
using Plotarium.Application;

namespace Plotarium.Infrastructure;

public sealed class ChromiumRenderer(BrowserSettings settings, AssetCatalog assets) : IChartRenderer
{
    private static readonly string Bridge = ReadBridge();

    public async Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(settings.ExecutablePath) || !File.Exists(settings.ExecutablePath))
            throw new RenderFailure("browser_missing", "A configured Chromium executable is required.", 503);
        await using var browser = await ChromiumProcess.LaunchAsync(settings, token);
        await using var cdp = await CdpConnection.OpenAsync(browser.Endpoint, token);
        using var pumpStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var target = await cdp.SendAsync("Target.createTarget", new { url = "about:blank" }, null, token);
        var attached = await cdp.SendAsync("Target.attachToTarget", new { targetId = target.GetProperty("targetId").GetString(), flatten = true }, null, token);
        var session = attached.GetProperty("sessionId").GetString()!;
        var blocked = 0;
        Exception? pumpError = null;
        var pump = PumpAsync();
        try
        {
            await cdp.SendAsync("Browser.setDownloadBehavior", new { behavior = "deny" }, null, token);
            await cdp.SendAsync("Page.enable", new { }, session, token);
            await cdp.SendAsync("Runtime.enable", new { }, session, token);
            await cdp.SendAsync("Network.enable", new { }, session, token);
            await cdp.SendAsync("Network.setBypassServiceWorker", new { bypass = true }, session, token);
            await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new { width = request.Width, height = request.Height, deviceScaleFactor = request.Scale, mobile = false }, session, token);
            await cdp.SendAsync("Emulation.setDefaultBackgroundColorOverride", new { color = new { r = 0, g = 0, b = 0, a = 0 } }, session, token);
            // There is no remote URL, server, or CDN. Deny network before installing local content.
            await cdp.SendAsync("Fetch.enable", new { patterns = new[] { new { urlPattern = "*", requestStage = "Request" } } }, session, token);
            var frameTree = await cdp.SendAsync("Page.getFrameTree", new { }, session, token);
            var frameId = frameTree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString();
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
            await cdp.SendAsync("Page.setDocumentContent", new { frameId, html = Shell(nonce) }, session, token);
            await EvaluateTrustedAsync(Bridge);
            var window = await cdp.SendAsync("Runtime.evaluate", new { expression = "window" }, session, token);
            var objectId = window.GetProperty("result").GetProperty("objectId").GetString()!;
            
            if (request.Engine == "echarts") await EvaluateTrustedAsync(assets.ReadScript("/assets/echarts/echarts.min.js"));
            else if (request.Engine == "plotly") await EvaluateTrustedAsync(assets.ReadScript("/assets/plotly/plotly.min.js"));
            else await CallAsync("function(value) { return window.Plotarium.loadMermaid(value); }", new { modules = assets.MermaidModules(), nonce }, initialization:true);
            
            var value = new { engine = request.Engine, spec = request.Spec, source = request.Source, width = request.Width, height = request.Height, scale = request.Scale, background = request.Background, theme = request.Theme };
            var rendered = await CallAsync("function(request) { return window.Plotarium.render(request); }", value);
            CheckNetwork();
            var svg = rendered.GetProperty("svg").GetString()!;
            SvgOutputPolicy.Validate(Encoding.UTF8.GetBytes(svg));
            if (Encoding.UTF8.GetByteCount(svg) > Contract.MaxOutputBytes) throw new RenderFailure("output_too_large", "Rendered output exceeds the limit.", 413);
            
            if (request.Format == "svg") return new(Encoding.UTF8.GetBytes(svg), "image/svg+xml", "svg");
            var background = request.Background;
            await CallAsync("function(value) { return window.Plotarium.present(value); }", new { svg = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)), background, width = request.Width, height = request.Height });
            if (request.Format == "pdf")
            {
                var pdfResult = await cdp.SendAsync("Page.printToPDF", new {
                    printBackground = true, preferCSSPageSize = false,
                    paperWidth = request.Width / 96.0, paperHeight = request.Height / 96.0,
                    marginTop = 0, marginBottom = 0, marginLeft = 0, marginRight = 0,
                    scale = 1, landscape = false
                }, session, token);
                CheckNetwork();
                var pdfBytes = Convert.FromBase64String(pdfResult.GetProperty("data").GetString()!);
                return new(pdfBytes, "application/pdf", "pdf");
            }
            var screenshot = await cdp.SendAsync("Page.captureScreenshot", new {
                format = request.Format, fromSurface = true, captureBeyondViewport = false,
                clip = new { x = 0, y = 0, width = request.Width, height = request.Height, scale = 1 }
            }, session, token);
            CheckNetwork();
            var encoded = screenshot.GetProperty("data").GetString()!;
            if (encoded.Length > (Contract.MaxOutputBytes * 4L / 3) + 8) throw new RenderFailure("output_too_large", "Rendered output exceeds the limit.", 413);
            return new(Convert.FromBase64String(encoded), "image/" + request.Format, request.Format == "jpeg" ? "jpg" : request.Format);

            void CheckNetwork()
            {
                if (pumpError is not null) throw new IOException("Browser interception failed.", pumpError);
                if (Volatile.Read(ref blocked) != 0) throw new RenderFailure("external_resource_blocked", "Rendering attempted to load an external resource.");
            }
            async Task EvaluateTrustedAsync(string script)
            {
                // Only hash-verified, packaged vendor code and the compiled bridge reach this method.
                var response = await cdp.SendAsync("Runtime.evaluate", new { expression = script, awaitPromise = true }, session, token);
                if (response.TryGetProperty("exceptionDetails", out _)) throw new RenderFailure("engine_initialization_failed", "A packaged renderer could not initialize.", 503);
            }
            async Task<JsonElement> CallAsync(string function, object argument, bool initialization = false)
            {
                var result = await cdp.SendAsync("Runtime.callFunctionOn", new { objectId, functionDeclaration = function, arguments = new[] { new { value = argument } }, awaitPromise = true, returnByValue = true }, session, token);
                if (result.TryGetProperty("exceptionDetails", out _)) throw new RenderFailure(initialization ? "engine_initialization_failed" : "engine_rejected_definition", "The engine could not process this definition under the configured policy.", initialization ? 503 : 422);
                if (!result.GetProperty("result").TryGetProperty("value", out var answer)) throw new IOException("Missing renderer response.");
                return answer.Clone();
            }
        }
        finally
        {
            await pumpStop.CancelAsync();
            try { await pump; } catch (OperationCanceledException) { }
        }

        async Task PumpAsync()
        {
            try
            {
                await foreach (var item in cdp.Events.ReadAllAsync(pumpStop.Token))
                {
                    if (!item.TryGetProperty("method", out var method)) continue;
                    if (method.GetString() == "Page.javascriptDialogOpening")
                        await cdp.SendAsync("Page.handleJavaScriptDialog", new { accept = false }, session, pumpStop.Token);
                    else if (method.GetString() == "Fetch.requestPaused")
                    {
                        Interlocked.Increment(ref blocked);
                        var id = item.GetProperty("params").GetProperty("requestId").GetString();
                        await cdp.SendAsync("Fetch.failRequest", new { requestId = id, errorReason = "BlockedByClient" }, session, pumpStop.Token);
                    }
                }
            }
            catch (OperationCanceledException) when (pumpStop.IsCancellationRequested) { }
            catch (Exception error) { pumpError = error;  }
        }
    }

    private static string Shell(string nonce) => "<!doctype html><html><head><meta charset='utf-8'>" +
        "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; script-src blob: 'nonce-" + nonce + "'; style-src 'unsafe-inline'; img-src data: blob:; font-src 'none'; connect-src 'none'; object-src 'none'; frame-src 'none'; worker-src 'none'; base-uri 'none'; form-action 'none'\">" +
        "<style>html,body{margin:0;padding:0;overflow:hidden;}#chart{position:relative;overflow:hidden;}</style></head><body><div id='chart'></div></body></html>";

    private static string ReadBridge()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Plotarium.Infrastructure.render-bridge.js") ?? throw new InvalidOperationException("Missing render bridge.");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
