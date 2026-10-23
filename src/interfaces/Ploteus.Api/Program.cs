using Ploteus.Api.Interactive;
using Ploteus.Api.Stateless;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Ploteus.Application;
using Ploteus.Domain;
using Ploteus.Infrastructure;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o=>{o.Limits.MaxRequestBodySize=Contract.MaxBodyBytes; o.Limits.MaxRequestLineSize=32_768;});
// GET chart definitions may contain user data; suppress ASP.NET access logs with raw query strings.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics",LogLevel.Warning);
var section=builder.Configuration.GetSection("Ploteus");
var browserSettings=section.GetSection("Browser").Get<BrowserSettings>() ?? new BrowserSettings();
if(string.IsNullOrWhiteSpace(browserSettings.AssetsDirectory)) throw new InvalidOperationException("AssetsDirectory is required.");
var assetRoot=Path.IsPathRooted(browserSettings.AssetsDirectory)?browserSettings.AssetsDirectory:Path.Combine(AppContext.BaseDirectory,browserSettings.AssetsDirectory);
var concurrency=section.GetValue("Concurrency",2);
var queue=section.GetValue("QueueCapacity",4);
var seconds=section.GetValue("TimeoutSeconds",30);
if(concurrency is <1 or >32 || queue is <0 or >100 || seconds is <1 or >120 ||
   browserSettings.LaunchTimeoutMs is <1000 or >30000 || browserSettings.ProtocolTimeoutMs is <1000 or >120000)
    throw new InvalidOperationException("Invalid render limits.");
builder.Services.AddSingleton(new BoundedRenderGate(concurrency,queue));
builder.Services.AddSingleton(browserSettings);
builder.Services.AddSingleton(new AssetCatalog(assetRoot));
builder.Services.AddSingleton<IChartRenderer,ChromiumRenderer>();
builder.Services.AddSingleton(sp=>new RenderService(sp.GetRequiredService<IChartRenderer>(),sp.GetRequiredService<BoundedRenderGate>(),TimeSpan.FromSeconds(seconds)));
var interactiveConfig=section.GetSection("Interactive");
var shareCapacity=interactiveConfig.GetValue("MaxCharts",128);
var shareMinutes=interactiveConfig.GetValue("TtlMinutes",15);
builder.Services.AddSingleton(new ChartStore(shareCapacity,TimeSpan.FromMinutes(shareMinutes)));
var sources=section.GetSection("Sources").Get<RemotePayloadOptions>() ?? new RemotePayloadOptions();
builder.Services.AddSingleton(new RemotePayloadFetcher(sources));
builder.Services.AddSingleton<StatelessHtmlBuilder>();
var app=builder.Build();
using var meter=new Meter("Ploteus","0.6.0");
var completed=meter.CreateCounter<long>("ploteus.render.completed");
var failures=meter.CreateCounter<long>("ploteus.render.failed");
var latency=meter.CreateHistogram<double>("ploteus.render.duration","ms");
app.Use(async(context,next)=>
{
    context.Response.Headers.XContentTypeOptions="nosniff";
    context.Response.Headers.CacheControl="no-store";
    context.Response.Headers["Content-Security-Policy"]="default-src 'none'; sandbox";
    try { await next(context); }
    catch(RenderFailure ex)
    {
        failures.Add(1,new KeyValuePair<string,object?>("code",ex.Code));
        if(ex.Status==429)context.Response.Headers.RetryAfter="1";
        await Results.Problem(statusCode:ex.Status,title:ex.Message,extensions:new Dictionary<string,object?>{{"code",ex.Code},{"traceId",context.TraceIdentifier}}).ExecuteAsync(context);
    }
    catch(BadHttpRequestException ex) when(ex.StatusCode==413)
    { await Results.Problem(statusCode:413,title:"Request is too large.").ExecuteAsync(context); }
    catch(OperationCanceledException) when(context.RequestAborted.IsCancellationRequested) { context.Abort(); }
    catch(Exception)
    {
        // No exception message, graph input or browser stderr enters application logs.
        app.Logger.LogError("Render request failed. TraceId: {TraceId}",context.TraceIdentifier);
        if(!context.Response.HasStarted) await Results.Problem(statusCode:500,title:"Internal render error.",extensions:new Dictionary<string,object?>{{"traceId",context.TraceIdentifier}}).ExecuteAsync(context);
        else context.Abort();
    }
});
app.MapGet("/health/live",()=>Results.Json(new{status="alive"}));
app.MapGet("/health/ready",()=>Results.Json(new
{
    assets="verified-local-digests",browserExecutable=File.Exists(browserSettings.ExecutablePath),
    validation="presence-only; use integration tests for an actual render"
},statusCode:File.Exists(browserSettings.ExecutablePath)?200:503));
app.MapGet("/v1/capabilities",()=>Results.Json(new
{
    schemaVersion="1",engines=new[]{new{name="echarts",version=Contract.EngineVersion("echarts")},new{name="plotly",version=Contract.EngineVersion("plotly")},new{name="mermaid",version=Contract.EngineVersion("mermaid")}},
    formats=new[]{"png","jpeg","webp","svg","json","pdf","html"},json="definition-envelope; structural-only",
    limits=new{bodyBytes=Contract.MaxBodyBytes,maxPixels=Contract.MaxPixels,outputBytes=Contract.MaxOutputBytes,concurrency,queueCapacity=queue},
    profile="offline-static; ECharts GL; Plotly geographic fallback; GET inline, gzip-base64url or allowlisted external JSON; no arbitrary callbacks",
    geography=new { echartsMaps=new[]{"world","world-50m","africa","asia","europe","north-america","south-america","oceania","usa"}, plotlyGeoTraces=new[]{"choropleth","scattergeo"}, plotlyMapTypes=new[]{"scattermap","choroplethmap","densitymap"}, mapLibreTiles=false, staticMapFallback="scattergeo/choropleth; densitymap returns 422" },
    rasterizedSvg=new[]{"scattergl","scatterpolargl","scatter3d","mesh3d","surface","volume","isosurface","cone","streamtube","splom"},
    stateless=new{post="POST /v1/render",get="GET /v1/render?c=...",encodings=new[]{"json","gzip-base64url"},remoteSourcesEnabled=sources.AllowedOrigins.Length+sources.PrivateOrigins.Length>0,sourceUrl="GET /v1/render?sourceUrl=...",providers="GET /v1/render?provider=...&key=...",html="single response, inline assets; Mermaid SVG image viewer",storage="none"},
    interactive=new{enabled=true,create="POST /v1/charts",view="GET /v1/charts/{id}/view",mcp="POST /mcp",shares=new[]{"teams","whatsapp","chatgpt"}},
    browser="operator-installed Chromium; no Node runtime"
}));
app.MapGet("/v1/render",async Task<IResult> (HttpContext context,RenderService service,RemotePayloadFetcher fetcher,StatelessHtmlBuilder html)=>
{
    context.Response.Headers["Referrer-Policy"]="no-referrer";
    var request=await StatelessQuery.ResolveAsync(context,fetcher);
    if(request.Format=="html")
    {
        var (page,csp)=await html.BuildAsync(request,context.RequestAborted);
        context.Response.Headers.ContentSecurityPolicy=csp;
        context.Response.Headers["Referrer-Policy"]="no-referrer";
        context.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
        return Results.Content(page,"text/html; charset=utf-8");
    }
    var start=Stopwatch.GetTimestamp();
    try
    {
        var artifact=await service.RenderAsync(request,context.RequestAborted);
        completed.Add(1,new KeyValuePair<string,object?>("engine",request.Engine),new KeyValuePair<string,object?>("format",request.Format));
        // No attachment disposition: GET output can be embedded as an image or iframe.
        return Results.File(artifact.Bytes,artifact.ContentType);
    }
    finally {latency.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds,new KeyValuePair<string,object?>("engine",request.Engine));}
});
app.MapPost("/v1/render",async Task<IResult> (HttpContext context,RenderService service,ChartStore store)=>
{
    var request=await InteractiveRoutes.ReadRequest(context);
    if(request.Format=="html")
    {
        var item=store.Create(request);
        context.Response.Headers.ContentSecurityPolicy="default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' blob: data:; connect-src 'self'; worker-src blob:; object-src 'none'; base-uri 'none'; frame-ancestors 'self'";
        context.Response.Headers["Referrer-Policy"]="no-referrer";
        return Results.Content(InteractiveRoutes.RenderPage(item.Id,false),"text/html; charset=utf-8");
    }
    var start=Stopwatch.GetTimestamp();
    try
    {
        var artifact=await service.RenderAsync(request,context.RequestAborted);
        completed.Add(1,new KeyValuePair<string,object?>("engine",request.Engine),new KeyValuePair<string,object?>("format",request.Format));
        return Results.File(artifact.Bytes,artifact.ContentType,fileDownloadName:$"ploteus.{artifact.Extension}");
    }
    finally { latency.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds,new KeyValuePair<string,object?>("engine",request.Engine)); }
});
app.MapInteractiveRoutes(interactiveConfig);
app.MapMcp(interactiveConfig);
app.Run();
