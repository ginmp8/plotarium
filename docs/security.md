# Seguranca - Plotarium 0.5.0

**A API nao autentica nem autoriza clientes.** Todas as rotas (incluindo render, capabilities e health) sao acessiveis sem token, X-Api-Key, usuario ou sessao. Isso e deliberado por solicitacao do projeto, mas nao constitui autorizacao para exposicao publica: restrinja o bind a loopback ou rede privada com firewall e controle de acesso fora do servico. Nunca exponha o Chromium remoto; CDP usa endpoint local do processo do job.

Controles mantidos: JSON validado como dados; proibicao de callbacks, recursos externos e diretivas Mermaid; manifesto SHA-256 dos assets locais; bloqueio de requests de pagina via CDP Fetch; CSP restritiva; SVG XML inspecionado contra scripts/links/DTD; limites de tamanho, pixels, concorrencia/fila e tempo. Arquivos temporarios de browser sao de uso exclusivo do job e eliminados no fim.

Sem credenciais armazenadas no codigo, em arquivos de configuracao ou nos exemplos. O filho Chromium recebe uma lista restrita de variaveis de ambiente, sem segredos do host. O processo por renderizacao e encerrado ao final; validar memoria, limites de SO e eventuais leaks continua responsabilidade de homologacao.

**Sandbox:** nao desabilite no ambiente de producao. Uma chave de configuracao `Plotarium__Browser__DisableSandbox` aceita true apenas quando ASPNETCORE_ENVIRONMENT=Development, para teste em contêiner isolado onde user namespaces nao estao disponiveis. Esse modo reduz isolamento; nao aceita exposicao externa. A politica CDP/CSP nao substitui bloqueio de egress em nivel de rede/processo.

Capacidade default: concorrencia 2; fila 4; request 1MiB; saida 16MiB; 30s por operacao. Limites da API nao substituem cgroups, quotas de CPU, PIDs, memoria, limite de conexoes ou controle de rede. A entrada do cliente e nao confiavel; nao logar corpos/sources renderizados.

Licencas: os bundles JavaScript de terceiros possuem avisos em assets/licenses. Atualizar browser e .NET conforme patches suportados, revisar CVEs e redigir uma auditoria de carga antes de uso de producao.

## Geographic assets and WebGL (0.2.0)

- Only exact hash-verified TopoJSON from `assets/topojson/*.json` may be fulfilled by the CDP Fetch interceptor. Other network requests are rejected; browser document CSP only allows `connect-src https://plotarium.invalid` and blob workers. No port, query, custom host or external map tile URL is accepted.
- ECharts maps are selected from a server-controlled name allowlist, and GeoJSON is read only from locally pinned SHA-256 assets. No arbitrary GeoJSON uploads or JavaScript callbacks.
- For WebGL, the service tries the vendor's raster export and embeds the PNG in an inert SVG when necessary. That requires WebGL access in Chromium and may consume significantly more CPU/memory than vector-only charts.
- `Plotarium__Browser__EnableSoftwareWebGl=true` is permitted only in `ASPNETCORE_ENVIRONMENT=Development` because it enables the Chromium `--enable-unsafe-swiftshader` software renderer. Do not use this opt-in for public services or production; use an appropriately sandboxed browser/GPU environment instead.
- As in 0.2.0 baseline, API endpoints have **no authentication**: deploy only behind trusted network boundaries. No external map providers or tiles are accessed.


## Interactive viewer and MCP (0.3.0)

- **Links sao credenciais bearer nao autenticadas**: 128 charts ativos, TTL 15 minutos por padrao, 128 bits de aleatoriedade, sem armazenamento persistente. Qualquer portador do id le `definition` e `preview`; considere confidencialidade ao habilitar exposicao externa. Reiniciar invalida os links.
- As paginas do visualizador impõem Content Security Policy para scripts/estilos locais, fontes bloqueadas, sem `unsafe-inline`; excecoes controladas em `FrameAncestors` permitem embeds especificos. Assets externos, codigos JS arbitrarios e recursos arbitrarios nao sao aceitos.
- `PublicBaseUrl` aceita somente origin HTTPS explicito, sem hostname derivado do header `Host`. Falso publico sem HTTPS nao pode gerar cards/links absolutos confiaveis.
- `POST /mcp` foi feito como subconjunto Streamable HTTP de resposta JSON; nao oferece sessao, controles OAuth ou autenticacao. Requer gateway HTTPS autenticado e defesa CSRF/Origin em ambiente publico. O endpoint nega requests com Origin browser de forma conservadora; hosts externos devem usar as permissoes e politicas correspondentes.
- Teams e WhatsApp recebem descriptors e nao sofrem chamada de provedor. Integracao real exige consentimento, segredo do provider, anti-abuso/rate policy e observabilidade.
- Scripts do visualizador e bibliotecas locais ficam servidos apenas em caminhos que constam do manifesto SHA-256. Um servico multi-tenant publico requer revisao separada de privacidade, autorizacao e isolamento operacional.


## Stateless GET and upstream payload retrieval (0.5.0)

`GET /v1/render` accepts `c`, `sourceUrl`, or `provider`+`key`, with no persistent state. External URL input is strictly limited: exact operator-defined origins only; HTTPS for public origins, and explicit separate private-origin configuration when necessary. DNS is resolved once and all records checked; the handler pins the TCP connection to a vetted IP, keeps TLS hostname verification, disables proxy use, forbids redirects, enforces JSON media type, size cap and deadlines. Link-local/cloud metadata addresses are never granted access by the private-origin exception. Incoming remote JSON follows the same `RequestValidator` policy as POST.

GET queries can leak into client/browser/gateway access logs: `Cache-Control: no-store` and `Referrer-Policy: no-referrer` are set, and the ASP.NET hosting diagnostics request logging category is suppressed, but these do not constrain upstream proxies. Never put secrets in URL query strings. Restrict caller access at the gateway; this app intentionally does not authenticate.

Compressed Gzip+Base64URL expands to at most 1 MiB and is subject to request URL length limits. Stateless HTML embeds vetted local library scripts with a per-response CSP nonce and no external scripts, styles, remote map data, or definition fetch. Mermaid outputs a server-generated SVG image; GL provides a PNG fallback when browser WebGL is not available. See [stateless-get.md](stateless-get.md).
