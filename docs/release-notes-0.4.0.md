# Ploteus 0.4.0 — execução observada

## Adições

- ECharts GL 2.1.0 pinado, JS local SHA256-validado e séries `bar3D`, `scatter3D`, `line3D`, `lines3D`, `surface`, `map3D`, `polygons3D`, `graphGL`; componentes `globe`/`grid3D`. Chromium usa canvas/WebGL; saídas SVG incorporam raster, **não são vetores puros**.
- Tipos Plotly `scattermap`, `choroplethmap`, `densitymap` aceitos pelo contrato. Para exportação estática, o renderizador converte os dois primeiros para `scattergeo`/`choropleth` e usa os dados TopoJSON locais. `densitymap` é rejeitado com 422 (`offline_densitymap`) em vez de alterar indevidamente a semântica de densidade. Na visualização HTML, Plotly usa estilo MapLibre com GeoJSON local (sem mapa de ruas).
- Exemplos e PNGs reais sob `examples/rendered-0.4.0/`; Teams manifest template e payload de texto WhatsApp. Nenhuma integração envia mensagens sem credenciais/consentimento.
- Índice reutilizável em ChatGPT Library `/_external`: ECharts GL 2.1.0 e Microsoft.Extensions.AI.Abstractions 10.8.3, entre outras dependências.

## Limites de implantação e homologação

- MapLibre nativo não foi validado em um navegador com origem web neste sandbox: política de browser impede acesso até a `localhost`. A renderização estática não executa MapLibre, mas converte tipos com semântica preservada. Não há `planet_z6.pmtiles` ou tiles de ruas.
- O endpoint MCP é a implementação parcial herdada da 0.3.0, **não usa os binários do SDK oficial MCP 2.2.0**, porque o restore NuGet offline não foi confirmado; conexão/UX real ChatGPT ainda não homologada.
- TeamsJS e WhatsApp Business Platform exigem aplicativo/conta/credenciais e implantação HTTPS verificável; templates não representam homologação nos canais.
- Sem autenticação embutida: **não expor diretamente à Internet**. Restrinja acesso externamente e habilite apenas domínios e públicos confiáveis.
- Linux Chromium 144 + .NET SDK 10.0.302 usado neste ciclo; homologação Windows e produção não executadas.
