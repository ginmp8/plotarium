# Ploteus 0.6.0 — unified product identity

## Scope

The 0.6.0 release establishes **Ploteus** as the sole product identity across the solution, without dropping supported 0.5.0 rendering features. The renaming covers solution and project files, C# namespaces and assemblies, embedded JavaScript and resource identifiers, runtime configuration section and environment-variable prefix, telemetry, HTML UI, MCP UI URI, examples, OpenAPI, Postman and documentation.

## Behavior retained

- Single-request GET/POST stateless rendering with JSON, Gzip/Base64URL, external allowlisted `sourceUrl` and configured `provider` modes.
- ECharts/ECharts GL, Plotly/geo, Mermaid, PNG/JPEG/WebP/SVG/PDF/JSON and standalone HTML as supported by the runtime and local assets.
- Separate temporary in-memory interactive chart viewer, previews, channel descriptors and bounded MCP Streamable HTTP subset.
- SSRF protections for remote payload fetching: explicit origins, DNS/IP checks, redirects denied, bounded body size, MIME and timeout requirements.
- Browser sandbox/CSP checks, chart input validation, resource-size limits and bounded render concurrency.

## Product identifiers

- Solution `Ploteus.slnx`; `Ploteus.Domain`, `Ploteus.Application`, `Ploteus.Infrastructure`, `Ploteus.Api` assemblies and namespaces; `Ploteus` configuration root, `Ploteus__` environment variables, `ploteus.render.*` metrics, and `ui://ploteus/chart.html` MCP resource URI.
- Routes and request/response format envelopes remain unchanged. Clients using a previously registered MCP UI resource or previously configured environment variable name must register/configure the **new Ploteus identity**. No compatibility alias with another brand ships in this release.

## Verification

The executable receipts for this release live in `evidence/0.6.0`. Historical runtime-evidence snapshots are excluded from the distribution because editing their original bytes after a brand change would invalidate their content identity. This has no impact on production code, examples, source assets, or executable test harnesses. The release manifest and SHA-256 bind the exact distributed bytes.

## Limitations

This release changes product identity rather than broadening external platform certification. Public production deployment, native MapLibre worker behavior, live Teams/WhatsApp/ChatGPT app integration and full official MCP SDK restore are not established by local regression results.
