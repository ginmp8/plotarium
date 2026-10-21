using System.Security.Cryptography;
using System.Text.Json;
using Plotarium.Domain;
namespace Plotarium.Infrastructure;

public sealed class AssetCatalog
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
    public bool TryRead(Uri uri, out byte[]? bytes)
    {
        bytes = null;
        if (uri.Scheme != "https" || uri.Host != "plotarium.invalid" || !uri.IsDefaultPort || uri.Query.Length != 0 || uri.UserInfo.Length != 0) return false;
        return files.TryGetValue(uri.AbsolutePath, out bytes);
    }
}
