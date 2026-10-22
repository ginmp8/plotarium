using System.Reflection;
using System.Text;
using System.Text.Json;
using Plotarium.Application;
using Plotarium.Domain;
using Plotarium.Infrastructure;

namespace Plotarium.Api.Interactive;

/// <summary>HTTP-facing presenters. No chart definition is ever executed as code.</summary>
public static class InteractiveRoutes
{
    public static void MapInteractiveRoutes(this WebApplication app, IConfigurationSection config)
    {
        var configuredOrigin = config["PublicBaseUrl"]?.TrimEnd('/');
        if (configuredOrigin is not null && (!Uri.TryCreate(configuredOrigin,UriKind.Absolute,out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.UserInfo.Length > 0 || origin.Query.Length > 0 || origin.Fragment.Length > 0 ||
            origin.AbsolutePath != "/"))
            throw new InvalidOperationException("Plotarium:Interactive:PublicBaseUrl must be a root HTTPS origin with no query, credentials or path.");
        var ancestors = config.GetSection("FrameAncestors").Get<string[]>() ?? ["'self'"];
        if (ancestors.Length is < 1 or > 12 || ancestors.Any(x=>!ValidAncestor(x)))
            throw new InvalidOperationException("Invalid frame ancestor; use explicit HTTPS host origins or 'self'.");
        var csp = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' blob: data:; " +
                  "connect-src 'self'; worker-src blob:; font-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors "+string.Join(' ',ancestors);

        app.MapGet("/v1/interactive/capabilities", () => Results.Json(new
        {
            schemaVersion = "1",version="0.4.0",formats=new[]{"html","png","svg","jpeg","webp","pdf","json"},
            engines=new Dictionary<string,InteractiveCapabilities>{["echarts"]=ChartCapabilities.For("echarts"),["plotly"]=ChartCapabilities.For("plotly"),["mermaid"]=ChartCapabilities.For("mermaid")},
            channels = new[]{"browser","iframe","mcp-apps","teams-link-card","whatsapp-link"},
            sharing=new{mode="expiring-anonymous-link",persistent=false,publicOriginConfigured=configuredOrigin is not null},
            limitations=new[]{"no-arbitrary-scripts","only-prepackaged-engine-assets","remote-hosts-require-https-and-gateway-policy"}
        }));

        app.MapPost("/v1/charts",async Task<IResult> (HttpContext context,ChartStore store)=>
        {
            var request = await ReadRequest(context);
            var chart=store.Create(request);
            return Results.Json(Describe(chart,configuredOrigin),statusCode:201);
        });

        app.MapGet("/v1/charts/{id}/definition",(string id,ChartStore store)=>
        {
            var item=store.Get(id);if(item is null)return Results.NotFound();
            return Results.Json(new{item.Id,item.ExpiresAt,request=item.Request,capabilities=ChartCapabilities.For(item.Request.Engine)});
        });

        foreach(var (suffix,embed) in new[]{("view",false),("embed",true)})
        {
            app.MapGet("/v1/charts/{id}/"+suffix,(HttpContext context,string id,ChartStore store)=>
            {
                if(store.Get(id) is null)return Results.NotFound();
                context.Response.Headers.ContentSecurityPolicy=csp;
                context.Response.Headers["Referrer-Policy"]="no-referrer";
                context.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
                return Results.Content(RenderPage(id,embed),"text/html; charset=utf-8");
            });
        }

        app.MapGet("/v1/charts/{id}/preview",async Task<IResult> (string id,string? format,ChartStore store,RenderService render,HttpContext context)=>
        {
            var chart=store.Get(id);if(chart is null)return Results.NotFound();
            var chosen = format ?? "png";
            if(chosen is not ("png" or "svg" or "jpeg" or "webp" or "pdf"))
                throw new RenderFailure("preview_format","Choose png, svg, jpeg, webp or pdf.");
            var file=await render.RenderAsync(chart.Request with {Format=chosen},context.RequestAborted);
            return Results.File(file.Bytes,file.ContentType,fileDownloadName:"plotarium."+file.Extension);
        });

        app.MapGet("/v1/charts/{id}/share/{channel}",(string id,string channel,ChartStore store)=>
        {
            var chart=store.Get(id);if(chart is null)return Results.NotFound();
            if(channel is not ("teams" or "whatsapp" or "chatgpt" or "web"))
                throw new RenderFailure("channel","Unknown publication channel.");
            return Results.Json(DescribeChannel(chart,channel,configuredOrigin));
        });

        app.MapGet("/v1/viewer/adapters/{engine}.js",(string engine)=>
        {
            if(engine is not ("echarts" or "plotly" or "mermaid"))return Results.NotFound();
            var resource="Plotarium.Api.adapters."+engine+".js";
            using var stream=typeof(InteractiveRoutes).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Missing bundled engine adapter.");
            using var buffer=new MemoryStream();stream.CopyTo(buffer);
            return Results.File(buffer.ToArray(),"text/javascript");
        });
        app.MapGet("/v1/viewer/{file}",(string file)=>
        {
            if(file is not ("viewer.js" or "viewer.css"))return Results.NotFound();
            var resource="Plotarium.Api."+file;
            using var stream=typeof(InteractiveRoutes).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Missing packaged viewer resource.");
            using var buffer=new MemoryStream();stream.CopyTo(buffer);
            return Results.File(buffer.ToArray(),file.EndsWith(".js",StringComparison.Ordinal)?"text/javascript":"text/css");
        });

        app.MapGet("/v1/assets/{**path}",(string path,AssetCatalog assets)=>
        {
            if(path.Length is < 2 or > 250 || path.Contains('\\') || path.Contains("..",StringComparison.Ordinal) || path.StartsWith('/'))return Results.NotFound();
            if(!assets.TryReadPublic(path,out var file) || file is null)return Results.NotFound();
            var mime=path.EndsWith(".js",StringComparison.Ordinal)?"text/javascript":
                path.EndsWith(".mjs",StringComparison.Ordinal)?"text/javascript":
                path.EndsWith(".json",StringComparison.Ordinal)||path.EndsWith(".geojson",StringComparison.Ordinal)?"application/json":"application/octet-stream";
            return Results.File(file,mime);
        });
    }

    public static async Task<RenderRequest> ReadRequest(HttpContext context)
    {
        if(!context.Request.HasJsonContentType())throw new RenderFailure("content_type","Use application/json.",415);
        if(context.Request.ContentLength>Contract.MaxBodyBytes)throw new RenderFailure("body_limit","Request is too large.",413);
        using var stream=new MemoryStream();var buffer=new byte[8192];int n;
        while((n=await context.Request.Body.ReadAsync(buffer,context.RequestAborted))>0)
        {
            if(stream.Length+n>Contract.MaxBodyBytes)throw new RenderFailure("body_limit","Request is too large.",413);
            stream.Write(buffer,0,n);
        }
        return RequestValidator.Parse(stream.ToArray());
    }

    public static object Describe(StoredChart chart,string? origin)
    {
        var basePath="/v1/charts/"+chart.Id;
        string Address(string path)=>origin is null?path:origin+path;
        return new {
            chart.Id,chart.CreatedAt,chart.ExpiresAt,chart.Request.Engine,
            mode="interactive",persistence="process-memory",clientCanInteract=true,
            viewUrl=Address(basePath+"/view"),embedUrl=Address(basePath+"/embed"),
            previewUrl=Address(basePath+"/preview?format=png"),definitionUrl=Address(basePath+"/definition"),
            capabilities=ChartCapabilities.For(chart.Request.Engine),publiclyShareable=origin is not null,
            channels=new {teams=Address(basePath+"/share/teams"),whatsapp=Address(basePath+"/share/whatsapp"),chatgpt=Address(basePath+"/share/chatgpt")}
        };
    }

    public static object DescribeChannel(StoredChart chart,string channel,string? origin)
    {
        var root="/v1/charts/"+chart.Id;
        var view=origin is null?root+"/view":origin+root+"/view";
        var preview=origin is null?root+"/preview?format=png":origin+root+"/preview?format=png";
        var ready=origin is not null;
        object? native=null;
        if(ready && channel=="teams")native=new {
            type="AdaptiveCard",version="1.5",
            body=new object[]{new{type="TextBlock",text="Plotarium • Gráfico interativo",weight="Bolder",size="Medium"},new{type="Image",url=preview,altText="Prévia estática do gráfico"}},
            actions=new[]{new{type="Action.OpenUrl",title="Abrir gráfico interativo",url=view}}
        };
        if(ready && channel=="whatsapp")native=new{
            text="Abrir gráfico interativo: "+view, previewUrl=preview,
            urlButton=new {title="Abrir gráfico",url=view},sendViaProviderRequired=true,
            cloudApiMessageTemplate=new {
                messaging_product="whatsapp",recipient_type="individual",type="text",
                text=new {preview_url=true,body="Visualize o gráfico interativo: "+view}
            },
            recipientRequired=true,
            note="Before POST /{PHONE_NUMBER_ID}/messages, supply the authorized recipient as 'to', and use server-held Meta authorization. This response does not send messages."
        };
        if(ready && channel=="chatgpt")native=new {
            mcpEndpoint=origin+"/mcp",resourceUri="ui://plotarium/chart.html",tool="create_interactive_chart",
            note="Register the MCP server as a custom app. The app host controls whether its UI may load the public HTTPS origin."
        };
        return new {channel,ready,chart.Id,chart.ExpiresAt,viewUrl=view,previewUrl=preview,
            native, reason=ready?null:"Set Plotarium:Interactive:PublicBaseUrl to an approved HTTPS origin before sharing outside this host."};
    }

    public static string RenderPage(string id,bool embed) => "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">"+
        "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Plotarium · Gráfico interativo</title>"+
        "<link rel=\"stylesheet\" href=\"/v1/viewer/viewer.css\"><script src=\"/v1/viewer/viewer.js\" defer></script></head>"+
        "<body data-chart-id=\""+id+"\" data-embed=\""+(embed?"true":"false")+"\"><main class=\"shell\">"+
        "<header><div class=\"identity\"><span class=\"mark\" aria-hidden=\"true\">P</span><div>"+
        "<span class=\"brand\">PLOTARIUM / INTERACTIVE</span><h1>Explore o gráfico</h1></div></div>"+
        "<span id=\"engine\" class=\"pill\">Carregando</span></header><section class=\"frame\" aria-label=\"Visualização interativa\">"+
        "<nav class=\"toolbar\" aria-label=\"Ferramentas do gráfico\"><div class=\"tools\">"+
        "<button id=\"zoomin\" aria-label=\"Ampliar\">+ Zoom</button><button id=\"zoomout\" aria-label=\"Reduzir\">− Zoom</button>"+
        "<button id=\"reset\">Restaurar</button><button id=\"theme\">Tema escuro</button></div><div class=\"tools\">"+
        "<button id=\"definition\">JSON</button><button id=\"download\">PNG</button><button id=\"share\" class=\"primary\">Copiar link</button></div></nav>"+
        "<div class=\"stage\"><div id=\"state\" role=\"status\" aria-live=\"polite\">Carregando…</div><div id=\"chart\" role=\"img\" aria-label=\"Gráfico interativo\"></div></div>"+
        "<div class=\"details\"><span id=\"selection\" aria-live=\"polite\">Selecione pontos para explorar os dados</span>"+
        "<span>Escala: <span id=\"status\">100%</span></span></div></section>"+
        "<footer><span>Interatividade conforme a biblioteca · Sem execução de callbacks do usuário</span><span id=\"expires\"></span></footer>"+
        "</main></body></html>";

    private static bool ValidAncestor(string item)
    {
        if(item=="'self'")return true;
        if(item.Contains('*')||item.Contains(';')||item.Any(char.IsControl))return false;
        return Uri.TryCreate(item,UriKind.Absolute,out var url)&&url.Scheme=="https"&&url.UserInfo.Length==0&&url.Query.Length==0&&url.Fragment.Length==0&&url.AbsolutePath=="/";
    }
}
