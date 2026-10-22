# Plotarium 0.4.0 - Interactive chart delivery

The service now has two distinct delivery paths:

- **Static**: `POST /v1/render` with `format=png|jpeg|webp|svg|pdf|json` (unchanged from 0.2.0).
- **Interactive**: `POST /v1/charts` stores a validated chart in bounded, process-local memory and returns expiring links. `POST /v1/render` with `format=html` returns the same hosted viewer HTML, not a standalone offline file.

Charts are rendered natively in the viewer by bundled ECharts 6.1.0, Plotly 4.1.2, or Mermaid 12.1.0, with no CDN or user-supplied JavaScript. ECharts GL is locally bundled. Plotly MapLibre trace types use the 0.4.0 constrained offline strategies described in release notes; native browser-hosted MapLibre has not been homologated. All API payloads retain the original `spec`/`source` schema. The HTML viewer needs network access to the same Plotarium origin for assets and the ephemeral definition; saving the HTML file by itself does not preserve a self-contained chart.

## Run on Windows

```powershell
$env:Plotarium__Browser__ExecutablePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
dotnet build Plotarium.slnx -c Release
dotnet run --project src/interfaces/Plotarium.Api -c Release
```

Chrome is needed for static image/PDF previews, not for creating a chart or serving an interactive browser viewer. `Plotarium__ApiKey` is not needed or supported. By default, bind only to localhost or a private protected network.

## cURLs for Postman (Import / Raw text)

Tambem ha uma [collection Postman v2.1 com 16 requests](../examples/postman/Plotarium-0.4.0.postman_collection.json), incluindo `chartId` gerado automaticamente pelos requests de criacao. Importe pelo menu **Import > File** no Postman.

Create an ECharts map chart:

```bash
curl --location 'https://localhost:55731/v1/charts' --header 'Content-Type: application/json' --data-raw '{"engine":"echarts","width":1000,"height":650,"spec":{"series":[{"type":"map","map":"world","roam":true,"data":[{"name":"Brazil","value":80},{"name":"India","value":60}]}]}}'
```

Return an HTML viewer directly (HTML references the local Plotarium backend):

```bash
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"plotly","format":"html","spec":{"data":[{"type":"scatter","mode":"lines+markers","x":[1,2,3,4],"y":[2,6,4,10]}]}}'
```

Create a Mermaid diagram:

```bash
curl --location 'https://localhost:55731/v1/charts' --header 'Content-Type: application/json' --data-raw '{"engine":"mermaid","source":"flowchart LR\nA[Inicio] --> B[Processamento] --> C[Fim]"}'
```

For PowerShell/Windows `curl.exe`, avoid JSON escaping issues by piping a literal string into `--data-binary "@-"`:

```powershell
'{"engine":"echarts","spec":{"series":[{"type":"bar","data":[10,20]}]}}' | curl.exe -k -X POST "https://localhost:55731/v1/charts" -H "Content-Type: application/json" --data-binary "@-"
```

All examples are anonymous. `-k` is solely for the local development TLS certificate with curl, not an instruction for a production client.

## New endpoints

| Endpoint | Behavior |
| --- | --- |
| `POST /v1/render` with `format: "html"` | Returns hosted interactive HTML |
| `POST /v1/charts` | Creates expiring chart, returns descriptor, HTTP 201 |
| `GET /v1/charts/{id}/view` | Full viewer with controls |
| `GET /v1/charts/{id}/embed` | Compact iframe viewer |
| `GET /v1/charts/{id}/definition` | Original validated request and capabilities |
| `GET /v1/charts/{id}/preview?format=png` | On-demand static preview; supports PNG, SVG, JPEG, WebP, PDF |
| `GET /v1/charts/{id}/share/teams` | Teams Adaptive Card JSON if HTTPS public base configured |
| `GET /v1/charts/{id}/share/whatsapp` | WhatsApp link/button descriptor if HTTPS public base configured; **not a message sender** |
| `GET /v1/charts/{id}/share/chatgpt` | MCP Apps registration metadata; host registration still required |
| `GET /v1/interactive/capabilities` | Per-engine feature and fallback metadata |
| `GET /v1/viewer/...` and `/v1/assets/...` | Restrictively served packaged viewer and engine assets |
| `POST /mcp` | Stateless JSON response subset of MCP Streamable HTTP: initialize, tools/list, tools/call, resources/list, resources/read, ping |

### Example create response (abbreviated)

```json
{
  "id": "<32-character-random-id>",
  "engine": "echarts",
  "mode": "interactive",
  "viewUrl": "/v1/charts/<id>/view",
  "embedUrl": "/v1/charts/<id>/embed",
  "previewUrl": "/v1/charts/<id>/preview?format=png",
  "publiclyShareable": false
}
```

An id is a bearer capability: anyone with that id can access the chart definition and preview until its expiration. Defaults: 128 simultaneously active charts, 15 minutes TTL, RAM-only; restart clears all chart links. A full active store returns HTTP 429 instead of evicting an active link. Share URLs are *relative* until an explicitly configured trusted HTTPS base is supplied. The server does not infer publicly accessible URLs from the Host header.

## Host channels

**Browser:** normal `viewUrl`; ECharts tooltips, legend and selection; Plotly hover, pan, modebar and 3D rotation when supported; Mermaid viewport-only zoom/pan (no arbitrary click handlers). The outer toolbar offers theme toggle, scale, reset, definition JSON and static PNG preview.

**Teams:** `share/teams` provides a correctly shaped Adaptive Card with preview image and `Action.OpenUrl`; to embed in a Teams tab, configure the tab URL to use `/embed`. Full ECharts/Plotly interactions do not execute inside an ordinary Adaptive Card itself. Host app installation/manifest, permissions and real Teams validation are outside this package.

**WhatsApp:** `share/whatsapp` provides link metadata, preview URL and a button descriptor for a WhatsApp BSP/provider to translate and send. WhatsApp chat messages do not execute arbitrary browser JavaScript; the interactive chart opens via HTTPS link. Provider-specific send actions are intentionally not implemented; they require credentials, consent and rate policy.

**ChatGPT/MCP Apps:** `POST /mcp` exposes `create_interactive_chart` with `_meta.ui.resourceUri = "ui://plotarium/chart.html"` and a matching `resources/read` HTML widget with `text/html;profile=mcp-app`. The widget receives the tool result and presents the viewer as a nested iframe from the trusted HTTPS origin. Registration with a compatible MCP Apps host, host frame policy and real ChatGPT testing are required; this is not equivalent to a preinstalled ChatGPT app. MCP handler supports JSON responses only, not SSE/subscriptions. It is *not* certified for arbitrary MCP transports or authentication requirements of public clients.

## Production exposure

The application intentionally has **no built-in authentication**. Do not expose the server or MCP endpoint unprotected on the public Internet. Use a trusted HTTPS gateway with access control/rate limiting and consider whether anonymous bearer links containing sensitive chart data are permitted at all. Local development settings are not proof of public deployment readiness.

Configure an external HTTPS origin explicitly (never let an arbitrary request Host header define public links):

```json
{
  "Plotarium": {
    "Interactive": {
      "TtlMinutes": 15,
      "MaxCharts": 128,
      "PublicBaseUrl": "https://plotarium.example.org",
      "FrameAncestors": ["'self'", "https://teams.microsoft.com"]
    }
  }
}
```

The gateway must publish the same application and asset paths, enforce HTTPS, and apply provider-specific access policy. `FrameAncestors` is an explicit CSP allowlist; keep `'self'` if internal iframes are used. Browser asset endpoints never accept arbitrary URLs and do not execute chart-supplied scripts.

## Add a new rendering library

The interactive viewer calls a stable `PlotariumAdapters[engine].mount(context)` contract; each bundled adapter returns `resize`, `reset` and `dispose` methods. A new engine can have its own `Interactive/adapters/<engine>.js`, bundled assets and declared capabilities, without changing `viewer.js`. However, adding an engine also requires server registration, schema/security validation, an asset digest update, and (if static previews are needed) implementation of a new static-renderer branch in Chromium. Do not allow clients to supply executable plugin URLs. Extend contract and render tests for each new engine; UI support is not a guarantee of all chart types.

## Validation and limits

A reproducible UI component smoke is included at `tests/Plotarium.UiTests/smoke.py`. It requires optional Python Playwright and a locally installed Chromium; the viewer HTML is loaded with `set_content` and the local scripts and responses are injected as fixtures (not a full network navigation test). Example: `python tests/Plotarium.UiTests/smoke.py --base-url http://localhost:5000 --chromium /usr/bin/chromium`. On Windows, supply your `chrome.exe` path. The script saves sample PNGs to `artifacts/ui`.

Run `dotnet run --project tests/Plotarium.ContractTests -c Release` and `dotnet run --project tests/Plotarium.HttpTests -c Release -- http://localhost:5000/`. The latter needs a running server with Chrome installed and tests the full render-format matrix plus interactive/MCP endpoints. Sandbox root launches may require *explicit development-only* `Plotarium__Browser__DisableSandbox=true`; never use that setting for production. Real Windows, real Teams/WhatsApp/ChatGPT clients and public domain access need external environment validation. See `evidence/0.3.0/` for executed/blocked distinctions.

## 0.4.0 host clients

Os modelos Teams/WhatsApp estão em `examples/integrations/`; o serviço só devolve descritores, não publica/enviar automaticamente. O HTML viewer continua a oferecer interatividade da engine no navegador; MCP Apps ainda depende de homologação no host. Limite o `FrameAncestors` estritamente, habilite autenticação externa e configure `PublicBaseUrl` HTTPS autorizado antes de compartilhar.
