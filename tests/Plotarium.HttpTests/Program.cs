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
var report=JsonSerializer.Serialize(new{failed,passed=results.Count-failed,results,scope="HTTP signatures and basic dimensions, not perceptual equivalence"},new JsonSerializerOptions{WriteIndented=true});
await File.WriteAllTextAsync(Path.Combine(output,"results.json"),report);Console.WriteLine(report);return failed==0?0:1;
