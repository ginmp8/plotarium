using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ploteus.Domain;

namespace Ploteus.Application;

/// <summary>Decode GET payloads into the same validated RenderRequest used by POST.</summary>
public static class StatelessPayloadDecoder
{
    public const int MaxEncodedCharacters = 24_576;

    public static RenderRequest FromInline(string payload, string? encoding, IReadOnlyDictionary<string,string> overrides)
    {
        if (string.IsNullOrEmpty(payload)) throw new RenderFailure("query_payload", "Parameter c is required.", 400);
        if (payload.Length > MaxEncodedCharacters) throw new RenderFailure("query_limit", "Encoded chart payload exceeds the GET limit.", 414);
        byte[] bytes;
        if (encoding is null or "json") bytes = Encoding.UTF8.GetBytes(payload);
        else if (encoding == "gzip-base64url") bytes = Inflate(payload);
        else throw new RenderFailure("encoding", "Use encoding=json or gzip-base64url.", 400);

        if (bytes.Length > Contract.MaxBodyBytes) throw new RenderFailure("body_limit", "Decoded payload is too large.", 413);
        RenderRequest request;
        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            var element = doc.RootElement;
            if (element.ValueKind == JsonValueKind.Object && IsEnvelope(element))
                request = RequestValidator.Parse(bytes);
            else if (IsMermaid(overrides))
            {
                if (element.ValueKind != JsonValueKind.String) throw new RenderFailure("query_payload", "Mermaid requires source text.", 400);
                request = new RenderRequest { Engine = "mermaid", Source = element.GetString() };
            }
            else
            {
                if (element.ValueKind != JsonValueKind.Object) throw new RenderFailure("query_payload", "Expected a JSON chart specification.", 400);
                if (!overrides.TryGetValue("engine", out var engine)) throw new RenderFailure("engine", "engine is required for a bare specification.", 400);
                request = new RenderRequest { Engine = engine, Spec = element.Clone() };
            }
        }
        catch (JsonException)
        {
            if (!IsMermaid(overrides)) throw new RenderFailure("json", "Invalid JSON chart payload.", 400);
            request = new RenderRequest { Engine = "mermaid", Source = Encoding.UTF8.GetString(bytes) };
        }
        return ApplyOverrides(request, overrides);
    }

    public static RenderRequest FromRemote(ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string,string> overrides)
    {
        var request = RequestValidator.Parse(payload);
        return ApplyOverrides(request, overrides);
    }

    private static RenderRequest ApplyOverrides(RenderRequest request, IReadOnlyDictionary<string,string> overrides)
    {
        if (overrides.TryGetValue("engine", out var engine) && engine != request.Engine)
            throw new RenderFailure("engine_conflict", "Query engine differs from payload engine.", 400);
        if (overrides.TryGetValue("format", out var format)) request = request with {Format=format};
        if (overrides.TryGetValue("width", out var width)) request = request with {Width=PositiveInteger(width,"width")};
        if (overrides.TryGetValue("height", out var height)) request = request with {Height=PositiveInteger(height,"height")};
        if (overrides.TryGetValue("scale", out var scale)) request = request with {Scale=PositiveInteger(scale,"scale")};
        if (overrides.TryGetValue("theme", out var theme)) request = request with {Theme=theme};
        if (overrides.TryGetValue("background", out var background)) request = request with {Background=background};
        // Serialization retains all native chart properties, including duplicated names; the
        // common RequestValidator enforces the exact same policy as POST.
        return RequestValidator.Parse(JsonSerializer.SerializeToUtf8Bytes(request,Contract.Json));
    }

    private static int PositiveInteger(string value,string name) =>
        int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var parsed) && parsed>0
            ? parsed : throw new RenderFailure("query_parameter",$"{name} must be a positive integer.",400);

    private static bool IsMermaid(IReadOnlyDictionary<string,string> overrides) =>
        overrides.TryGetValue("engine",out var engine) && engine=="mermaid";

    private static bool IsEnvelope(JsonElement obj) =>
        obj.TryGetProperty("engine",out _) || obj.TryGetProperty("spec",out _) ||
        obj.TryGetProperty("source",out _) || obj.TryGetProperty("format",out _) ||
        obj.TryGetProperty("width",out _) || obj.TryGetProperty("height",out _);

    private static byte[] Inflate(string payload)
    {
        if (payload.Length==0 || payload.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new RenderFailure("compressed_payload", "Invalid Base64URL payload.",400);
        var s = payload.Replace('-','+').Replace('_','/');
        if (s.Length%4 == 1) throw new RenderFailure("compressed_payload", "Invalid Base64URL length.",400);
        s = s.PadRight((s.Length+3)/4*4,'=');
        byte[] compressed;
        try {compressed=Convert.FromBase64String(s);}
        catch(FormatException) {throw new RenderFailure("compressed_payload","Invalid Base64URL payload.",400);}
        try
        {
            using var input = new MemoryStream(compressed,false);
            using var unzip = new GZipStream(input,CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];int n;
            while ((n=unzip.Read(buffer))>0)
            {
                if (output.Length+n>Contract.MaxBodyBytes)
                    throw new RenderFailure("body_limit", "Decompressed payload exceeds the maximum size.",413);
                output.Write(buffer,0,n);
            }
            return output.ToArray();
        }
        catch(InvalidDataException) {throw new RenderFailure("compressed_payload", "Invalid gzip stream.",400);}
        catch(IOException) {throw new RenderFailure("compressed_payload", "Invalid gzip stream.",400);}
    }
}
