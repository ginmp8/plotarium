# Arquitetura - Plotarium 0.3.0

`HTTP -> validacao de corpo -> RequestValidator -> RenderService -> [JSON envelope OU fila limitada -> ChromiumRenderer -> CDP loopback -> engine/assets locais -> SVG/PNG/JPEG/WebP/PDF] -> HTTP`.

Domain: contratos e limites; Application: validacao, controle de fila/tempo, XML SVG; Infrastructure: assets hashados, processo isolado por render, protocolo CDP WebSocket em C#, carregamento offline e exportacao; Interfaces: API minimal ASP.NET Core. Nenhum banco, fila externa ou autenticacao.

## Renderer

- `ChromiumProcess`: executavel Chrome/Chromium instalado pelo operador, perfil temporario por job, porta CDP efemera em loopback; mata o processo/destroi o perfil na saida.
- `CdpConnection`: WebSocket limitado, correlacao de requests, limites de mensagens e propagacao de cancelamento.
- `AssetCatalog`: valida hashes e estrutura de modulos do Mermaid antes de usar os bytes nativos.
- `ChromiumRenderer`: injeta codigo local previamente verificado em um documento offline; sem interpolacao do JSON como script; usa ECharts, Plotly e Mermaid efetivos; recusa requests de rede. SVG passa por politica XML; raster usa `Page.captureScreenshot`; PDF usa `Page.printToPDF` apos apresentar SVG validado.

Nao ha pacote PuppeteerSharp ou dependencia Node. A integracao depende de APIs CDP usadas pelo Chromium homologado. O Chrome deve rodar com sandbox, exceto opt-in de smoke local (Development, sem exposicao externa).

## Contrato

`POST /v1/render` recebe ECharts `spec` (option), Plotly `spec` (`data` e `layout`) ou Mermaid `source` (texto). O campo `definition` nao faz parte do contrato v1. `format` aceita png/jpeg/webp/svg/pdf/json/html, sendo HTML um visualizador hospedado no processo, com id temporario. JSON e envelope estruturado e nao requer browser. SVG e PDF sao saidas renderizadas, nao source. `GET /v1/capabilities` declara versoes e formatos.

Apos remover autenticacao, nao existe middleware de credenciais nem segredo de inicializacao. Limites globais continuam, mas quotas por consumidor deixam de existir. Restrinja acesso no host/rede; documente o risco em `docs/security.md`.

Nuvem, desempenho, rendering fidedigno em todas as familias nativas, SO Windows/macOS, PDF/A e consistencia pixel-a-pixel exigem validacao separada. Documentos historicos de 0.1.0-alpha.1 em `evidence/` registram aquela fase, nao a validacao de 0.2.0.


## Interactive publication (0.3.0)

`POST /v1/charts -> RequestValidator -> ChartStore (RAM, TTL, capacidade) -> id aleatorio -> GET /view|embed -> navegador do cliente -> adaptador ECharts/Plotly/Mermaid -> assets locais verificados`.

`GET /preview` reutiliza `RenderService` e o Chromium no backend para formatos estaticos; o HTML devolve especificacoes de dados validadas e scripts empacotados, sem avaliar codigo recebido do cliente. O visualizador generico nao conhece APIs das engines, que ficam isoladas nos adaptadores com contrato `mount/resize/reset/dispose`.

`GET /share/teams|whatsapp|chatgpt` produz **descritores**, nao envia mensagens nem instala aplicativos. `/mcp` oferece um subconjunto MCP JSON sobre HTTP e um recurso `ui://plotarium/chart.html` que pode referenciar o visualizador HTTPS externo. Sem SSE/subscriptions. Integracoes de host dependem de registro, infraestrutura e validacao externa.

IDs temporarios sao **capabilities bearer**: quem obtem a URL pode ler o grafico ate expirar. O desenho preserva o comportamento anônimo anterior, exigindo protecao por rede/gateway em deploy publico; nao equivale a autorizacao por usuario.
