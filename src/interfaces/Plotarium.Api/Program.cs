using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Plotarium.Application;
using Plotarium.Domain;
using Plotarium.Infrastructure;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=Contract.MaxBodyBytes);
var section=builder.Configuration.GetSection("Plotarium");
var browserSettings=section.GetSection("Browser").Get<BrowserSettings>() ?? new BrowserSettings();
if(string.IsNullOrWhiteSpace(browserSettings.AssetsDirectory)) throw new InvalidOperationException("AssetsDirectory is required.");
var assetRoot=Path.IsPathRooted(browserSettings.AssetsDirectory)?browserSettings.AssetsDirectory:Path.Combine(AppContext.BaseDirectory,browserSettings.AssetsDirectory);
var concurrency=section.GetValue("Concurrency",2);
var queue=section.GetValue("QueueCapacity",4);
var seconds=section.GetValue("TimeoutSeconds",30);
if(concurrency is <1 or >32 || queue is <0 or >100 || seconds is <1 or >120 ||
   browserSettings.LaunchTimeoutMs is <1000 or >30000 || browserSettings.ProtocolTimeoutMs is <1000 or >120000)
    throw new InvalidOperationException("Invalid render limits.");
var key=section["ApiKey"];
var anonymousDevelopment=builder.Environment.IsDevelopment() && section.GetValue("AllowAnonymousDevelopment",false);
if(!anonymousDevelopment && (key is null || Encoding.UTF8.GetByteCount(key)<32))
    throw new InvalidOperationException("Set Plotarium__ApiKey to a secret of at least 32 bytes.");
var expectedKeyHash=key is null?Array.Empty<byte>():SHA256.HashData(Encoding.UTF8.GetBytes(key));
builder.Services.AddSingleton(new BoundedRenderGate(concurrency,queue));
builder.Services.AddSingleton(browserSettings);
builder.Services.AddSingleton(new AssetCatalog(assetRoot));
builder.Services.AddSingleton<IChartRenderer,ChromiumRenderer>();
builder.Services.AddSingleton(sp=>new RenderService(sp.GetRequiredService<IChartRenderer>(),sp.GetRequiredService<BoundedRenderGate>(),TimeSpan.FromSeconds(seconds)));
var app=builder.Build();
using var meter=new Meter("Plotarium","0.1.0-alpha.1");
var completed=meter.CreateCounter<long>("plotarium.render.completed");
var failures=meter.CreateCounter<long>("plotarium.render.failed");
var latency=meter.CreateHistogram<double>("plotarium.render.duration","ms");
app.Use(async(context,next)=>
{
    context.Response.Headers.XContentTypeOptions="nosniff";
    context.Response.Headers.CacheControl="no-store";
    context.Response.Headers["Content-Security-Policy"]="default-src 'none'; sandbox";
    if(context.Request.Path=="/health/live") { await next(context); return; }
    var loopback=context.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);
    if(!(anonymousDevelopment && loopback))
    {
        var supplied=context.Request.Headers["X-Api-Key"];
        if(supplied.Count!=1 || supplied[0] is not { Length: >=32 and <=1024 } candidate || expectedKeyHash.Length!=32 ||
           !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(candidate)),expectedKeyHash))
        {
            await Results.Problem(statusCode:401,title:"Authentication required.",extensions:new Dictionary<string,object?>{{"code","unauthorized"}}).ExecuteAsync(context);return;
        }
    }
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
        // No exception message, graph input, API key or browser stderr enters application logs.
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
    formats=new[]{"png","jpeg","webp","svg","json","pdf"},json="definition-envelope; structural-only",
    limits=new{bodyBytes=Contract.MaxBodyBytes,maxPixels=Contract.MaxPixels,outputBytes=Contract.MaxOutputBytes,concurrency,queueCapacity=queue},
    profile="offline-static; no callbacks, HTML labels, external data, geographic providers or ECharts GL",
    browser="operator-installed Chromium; no Node runtime"
}));
app.MapPost("/v1/render",async(HttpContext context,RenderService service)=>
{
    if(!context.Request.HasJsonContentType())throw new RenderFailure("content_type","Use application/json.",415);
    if(context.Request.ContentLength>Contract.MaxBodyBytes)throw new RenderFailure("body_limit","Request is too large.",413);
    using var body=new MemoryStream();var buffer=new byte[8192];int count;
    while((count=await context.Request.Body.ReadAsync(buffer,context.RequestAborted))>0)
    {
        if(body.Length+count>Contract.MaxBodyBytes)throw new RenderFailure("body_limit","Request is too large.",413);
        body.Write(buffer,0,count);
    }
    var request=RequestValidator.Parse(body.ToArray());
    var start=Stopwatch.GetTimestamp();
    try
    {
        var artifact=await service.RenderAsync(request,context.RequestAborted);
        completed.Add(1,new KeyValuePair<string,object?>("engine",request.Engine),new KeyValuePair<string,object?>("format",request.Format));
        return Results.File(artifact.Bytes,artifact.ContentType,fileDownloadName:$"plotarium.{artifact.Extension}");
    }
    finally { latency.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds,new KeyValuePair<string,object?>("engine",request.Engine)); }
});
app.Run();
