# Plotarium 0.3.0 — Release notes

## Novos recursos

- Saida `html` para visualizador hospedado pelo Plotarium; API de criacao de charts temporarios (`POST /v1/charts`) com view, embed, preview, definition e TTL.
- Adaptadores de interface ECharts, Plotly e Mermaid empacotados, carregados por engine; controles de zoom, tema, restauracao, selecao quando suportada, exportacao PNG, acesso ao JSON e compartilhamento.
- Descritores de compartilhamento para Teams (Adaptive Card de imagem e URL), WhatsApp (URL, imagem, descritor de botao) e ChatGPT (MCP tool/UI resource). Nenhum provider envia mensagem por conta do servidor.
- MCP JSON-RPC HTTP com `initialize`, `ping`, `tools/list`, `tools/call`, `resources/list` e `resources/read`, associando ferramenta ao recurso `ui://plotarium/chart.html`.
- Declaração de capacidades por engine e separação do visualizador generico em adaptadores independentes.
- Collection Postman v2.1 com 12 requests, preview PNG e harness Playwright de componentes reproduzivel.

## Compatibilidade

- `POST /v1/render` com `png`, `jpeg`, `webp`, `svg`, `json`, `pdf` segue compatível com 0.2.0. Adicionou-se somente `html`.
- Mapas offline, raster-em-SVG para certas séries WebGL e listas explícitas de exclusão de 0.2.0 continuam válidos.
- A API permanece deliberadamente **sem autenticação**, conforme a versão 0.1.1; os links temporários são capabilities bearer e seu uso publico requer gateway de confiança.
- Não é standalone HTML: o visualizador depende de links temporarios, definição e assets locais.

## Limitações importantes

- **Teams, WhatsApp e ChatGPT:** formatos e contratos preparados; integração real depende de aplicativos, credenciais, domínio HTTPS, host e testes externos. Não foi homologada a entrega/mensagem em nenhum desses hosts.
- O servidor MCP é um subconjunto documentado de Streamable HTTP com respostas JSON; não implementa SSE, sessões OAuth ou registro automático em ChatGPT.
- Validação de interface com Chromium sob `about:blank` usou scripts pré-verificados (restrição de navegação da política do ambiente); não equivale ao teste completo de cliente externo. Mermaid ESM interativo ainda depende de validação visual em browser com origem acessível.
- TTL/capacidade/in-memory: links expiram, não sobrevivem a restart e não se replicam entre instâncias. Considere cache compartilhado ou persistência autorizada numa evolução planejada.

## Evidência

Veja `evidence/0.3.0/validation.json` para o conjunto de testes executados, provas negativas, verificação de compilação e lacunas ambientais, e `docs/interactive.md` para comandos de uso.
