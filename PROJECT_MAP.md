# Ploteus 0.6.0 - Mapa do projeto

- `src/domain/Ploteus.Domain`: `RenderRequest`, tipos, formatos e limites.
- `src/application/Ploteus.Application`: `RequestValidator`, `RenderService`, fila limitada e politica de XML/SVG.
- `src/infrastructure/Ploteus.Infrastructure`: integridade dos assets, processo Chromium, CDP/WebSocket, carregamento local, render ECharts/Plotly/Mermaid e exportacao nos formatos de saida.
- `src/interfaces/Ploteus.Api`: endpoints, configuracao, headers defensivos, ProblemDetails e metricas (sem autenticacao).
- `tests/Ploteus.ContractTests`: regras de entrada, limites, outputs JSON e hash dos assets.
- `tests/Ploteus.HttpTests`: endpoints publicos e matriz 3 motores x 6 formatos contra um processo real.
- `assets/`: bibliotecas JavaScript versionadas e manifesto de integridade; `docs/research` e `evidence/0.6.0`: pesquisa de referência e validação atual; `examples/`: corpos HTTP exemplificados.

Contrato base POST /v1/render; `spec` ECharts/Plotly; `source` Mermaid. Nao sao necessarios headers de autenticacao. API requer navegador para imagem; JSON funciona sem navegador. `docs/security.md` enumera riscos de servir clientes anonimos.

## 0.2.0

- `assets/geojson/` e `assets/topojson/`: mapas locais com proveniência e hashes, `scripts/generate_geodata.py` para conversão reproduzível.
- `src/infrastructure/.../render-bridge.js`: registro de mapas ECharts e exportação Plotly PNG para preservar WebGL em SVG raster-in-SVG.
- `src/infrastructure/.../ChromiumRenderer.cs`: carrega mapas exigidos e intercepta **apenas** GET aos TopoJSON verificados; requests externos permanecem proibidos.
- `tests/...` e `scripts/coverage_smoke.py`: contrato, 3×6 formatos HTTP e matriz de tipos com exclusões explícitas. Casos completos em `examples/coverage/`.
- `docs/chart-compatibility.md` identifica suporte e limites, `docs/geographic-data.md` documenta a fonte dos polígonos. Sem autenticação.


## 0.3.0 - Interactive delivery

- `src/application/Ploteus.Application/InteractiveCharts.cs`: `ChartStore` de links criptograficamente aleatorios, expiracao e limite de capacidade, com capacidades declaradas por engine. Sem persistencia de graficos em disco ou banco.
- `src/interfaces/Ploteus.Api/Interactive/InteractiveRoutes.cs`: `POST /v1/charts`, visualizador, embed, preview e descritores Teams/WhatsApp/ChatGPT. O uso externo requer origem HTTPS configurada e gateway protegido.
- `src/interfaces/Ploteus.Api/Interactive/viewer.js`, `viewer.css` e `adapters/*.js`: HTML servido pelo backend, com engines locais e sem scripts arbitrarios do usuario. Novas libs precisam ser aprovadas/registradas no servidor antes de incluir um adaptador.
- `src/interfaces/Ploteus.Api/Interactive/McpEndpoints.cs`: subconjunto JSON-RPC/MCP Streamable HTTP com ferramenta de criacao e recurso de interface `ui://ploteus/chart.html`; sem certificacao de cliente ChatGPT real nem SSE.
- `docs/interactive.md`, `docs/release-notes-0.3.0.md`: contratos, importacao de cURL pelo Postman, integracoes e limites operacionais.
- `tests/Ploteus.ContractTests` e `tests/Ploteus.HttpTests`: protecao do contrato original e novas rotas, recursos e MCP.


## 0.6.0 - Identidade e API stateless

- `src/interfaces/Ploteus.Api/Stateless/*`: consultas GET com JSON, Gzip/Base64URL, origem remota segura, provider configurado, HTML autocontido.
- `src/application/Ploteus.Application/StatelessPayloadDecoder.cs`: normalização com o mesmo validador do POST.
- `src/interfaces/Ploteus.Api/appsettings.json`: seção raiz `Ploteus`; telemetria `ploteus.render.*`.
- `scripts/stateless_smoke.py`, `scripts/stateless_ui_smoke.py`, `scripts/run_http_regression.py`, `scripts/feature_gl_smoke.py`: suites executáveis.
- `examples/postman/Ploteus-0.6.0.postman_collection.json`: requests para todas as formas de entrada.
- `evidence/0.6.0`: logs e relatórios atuais; arquivos históricos de evidências não são reescritos artificialmente.
