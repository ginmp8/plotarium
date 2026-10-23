# One-call, stateless GET transport (Plotarium 0.5.0)

All three GET modes produce the same validated `RenderRequest` used by `POST /v1/render`. No charts, remote JSON or image bytes are stored by these GET endpoints. Each request is independently renderable on any Plotarium instance with the same packaged assets and operator configuration.

## Endpoints

| Form | Query | Payload shape | Default enabled |
|---|---|---|---|
| Inline JSON | `GET /v1/render?c=...` | Bare ECharts/Plotly spec **plus** `engine`, full POST envelope, or Mermaid source (`engine=mermaid`) | Yes |
| Compressed JSON | `GET /v1/render?c=...&encoding=gzip-base64url` | Same as above, compressed with Gzip and Base64URL without padding | Yes |
| Remote URL | `GET /v1/render?sourceUrl=...` | Complete POST `RenderRequest` JSON from the origin | Only allowed origins |
| Named source | `GET /v1/render?provider=reports&key=monthly` | Complete POST `RenderRequest` JSON from configured base URL + safe key | Only configured providers |
| Existing POST | `POST /v1/render` | Complete POST `RenderRequest` JSON in the body | Yes |

These operations always return the requested representation directly, not a JSON URL or a job ID. Formats: `png`, `jpeg`, `webp`, `svg`, `pdf`, `json`, `html`. Use `format=json` to validate/debug without launching the browser. `format=html` on **GET** inlines executable assets and the definition into a portable document; for Mermaid, it embeds a validated SVG image. Browser interactivity depends on engine and client capabilities.

## Parameter policy

- Exactly one input mode (`c`, `sourceUrl`, or `provider` together with `key`). No ambiguous combinations, unknown parameters, duplicates or empty values.
- `engine`, `format`, `width`, `height`, `scale`, `theme`, `background` are optional overrides. `engine` can only be set for a bare inline spec or used to verify the engine already declared by a full envelope.
- `encoding` only applies to `c`: omit or choose `json` for URL-encoded raw text; choose `gzip-base64url` for compressed input.
- Upstream JSON must be a full envelope: `{ "engine":"echarts", "format":"png", "spec":{...} }` or Mermaid `{ "engine":"mermaid", "source":"flowchart LR\\nA-->B" }`.
- Compression does not enable unbounded URLs. Maximum query string: 24,576 bytes; maximum encoded `c`: 24,576 characters; maximum decoded JSON: 1,048,576 bytes. Server request-line limit: 32 KiB. Reverse proxies may impose smaller limits.
- Query `format` overrides the payload output format. `Content-Type` matches the result; images are returned inline, without an attachment-only disposition.

## Quick examples

### PowerShell - URL-encoded JSON

```powershell
curl.exe -G "https://localhost:55731/v1/render" `
  --data-urlencode "engine=echarts" `
  --data-urlencode "format=png" `
  --data-urlencode 'c={"xAxis":{"type":"category","data":["Jan","Fev"]},"yAxis":{"type":"value"},"series":[{"type":"bar","data":[5,8]}]}' `
  -o chart.png
```

### Python stdlib - deterministic Gzip+Base64URL

```python
import base64, gzip, json, urllib.parse
payload = {"engine":"echarts", "spec":{"series":[{"type":"pie", "data":[{"value":4,"name":"A"},{"value":6,"name":"B"}]}]}}
raw = json.dumps(payload, separators=(',',':')).encode('utf-8')
c = base64.urlsafe_b64encode(gzip.compress(raw, mtime=0)).decode('ascii').rstrip('=')
url = 'https://localhost:55731/v1/render?' + urllib.parse.urlencode({'encoding':'gzip-base64url','c':c,'format':'svg'})
print(url)  # GET this URL once, without a preceding POST.
```

### External JSON (one client request)

```powershell
curl.exe -G "https://localhost:55731/v1/render" `
  --data-urlencode "sourceUrl=https://charts.example.com/definitions/monthly" `
  --data-urlencode "format=pdf" -o report.pdf
```

The origin must be enabled in the operator configuration. The server fetches this URL once, validates its JSON and returns a PDF to the original caller. This is **not an HTTP redirect or a second client call**.

### Configured provider (safer for production)

```powershell
curl.exe -G "https://localhost:55731/v1/render" `
  --data-urlencode "provider=reports" `
  --data-urlencode "key=monthly" `
  --data-urlencode "format=png" -o report.png
```

`provider=reports` maps to an administrator-defined base URL. The `key` must consist of 1..128 ASCII letters, digits, `_` or `-`; no slashes, `..` or queries.

## Trusted-source configuration

`appsettings.json` uses safe defaults:

```json
{
  "Plotarium": {
    "Sources": {
      "TimeoutSeconds": 8,
      "AllowedOrigins": [],
      "PrivateOrigins": [],
      "Providers": {}
    }
  }
}
```

Example for an **operator-owned HTTPS** payload service:

```json
{
  "Plotarium": {
    "Sources": {
      "AllowedOrigins": ["https://charts.example.com"],
      "Providers": {"reports": "https://charts.example.com/definitions/"},
      "TimeoutSeconds": 8
    }
  }
}
```

Environment form: `Plotarium__Sources__AllowedOrigins__0=https://charts.example.com` and `Plotarium__Sources__Providers__reports=https://charts.example.com/definitions/`. Do not include the payload in server configuration.

Only for an isolated trusted development network, you can explicitly allow a private source such as `Plotarium__Sources__PrivateOrigins__0=http://127.0.0.1:6000`. Do not expose that permission to untrusted clients. Private origin configuration must be reviewed together with egress firewall rules.

## Threat boundaries and expected errors

| Condition | HTTP |
|---|---|
| Unknown or conflicting query parameters, malformed JSON/Base64URL | 400 |
| Unlisted source origin or unsafe DNS scope | 403 |
| Unsupported chart specification or output mode | 422 |
| Invalid provider key | 400 |
| URL too long | 414 |
| Source body too large / decompression limit | 413 |
| Remote redirect, non-JSON MIME, HTTP source failure | 502 |
| Source timeout | 504 |
| Browser missing / not available for PNG/PDF | 503 |

URLs containing `c` or `sourceUrl` may be retained by reverse proxies, analytics, browser history and clients despite the application suppressing raw ASP.NET Core request logs. Do not transport confidential payloads or credentials by GET unless the whole deployment has been assessed; prefer POST for secret-bearing data. Disallow arbitrary external HTML/JavaScript; the remote payload is JSON only and still passes the strict Plotarium validator. No redirects, HTTP proxies or second DNS resolution are permitted by the outbound handler.
