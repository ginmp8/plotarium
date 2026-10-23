using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Plotarium.Application;
using Plotarium.Domain;
using Plotarium.Infrastructure;

namespace Plotarium.Api.Stateless;

/// <summary>One HTTP response, no chart store and no follow-up fetches for JS, CSS or chart definition.</summary>
public sealed class StatelessHtmlBuilder(AssetCatalog assets,RenderService renderer)
{
    public async Task<(string Html,string Csp)> BuildAsync(RenderRequest request,CancellationToken cancellationToken)
    {
        var nonce=Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var csp="default-src 'none'; script-src 'nonce-"+nonce+"' 'wasm-unsafe-eval'; "+
            "style-src 'nonce-"+nonce+"' 'unsafe-inline'; img-src data: blob:; "+
            "font-src data:; connect-src 'none'; worker-src blob:; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'self'";
        var json=JsonSerializer.Serialize(request,Contract.Json);
        var html=new StringBuilder(6_000_000);
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Plotarium - Stateless chart</title>");
        html.Append("<style nonce=\"").Append(nonce).Append("\">").Append(Css).Append("</style></head><body data-plotarium-stateless=\"true\">");
        html.Append("<main><header><strong>Plotarium</strong><span id=\"engine\"></span><nav><button id=\"minus\" type=\"button\">-</button><button id=\"plus\" type=\"button\">+</button><button id=\"reset\" type=\"button\">Reset</button><button id=\"download\" type=\"button\">Download</button></nav></header>");
        html.Append("<div id=\"stage\"><div id=\"chart\" aria-label=\"Interactive chart\"></div></div><output id=\"state\" role=\"status\"></output></main>");
        html.Append("<script id=\"plotarium-payload\" type=\"application/json\">").Append(json).Append("</script>");
        switch(request.Engine)
        {
            case "echarts":
                Script(html,nonce,assets.ReadScript("/assets/echarts/echarts.min.js"));
                if(RequestValidator.UsesEchartsGl(request))
                    Script(html,nonce,assets.ReadScript("/assets/echarts-gl/echarts-gl.min.js"));
                break;
            case "plotly": Script(html,nonce,assets.ReadScript("/assets/plotly/plotly.min.js"));break;
            case "mermaid":
                var svg=await renderer.RenderAsync(request with {Format="svg",Scale=1},cancellationToken);
                if(svg.Bytes.Length>Contract.MaxOutputBytes)throw new RenderFailure("output_limit","Rendered SVG is too large.",413);
                html.Append("<template id=\"mermaid-data\" data-src=\"data:image/svg+xml;base64,").Append(Convert.ToBase64String(svg.Bytes)).Append("\"></template>");
                break;
        }
        // If the client has no WebGL, a single HTML response can still display a real,
        // server-rendered 3D snapshot. Native WebGL remains interactive when supported.
        if(request.Engine=="echarts" && RequestValidator.UsesEchartsGl(request))
        {
            try
            {
                var preview=await renderer.RenderAsync(request with {Format="png",Scale=1},cancellationToken);
                html.Append("<template id=\"fallback-data\" data-src=\"data:image/png;base64,")
                    .Append(Convert.ToBase64String(preview.Bytes)).Append("\"></template>");
            }
            catch(RenderFailure) when(!cancellationToken.IsCancellationRequested)
            {
                // No fallback available: leave the standalone native GL chart available.
            }
        }
        // Local maps are inserted as JSON, never fetched by the browser.
        if(request.Engine=="echarts" && request.Spec is { } spec)
        {
            var maps=new HashSet<string>(StringComparer.Ordinal);
            static void AddMaps(JsonElement container,HashSet<string> names)
            {
                if(container.ValueKind==JsonValueKind.Array) { foreach(var child in container.EnumerateArray())AddMaps(child,names);return; }
                if(container.ValueKind==JsonValueKind.Object && container.TryGetProperty("map",out var map) && map.ValueKind==JsonValueKind.String)
                    names.Add(map.GetString()!);
            }
            if(spec.TryGetProperty("series",out var series))AddMaps(series,maps);
            if(spec.TryGetProperty("geo",out var geo))AddMaps(geo,maps);
            var mapData=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
            foreach(var name in maps)
                using(var doc=JsonDocument.Parse(assets.ReadGeoJson(name)))mapData[name]=doc.RootElement.Clone();
            html.Append("<script type=\"application/json\" id=\"maps\">")
                .Append(JsonSerializer.Serialize(mapData,Contract.Json)).Append("</script>");
        }
        if(request.Engine=="plotly" && request.Spec is { } plotlySpec && plotlySpec.TryGetProperty("data",out var traces) &&
           traces.EnumerateArray().Any(t=>t.TryGetProperty("type",out var type) && type.GetString() is "scattermap" or "choroplethmap" or "densitymap"))
        {
            html.Append("<script type=\"application/json\" id=\"plotly-world\">")
                .Append(assets.ReadGeoJson("world")).Append("</script>");
        }
        Script(html,nonce,Javascript);
        html.Append("</body></html>");
        if(Encoding.UTF8.GetByteCount(html.ToString())>Contract.MaxOutputBytes)
            throw new RenderFailure("output_limit","Standalone HTML exceeds the output limit.",413);
        return (html.ToString(),csp);
    }

    private static void Script(StringBuilder buffer,string nonce,string source)
    {
        if(source.Contains("</script",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Trusted script asset contains an HTML script closing sequence.");
        buffer.Append("<script nonce=\"").Append(nonce).Append("\">").Append(source).Append("</script>");
    }

    private const string Css="""
*{box-sizing:border-box}html,body{margin:0;height:100%;font:14px system-ui,Arial;background:#f6f8fb;color:#152334}main{min-height:100vh;display:flex;flex-direction:column;padding:16px 20px;gap:12px}header{display:flex;align-items:center;gap:16px;border:1px solid #dde3ed;border-radius:12px;padding:12px 18px;background:white}header strong{font-size:18px;letter-spacing:.05em}header span{color:#64748b}nav{margin-left:auto;display:flex;gap:7px}button{font:inherit;cursor:pointer;padding:7px 12px;border:1px solid #cdd5df;border-radius:7px;background:#fff;color:#152334}button:hover,button:focus-visible{background:#e6f0fc;outline:2px solid transparent}#stage{min-height:330px;flex:1;display:flex;align-items:center;justify-content:center;overflow:hidden;background:#fff;border-radius:12px;border:1px solid #dde3ed}#chart{height:72vh;min-height:300px;width:100%;transform-origin:center center}#state{color:#b33;min-height:20px}#stage img{object-fit:contain;width:100%;height:100%}@media(max-width:600px){header{flex-wrap:wrap}nav{margin-left:0}main{padding:8px}#chart{height:70vh}}@media(prefers-reduced-motion:reduce){*{transition:none!important;animation:none!important}}
""";

    private const string Javascript="""
(async()=>{
  'use strict';
  const request=JSON.parse(document.getElementById('plotarium-payload').textContent);
  const host=document.getElementById('chart');const state=document.getElementById('state');
  const engine=document.getElementById('engine');engine.textContent=request.engine.toUpperCase();
  let current=null,zoom=1;
  const glFallback=document.getElementById('fallback-data');
  function showFallback(){
    if(!glFallback)return false;
    const image=document.createElement('img');image.alt='Server-rendered 3D chart preview';image.src=glFallback.dataset.src;
    host.replaceChildren(image);document.body.dataset.renderMode='static-fallback';
    state.textContent='WebGL unavailable in this browser; showing a static preview.';
    current={reset:()=>{},resize:()=>{}};return true;
  }
  const doZoom=()=>{host.style.transform='scale('+zoom+')';};
  document.getElementById('plus').addEventListener('click',()=>{zoom=Math.min(3,+(zoom+.2).toFixed(2));doZoom();});
  document.getElementById('minus').addEventListener('click',()=>{zoom=Math.max(.5,+(zoom-.2).toFixed(2));doZoom();});
  document.getElementById('reset').addEventListener('click',()=>{zoom=1;doZoom();current?.reset?.();});
  document.getElementById('download').addEventListener('click',async()=>{
    try{
      let url;
      if(request.engine==='echarts') url=current.chart.getDataURL({type:'png',pixelRatio:2});
      else if(request.engine==='plotly') {await Plotly.downloadImage(host,{format:'png',filename:'plotarium'});return;}
      else url=document.getElementById('mermaid-data').dataset.src;
      const a=document.createElement('a');a.download='plotarium.'+(request.engine==='mermaid'?'svg':'png');a.href=url;a.click();
    }catch{state.textContent='Download unavailable for this chart.';}
  });
  try{
    if(request.engine==='echarts') {
      if(glFallback && !document.createElement('canvas').getContext('webgl')){showFallback();return;}
      const opts=structuredClone(request.spec);
      const maps=document.getElementById('maps');
      if(maps){for(const [name,definition] of Object.entries(JSON.parse(maps.textContent)))echarts.registerMap(name,definition);}
      const chart=echarts.init(host,request.theme==='dark'?'dark':null,{renderer:'canvas'});
      opts.animation=false;if(!opts.tooltip)opts.tooltip={trigger:'item'};
      chart.setOption(opts,{notMerge:true});chart.on('click',p=>{state.textContent='Selected: '+String(p.name??'element');});
      current={chart,reset:()=>chart.dispatchAction({type:'dataZoom',start:0,end:100}),resize:()=>chart.resize()};
    } else if(request.engine==='plotly') {
      const figure=structuredClone(request.spec);
      const world=document.getElementById('plotly-world');
      if(world) {
        const geo=JSON.parse(world.textContent);
        const dark=request.theme==='dark';
        figure.layout={...(figure.layout||{}),map:{...(figure.layout?.map||{}),style:{version:8,sources:{world:{type:'geojson',data:geo}},layers:[
          {id:'background',type:'background',paint:{'background-color':dark?'#17232c':'#dcecf4'}},
          {id:'land',type:'fill',source:'world',paint:{'fill-color':dark?'#3b5664':'#d1d8cd'}}]}}};
        for(const trace of figure.data)if(trace.type==='choroplethmap'&&!trace.geojson){trace.geojson=geo;trace.featureidkey=trace.featureidkey||'properties.iso3';}
      }
      await Plotly.newPlot(host,figure.data,{...(figure.layout||{}),autosize:true,paper_bgcolor:request.theme==='dark'?'#17232c':'#ffffff'},
        {displayModeBar:true,displaylogo:false,responsive:true,scrollZoom:true});
      current={reset:()=>Plotly.relayout(host,{'xaxis.autorange':true,'yaxis.autorange':true}),resize:()=>Plotly.Plots.resize(host)};
    } else {
      const image=document.createElement('img');image.alt='Mermaid diagram';image.src=document.getElementById('mermaid-data').dataset.src;
      host.replaceChildren(image);current={reset:()=>{},resize:()=>{}};
    }
    window.addEventListener('resize',()=>{current?.resize?.();});
  }catch(err){if(!showFallback())state.textContent='Unable to render chart in this browser.';console.error('Plotarium stateless viewer failed:',err?.name||'Error');}
})();
""";
}
