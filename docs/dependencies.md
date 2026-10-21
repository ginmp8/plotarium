# Dependencias - Plotarium 0.1.1

| Dependencia | Para que serve | Disponibilidade |
|---|---|---|
| .NET SDK 10 | Compilar e executar testes | Instalacao pelo operador |
| ASP.NET Core 10 Runtime | Rodar API publicada (framework-dependent) | Instalacao pelo operador |
| Chrome/Chromium | Renderizar formatos PNG/JPEG/WebP/SVG/PDF | Instalacao pelo operador; configurar Plotarium__Browser__ExecutablePath |
| Fontes do SO | Medidas de texto e layout | Instalacao pelo operador |
| ECharts 6.1.0 | Graficos | Assets locais incluido no ZIP |
| Plotly.js 4.1.2 | Graficos | Assets locais incluidos no ZIP |
| Mermaid 12.1.0 | Diagramas | ESM e chunks locais incluidos no ZIP |

**Nenhum pacote NuGet externo (zero PackageReference), nem PuppeteerSharp.** O servico se comunica com Chrome via CDP WebSocket usando bibliotecas .NET. Restauracao do SDK/framework depende da instalacao do .NET, mas nao do feed NuGet do antigo renderer. `global.json` especifica 10.0.100 com rollForward latestFeature; a validacao Linux usou SDK 10.0.302. JSON de definicao nao requer navegador.

Para Linux: `export Plotarium__Browser__ExecutablePath=/usr/bin/chromium`. Para Windows: caminho absoluto do Chrome/Chromium instalado. Valide o sandbox e fonts do navegador no ambiente real. Build/verificacao local nao atesta Windows/macOS, confiabilidade sob carga, CVEs ou comportamento pixel-identico.

`evidence/` e `docs/research/` preservam material historico do desenvolvimento original de 0.1.0-alpha.1; as informacoes sobre NuGet bloqueado nao se aplicam a este renderer 0.1.1.
