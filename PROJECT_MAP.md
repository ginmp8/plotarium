# Plotarium - mapa do projeto

## Estado e ponto de entrada

**0.1.0-alpha.1, candidato com nucleo testado e integracao grafica bloqueada.** Comece por README.md, docs/dependencies.md e docs/validation.md. O impedimento atual e restore NuGet (NU1301), nao ausencia de implementacao intencional dos adaptadores.

## Responsabilidades e fluxos

| Parte | Entrada / responsabilidade | Saida / efeitos | Evidencia |
|---|---|---|---|
| API / Program.cs | HTTP, API key, corpo limitado, composition root | ProblemDetails ou attachment; metricas sem payload | Codigo e sintaxe inspecionados; HTTP nao executado |
| RequestValidator | JSON UTF-8 | RenderRequest estrito; rejeita duplicatas/limites/politicas | Testes de nucleo executados |
| RenderService / formato json | RenderRequest | Definition com request reutilizavel, sem browser | Testes executados; nao valida semantica da engine |
| BoundedRenderGate | Requisicao de imagem | Permissao limitada ou 429; cancelamento libera admissao | Testes concorrentes de nucleo executados |
| ChromiumRenderer | Requisicao admitida | Processo proprio, pagina local e bytes | Implementado; depende de PuppeteerSharp para compilar/executar |
| AssetCatalog | Manifesto e arquivos locais | Catalogo imutavel em memoria, hashes conferidos | Mesmo arquivo de producao compilado/testado no nucleo |
| renderer.js / ECharts | option JSON | SVG da engine; pagina para raster/PDF | Sintaxe JavaScript verificada; engine nao executada |
| renderer.js / Plotly | data + layout | toImage SVG ou pagina Canvas/WebGL | Sintaxe verificada; engine nao executada |
| renderer.js / Mermaid | DSL textual | parse/render em strict e SVG | Fechamento lexical dos modulos verificado; engine nao executada |
| SvgOutputPolicy | SVG produzido | XML restrito ou rejeicao | Testes de DTD, scripts, handlers, recursos externos e imagens embutidas |
| HttpTests | URL real + chave pelo ambiente | 18 combinacoes motor/formato e casos HTTP negativos | Harness compilado; testes nao executados |

## Caminhos alternativos e falhas

Autenticacao falha antes da renderizacao. JSON bypassa fila/browser, mas passa pela validacao estrutural. Imagens passam por admissao com deadline incluindo espera. Fila cheia retorna 429 sem criar browser. Cancelamento propaga ate as esperas; finally tenta fechar e mata somente o processo do trabalho. Renderizacao ou sintaxe de engine rejeitada retorna erro controlado. Nenhuma rede de usuario e permitida no perfil. Egress do processo e isolamento do SO ainda exigem teste real e politica de implantacao.

O status /health/ready confirma presenca, nao sucesso de renderizacao. Nao ha reexecucao automatica, persistencia, cache de graficos, fila externa ou fallback para outro motor. Problemas nesses pontos nao sao ocultados por uma segunda engine.

## Conhecimento das referencias -> decisoes de implementacao

| Referencia | Conhecimento aproveitado | Implementacao / decisao | O que nao foi copiado |
|---|---|---|---|
| echarts-node-export-server | Contrato option->imagem e separacao SVG/Canvas | Envelope independente e adaptador ECharts | Servidor GPL, headers legados, ECharts 5.6.0 embarcado |
| QuickChart | Entrada declarativa e formatos como servico | POST uniforme, formato explicito e exemplos | Codigo AGPL, new Function, callbacks arbitrarios, log de payload |
| mermaid.ink | Fontes prontas, exportacao e limites | font readiness, DSL Mermaid e renderizacao isolada | securityLevel loose, cache externo, GET com dados sensiveis |
| Instachart | API pequena com poucos conceitos | Sem banco, gateway ou fila externos | Modelos Go e gramaticas que reduzem variedade |
| Kroki | Catalogo de motores, isolamento, timeout, modulo Mermaid | IChartRenderer, gate, arquivos locais e catalogo de capacidades | Gateway Java, companions Node, --no-sandbox |
| ECharts core | Configuracao nativa, SVG e ciclo dispose | ECharts 6.1.0 real no browser | Reimplementacao dos algoritmos ou dependencia de npm |
| Plotly.js | DOM/toImage/strict bundle, raster em SVG quando WebGL | Plotly 4.1.2 strict, layout controlado pelo servidor | Python/Kaleido, promessa de SVG sempre vetorial |

Cada guia em docs/research inclui caminhos, hashes, linhas e limites. evidence/knowledge-graph.json representa afirmacoes de fontes; evidence/implementation-map.json representa o candidato. Arestas de implementacao sao derivadas de codigo, nao caminhos observados em runtime.

## Pacotes e reuso

O workspace persistente proposto e /Projetos/Plotarium. Referencias reutilizaveis: /_external/visualization/2026-10-21. Arquivos de fontes tipograficas, SDK, Chromium, caches, bin/obj e estado privado .rhapsodia nao fazem parte da distribuicao do projeto. Os sete ZIPs originais ficam separados; excertos nao sao codigo de produto.

## Criterio de conclusao funcional

Restaurar a versao pinada e transitivas, gerar lock, compilar a solucao, rodar core e a matriz HTTP, revisar imagens, testar cancelamento/egress/isolamento e homologar SO/browser/fontes. Persistencia do workspace e um resultado independente: salvar os arquivos nao aprova esses gates.
