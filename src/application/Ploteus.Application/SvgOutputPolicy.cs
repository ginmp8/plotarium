using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Ploteus.Domain;
namespace Ploteus.Application;

// A restrictive output profile, not a general-purpose HTML sanitizer.
public static partial class SvgOutputPolicy
{
    public static void Validate(byte[] bytes)
    {
        if(bytes.Length > Contract.MaxOutputBytes) throw new RenderFailure("svg_limit", "SVG is too large.", 413);
        try
        {
            using var stream = new MemoryStream(bytes, false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = Contract.MaxOutputBytes });
            var doc = XDocument.Load(reader);
            if (doc.Root?.Name != XName.Get("svg", "http://www.w3.org/2000/svg")) Deny();
            if (doc.DescendantNodes().OfType<XProcessingInstruction>().Any()) Deny();
            foreach(var e in doc.Descendants())
            {
                if (e.Name.NamespaceName != "http://www.w3.org/2000/svg" || e.Name.LocalName.ToLowerInvariant() is
                    "script" or "foreignobject" or "iframe" or "object" or "embed" or "a" or "animate" or "set" or "animatetransform") Deny();
                foreach(var a in e.Attributes())
                {
                    var name = a.Name.LocalName.ToLowerInvariant();
                    if (name.StartsWith("on", StringComparison.Ordinal) || a.Name == XNamespace.Xml + "base") Deny();
                    if (name is "href" or "src")
                    {
                        var value = a.Value.Trim();
                        if (!value.StartsWith('#') && !(e.Name.LocalName == "image" && EmbeddedRaster().IsMatch(value))) Deny();
                    }
                    if (!a.IsNamespaceDeclaration && name is not ("href" or "src")) CheckCss(a.Value);
                }
                if (e.Name.LocalName == "style") CheckCss(e.Value);
            }
        }
        catch(XmlException) { Deny(); }
    }
    private static void CheckCss(string value)
    {
        if (value.Contains('\\') || value.Contains("@import", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) || value.Contains("expression(", StringComparison.OrdinalIgnoreCase)) Deny();
        // Paint-server references within the same SVG are allowed; external CSS resources are not.
        var remaining = LocalUrl().Replace(value, "");
        if (ExternalUrl().IsMatch(remaining)) Deny();
    }
    private static void Deny() => throw new RenderFailure("svg_policy", "SVG is outside the supported inert output profile.");
    [GeneratedRegex(@"\Adata:image/(?:png|jpeg|webp);base64,[A-Za-z0-9+/=]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedRaster();
    [GeneratedRegex("url\\(\\s*['\"]?#[A-Za-z0-9_.:-]+['\"]?\\s*\\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalUrl();
    [GeneratedRegex(@"url\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalUrl();
}
