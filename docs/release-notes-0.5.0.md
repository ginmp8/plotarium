# Plotarium 0.5.0 - stateless GET rendering

## Added

- `GET /v1/render?c=<URL-encoded JSON>&engine=echarts&format=png` accepts a native engine specification, or a complete POST-compatible `RenderRequest` envelope.
- `GET /v1/render?c=<Gzip+Base64URL>&encoding=gzip-base64url` accepts compressed requests in a single client call. Query values such as `format`, `width`, `height`, `theme`, `scale` override output preferences; an existing payload engine cannot be changed by query.
- `GET /v1/render?sourceUrl=<URL-encoded absolute URL>` retrieves a **complete** POST-compatible JSON envelope from an explicitly allowlisted origin, then validates and renders it. One client GET, one bounded server-to-source GET. No cache/database is created in Plotarium.
- `GET /v1/render?provider=reports&key=monthly` uses an operator-configured URL prefix; the key is a single safe path segment.
- `GET ...&format=html` generates a one-response standalone chart page: ECharts/Plotly JavaScript is inlined from verified local assets, Mermaid uses a local SVG preview, and ECharts GL uses a server-rendered PNG fallback when WebGL is unavailable. No backend chart ID is stored.
- Request-line limit of 32 KiB at Kestrel; effective max GET query or `c` length is 24,576 bytes/characters. Decoded input is bounded by the existing 1 MiB limit.

## Security defaults

- Remote source fetching **disabled by default** (`Sources.AllowedOrigins=[]`, `Sources.PrivateOrigins=[]`), with per-origin exact matching.
- Public sources must use HTTPS and resolve to public routable IPs. Explicit private sources may use operator-approved HTTP(S) origins but remain pinned to validated private addresses. Metadata/link-local ranges are never eligible for the private-source exception.
- DNS is resolved once, all returned addresses validated, and the underlying socket connects directly to one validated IP; TLS still verifies the original hostname. Proxy use and redirects are disabled. Remote responses must be 2xx JSON, without compressed HTTP Content-Encoding, and below 1 MiB. Timeout defaults to 8 seconds and is configurable (1..30).
- Input is still inspected with `RequestValidator`: no callbacks, external browser resources or unsupported specs.
- GET responses use `Cache-Control: no-store` and `Referrer-Policy: no-referrer`. Application-access logs for raw GET query strings are suppressed, but upstream gateways and clients can still log URLs. Never place secrets or sensitive chart data in `c`/`sourceUrl`.

## Compatibility and limitations

- `POST /v1/render` and the existing temporary chart/MCP endpoints remain unchanged for compatibility. The **new GET flows never use ChartStore**. The legacy POST `format=html` is not standalone; GET `format=html` is standalone.
- The server still requires a configured, operator-supplied Chromium for binary renders and Mermaid SVG / GL fallback. Clients with WebGL may use native ECharts GL interactions; browsers without WebGL see a static 3D image that supports zoom controls, not 3D rotation.
- Browser workers needed by some MapLibre traces or Plotly geographic data cannot be guaranteed to work in strictly offline standalone HTML. Static geographic output uses the previously validated Plotarium rendering fallback where applicable.
- Image/PDF/JSON generation is synchronous. Remote source payloads are fetched on every GET; this version does not cache external responses or final artifacts.
- Teams/WhatsApp/ChatGPT account-level and SDK integrations are not certified by these GET tests.
- No authentication is added. Use a trusted network boundary and an auth gateway when required.

## Validation (see evidence/0.5.0)

- 113/113 contract tests (10 new GET decoder tests).
- 35/35 existing HTTP regression cases.
- 26/26 new stateless GET HTTP cases (including local-source provider, SSRF rejections, redirects, invalid MIME, timeout, decompression limit, PNG and HTML).
- 4/4 Chromium browser HTML checks, with CSP injected as a meta policy. ECharts, Plotly, Mermaid and ECharts GL with fallback were exercised; no additional network request observed.
- Build and publish receipts and release content SHA-256 are included in `evidence/0.5.0`.
