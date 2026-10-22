using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Plotarium.Domain;
namespace Plotarium.Infrastructure;

public sealed partial class AssetCatalog
{
    private readonly IReadOnlyDictionary<string, byte[]> files;
    public AssetCatalog(string directory)
    {
        var root = Path.GetFullPath(directory);
        var loaded = new Dictionary<string,byte[]>(StringComparer.Ordinal);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"manifest.json")));
        if (manifest.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported asset manifest.");
        foreach(var engine in new[]{"echarts","plotly","mermaid"})
            if (manifest.RootElement.GetProperty("engines").GetProperty(engine).GetString() != Contract.EngineVersion(engine))
                throw new InvalidDataException("Asset engine version mismatch.");
        foreach(var item in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var relative = item.GetProperty("path").GetString()!;
            if (relative.Contains('\\') || relative.Split('/').Any(s=>s is "" or "." or "..") || Path.IsPathRooted(relative))
                throw new InvalidDataException("Unsafe asset path.");
            var path = Path.GetFullPath(Path.Combine(root,relative));
            if (!path.StartsWith(root+Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Asset escaped root.");
            for(var current = new FileInfo(path) as FileSystemInfo; current is not null && current.FullName != root; current = new DirectoryInfo(Path.GetDirectoryName(current.FullName)!))
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Asset links are not allowed.");
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length != item.GetProperty("bytes").GetInt64() || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Asset digest mismatch.");
            if (!loaded.TryAdd("/assets/"+relative, bytes)) throw new InvalidDataException("Duplicate asset path.");
        }
        foreach(var entry in new[]{"/assets/echarts/echarts.min.js","/assets/plotly/plotly.min.js","/assets/mermaid/mermaid.esm.min.mjs"})
            if (!loaded.ContainsKey(entry)) throw new InvalidDataException("Required engine asset is missing.");
        files = loaded;
    }
    public string ReadScript(string path) => files.TryGetValue(path, out var bytes)
        ? Encoding.UTF8.GetString(bytes) : throw new InvalidDataException("Missing verified renderer asset.");

    public string ReadGeoJson(string mapName) => ReadScript("/assets/geojson/" + mapName + ".geojson");

    public IReadOnlyDictionary<string,string> MermaidModules()
    {
        var modules = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var (path, bytes) in files.Where(x=>x.Key.StartsWith("/assets/mermaid/",StringComparison.Ordinal)))
        {
            var source = Encoding.UTF8.GetString(bytes);
            var rewritten = RelativeModuleReference().Replace(source, match => {
                var target = new Uri(new Uri("https://plotarium.invalid" + path),match.Groups["path"].Value).AbsolutePath;
                if(!files.ContainsKey(target) || !target.StartsWith("/assets/mermaid/",StringComparison.Ordinal))
                    throw new InvalidDataException("Unresolved Mermaid module.");
                return match.Groups["quote"].Value + "@plotarium" + target + match.Groups["quote"].Value;
            });
            modules.Add("@plotarium"+path,rewritten);
        }
        return modules;
    }
    [GeneratedRegex("(?<quote>['\\\"])(?<path>\\.{1,2}/[^'\\\"]+\\.mjs)\\k<quote>", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeModuleReference();

    public bool TryReadTopoJson(Uri uri, out byte[]? bytes)
    {
        bytes = null;
        if(!TryRead(uri, out var candidate) || !uri.AbsolutePath.StartsWith("/assets/topojson/", StringComparison.Ordinal) ||
           !uri.AbsolutePath.EndsWith(".json", StringComparison.Ordinal) || candidate is null || candidate.Length > 2_000_000) return false;
        bytes = candidate; return true;
    }

    public bool TryRead(Uri uri, out byte[]? bytes)
    {
        bytes = null;
        if (uri.Scheme != "https" || uri.Host != "plotarium.invalid" || !uri.IsDefaultPort || uri.Query.Length != 0 || uri.UserInfo.Length != 0) return false;
        return files.TryGetValue(uri.AbsolutePath, out bytes);
    }
}
