# Dependencias - Plotarium 0.2.0

| Dependencia | Para que serve | Disponibilidade |
|---|---|---|
| .NET SDK 10 | Compilar e executar testes | Instalacao pelo operador |
| ASP.NET Core 10 Runtime | Rodar API publicada (framework-dependent) | Instalacao pelo operador |
| Chrome/Chromium | Renderizar formatos PNG/JPEG/WebP/SVG/PDF | Instalacao pelo operador; configurar Plotarium__Browser__ExecutablePath |
| Fontes do SO | Medidas de texto e layout | Instalacao pelo operador |
| ECharts 6.1.0 | Graficos | Assets locais incluido no ZIP |
| ECharts GL 2.1.0 | Graficos 3D e globo em ambientes WebGL | Bundle JS local com SHA-256 e licença incluídos |
| Plotly.js 4.1.2 | Graficos | Assets locais incluidos no ZIP |
| Mermaid 12.1.0 | Diagramas | ESM e chunks locais incluidos no ZIP |

**Nenhum pacote NuGet externo (zero PackageReference), nem PuppeteerSharp.** O servico se comunica com Chrome via CDP WebSocket usando bibliotecas .NET. Restauracao do SDK/framework depende da instalacao do .NET, mas nao do feed NuGet do antigo renderer. `global.json` especifica 10.0.100 com rollForward latestFeature; a validacao Linux usou SDK 10.0.302. JSON de definicao nao requer navegador.

Para Linux: `export Plotarium__Browser__ExecutablePath=/usr/bin/chromium`. Para Windows: caminho absoluto do Chrome/Chromium instalado. Valide o sandbox e fonts do navegador no ambiente real. Build/verificacao local nao atesta Windows/macOS, confiabilidade sob carga, CVEs ou comportamento pixel-identico.

`evidence/` e `docs/research/` preservam material historico do desenvolvimento original de 0.1.0-alpha.1; as informacoes sobre NuGet bloqueado nao se aplicam a este renderer 0.2.0.

## Geographic data (0.2.0)

- Source: user-supplied `plotly.js-main.zip` with `plotly.js-main/dist/topojson/*_110m.json` and `*_50m.json`, reusing Plotly's bundled Natural Earth-derived datasets. All files and SHA-256 digests are included in `assets/manifest.json` and loaded only after verification.
- Conversion: `python3 scripts/generate_geodata.py /path/to/plotly.js-main.zip`, produces `assets/geojson/*.geojson` from the TopoJSON and updates the asset manifest. No pip, npm, service, GDAL, internet or Node.js is required.
- Plotly WebGL requires working Chrome/Chromium WebGL; `EnableSoftwareWebGl` is an **opt-in Development-only** browser option (not a production configuration default). ECharts GL 2.1.0 is now bundled and SHA256-verified. WebGL must be available; SVG outputs with GL content are raster-embedded, not native vector.

## Recursos reutilizáveis 0.4.0

Na ChatGPT Library (não dentro do ZIP): `/_external/javascript/echarts-gl/2.1.0/`, `/_external/geospatial/protomaps/`, `/_external/mcp/`, `/_external/dotnet/nuget/packages/microsoft.extensions.ai.abstractions/10.8.3/`. O pacote de origem do ECharts GL está copiado e com licença porque é exigido em runtime offline do Plotarium; não assumir que outra máquina tenha acesso à Library.
