# Compatibilidade de tipos de gráficos — Plotarium 0.2.0

**O Plotarium não implementa todos os tipos e plugins do ECharts e Plotly.** Ele recebe a configuração `spec` original, usa os bundles ECharts 6.1.0 / Plotly.js strict 4.1.2 e entrega SVG ou raster/arquivo. O suporte deve ser medido por casos concretos executados, não inferido só pela lista de módulos de cada biblioteca.

## ECharts 6.1.0

Os casos para estes tipos foram executados em `scripts/coverage_smoke.py`:

- `line`, `bar`, `pie`, `scatter`, `effectScatter`, `radar`, `tree`, `treemap`, `sunburst`, `boxplot`, `candlestick`, `heatmap`, `parallel`, `lines`, `graph`, `sankey`, `funnel`, `gauge`, `pictorialBar`, `themeRiver`, `chord`.
- **Mapas**: `map=world` (199 polígonos/países), `map=world-50m` (258), `map=usa` (51 unidades), `geo.map=world` com linhas. Também ficam disponíveis para seleção `africa`, `asia`, `europe`, `north-america`, `south-america`, `oceania`.
- SVG nativo, salvo quando o próprio ECharts produzir camadas raster. Raster/PDF são derivados do SVG verificado.

**Não suportado:** `series.type="custom"` com `renderItem` arbitrário (callback JS), `bar3D`, `scatter3D`, `globe`, `surface`, `map3D` e demais extensões **ECharts GL** (nenhuma versão da extensão está incluída ou validada para ECharts 6.1.0). Não se afirma equivalência com plugins arbitrários de ECharts.

## Plotly.js strict 4.1.2

- **Geografia offline**: `choropleth`, `scattergeo` com TopoJSON `world_110m`, `world_50m`, EUA e continentes incluídos; nenhum pedido a CDN.
- **Traces 2D, polares, financeiros, hierárquicos e especiais**: ver os testes individuais em `examples/coverage/plotly-*.json` e a matriz `evidence/0.2.0/coverage-results.json`.
- **Traces WebGL 2D/3D**: `scattergl`, `scatterpolargl`, `scatter3d`, `cone`, `isosurface`, `mesh3d`, `parcoords`, `streamtube`, `surface`, `volume` foram executados com Chromium local + SwiftShader de teste. O PNG da própria Plotly é incorporado em SVG autocontido (não vetorial) para preservar renderização WebGL.
- **MapLibre não incluído no perfil offline**: `scattermap`, `choroplethmap`, `densitymap` dão **HTTP 422** com `code=offline_maplibre`. Esses traces funcionam em Plotly com configuração MapLibre/tile providers apropriada, mas falharam nos testes offline do Plotarium. Use `scattergeo`/`choropleth` para mapas offline. `scattermapbox`, `choroplethmapbox`, `densitymapbox` foram removidos pelo Plotly v4 e retornam `code=removed_mapbox`.

**Não comprovado:** perfeição visual de cada tipo, milhares de pontos em cada trace, cobertura combinatória de layouts, estilos com rede, plugins remotos, fontes ausentes, precisão científica/cartográfica e comportamento uniforme entre Chrome/Chromium, Windows, Linux e macOS. Imagens WebGL exigem GPU funcional; opcional `EnableSoftwareWebGl` só é autorizado em Development e não equivale a homologação de produção.

## Metodologia

- `tests/Plotarium.ContractTests` valida parser e limites; `tests/Plotarium.HttpTests` verifica os 18 pares engine/formato e HTTP.
- `scripts/coverage_smoke.py` faz render SVG com **casos próprios por tipo**, verifica HTTP 200, MIME, XML SVG e tamanho; casos deliberadamente não suportados exigem HTTP 422 com código correto. Não prova equivalência perceptual. Artefatos com `<image data:image/png;base64>` são inspecionados também com Pillow **apenas na avaliação local**, não requisito em runtime.
- Atestado vinculante: veja `evidence/0.2.0/`. Não confundir `format=json` (envelope estrutural) com renderização visual nem aceitar HTTP 200 com imagem em branco como prova de suporte.
