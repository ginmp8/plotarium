using System.Text.Json;
using System.Text.Json.Serialization;
namespace Plotarium.Domain;
public sealed record RenderRequest
{
    public string Engine { get; init; } = "";
    public string Format { get; init; } = "png";
    public int Width { get; init; } = 800;
    public int Height { get; init; } = 450;
    public int Scale { get; init; } = 1;
    public string Theme { get; init; } = "light";
    public string Background { get; init; } = "#ffffff";
    public JsonElement? Spec { get; init; }
    public string? Source { get; init; }
}
public sealed record RenderArtifact(byte[] Bytes, string ContentType, string Extension);
public sealed record Definition(string SchemaVersion, string Normalization, string Validation,
    string EngineVersion, RenderRequest Request);
public sealed class RenderFailure(string code, string message, int status = 422) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public static class Contract
{
    public const int MaxBodyBytes = 1_048_576;
    public const int MaxOutputBytes = 16_777_216;
    public const long MaxPixels = 16_777_216;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public static string EngineVersion(string engine) => engine switch
    {
        "echarts" => "6.1.0", "plotly" => "4.1.2", "mermaid" => "12.1.0",
        _ => throw new RenderFailure("engine", "Unsupported engine.")
    };
    public static (string Mime, string Extension) Format(string format) => format switch
    {
        "png" => ("image/png", "png"), "jpeg" => ("image/jpeg", "jpg"),
        "webp" => ("image/webp", "webp"), "svg" => ("image/svg+xml", "svg"),
        "json" => ("application/json", "json"), "pdf" => ("application/pdf", "pdf"),
        "html" => ("text/html; charset=utf-8", "html"),
        _ => throw new RenderFailure("format", "Unsupported format.")
    };
}
