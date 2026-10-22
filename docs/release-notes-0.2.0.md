# Plotarium 0.2.0 — Release notes

## Novos recursos

- Mapas ECharts `map` e `geo` usando mapas GeoJSON locais nomeados `world`, `world-50m`, seis continentes/regiões e `usa` (nomes completos dos estados); `chord` habilitado.
- Gráficos geográficos Plotly `choropleth` e `scattergeo` com TopoJSON em memória, roteado por interceptação CDP sem rede externa.
- Ampliação dos tipos ECharts e Plotly estáticos/2D/financeiros/hierárquicos/estatísticos/3D; 69 fixtures separados em `examples/coverage` e matriz HTTP em `evidence/0.2.0/coverage-results.json`.
- WebGL do Plotly exportado por `Plotly.toImage(png)` para preservar conteúdo 3D. Em SVG, PNG embutido em `<image>`: o formato é SVG autocontido **mas o gráfico não é vetorial**.
- Assets locais geográficos integrados no manifesto SHA-256 e copiados pelo `dotnet publish`.
- API continua **sem autenticação**, com único endpoint `POST /v1/render`. Interface, contrato (`spec` ou `source`) e demais 6 formatos preservados.

## Compatibilidade e lacunas conhecidas

- Plotly MapLibre `scattermap`, `choroplethmap` e `densitymap`: **não funcionam offline** e retornam HTTP 422 `offline_maplibre`. `*mapbox` removidos pelo Plotly 4 retornam 422 `removed_mapbox`.
- ECharts GL (`globe`, `bar3D`, `map3D`, `surface` etc.) depende de extensão não empacotada; `custom` exige callbacks JavaScript explicitamente desabilitados.
- Plugins externos, recursos remotos/tiles, geocodificação, HTML arbitrário e URLs controladas pelo cliente permanecem bloqueados.
- WebGL depende de GPU/Chromium adequado; `EnableSoftwareWebGl=true` é restrito a Development. Não foi homologado Windows/macOS ou ambiente produtivo.
- Os testes medem casos definidos, formato e legibilidade de renderizações; não demonstram suporte a cada combinação possível de trace, layout e dados.

## Gates executados

- .NET 10 restore/build/publish: sucesso, **0 warnings/0 errors** no build.
- 90/90 testes de contrato.
- 24/24 testes HTTP de engines × formatos, erros e anonimato.
- 69 cenários por tipo: **66 renderizados**, **3 rejeitados conforme contrato**, **0 falhas inesperadas**.
- 6/6 casos crossformat geográficos/3D, inspeção visual manual de ECharts mapa-múndi, Plotly coroplético, Plotly 3D.
- Proveniência e recibos: `docs/geographic-data.md`, `evidence/0.2.0/`, `assets/manifest.json`.

## Segurança

Nenhuma chave/token ou autenticação. Não publicar em rede pública. Usar bind loopback/rede privada, firewall, Chromium com sandbox habilitado em produção e limites de recursos. Não incluir executável Chromium no ZIP.
