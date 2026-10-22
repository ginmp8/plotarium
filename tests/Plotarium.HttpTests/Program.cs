using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
if(args.Length!=1 || !Uri.TryCreate(args[0],UriKind.Absolute,out var origin) || origin.Scheme is not ("http" or "https"))
{ Console.Error.WriteLine("Usage: dotnet run --project tests/Plotarium.HttpTests -- http://localhost:5000/"); return 2; }
using var http=new HttpClient{BaseAddress=origin,Timeout=TimeSpan.FromSeconds(60)};
var results=new List<object>();int failed=0;
var output=Path.Combine("artifacts","integration",DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff"));Directory.CreateDirectory(output);
var engines=new Dictionary<string,object>
{
    ["echarts"]=new{engine="echarts",width=480,height=240,spec=new{xAxis=new{type="category",data=new[]{"A","B"}},yAxis=new{type="value"},series=new[]{new{type="bar",data=new[]{1,2}}}}},
    ["plotly"]=new{engine="plotly",width=480,height=240,spec=new{data=new[]{new{type="scatter",mode="lines",x=new[]{1,2},y=new[]{2,4}}},layout=new{title=new{text="Smoke"}}}},
    ["mermaid"]=new{engine="mermaid",width=480,height=240,source="flowchart LR\nA[Input] --> B[Output]"}
};
foreach(var (engine,request) in engines)
foreach(var format in new[]{"png","svg","json","jpeg","webp","pdf"})
{
    var body=JsonSerializer.SerializeToNode(request)!.AsObject();body["format"]=format;
    try
    {
        using var response=await http.PostAsJsonAsync("v1/render",body);
        if(!response.IsSuccessStatusCode)throw new Exception($"HTTP {(int)response.StatusCode}: "+(await response.Content.ReadAsStringAsync())[..Math.Min(280,(await response.Content.ReadAsStringAsync()).Length)]);
        var bytes=await response.Content.ReadAsByteArrayAsync();if(bytes.Length<8)throw new Exception("Empty artifact");
        var mime=format switch{"png"=>"image/png","svg"=>"image/svg+xml","json"=>"application/json","jpeg"=>"image/jpeg","webp"=>"image/webp",_=>"application/pdf"};
        if(response.Content.Headers.ContentType?.MediaType!=mime)throw new Exception("Unexpected MIME");
        switch(format)
        {
            case "png":
                if(bytes.Length<24 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) ||
                   BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4))!=480 || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4))!=240)
                    throw new Exception("PNG dimensions/signature invalid");break;
            case "svg":
                using(var stream=new MemoryStream(bytes))
                using(var xml=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))
                    if(XDocument.Load(xml).Root?.Name.LocalName!="svg")throw new Exception("Not SVG");break;
            case "json":
                using(var definition=JsonDocument.Parse(bytes))
                    if(definition.RootElement.GetProperty("normalization").GetString()!="envelope-only")throw new Exception("Wrong JSON contract");break;
            case "jpeg":if(bytes[0]!=255 || bytes[1]!=216)throw new Exception("Not JPEG");break;
            case "webp":if(bytes.Length<12 || Encoding.ASCII.GetString(bytes,0,4)!="RIFF" || Encoding.ASCII.GetString(bytes,8,4)!="WEBP")throw new Exception("Not WebP");break;
            case "pdf":if(Encoding.ASCII.GetString(bytes,0,5)!="%PDF-")throw new Exception("Not PDF");break;
        }
        await File.WriteAllBytesAsync(Path.Combine(output,$"{engine}.{format}"),bytes);
        results.Add(new{name=engine+"/"+format,status="pass",sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),bytes=bytes.Length});
    }
    catch(Exception ex){failed++;results.Add(new{name=engine+"/"+format,status="fail",error=ex is TaskCanceledException?"deadline":ex.GetType().Name+": "+ex.Message});}
}
foreach(var (name,content,status) in new[]{("invalid-json","{",HttpStatusCode.BadRequest),("unknown-engine","{\"engine\":\"other\",\"spec\":{}}",HttpStatusCode.UnprocessableEntity)})
{
    using var response=await http.PostAsync("v1/render",new StringContent(content,Encoding.UTF8,"application/json"));
    var pass=response.StatusCode==status;if(!pass)failed++;results.Add(new{name,status=pass?"pass":"fail",http=(int)response.StatusCode});
}
using(var unauthenticated=new HttpClient{BaseAddress=origin,Timeout=TimeSpan.FromSeconds(10)})
{
    using var response=await unauthenticated.GetAsync("v1/capabilities");
    var pass=response.StatusCode==HttpStatusCode.OK;if(!pass)failed++;results.Add(new{name="anonymous_capabilities",status=pass?"pass":"fail",http=(int)response.StatusCode});
}
// The correct route is POST /v1/render, not a separate URL for each engine.
using (var wrongRoute = await http.PostAsJsonAsync("v1/render/echarts", engines["echarts"]))
{
    var pass = wrongRoute.StatusCode == HttpStatusCode.NotFound;
    if (!pass) failed++;
    results.Add(new { name = "incorrect_engine_route_is_404", status = pass ? "pass" : "fail", http = (int)wrongRoute.StatusCode });
}
using (var invalidBody = await http.PostAsJsonAsync("v1/render", new { engine = "echarts", definition = new { series = new[] { new { type = "pie", data = new[] { 1, 2 } } } } }))
{
    var pass = invalidBody.StatusCode == HttpStatusCode.BadRequest;
    if (!pass) failed++;
    results.Add(new { name = "definition_instead_of_spec_is_400", status = pass ? "pass" : "fail", http = (int)invalidBody.StatusCode });
}
using (var response = await http.PostAsJsonAsync("v1/render", new { engine = "echarts", format = "json", spec = new { series = new[] { new { type = "pie", data = new[] { 1, 2 } } } } }))
{
    var pass = response.StatusCode == HttpStatusCode.OK;
    if (!pass) failed++;
    results.Add(new { name = "anonymous_json_render", status = pass ? "pass" : "fail", http = (int)response.StatusCode });
}

// Interactive 0.3.0 regression scenarios: request, resource, sharing, and MCP protocol boundaries.
foreach(var (engine,definition) in engines)
{
    try
    {
        using var created=await http.PostAsJsonAsync("v1/charts",definition);
        if(created.StatusCode!=HttpStatusCode.Created)throw new Exception("Create "+(int)created.StatusCode);
        using var parsed=JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var chart=parsed.RootElement;
        var id=chart.GetProperty("id").GetString()!;
        if(id.Length!=32 || !chart.GetProperty("capabilities").GetProperty("interactive").GetBoolean())throw new Exception("Invalid chart descriptor");
        var view=chart.GetProperty("viewUrl").GetString()!;
        using(var page=await http.GetAsync(view))
        {
            if(page.StatusCode!=HttpStatusCode.OK || page.Content.Headers.ContentType?.MediaType!="text/html") throw new Exception("View unavailable");
            if(page.Headers.TryGetValues("Content-Security-Policy",out var headers)&&headers.Any(x=>x.Contains("unsafe-inline",StringComparison.Ordinal)))
                throw new Exception("Unsafe inline script CSP");
            var html=await page.Content.ReadAsStringAsync();
            if(!html.Contains("data-chart-id=\""+id+"\"",StringComparison.Ordinal))throw new Exception("Viewer lost identity");
        }
        using(var embed=await http.GetAsync($"v1/charts/{id}/embed"))
            if(embed.StatusCode!=HttpStatusCode.OK||(await embed.Content.ReadAsStringAsync()).Contains("data-embed=\"true\"")==false)
                throw new Exception("Embed unavailable");
        using(var definitionResponse=await http.GetAsync($"v1/charts/{id}/definition"))
        {
            if(definitionResponse.StatusCode!=HttpStatusCode.OK)throw new Exception("Definition unavailable");
            using var data=JsonDocument.Parse(await definitionResponse.Content.ReadAsStringAsync());
            if(data.RootElement.GetProperty("request").GetProperty("engine").GetString()!=engine)throw new Exception("Definition changed engine");
        }
        using(var channel=await http.GetAsync($"v1/charts/{id}/share/teams"))
        {
            using var data=JsonDocument.Parse(await channel.Content.ReadAsStringAsync());
            if(data.RootElement.GetProperty("ready").GetBoolean())throw new Exception("Local origin must not claim public Teams support");
        }
        using(var asset=await http.GetAsync("v1/viewer/adapters/"+engine+".js"))
            if(asset.StatusCode!=HttpStatusCode.OK || asset.Content.Headers.ContentType?.MediaType!="text/javascript")
                throw new Exception("Missing engine adapter");
        if(engine=="echarts")
        {
            using var preview=await http.GetAsync($"v1/charts/{id}/preview?format=png");
            var bytes=await preview.Content.ReadAsByteArrayAsync();
            if(preview.StatusCode!=HttpStatusCode.OK || bytes.Length<24 || bytes[0]!=137) throw new Exception("Interactive preview failed: "+(int)preview.StatusCode);
        }
        results.Add(new{name="interactive_create_view_share_"+engine,status="pass"});
    }
    catch(Exception ex){failed++;results.Add(new{name="interactive_create_view_share_"+engine,status="fail",error=ex.Message});}
}
using(var viewAsset=await http.GetAsync("v1/assets/mermaid/mermaid.esm.min.mjs"))
{
    var pass=viewAsset.StatusCode==HttpStatusCode.OK&&viewAsset.Content.Headers.ContentType?.MediaType=="text/javascript";
    if(!pass)failed++;results.Add(new{name="mermaid_esm_asset",status=pass?"pass":"fail",http=(int)viewAsset.StatusCode});
}
using(var notFound=await http.GetAsync("v1/charts/00000000000000000000000000000000/view"))
{
    var pass=notFound.StatusCode==HttpStatusCode.NotFound;
    if(!pass)failed++;results.Add(new{name="invalid_chart_link_404",status=pass?"pass":"fail",http=(int)notFound.StatusCode});
}
using(var noScript=await http.PostAsJsonAsync("v1/charts",new{engine="echarts",spec=new{series=new[]{new{type="custom",data=new[]{1}}}}}))
{
    var pass=noScript.StatusCode==HttpStatusCode.UnprocessableEntity;
    if(!pass)failed++;results.Add(new{name="unsafe_custom_script_refused",status=pass?"pass":"fail",http=(int)noScript.StatusCode});
}
using(var rawHtml=await http.PostAsJsonAsync("v1/render",new{engine="echarts",format="html",spec=new{series=new[]{new{type="bar",data=new[]{3,4}}}}}))
{
    var pass=rawHtml.StatusCode==HttpStatusCode.OK&&rawHtml.Content.Headers.ContentType?.MediaType=="text/html";
    if(!pass)failed++;results.Add(new{name="html_as_render_format",status=pass?"pass":"fail",http=(int)rawHtml.StatusCode});
}
var mcpRequests=new object[]{
    new{jsonrpc="2.0",id=1,method="initialize",@params=new{protocolVersion="2025-11-25",capabilities=new{}}},
    new{jsonrpc="2.0",id=2,method="tools/list",@params=new{}},
    new{jsonrpc="2.0",id=3,method="resources/read",@params=new{uri="ui://plotarium/chart.html"}},
    new{jsonrpc="2.0",id=4,method="tools/call",@params=new{name="create_interactive_chart",arguments=new{engine="echarts",spec=new{series=new[]{new{type="bar",data=new[]{5,6}}}}}}}
};
foreach(var request in mcpRequests)
{
    var method=request.GetType().GetProperty("method")!.GetValue(request)!.ToString();
    try
    {
        using var response=await http.PostAsJsonAsync("mcp",request);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result=json.RootElement.GetProperty("result");
        if(response.StatusCode!=HttpStatusCode.OK)throw new Exception("MCP HTTP "+(int)response.StatusCode);
        if(method=="tools/list"&&result.GetProperty("tools")[0].GetProperty("_meta").GetProperty("ui").GetProperty("resourceUri").GetString()!="ui://plotarium/chart.html")throw new Exception("No MCP app resource association");
        if(method=="resources/read"&&result.GetProperty("contents")[0].GetProperty("mimeType").GetString()!="text/html;profile=mcp-app")throw new Exception("Wrong MCP UI resource MIME");
        if(method=="tools/call"&&!result.GetProperty("structuredContent").GetProperty("chartId").GetString()!.All(Uri.IsHexDigit))throw new Exception("Missing MCP chart id");
        results.Add(new{name="mcp_"+method,status="pass"});
    }catch(Exception ex){failed++;results.Add(new{name="mcp_"+method,status="fail",error=ex.Message});}
}

var report=JsonSerializer.Serialize(new{failed,passed=results.Count-failed,results,scope="HTTP signatures and basic dimensions, not perceptual equivalence"},new JsonSerializerOptions{WriteIndented=true});
await File.WriteAllTextAsync(Path.Combine(output,"results.json"),report);Console.WriteLine(report);return failed==0?0:1;
