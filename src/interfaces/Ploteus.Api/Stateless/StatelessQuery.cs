using System.Text;
using Microsoft.Extensions.Primitives;
using Ploteus.Application;
using Ploteus.Domain;

namespace Ploteus.Api.Stateless;

/// <summary>GET transport only; all chart rules are enforced by the shared POST validator.</summary>
public static class StatelessQuery
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {"c","encoding","sourceUrl","provider","key","engine","format","width","height","scale","theme","background"};

    public static async Task<RenderRequest> ResolveAsync(HttpContext context,RemotePayloadFetcher fetcher)
    {
        var query=context.Request.Query;
        if(Encoding.UTF8.GetByteCount(context.Request.QueryString.Value??"")>StatelessPayloadDecoder.MaxEncodedCharacters)
            throw new RenderFailure("query_limit","GET query is too long.",414);
        var parameters=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var (key,values) in query)
        {
            if(!Allowed.Contains(key)||values.Count!=1||string.IsNullOrEmpty(values[0]))
                throw new RenderFailure("query_parameter","Unknown, empty or repeated query parameter.",400);
            parameters.Add(key,values[0]!);
        }
        var inline=parameters.ContainsKey("c");
        var remote=parameters.ContainsKey("sourceUrl");
        var provider=parameters.ContainsKey("provider")||parameters.ContainsKey("key");
        if((inline?1:0)+(remote?1:0)+(provider?1:0)!=1)
            throw new RenderFailure("query_payload","Supply exactly one of c, sourceUrl or provider+key.",400);
        if(provider && (!parameters.ContainsKey("provider")||!parameters.ContainsKey("key")))
            throw new RenderFailure("provider_key","Both provider and key are required.",400);
        if(!inline&&parameters.ContainsKey("encoding"))
            throw new RenderFailure("encoding","encoding applies only to parameter c.",400);
        var output=parameters.Where(kv=>kv.Key is "engine" or "format" or "width" or "height" or "scale" or "theme" or "background")
            .ToDictionary(kv=>kv.Key,kv=>kv.Value,StringComparer.Ordinal);
        if(inline) return StatelessPayloadDecoder.FromInline(parameters["c"],parameters.GetValueOrDefault("encoding"),output);
        var target=remote?parameters["sourceUrl"]:fetcher.ResolveProvider(parameters["provider"],parameters["key"]).AbsoluteUri;
        var bytes=await fetcher.GetAsync(target,context.RequestAborted);
        return StatelessPayloadDecoder.FromRemote(bytes,output);
    }
}
