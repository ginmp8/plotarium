using System.Text.Json;
using System.Text.Encodings.Web;
using Ploteus.Application;
using Ploteus.Domain;

namespace Ploteus.Api.Interactive;

/// <summary>Bounded stateless MCP Streamable HTTP subset for chart publication; JSON responses (no SSE or subscriptions).</summary>
public static class McpEndpoints
{
    public const string UiUri = "ui://ploteus/chart.html";
    public static void MapMcp(this WebApplication app, IConfigurationSection config)
    {
        var baseUrl = config["PublicBaseUrl"]?.TrimEnd('/');
        app.MapPost("/mcp",async Task<IResult> (HttpContext context, ChartStore store)=>
        {
            // Block browser-originated cross-site mutation. A reverse proxy should also restrict this endpoint.
            if(context.Request.Headers.Origin.Count>0)
                return Results.StatusCode(403);
            if(!context.Request.HasJsonContentType())return Results.StatusCode(415);
            if(context.Request.ContentLength>Contract.MaxBodyBytes)return Results.StatusCode(413);
            using var body=new MemoryStream();var buffer=new byte[8192];int count;
            while((count=await context.Request.Body.ReadAsync(buffer,context.RequestAborted))>0)
            {if(body.Length+count>Contract.MaxBodyBytes)return Results.StatusCode(413);body.Write(buffer,0,count);}
            try
            {
                using var payload=JsonDocument.Parse(body.ToArray());
                var root=payload.RootElement;
                if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("jsonrpc",out var protocol) || protocol.GetString()!="2.0" ||
                    !root.TryGetProperty("method",out var method) || method.ValueKind!=JsonValueKind.String)
                    return JsonRpcError(null,-32600,"Invalid Request");
                JsonElement? id=root.TryGetProperty("id",out var identity)?identity.Clone():null;
                var command=method.GetString()!;
                if(id is null && command.StartsWith("notifications/",StringComparison.Ordinal))return Results.StatusCode(202);
                if(id is null)return JsonRpcError(null,-32600,"A request id is required");
                var args=root.TryGetProperty("params",out var value)&&value.ValueKind==JsonValueKind.Object?value:default;
                object? result;
                try
                {
                    switch(command)
                    {
                        case "initialize":
                            result=new {protocolVersion="2025-11-25",serverInfo=new{name="ploteus",version="0.6.0"},
                                capabilities=new{tools=new{listChanged=false},resources=new{listChanged=false}}};break;
                        case "ping":result=new{};break;
                        case "tools/list":result=new{tools=new object[]{
                            new {name="create_interactive_chart",title="Create interactive chart",
                                description="Create a temporary interactive Ploteus chart from a safe ECharts, Plotly or Mermaid definition. Returns an expiring HTML view and optional MCP Apps widget.",
                                inputSchema=InputSchema(),_meta=new{ui=new{resourceUri=UiUri}}},
                            new {name="get_chart",title="Get chart",description="Fetch metadata for an existing temporary chart id.",
                                inputSchema=new{type="object",properties=new{id=new{type="string",minLength=32,maxLength=32}},required=new[]{"id"},additionalProperties=false}}
                        }};break;
                        case "resources/list":result=new{resources=new[]{new{uri=UiUri,name="Ploteus interactive chart",mimeType="text/html;profile=mcp-app",description="Interactive chart viewer powered by Ploteus"}}};break;
                        case "resources/read":
                        {
                            if(args.ValueKind!=JsonValueKind.Object || !args.TryGetProperty("uri",out var link) || link.GetString()!=UiUri)
                                return JsonRpcError(id,-32602,"Unknown UI resource");
                            var html=WidgetHtml(baseUrl);
                            result=new {contents=new[]{new {uri=UiUri,mimeType="text/html;profile=mcp-app",text=html,
                                _meta=baseUrl is null ? null : new {ui=new {csp=new{frameDomains=new[]{baseUrl},connectDomains=Array.Empty<string>(),resourceDomains=Array.Empty<string>()}}}}}};
                            break;
                        }
                        case "tools/call":
                        {
                            if(args.ValueKind!=JsonValueKind.Object || !args.TryGetProperty("name",out var name))
                                return JsonRpcError(id,-32602,"Missing tool name");
                            var tool=name.GetString();
                            var parameters=args.TryGetProperty("arguments",out var parameter)&&parameter.ValueKind==JsonValueKind.Object?parameter:default;
                            if(tool=="create_interactive_chart")
                            {
                                if(parameters.ValueKind!=JsonValueKind.Object)return JsonRpcError(id,-32602,"Missing chart input");
                                try
                                {
                                    var request=RequestValidator.Parse(JsonSerializer.SerializeToUtf8Bytes(parameters));
                                    var saved=store.Create(request);
                                    var desc=InteractiveRoutes.Describe(saved,baseUrl);
                                    result=new{content=new[]{new {type="text",text="Interactive chart created: "+(baseUrl??"local")+"/v1/charts/"+saved.Id+"/view (expires "+saved.ExpiresAt.ToString("O")+")"}},
                                        structuredContent=new{chartId=saved.Id,request=saved.Request,description=desc,publicOrigin=baseUrl},
                                        _meta=new{ui=new{resourceUri=UiUri}}};
                                }
                                catch(RenderFailure exception){return JsonRpcError(id,-32602,exception.Message);}
                            }
                            else if(tool=="get_chart")
                            {
                                if(parameters.ValueKind!=JsonValueKind.Object || !parameters.TryGetProperty("id",out var chartId))return JsonRpcError(id,-32602,"Missing chart id");
                                var saved=store.Get(chartId.GetString()??"");
                                if(saved is null)return JsonRpcError(id,-32602,"Chart not found or expired");
                                result=new{content=new[]{new{type="text",text=JsonSerializer.Serialize(InteractiveRoutes.Describe(saved,baseUrl),Contract.Json)}},
                                    structuredContent=new{chartId=saved.Id,request=saved.Request,publicOrigin=baseUrl}};
                            }
                            else return JsonRpcError(id,-32601,"Unknown tool");
                            break;
                        }
                        default:return JsonRpcError(id,-32601,"Method not found");
                    }
                    return Results.Json(new {jsonrpc="2.0",id,result});
                }
                catch(JsonException){return JsonRpcError(id,-32602,"Invalid params");}
            }
            catch(JsonException){return JsonRpcError(null,-32700,"Parse error");}
        });
        app.MapGet("/mcp",()=>Results.StatusCode(405)); // No SSE; callers MUST use POST.
    }

    private static IResult JsonRpcError(JsonElement? id,int code,string message) =>
        Results.Json(new{jsonrpc="2.0",id,error=new{code,message}});

    private static object InputSchema() => new {
        type="object",required=new[]{"engine"},additionalProperties=false,
        properties=new Dictionary<string,object>{
            ["engine"]=new{type="string",@enum=new[]{"echarts","plotly","mermaid"}},
            ["spec"]=new{type="object",description="ECharts options or Plotly figure (data/layout)"},
            ["source"]=new{type="string",description="Mermaid diagram text"},
            ["format"]=new{type="string",@enum=new[]{"png","svg","html","json"}},
            ["width"]=new{type="integer",minimum=64,maximum=4096},
            ["height"]=new{type="integer",minimum=64,maximum=4096},
            ["theme"]=new{type="string",@enum=new[]{"light","dark"}},
            ["background"]=new{type="string",description="CSS hex color or transparent"}}
    };

    private static string WidgetHtml(string? origin)
    {
        // The UI resource deliberately uses the same renderer in a nested sandboxed iframe; no custom scripts from model input.
        var originJson=JsonSerializer.Serialize(origin,Contract.Json).Replace("<","\\u003c",StringComparison.Ordinal);
        return "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"+
            "<style>body{margin:0;font:14px system-ui;background:#f5f7f9;color:#1c3647}.bar{padding:10px 14px;display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid #dae2e7}iframe{width:100%;min-height:420px;height:70vh;border:0}.message{padding:18px;color:#5d7080}a{color:#176285}</style></head>"+
            "<body><div class=\"bar\"><strong>PLOTEUS · Interativo</strong><a id=\"open\" target=\"_blank\" rel=\"noopener noreferrer\" hidden>Abrir</a></div><div id=\"message\" class=\"message\">Aguardando o gráfico…</div><iframe id=\"view\" title=\"Gráfico interativo Ploteus\" hidden></iframe>"+
            "<script>const base="+originJson+";const msg=document.getElementById('message');const frame=document.getElementById('view');"+
            "function accept(data){const content=data?.structuredContent??data?.result?.structuredContent??data;const id=content?.chartId??content?.chart?.id;if(!/^[a-f0-9]{32}$/.test(id||''))return;if(!base){msg.textContent='Configure PublicBaseUrl HTTPS para exibir o gráfico neste cliente.';return;}const url=base+'/v1/charts/'+id+'/embed';frame.src=url;frame.hidden=false;msg.hidden=true;const link=document.getElementById('open');link.href=base+'/v1/charts/'+id+'/view';link.hidden=false;}"+
            "addEventListener('message',event=>{if(event.source!==parent||event.data?.jsonrpc!=='2.0')return;if(event.data.method==='ui/notifications/tool-result')accept(event.data.params);});"+
            "if(window.openai?.toolOutput)accept(window.openai.toolOutput);</script></body></html>";
    }
}
