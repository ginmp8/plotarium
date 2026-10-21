# Evidencias de validacao

## Executado

- Intake dos sete arquivos: inventario completo de membros e SHA-256, limites de arquivo e registro de symlink nao seguido.
- Extracao para leitura de 62 arquivos, 13.354 linhas; analise semantica focada nos fluxos relevantes, nao em todas essas linhas ou todo o corpus.
- SDK .NET 10.0.302: SHA-512 comparado com metadata oficial, instalacao isolada, `--info`, compilacao e execucao do nucleo.
- Primeiro ciclo de testes: 18 casos, falhas reais antes da implementacao da validacao (`core-red.json`), depois 18 aprovados (`core-green-initial.json`).
- Suite final do nucleo: 75 aprovados, 0 falhas (`core-tests.json`), incluindo entrada, limites, duplicate keys, SVG ativo, admissao, cancelamento, timeout, definicao JSON, assets e adulteracao.
- O teste AssetCatalog compila o mesmo arquivo de producao como linked source, sem substituir PuppeteerSharp por tipos falsos. Isto NAO compila o restante de Infrastructure.
- Restore completo tentado: primeira tentativa parou por configuracao ambiente de plataforma; segunda, com plataforma normalizada somente no comando do sandbox, falhou NU1301 por acesso ao NuGet.

## Bloqueado / nao executado

Restore da Infra/API, build/publish completo, browser real via C#, PNG/SVG/PDF/JPEG/WebP gerados pela API, testes de autenticacao HTTP real, carga, tempos p50/p95, CPU/RSS, kill/recovery real, matriz Windows/macOS, perceptual diff, auditoria de vulnerabilidades e imagem de container. Nenhum resultado visual foi inventado e nenhum output falso foi incluido.

Os testes HTTP estao implementados e devem ser executados somente contra a API real. A contagem 75 nao inclui testes de browser nem testes dos servidores originais.

## Matriz exigida antes de promover

1. Restore, lock e build limpos com a versao pinada de PuppeteerSharp.
2. Tres engines x seis formatos; MIME, assinatura, dimensoes, conteudo nao vazio e revisao visual.
3. ECharts bar/line/pie/sankey/graph/heatmap; Plotly line/scatter/box/sankey e 3D somente quando declarado; Mermaid flowchart/sequence/state/class/ER. Tipos nao testados nao ganham cobertura automaticamente.
4. Chave incorreta, JSON malformado/duplicado, XSS, Mermaid init/frontmatter/click/HTML, referencias a file/http/data e tentativa de buscar metadados internos. Observar egress de processo, nao somente evento de pagina.
5. Cancelamento antes/depois de launch, timeout de render, crash de browser, fila cheia, saida excessiva e recuperacao de permissoes; nenhum processo orfao.
6. Fontes fixadas, labels longos, Unicode/pt-BR, transparencia/JPEG opaco e escalas 1/2/3 dentro do limite.
7. Testes repetidos com SO/arquitetura/browser e assets pinados; nao confundir reprodutibilidade do build com igualdade de pixels.

## Identidade

`candidate-files.json` registra hashes dos arquivos de entrega (exclui caches, .rhapsodia e artefatos de build). `validation-receipt.json` separa testes executados dos gates bloqueados. Recebimento na Library e registrado separadamente: persistencia bem-sucedida nao significa validacao funcional da API.
