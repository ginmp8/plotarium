# Arquitetura - Ploteus 0.3.0

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

`GET /share/teams|whatsapp|chatgpt` produz **descritores**, nao envia mensagens nem instala aplicativos. `/mcp` oferece um subconjunto MCP JSON sobre HTTP e um recurso `ui://ploteus/chart.html` que pode referenciar o visualizador HTTPS externo. Sem SSE/subscriptions. Integracoes de host dependem de registro, infraestrutura e validacao externa.

IDs temporarios sao **capabilities bearer**: quem obtem a URL pode ler o grafico ate expirar. O desenho preserva o comportamento anônimo anterior, exigindo protecao por rede/gateway em deploy publico; nao equivale a autorizacao por usuario.


## Stateless GET layer (0.5.0)

`StatelessQuery` translates GET query modes into the same `RenderRequest` contract as POST; `StatelessPayloadDecoder` handles JSON and bounded Gzip/Base64URL, then always invokes `RequestValidator`. `RemotePayloadFetcher` is an outbound adapter, not a cache: exact operator allowlist, one DNS resolution, validated pinned socket, no proxy or redirects, JSON-only content, bounded bytes/time. `StatelessHtmlBuilder` constructs a self-contained presentation using only hashed local assets. The GET route bypasses `ChartStore` entirely. The process retains its existing bounded rendering gate and transient Chromium process per static render; neither is a chart/payload store.

Future storage providers can be introduced separately from GET and POST input parsing without changing rendering or engines. The legacy `POST /v1/charts` stateful feature is preserved for backwards compatibility but is not used by any 0.5.0 GET flow.
