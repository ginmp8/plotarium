# Mapeamento das referencias

Corpus: **7 arquivos ZIP, 10729 entradas**, 62 arquivos selecionados e 13354 linhas materializadas para leitura. Os numeros representam inventario e extracao, nao auditoria semantica de cada linha. A revisao semantica foi focada nos contratos, fluxos, limites, renderizadores e dependencias que mudam a API proposta.

## Guias

- [echarts-export-server](echarts-export-server.md): bundle ECharts 5.6.0; nao confundir com a versao do servidor.
- [quickchart](quickchart.md): package 1.8.1; Chart.js 2/3/4 declarados.
- [mermaid-ink](mermaid-ink.md): package 16.0.0; mermaid ^11.12.2; puppeteer ^25.0.2.
- [instachart](instachart.md): Go 1.21.3; echo/v4 4.12.0; go-charts/v2 2.6.6.
- [kroki](kroki.md): gateway pom 0.33.0, Java 21; Mermaid companion com puppeteer 25.1.0 e assets Mermaid 12.1.0.
- [echarts](echarts.md): package e bundle 6.1.0.
- [plotly](plotly.md): package e bundle 4.1.2; bundle strict selecionado.

## Rastreabilidade

`source-corpus.json` fixa os sete arquivos originais; `*-members.json` enumera suas entradas; `source-read-index.json` fixa arquivos/linhas lidos; `knowledge-graph.json` liga afirmacoes e decisoes as fontes. Os arquivos estao em `evidence/`.

Os excertos numerados e licencas completas ficam no pacote compartilhado em `/_external/visualization/2026-10-21/`. Os arquivos originais sao preservados sem modificacoes nesse mesmo catalogo. A API usa somente distribuicoes browser de engines permissivas, nao os servidores copyleft.

## Desvios de contexto corrigidos

QuickChart declara AGPL, nao uma dependencia livre de obrigacoes de redistribuicao. O exportador ECharts fornecido embute 5.6.0, distinto do core ECharts 6.1.0 recebido. Mermaid do mermaid.ink e 11.x; o asset adotado e 12.1.0 do Kroki. ECharts SSR SVG nao exige Chromium, mas nao elimina as necessidades DOM de Plotly/Mermaid nesta solucao conjunta.

## Cobertura e desconhecidos

Nao houve execucao dos originais, crawl de repositorios remotos, equivalencia funcional, benchmark ou auditoria completa de seguranca. Arquivos de imagens/fixtures, fontes e motores nao utilizados foram inventariados, nao interpretados integralmente. `CLAUDE.md` symlink de mermaid.ink foi identificado e nao seguido. Quatro caminhos inicialmente pesquisados no Kroki nao existiam; foram corrigidos por descoberta dos arquivos reais.
