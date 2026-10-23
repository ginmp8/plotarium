# Ploteus 0.6.0

**Stateless Visual Rendering Engine** — API ASP.NET Core 10 that transforms ECharts 6, ECharts GL 2.1, Plotly.js 4 and Mermaid into PNG, JPEG, WebP, SVG, PDF, JSON envelopes and HTML interactive views.

The name **Ploteus** evokes Proteus, a mythological figure capable of changing shape: one rendering contract, multiple visual representations. The source tree, namespaces, assemblies, package metadata, UI, configuration, MCP resource identifiers and telemetry use the same product identity.

## Stateless rendering: one client request

| Route | Input | Result |
|---|---|---|
| `POST /v1/render` | Complete JSON `RenderRequest` | Direct chart artifact; no stored definition |
| `GET /v1/render?c=...` | URL-encoded JSON envelope or engine specification | Direct chart artifact |
| `GET /v1/render?c=...&encoding=gzip-base64url` | Gzip-compressed UTF-8 JSON as Base64URL | Direct chart artifact |
| `GET /v1/render?sourceUrl=...` | Operator-allowlisted HTTP(S) API serving POST-compatible JSON | Server fetches payload and returns chart |
| `GET /v1/render?provider=...&key=...` | Preconfigured provider and bounded key | Server fetches payload and returns chart |

The GET source modes use no chart registration, storage, Redis or database. Any server-side remote payload retrieval is internal to the single client call. To render inline JSON, use `c=` with URL encoding; large requests should use the stateless POST endpoint. Format selection supports `format=html` (standalone HTML with embedded JS/CSS/assets), `png`, `svg`, `pdf`, `jpeg`, `webp` or `json` (normalized request envelope). Binary formats require local Chromium; JSON does not.

See [GET contracts and limits](docs/stateless-get.md), [HTTP API contract](docs/api-contract.md), [OpenAPI 3.1](docs/openapi.json), [Postman collection](examples/postman/Ploteus-0.6.0.postman_collection.json), and [current release notes](docs/release-notes-0.6.0.md).

## Build and run (.NET 10)

The Chromium/Chrome executable is supplied by the operator. For Linux:

```bash
export Ploteus__Browser__ExecutablePath=/usr/bin/chromium
dotnet restore Ploteus.slnx -p:Platform='Any CPU'
dotnet build Ploteus.slnx -c Release -p:Platform='Any CPU' --no-restore
dotnet run --project src/interfaces/Ploteus.Api -c Release
```

For Windows PowerShell:

```powershell
$env:Ploteus__Browser__ExecutablePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
dotnet restore Ploteus.slnx
dotnet build Ploteus.slnx -c Release
dotnet run --project src/interfaces/Ploteus.Api -c Release
```

**Example: one-call stateless POST (SVG)**

```bash
curl -X POST http://localhost:5000/v1/render -H 'Content-Type: application/json' \
  -d '{"engine":"echarts","format":"svg","spec":{"xAxis":{"type":"category","data":["A","B"]},"yAxis":{"type":"value"},"series":[{"type":"bar","data":[1,2]}]}}' \
  -o example.svg
```

**Example: one-call GET (PNG)**

```bash
curl -G http://localhost:5000/v1/render --data-urlencode 'engine=echarts' \
  --data-urlencode 'format=png' \
  --data-urlencode 'c={"xAxis":{"type":"category","data":["A","B"]},"yAxis":{"type":"value"},"series":[{"type":"bar","data":[1,2]}]}' \
  -o example.png
```

**Remote data sources (opt-in):** set `Ploteus__Sources__AllowedOrigins__0=https://charts.example.com`, or configure a provider through `Ploteus__Sources__Providers__reports=https://charts.example.com/definitions/`. Remote retrieval is disabled by default, rejects redirects and unsafe IPs, and has strict MIME, timeout and body-size limits. Test-only loopback allowances are **not** for production. The API intentionally has no application-level authentication: deploy only on a trusted network or behind an access-controlled gateway. Secrets and private data do not belong in the GET URL.

## Other supported interfaces

- `GET /v1/capabilities`, `GET /health/live`, `GET /health/ready`.
- `POST /v1/charts` plus `/v1/charts/{id}/view`, `/embed`, `/preview`, `/definition`, `/share/{channel}` remain available as **optional, temporary in-memory** interactive workflows. They are separate from stateless rendering.
- `POST /mcp` exposes the bounded MCP protocol subset with `ui://ploteus/chart.html` as its chart resource identifier (full official SDK and host certification are outside the tested scope).
- ECharts GL, Plotly geographic fallbacks, and offline map assets are packaged. Native MapLibre workers require compatible browsers and are not certified across all environments.
- Teams/WhatsApp responses are descriptors/examples, not proof of a live messaging provider integration.

See [architecture](docs/architecture.md), [security](docs/security.md), [operational guidance](docs/operations.md), [supported chart types](docs/chart-compatibility.md), [integration guide](docs/interactive.md), [licenses](THIRD-PARTY-NOTICES.md), and [observed validation](docs/validation.md).

## Validation

```bash
dotnet tests/Ploteus.ContractTests/bin/Release/net10.0/Ploteus.ContractTests.dll
python3 scripts/run_http_regression.py
python3 scripts/stateless_smoke.py /absolute/path/to/Ploteus.Api.dll
python3 scripts/stateless_ui_smoke.py
```

The executable GET and browser tests run against a locally configured development server and use only the operator-approved local fixture origins. Binary preview tests need Chromium and a compatible sandbox. See the release evidence for actual executed results and limitations.
