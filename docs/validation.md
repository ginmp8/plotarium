# Validação executada — Plotarium 0.2.0

**Escopo:** verificação local Linux/Chromium de compatibilidade HTTP, renderização offline de mapas, saída WebGL e comportamento negativo. Não equivale a homologação universal dos tipos de gráficos nem a teste em Windows/produção.

## Comandos e resultados observados

| Gate | Resultado | Evidência |
|---|---|---|
| `dotnet restore Plotarium.slnx -p:Platform="Any CPU" --ignore-failed-sources` | aprovado, sem pacotes NuGet externos | `evidence/0.2.0/restore.log` |
| `dotnet build Plotarium.slnx -p:Platform="Any CPU" -c Release --no-restore` | **0 errors, 0 warnings** | `evidence/0.2.0/build.log` |
| `dotnet tests/Plotarium.ContractTests/bin/Release/net10.0/Plotarium.ContractTests.dll` | **90 aprovados, 0 falhas** | `evidence/0.2.0/contract-tests.json` |
| `dotnet tests/Plotarium.HttpTests/bin/Release/net10.0/Plotarium.HttpTests.dll http://127.0.0.1:55820/` | **24 aprovados, 0 falhas** | `evidence/0.2.0/http-tests.json` |
| `python3 scripts/geo_format_smoke.py http://127.0.0.1:55820` | **6 aprovados, 0 falhas**, PNG/SVG/PDF | `evidence/0.2.0/geo-format-results.json` |
| `python3 scripts/coverage_smoke.py http://127.0.0.1:55820` | **66 SVG válidos + 3 rejeições esperadas** (`offline_maplibre`); 0 falhas inesperadas | `evidence/0.2.0/coverage-results.json` |

O harness HTTP verifica 18 combinações de 3 engines × 6 formatos com MIME e assinatura válidos, mais autenticação anônima, erros negativos e rota correta. `format=json` é um envelope validado estruturalmente e não uma imagem renderizada. Não confundir retornos HTTP 200 com garantia de integridade visual.

## Revisão de saídas visuais

- ECharts `world` em PNG: contornos de países, América/Ásia e valores por país visíveis.
- Plotly `choropleth` em PNG: fronteiras e cores por países visíveis.
- Plotly `scatter3d` em PNG: eixos e pontos 3D visíveis após usar `Plotly.toImage` em vez de screenshot branco.
- Plotly WebGL `format=svg` incorpora PNG raster em `<image href="data:image/png;base64,...">`; **não é SVG vetorial**. Arquivo continua autocontido e passa pela política de XML.
- PDFs gerados são verificados quanto à assinatura/formato; esta validação não prova fidelidade cartográfica ou equivalência visual com navegador desktop.

## Limites e ambiente de teste

- SDK .NET 10.0.302, Chromium 144.0.7559.96, Linux x64.
- Renderizador sem Node.js, sem PuppeteerSharp, sem NuGet externos; usa Chromium instalado pelo operador e assets versionados com SHA-256.
- `Plotarium__Browser__DisableSandbox=true` e `Plotarium__Browser__EnableSoftwareWebGl=true` foram usados **somente em Development**, dentro do contêiner restrito; sandbox de produção não foi homologado.
- MapLibre (`scattermap`, `choroplethmap`, `densitymap`) retorna HTTP 422 `offline_maplibre` por decisão explícita. ECharts GL e callbacks JavaScript arbitrários não estão disponíveis.
- Não testados: Windows/macOS, segurança de serviço anônimo exposto à internet, desempenho sob carga de produção, GPU em todas as máquinas, todos os specs/plugins/layouts, correção pixel a pixel, provedores de mapas remotos.

O histórico `evidence/` da 0.1.0-alpha.1 preserva limitações e receipts daquela implementação e **não** é evidência executada nesta release. Os recibos autoritativos da 0.2.0 estão em `evidence/0.2.0/`.


## Plotarium 0.3.0

- Linux .NET 10 Release build: **PASS**, 0 errors/warnings.
- ContractTests: **95/95 PASS**.
- HttpTests on a nonroot server with sandboxed Chromium: **35/35 PASS**, including the original 18 engine/format responses and new interactive/MCP tests.
- UI component tests using real ECharts/Plotly JS in Chromium and fixed local fixtures: **3/3 PASS** (including world map, theme/zoom/reset); no JavaScript errors.
- OpenAPI JSON 3.1 parses; JavaScript syntax checks for viewer and engine adapters pass.
- `GET /v1/charts/{id}/view` and `format:html` return hosted HTML; they are not self-contained offline HTML downloads.
- **Not tested:** Windows, public-host registration, actual Teams/WhatsApp/ChatGPT host interaction, and the Mermaid ESM interactive viewer in a browser on a fully navigable origin. The managed Chromium policy prohibited localhost navigation in the test environment; only component tests circumvented the need for navigating, without modifying policy.
- See `evidence/0.3.0/validation.json` for executable evidence and hashes.
