using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ploteus.Domain;

namespace Ploteus.Application;

/// <summary>Process-local, bounded, expiring, anonymous capability links. IDs are bearer capabilities, not authentication.</summary>
public sealed class ChartStore(int capacity, TimeSpan lifetime, TimeProvider? clock = null)
{
    private readonly ConcurrentDictionary<string, StoredChart> entries = new(StringComparer.Ordinal);
    private readonly object writeGate = new();
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly int maxEntries = capacity is > 0 and <= 1024 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly TimeSpan ttl = lifetime >= TimeSpan.FromSeconds(30) && lifetime <= TimeSpan.FromHours(24)
        ? lifetime : throw new ArgumentOutOfRangeException(nameof(lifetime));

    public StoredChart Create(RenderRequest request)
    {
        lock(writeGate)
        {
            var now = time.GetUtcNow();
            foreach (var (key,item) in entries)
                if (item.ExpiresAt <= now) entries.TryRemove(key, out _);
            if (entries.Count >= maxEntries) throw new RenderFailure("chart_capacity", "The active chart limit was reached.", 429);
            var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            var chart = new StoredChart(id, now, now.Add(ttl), request);
            if (!entries.TryAdd(id,chart)) throw new InvalidOperationException("Chart identifier collision.");
            return chart;
        }
    }

    public StoredChart? Get(string id)
    {
        if (id.Length != 32 || !id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) return null;
        if (!entries.TryGetValue(id,out var chart)) return null;
        if (chart.ExpiresAt > time.GetUtcNow()) return chart;
        // Never delete an updated value (current design is immutable, but retain compare-and-remove semantics).
        ((ICollection<KeyValuePair<string,StoredChart>>)entries).Remove(new KeyValuePair<string,StoredChart>(id,chart));
        return null;
    }
}

public sealed record StoredChart(string Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, RenderRequest Request);

public sealed record InteractiveCapabilities(bool Interactive, bool SupportsPointSelection,
    bool SupportsArbitraryCallbacks, string[] Features, string[] Limitations);

public static class ChartCapabilities
{
    public static InteractiveCapabilities For(string engine) => engine switch
    {
        "echarts" => new(true,true,false,
            ["zoom","pan","tooltip","legend","selection","dataZoom","export","resize","geo-offline","webgl3d","globe"],
            ["no-custom-javascript","gl-requires-webgl","no-external-sources"]),
        "plotly" => new(true,true,false,
            ["zoom","pan","hover","selection","legend","3d","geo-offline","maplibre-geojson-offline","export","resize"],
            ["webgl-host-dependent","geojson-basemap-only-no-street-tiles","no-arbitrary-callbacks"]),
        "mermaid" => new(true,false,false,
            ["viewport-zoom","viewport-pan","export","resize"],
            ["no-diagram-callbacks","no-html-labels","strict-security"]),
        _ => throw new RenderFailure("engine","Unsupported engine.")
    };
}
