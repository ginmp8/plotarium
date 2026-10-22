# Plotarium 0.2.0 - Mapa do projeto

- `src/domain/Plotarium.Domain`: `RenderRequest`, tipos, formatos e limites.
- `src/application/Plotarium.Application`: `RequestValidator`, `RenderService`, fila limitada e politica de XML/SVG.
- `src/infrastructure/Plotarium.Infrastructure`: integridade dos assets, processo Chromium, CDP/WebSocket, carregamento local, render ECharts/Plotly/Mermaid e exportacao nos formatos de saida.
- `src/interfaces/Plotarium.Api`: endpoints, configuracao, headers defensivos, ProblemDetails e metricas (sem autenticacao).
- `tests/Plotarium.ContractTests`: regras de entrada, limites, outputs JSON e hash dos assets.
- `tests/Plotarium.HttpTests`: endpoints publicos e matriz 3 motores x 6 formatos contra um processo real.
- `assets/`: bibliotecas JavaScript versionadas e manifesto de integridade; `docs/research` e `evidence`: corpus/analise historica; `examples/`: corpos HTTP exemplificados.

Contrato unico POST /v1/render; `spec` ECharts/Plotly; `source` Mermaid. Nao sao necessarios headers de autenticacao. API requer navegador para imagem; JSON funciona sem navegador. `docs/security.md` enumera riscos de servir clientes anonimos.

## 0.2.0

- `assets/geojson/` e `assets/topojson/`: mapas locais com proveniência e hashes, `scripts/generate_geodata.py` para conversão reproduzível.
- `src/infrastructure/.../render-bridge.js`: registro de mapas ECharts e exportação Plotly PNG para preservar WebGL em SVG raster-in-SVG.
- `src/infrastructure/.../ChromiumRenderer.cs`: carrega mapas exigidos e intercepta **apenas** GET aos TopoJSON verificados; requests externos permanecem proibidos.
- `tests/...` e `scripts/coverage_smoke.py`: contrato, 3×6 formatos HTTP e matriz de tipos com exclusões explícitas. Casos completos em `examples/coverage/`.
- `docs/chart-compatibility.md` identifica suporte e limites, `docs/geographic-data.md` documenta a fonte dos polígonos. Sem autenticação.
