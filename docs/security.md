# Seguranca - Plotarium 0.2.0

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
