using System.Text;
using System.Text.Json;
using Plotarium.Application;
using Plotarium.Domain;
var results = new List<object>(); int failed = 0;
void Test(string name, Action test)
{
    try { test(); results.Add(new { name, status = "pass" }); }
    catch(Exception ex) { failed++; results.Add(new { name, status = "fail", error = ex.Message }); }
}
RenderRequest Parse(string json) => RequestValidator.Parse(Encoding.UTF8.GetBytes(json));
void Reject(string json)
{
    try { _ = Parse(json); } catch(RenderFailure) { return; }
    throw new Exception("Expected a controlled RenderFailure.");
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}.");
}
const string valid = "{\"engine\":\"echarts\",\"spec\":{\"series\":[{\"type\":\"bar\",\"data\":[1,2]}]}}";
Test("default width", () => Equal(800,Parse(valid).Width));
Test("valid plotly", () => Equal("plotly",Parse("{\"engine\":\"plotly\",\"spec\":{\"data\":[{\"x\":[1],\"y\":[2]}]}}").Engine));
Test("valid mermaid", () => Equal("mermaid",Parse("{\"engine\":\"mermaid\",\"source\":\"flowchart LR\\nA-->B\"}").Engine));
Test("unknown engine", () => Reject(valid.Replace("echarts", "unknown")));
Test("unknown format", () => Reject(valid.Replace("\"spec\"", "\"format\":\"gif\",\"spec\"")));
Test("negative width", () => Reject(valid.Replace("\"spec\"", "\"width\":-1,\"spec\"")));
Test("too large width", () => Reject(valid.Replace("\"spec\"", "\"width\":4097,\"spec\"")));
Test("pixel cap", () => Reject(valid.Replace("\"spec\"", "\"width\":4096,\"height\":4096,\"scale\":2,\"spec\"")));
Test("duplicate engine", () => Reject(valid.Replace("\"engine\":", "\"engine\":\"plotly\",\"engine\":")));
Test("nested duplicate", () => Reject(valid.Replace("\"series\":", "\"x\":1,\"x\":2,\"series\":")));
Test("missing spec", () => Reject("{\"engine\":\"echarts\"}"));
Test("mermaid spec confusion", () => Reject("{\"engine\":\"mermaid\",\"source\":\"graph LR;A-->B\",\"spec\":{}}"));
Test("mermaid init directive", () => Reject("{\"engine\":\"mermaid\",\"source\":\"%%{init: {}}%%\\ngraph LR;A-->B\"}"));
Test("prototype key", () => Reject(valid.Replace("\"series\":", "\"__proto__\":{},\"series\":")));
Test("plotly config rejected", () => Reject("{\"engine\":\"plotly\",\"spec\":{\"data\":[],\"config\":{}}}"));
Test("raw JS spec rejected", () => Reject("{\"engine\":\"echarts\",\"spec\":\"function(){}\"}"));
Test("malformed JSON controlled", () => Reject("{"));
Test("oversize controlled", () => Reject(new string(' ',Contract.MaxBodyBytes+1)));

Test("spec survives input document disposal", () => Equal(JsonValueKind.Array,Parse(valid).Spec!.Value.GetProperty("series").ValueKind));
foreach(var json in new[]{"null","[]","42","{\"Engine\":\"echarts\",\"spec\":{}}","{\"engine\":null}","{\"engine\":\"echarts\",\"width\":\"800\"}"})
    Test("shape "+json,()=>Reject(json));
foreach(var field in new[]{"\"height\":0","\"scale\":0","\"scale\":4","\"theme\":\"custom\"","\"background\":\"red\"","\"background\":null","\"format\":\"jpeg\",\"background\":\"transparent\"","\"format\":\"svg\",\"scale\":2","\"arbitrary\":1"})
    Test("bad envelope "+field,()=>Reject(valid.Replace("\"spec\"",field+",\"spec\"")));
foreach(var type in new[]{"custom","map","bar3D","nonexistent"})
    Test("excluded echarts "+type,()=>Reject(valid.Replace("\"bar\"","\""+type+"\"")));
foreach(var extraProperty in new[]{"\"constructor\":{}","\"image\":\"https://example.invalid/a.png\"","\"symbol\":\"image://https://example.invalid/x\"","\"itemStyle\":{\"color\":\"url(https://example.invalid/x)\"}"})
    Test("active spec "+extraProperty,()=>Reject(valid.Replace("\"series\":",extraProperty+",\"series\":")));
foreach(var source in new[]{"---\nconfig: {}\n---\nflowchart LR;A-->B","graph LR; A-->B; click A callback","graph LR\nA[<b>label</b>]",new string('a',50001)})
    Test("mermaid policy length "+source.Length,()=>Reject(JsonSerializer.Serialize(new{engine="mermaid",source})));
foreach(var format in new[]{"png","jpeg","webp","svg","json","pdf"})
    Test("format "+format,()=>Equal(format,Parse(valid.Replace("\"spec\"","\"format\":\""+format+"\",\"spec\"")).Format));
Test("legal label url is text",()=>Equal("echarts",Parse(valid.Replace("\"series\":","\"title\":{\"text\":\"https://example.invalid\"},\"series\":")).Engine));
Test("oversize array",()=>Reject(JsonSerializer.Serialize(new{engine="echarts",spec=new{series=new[]{new{type="line",data=new int[50001]}}}})));
Test("deep nesting",()=>Reject(new string('[',34)+"0"+new string(']',34)));
const string svg="<svg xmlns='http://www.w3.org/2000/svg'><defs><linearGradient id='g'/></defs><rect width='10' height='10' fill='url(#g)'/></svg>";
Test("inert SVG",()=>SvgOutputPolicy.Validate(Encoding.UTF8.GetBytes(svg)));
foreach(var bad in new[]{
    "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>",
    "<svg xmlns='http://www.w3.org/2000/svg'><foreignObject/></svg>",
    "<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg xmlns='http://www.w3.org/2000/svg'>&x;</svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.invalid'/></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><style>rect{fill:url(https://example.invalid)}</style></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg' xml:base='https://example.invalid'/>",
    "<?xml-stylesheet href='https://example.invalid/a.css'?><svg xmlns='http://www.w3.org/2000/svg'/>",
    "<html/>"})
    Test("SVG active rejection "+bad[..Math.Min(65,bad.Length)],()=>{try{SvgOutputPolicy.Validate(Encoding.UTF8.GetBytes(bad));}catch(RenderFailure){return;}throw new Exception("Accepted active SVG");});
Test("embedded raster SVG",()=>SvgOutputPolicy.Validate(Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><image href='data:image/png;base64,AAAA'/></svg>")));
async Task TestAsync(string name,Func<Task> test)
{
    try{await test();results.Add(new{name,status="pass"});}
    catch(Exception ex){failed++;results.Add(new{name,status="fail",error=ex.Message});}
}
await TestAsync("gate queue overflow and recovery",async()=>{
    using var gate=new BoundedRenderGate(1,1);var first=await gate.EnterAsync(default);
    var second=gate.EnterAsync(default);
    try{using var impossible=await gate.EnterAsync(default);throw new Exception("Overflow admitted");}
    catch(RenderFailure ex){Equal(429,ex.Status);}
    first.Dispose();first.Dispose();using var next=await second.WaitAsync(TimeSpan.FromSeconds(2));
});
await TestAsync("cancelled waiter does not consume capacity",async()=>{
    using var gate=new BoundedRenderGate(1,1);var first=await gate.EnterAsync(default);
    using var cts=new CancellationTokenSource();var waiting=gate.EnterAsync(cts.Token);cts.Cancel();
    try{await waiting;throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
    first.Dispose();using var next=await gate.EnterAsync(default);
});
await TestAsync("JSON bypasses browser and round trips",async()=>{
    using var gate=new BoundedRenderGate(1,0);var renderer=new RecordingRenderer((_,_)=>throw new Exception("Browser called for JSON"));
    var service=new RenderService(renderer,gate,TimeSpan.FromSeconds(1));
    var req=Parse(valid.Replace("\"spec\"","\"format\":\"json\",\"spec\""));
    var result=await service.RenderAsync(req,default);using var json=JsonDocument.Parse(result.Bytes);
    Equal("envelope-only",json.RootElement.GetProperty("normalization").GetString());
    Equal("echarts",Parse(json.RootElement.GetProperty("request").GetRawText()).Engine);Equal(0,renderer.Calls);
});
await TestAsync("deadline is controlled 504",async()=>{
    using var gate=new BoundedRenderGate(1,0);var renderer=new RecordingRenderer(async(_,ct)=>{await Task.Delay(Timeout.Infinite,ct);return new([],"image/png","png");});
    var service=new RenderService(renderer,gate,TimeSpan.FromMilliseconds(30));
    try{await service.RenderAsync(Parse(valid),default);throw new Exception("Deadline ignored");}catch(RenderFailure ex){Equal(504,ex.Status);}
    using var recovered=await gate.EnterAsync(default);
});
await TestAsync("client cancellation is not timeout",async()=>{
    using var gate=new BoundedRenderGate(1,0);using var cts=new CancellationTokenSource();cts.Cancel();
    var service=new RenderService(new RecordingRenderer((_,_)=>throw new Exception("Unexpected")),gate,TimeSpan.FromSeconds(2));
    try{await service.RenderAsync(Parse(valid),cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
});
await TestAsync("output size enforced",async()=>{
    using var gate=new BoundedRenderGate(1,0);
    var renderer=new RecordingRenderer((_,_)=>Task.FromResult(new RenderArtifact(new byte[Contract.MaxOutputBytes+1],"image/png","png")));
    try{await new RenderService(renderer,gate,TimeSpan.FromSeconds(1)).RenderAsync(Parse(valid),default);throw new Exception("Oversize accepted");}catch(RenderFailure ex){Equal(413,ex.Status);}
});
await TestAsync("renderer failure releases admission",async()=>{
    using var gate=new BoundedRenderGate(1,0);var service=new RenderService(new RecordingRenderer((_,_)=>throw new RenderFailure("engine","Rejected")),gate,TimeSpan.FromSeconds(1));
    try{await service.RenderAsync(Parse(valid),default);throw new Exception("Unexpected success");}catch(RenderFailure ex){Equal("engine",ex.Code);}
    using var recovered=await gate.EnterAsync(default);
});


Test("production asset digests",()=>{
    var catalog=new Plotarium.Infrastructure.AssetCatalog(Path.Combine(Directory.GetCurrentDirectory(),"assets"));
    Equal(true,catalog.TryRead(new Uri("https://plotarium.invalid/assets/echarts/echarts.min.js"),out var bytes));
    Equal(true,bytes!.Length>1000);
    foreach(var target in new[]{"https://example.invalid/assets/echarts/echarts.min.js","file:///etc/passwd","http://plotarium.invalid/assets/echarts/echarts.min.js","https://plotarium.invalid:444/assets/echarts/echarts.min.js","https://plotarium.invalid/assets/echarts/echarts.min.js?x=1"})
        Equal(false,catalog.TryRead(new Uri(target),out _));
});
Test("tampered asset rejected",()=>{
    var temp=Path.Combine(Path.GetTempPath(),"plotarium-asset-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
    try{
        File.WriteAllText(Path.Combine(temp,"bad.js"),"bad");
        File.WriteAllText(Path.Combine(temp,"manifest.json"),JsonSerializer.Serialize(new{schemaVersion=1,engines=new{echarts="6.1.0",plotly="4.1.2",mermaid="12.1.0"},files=new[]{new{path="bad.js",bytes=3,sha256=new string('0',64)}}}));
        try{_ = new Plotarium.Infrastructure.AssetCatalog(temp);throw new Exception("Tampering accepted");}catch(InvalidDataException){}
    }finally{Directory.Delete(temp,true);}
});
Console.WriteLine(JsonSerializer.Serialize(new { failed, passed = results.Count-failed, results }, new JsonSerializerOptions{WriteIndented=true}));
return failed == 0 ? 0 : 1;

sealed class RecordingRenderer(Func<RenderRequest,CancellationToken,Task<RenderArtifact>> run):IChartRenderer
{
    public int Calls{get;private set;}
    public Task<RenderArtifact> RenderAsync(RenderRequest request,CancellationToken cancellationToken){Calls++;return run(request,cancellationToken);}
}
