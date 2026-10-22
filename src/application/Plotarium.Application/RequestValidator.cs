using System.Text.Json;
using System.Text.RegularExpressions;
using Plotarium.Domain;
namespace Plotarium.Application;

public static partial class RequestValidator
{
    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
        { "__proto__", "prototype", "constructor", "href", "src", "url", "image", "images" };
    public static RenderRequest Parse(ReadOnlyMemory<byte> input)
    {
        if (input.Length > Contract.MaxBodyBytes) throw new RenderFailure("body_limit", "Request is too large.", 413);
        try
        {
            using var doc = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = 32 });
            var nodes = 0;
            Inspect(doc.RootElement, ref nodes);
            var request = doc.RootElement.Deserialize<RenderRequest>(Contract.Json)
                ?? throw new RenderFailure("request", "Request is required.", 400);
            Validate(request);
            return request;
        }
        catch(JsonException) { throw new RenderFailure("json", "Invalid JSON or unsupported request property.", 400); }
    }
    private static void Inspect(JsonElement element, ref int nodes)
    {
        if (++nodes > 100_000) throw new RenderFailure("node_limit", "Too many JSON values.", 413);
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach(var prop in element.EnumerateObject())
            {
                if (!names.Add(prop.Name)) throw new RenderFailure("duplicate", "Duplicate JSON properties are not allowed.", 400);
                if (Forbidden.Contains(prop.Name)) throw new RenderFailure("property", "External resources and prototype properties are not supported.");
                Inspect(prop.Value, ref nodes);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() > 50_000) throw new RenderFailure("array_limit", "Array is too large.", 413);
            foreach(var item in element.EnumerateArray()) Inspect(item, ref nodes);
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString()!;
            if (text.Length > 100_000) throw new RenderFailure("string_limit", "String is too long.", 413);
            if (UnsafeResource().IsMatch(text)) throw new RenderFailure("resource", "External resources and active content are not supported.");
        }
    }
    private static void Validate(RenderRequest r)
    {
        _ = Contract.EngineVersion(r.Engine);
        _ = Contract.Format(r.Format);
        if (r.Width is < 64 or > 4096 || r.Height is < 64 or > 4096 || r.Scale is < 1 or > 3)
            throw new RenderFailure("dimensions", "Width/height must be 64..4096 and scale 1..3.");
        if ((long)r.Width*r.Height*r.Scale*r.Scale > Contract.MaxPixels)
            throw new RenderFailure("pixels", "Requested raster exceeds the pixel budget.");
        if (r.Theme is not ("light" or "dark")) throw new RenderFailure("theme", "Theme must be light or dark.");
        if (r.Background is null || !Background().IsMatch(r.Background)) throw new RenderFailure("background", "Use #RRGGBB or transparent.");
        if (r.Format == "jpeg" && r.Background == "transparent") throw new RenderFailure("background", "JPEG requires an opaque background.");
        if (r.Format is "svg" or "json" or "pdf" && r.Scale != 1)
            throw new RenderFailure("scale", "Scale applies only to raster formats.");
        if (r.Engine == "mermaid")
        {
            if (r.Spec is not null || string.IsNullOrWhiteSpace(r.Source)) throw new RenderFailure("mermaid_source", "Mermaid requires source, not spec.");
            if (r.Source.Length > 50_000) throw new RenderFailure("source_limit", "Mermaid source exceeds 50000 characters.", 413);
            if (r.Source.TrimStart().StartsWith("---", StringComparison.Ordinal) || MermaidPolicy().IsMatch(r.Source))
                throw new RenderFailure("mermaid_policy", "Mermaid configuration directives, links and HTML are disabled.");
            return;
        }
        if (r.Source is not null || r.Spec is not { ValueKind: JsonValueKind.Object } spec)
            throw new RenderFailure("spec", "Charts require a JSON object in spec, not source.");
        if (r.Engine == "plotly")
        {
            foreach(var prop in spec.EnumerateObject())
                if (prop.Name is not ("data" or "layout")) throw new RenderFailure("plotly_property", "Plotly spec supports only data and layout; config is server-owned.");
            if (!spec.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                throw new RenderFailure("plotly_data", "Plotly requires a nonempty data array.");
            if (spec.TryGetProperty("layout", out var layout) && layout.ValueKind != JsonValueKind.Object)
                throw new RenderFailure("plotly_layout", "Plotly layout must be an object.");
            foreach(var trace in data.EnumerateArray())
            {
                if (trace.ValueKind != JsonValueKind.Object) throw new RenderFailure("trace", "Every trace must be an object.");
                if (trace.TryGetProperty("type", out var type) && type.ValueKind != JsonValueKind.String)
                    throw new RenderFailure("trace", "Trace type must be a string.");
                if (trace.TryGetProperty("source", out var sourceValue) && sourceValue.ValueKind == JsonValueKind.String &&
                    Uri.TryCreate(sourceValue.GetString(), UriKind.Absolute, out _))
                    throw new RenderFailure("external_resource", "External chart resources are not supported.");
                if (trace.TryGetProperty("type", out type) && type.GetString() is "scattermapbox" or "choroplethmapbox" or "densitymapbox")
                    throw new RenderFailure("removed_mapbox", "Legacy Mapbox traces were removed from Plotly.js 4; use offline scattergeo or choropleth, or configure a separate MapLibre provider.");
                if (trace.TryGetProperty("type", out type) && type.GetString() is "scattermap" or "choroplethmap" or "densitymap")
                    throw new RenderFailure("offline_maplibre", "Plotly MapLibre tiles/styles are not available in the offline renderer; use scattergeo or choropleth.");
            }
        }
        else
        {
            if (spec.TryGetProperty("geo", out var geo))
            {
                var geos = geo.ValueKind == JsonValueKind.Array ? geo.EnumerateArray().ToArray() : new[] { geo };
                foreach(var item in geos)
                    if(item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("map",out var name) || name.ValueKind != JsonValueKind.String || !SupportedMap(name.GetString()))
                        throw new RenderFailure("offline_map", "ECharts geo must name a packaged local map.");
            }
            if (!spec.TryGetProperty("series", out var series) || series.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new RenderFailure("series", "ECharts requires a series object or array.");
            if (series.ValueKind == JsonValueKind.Array)
            {
                if (series.GetArrayLength() == 0) throw new RenderFailure("series", "ECharts series cannot be empty.");
                foreach(var item in series.EnumerateArray()) ValidateSeries(item);
            }
            else ValidateSeries(series);
        }
    }
    private static void ValidateSeries(JsonElement s)
    {
        if (s.ValueKind != JsonValueKind.Object || !s.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            throw new RenderFailure("series_type", "Every ECharts series requires a string type.");
        if (type.GetString() is not ("line" or "bar" or "pie" or "scatter" or "effectScatter" or "radar" or "tree" or "treemap" or "sunburst" or "boxplot" or "candlestick" or "heatmap" or "parallel" or "lines" or "graph" or "sankey" or "funnel" or "gauge" or "pictorialBar" or "themeRiver" or "chord" or "map"))
            throw new RenderFailure("series_profile", "Unsupported ECharts core series type; arbitrary custom callbacks and GL extensions are not installed.");
        if (type.GetString() == "map" && (!s.TryGetProperty("map", out var map) || map.ValueKind != JsonValueKind.String || !SupportedMap(map.GetString())))
            throw new RenderFailure("offline_map", "ECharts map must name a packaged local map.");
    }
    public static bool SupportedMap(string? name) => name is "world" or "world-50m" or "africa" or "asia" or "europe" or "north-america" or "south-america" or "oceania" or "usa";
    [GeneratedRegex(@"\A(?:#[0-9a-fA-F]{6}|transparent)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Background();
    [GeneratedRegex(@"(?:javascript\s*:|image\s*://|data\s*:|url\s*\(|@import|<\s*/?\s*(?:script|iframe|img|object|embed)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeResource();
    [GeneratedRegex(@"%%\s*\{|(?:^|[\r\n;])\s*click\s|<\s*/?\s*[a-zA-Z][a-zA-Z0-9]*(?:\s|>|/)|(?:^|[\r\n])\s*---", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MermaidPolicy();
}
