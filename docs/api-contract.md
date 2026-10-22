# Contrato HTTP v1 - Plotarium 0.2.0

**Sem autenticacao.** Nenhum header de identidade e necessario ou validado. `POST /v1/render`, Content-Type application/json, devolve arquivo com o MIME da representacao. Rotas publicas `GET /v1/capabilities`, `/health/live` e `/health/ready`.

| Campo | Valores | Obrigatoriedade |
|---|---|---|
| `engine` | `echarts`, `plotly`, `mermaid` | sim |
| `format` | `png` (default), `jpeg`, `webp`, `svg`, `json`, `pdf` | nao |
| `spec` | objeto ECharts option ou Plotly com `data`/`layout` | sim, apenas ECharts/Plotly |
| `source` | codigo Mermaid | sim, apenas Mermaid |
| `width`, `height` | 64..4096, defaults 800x450 | nao |
| `scale` | inteiro 1..3, somente raster | nao |
| `theme` | light/dark | nao |
| `background` | `#RRGGBB` ou transparent; JPEG exige opaco | nao |

**Atencao:** `definition` nao e aceito. Para Mermaid nao use `spec`.

```json
{"engine":"echarts","format":"svg","width":800,"height":450,"spec":{"xAxis":{"type":"category","data":["A","B"]},"yAxis":{"type":"value"},"series":[{"type":"bar","data":[1,2]}]}}
```

JSON devolve envelope `schemaVersion`, `normalization: envelope-only`, `validation: structural-only`, `engineVersion`, `request`. Nao converte um diagrama Mermaid em AST nem calcula configuracao final do Plotly; significa definicao reutilizavel.

HTTP: 200 output; 400 corpo malformado/schema/duplicata; 413 limite; 415 media type; 422 entrada/perfil invalido; 429 fila; 503 browser/assets; 504 prazo; 500 erro interno. 404 corresponde a rota incorreta (por exemplo `/v1/render/echarts`). **Nao ha HTTP 401 do aplicativo.**

Limites: 1MiB entrada, 16MiB saida, maxPixels 16.777.216, 100.000 nos JSON, 50.000 elementos por array, Mermaid 50.000 chars. Recursos remotos, HTML, codigo JavaScript do cliente e callbacks sao proibidos. Retorno binario direto, nao Base64 em JSON. Saida SVG valida politica XML.

## Maps and additional chart types (0.2.0)

- ECharts: `spec.series[].type="map"`, `spec.series[].map="world"`; or `spec.geo.map="world"` with `lines`, `effectScatter`, or other geo-coordinate series. Packaged maps are enumerated by `GET /v1/capabilities` (`geography.echartsMaps`). Map data `name` must match the local GeoJSON region names. `spec.series[].nameProperty="iso3"` may be used to align ISO3 identifiers if supported by the ECharts option. `usa` includes full state names.
- Plotly: `choropleth` and `scattergeo` work with bundled TopoJSON; supply ISO3 `locations` or `lat`/`lon`. Requires **no remote tiles**.
- Plotly `scattermap`, `densitymap`, `choroplethmap` explicitly return `422 offline_maplibre`. Plotly v4 removed legacy `*mapbox` traces (422 `removed_mapbox`). ECharts GL and arbitrary user callbacks remain unsupported.
- WebGL output `svg` can be self-contained raster-in-SVG (`<image href="data:image/png;base64,..."/>`). It is not always an editable vector chart. A Chromium build without GPU/WebGL support can fail with `422 engine_rejected_definition`.
- The fixed `https://plotarium.invalid/assets/topojson/` address is a CDP-intercepted internal namespace and is **never fetched over the Internet**. All other browser-origin network requests remain blocked; user-supplied URLs and external providers are not allowed.
