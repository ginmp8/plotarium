using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Ploteus.Domain;

namespace Ploteus.Api.Stateless;

/// <summary>Explicit outbound HTTP policy. The only network access is a single, pinned GET per render.</summary>
public sealed class RemotePayloadOptions
{
    public string[] AllowedOrigins { get; set; } = [];
    // Explicit, opt-in internal origins for trusted deployments. Link-local/metadata is always refused.
    public string[] PrivateOrigins { get; set; } = [];
    public Dictionary<string,string> Providers { get; set; } = new(StringComparer.Ordinal);
    public int TimeoutSeconds { get; set; } = 8;
}

public sealed class RemotePayloadFetcher
{
    private readonly RemotePayloadOptions options;
    private readonly HashSet<string> publicOrigins;
    private readonly HashSet<string> privateOrigins;
    private readonly Dictionary<string,Uri> providers;

    public RemotePayloadFetcher(RemotePayloadOptions options)
    {
        this.options=options;
        if (options.TimeoutSeconds is <1 or >30) throw new InvalidOperationException("Sources timeout must be 1..30 seconds.");
        publicOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        privateOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in options.AllowedOrigins) publicOrigins.Add(ValidateConfiguredOrigin(raw,false));
        foreach (var raw in options.PrivateOrigins) privateOrigins.Add(ValidateConfiguredOrigin(raw,true));
        if (publicOrigins.Overlaps(privateOrigins)) throw new InvalidOperationException("Public and private source allowlists must not overlap.");
        providers=new Dictionary<string,Uri>(StringComparer.Ordinal);
        foreach(var (name,raw) in options.Providers)
        {
            if (string.IsNullOrEmpty(name) || name.Length>64 || name.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))
                throw new InvalidOperationException("Invalid source provider identifier.");
            var url=ValidateInputUrl(raw);
            if(!url.AbsolutePath.EndsWith("/",StringComparison.Ordinal) || !string.IsNullOrEmpty(url.Query) || !OriginAllowed(url))
                throw new InvalidOperationException("Provider base URL must end with / and belong to an allowlisted origin.");
            providers.Add(name,url);
        }
    }

    public bool Enabled => publicOrigins.Count+privateOrigins.Count>0;
    public IReadOnlyCollection<string> ProviderNames => providers.Keys;

    public Uri ResolveProvider(string name,string key)
    {
        if(!providers.TryGetValue(name,out var baseUri))
            throw new RenderFailure("provider_forbidden","Unknown or disabled payload provider.",403);
        if(string.IsNullOrEmpty(key)||key.Length>128||key.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))
            throw new RenderFailure("provider_key","Provider key must contain 1..128 letters, digits, _ or -.",400);
        return new Uri(baseUri,Uri.EscapeDataString(key));
    }

    public async Task<byte[]> GetAsync(string target,CancellationToken token)
    {
        var url=ValidateInputUrl(target);
        var authority=url.GetLeftPart(UriPartial.Authority);
        var usePublic=publicOrigins.Contains(authority);
        if(!usePublic&&!privateOrigins.Contains(authority))
            throw new RenderFailure("source_forbidden","Remote payload origin is not allowed.",403);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var addresses=await Dns.GetHostAddressesAsync(url.DnsSafeHost,deadline.Token);
            if(addresses.Length==0 || addresses.Length>32 || addresses.Any(a=>usePublic?!PublicAddress(a):!PrivateAddress(a)))
                throw new RenderFailure("source_network","DNS returned an address outside the allowed network scope.",403);
            var pinned=addresses.OrderBy(a=>a.AddressFamily==AddressFamily.InterNetwork?0:1).First();
            var handler=new SocketsHttpHandler
            {
                AllowAutoRedirect=false,UseProxy=false,AutomaticDecompression=DecompressionMethods.None,
                ConnectTimeout=TimeSpan.FromSeconds(Math.Min(options.TimeoutSeconds,5)),
                PooledConnectionLifetime=TimeSpan.Zero,
                ConnectCallback=async (context,ct)=>
                {
                    if(!context.DnsEndPoint.Host.Equals(url.IdnHost,StringComparison.OrdinalIgnoreCase) || context.DnsEndPoint.Port!=url.Port)
                        throw new HttpRequestException("Outbound host mismatch.");
                    var socket=new Socket(pinned.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(pinned,url.Port),ct);
                        return new NetworkStream(socket,ownsSocket:true);
                    }
                    catch {socket.Dispose();throw;}
                }
            };
            using var http=new HttpClient(handler,disposeHandler:true) {Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds)};
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if((int)response.StatusCode>=300 && (int)response.StatusCode<400)
                throw new RenderFailure("source_redirect","Remote payload redirects are disabled.",502);
            if(!response.IsSuccessStatusCode)
                throw new RenderFailure("source_status","Payload source returned an unsuccessful response.",502);
            var mime=response.Content.Headers.ContentType?.MediaType;
            if(mime is null || !(mime.Equals("application/json",StringComparison.OrdinalIgnoreCase) || mime.EndsWith("+json",StringComparison.OrdinalIgnoreCase)))
                throw new RenderFailure("source_type","Payload source must return application/json.",502);
            if(response.Content.Headers.ContentEncoding.Count>0)
                throw new RenderFailure("source_encoding","Remote HTTP content encoding is not supported.",502);
            if(response.Content.Headers.ContentLength>Contract.MaxBodyBytes)
                throw new RenderFailure("source_limit","Remote payload is too large.",413);
            await using var body=await response.Content.ReadAsStreamAsync(deadline.Token);
            using var output=new MemoryStream();var buffer=new byte[8192];int n;
            while((n=await body.ReadAsync(buffer,deadline.Token))>0)
            {
                if(output.Length+n>Contract.MaxBodyBytes)
                    throw new RenderFailure("source_limit","Remote payload is too large.",413);
                output.Write(buffer,0,n);
            }
            return output.ToArray();
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested)
        { throw new RenderFailure("source_timeout","Remote payload deadline exceeded.",504); }
        catch(RenderFailure) { throw; }
        catch(Exception ex) when(ex is HttpRequestException or SocketException or IOException)
        { throw new RenderFailure("source_unavailable","Unable to retrieve the remote payload.",502); }
    }

    public Task<byte[]> GetAsync(Uri url,CancellationToken token)=>GetAsync(url.AbsoluteUri,token);

    private static Uri ValidateInputUrl(string raw)
    {
        if(string.IsNullOrWhiteSpace(raw)||raw.Length>2048||!Uri.TryCreate(raw,UriKind.Absolute,out var url)||
           url.Scheme is not ("https" or "http") || url.UserInfo.Length>0 || url.Fragment.Length>0 ||
           url.HostNameType==UriHostNameType.Unknown || url.Host.Contains('%') ||
           url.OriginalString.Any(char.IsControl))
            throw new RenderFailure("source_url","Expected an absolute HTTP(S) URL without credentials or fragment.",400);
        return url;
    }

    private static string ValidateConfiguredOrigin(string raw,bool privateNetwork)
    {
        var url=ValidateInputUrl(raw);
        if(url.AbsolutePath!="/" || url.Query.Length>0 || url.Fragment.Length>0 ||
            (!privateNetwork && url.Scheme!="https"))
            throw new InvalidOperationException("Source origins must be exact HTTPS origins (or explicitly configured private HTTP origins).");
        return url.GetLeftPart(UriPartial.Authority);
    }

    private bool OriginAllowed(Uri url)
    {
        var authority=url.GetLeftPart(UriPartial.Authority);
        return publicOrigins.Contains(authority)||privateOrigins.Contains(authority);
    }

    // Public: reject non-routable, documentation, link-local, reserved, private and cloud metadata ranges.
    private static bool PublicAddress(IPAddress address)
    {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        if(address.AddressFamily==AddressFamily.InterNetwork)
        {
            var b=address.GetAddressBytes();
            if(b[0] is 0 or 10 or 127 || b[0]>=224 || (b[0]==100 && b[1] is >=64 and <=127) ||
                (b[0]==169 && b[1]==254) || (b[0]==172 && b[1] is >=16 and <=31) ||
                (b[0]==192 && (b[1]==168 || b[1]==0)) ||
                (b[0]==198 && (b[1] is 18 or 19 || b[1]==51 && b[2]==100)) ||
                (b[0]==203 && b[1]==0 && b[2]==113)) return false;
            return !(b[0]==192 && b[1]==0 && b[2]==2);
        }
        if(address.AddressFamily!=AddressFamily.InterNetworkV6)return false;
        var v=address.GetAddressBytes();
        if((v[0]&0xe0)!=0x20 || (v[0]==0x20 && v[1]==0x01 && v[2]==0x0d && v[3]==0xb8))return false;
        return !IPAddress.IsLoopback(address) && !address.IsIPv6LinkLocal && !address.IsIPv6Multicast && !address.IsIPv6SiteLocal;
    }

    private static bool PrivateAddress(IPAddress address)
    {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        if(address.AddressFamily==AddressFamily.InterNetwork)
        {
            var b=address.GetAddressBytes();
            return b[0] is 10 or 127 || b[0]==172 && b[1] is >=16 and <=31 || b[0]==192 && b[1]==168;
        }
        if(address.AddressFamily!=AddressFamily.InterNetworkV6)return false;
        var v=address.GetAddressBytes();
        return IPAddress.IsLoopback(address) || (v[0]&0xfe)==0xfc;
    }
}
