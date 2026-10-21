# quickchart: mapa de engenharia reversa

**Identidade:** `quickchart-master(1).zip`; SHA-256 `72a50d2713e9ba9b10fcb2cf3f98292a35eb98fab3fded7e7e53c04f9432c9d9`.
**Versao observada:** package 1.8.1; Chart.js 2/3/4 declarados.
**Licenca declarada:** AGPL-3.0 (package e LICENSE).

## Fluxo principal
`GET/POST /chart -> parametros/defaults -> selecao de Chart.js/renderer -> configuracao -> canvas/SVG/PDF -> resposta`

## Evidencias e comportamento
### quickchart-01 - `index.js:34-56`
Parser com limite de corpo e rate limit configuravel. Default de largura/altura e exposicao GET/POST aparecem no mesmo servidor.

### quickchart-02 - `index.js:327-360`
GET e POST recebem definicoes de grafico; transporte por URL e conveniencia, nao requisito de engine.

### quickchart-03 - `lib/charts.js:132-155`
O caminho para configuracao string executa new Function. O comentario sobre VM nao transforma esse codigo em isolamento. O catch tambem referencia a configuracao original no log.

### quickchart-04 - `lib/charts.js:40-51`
Cache de renderer possui chave formada por dimensoes, versao e formato; cardinalidade requer controle separado.

### quickchart-05 - `lib/charts.js:425-433`
SVG e buffers usam caminhos diferentes; o conversor de PDF e uma camada adicional.

## Decisao para Plotarium
**Reutilizar:** Separar parametros de transporte, configuracao nativa, formatos e defaults; descrever erros HTTP e contratos de saida.
**Nao transportar:** Nada de new Function/eval com dados do cliente, logs de payload ou cache global sem limite. Chart.js, QR Code, Graphviz e recursos comerciais nao entram no escopo.

## Limites da conclusao
QuickChart e referencia de API, nao uma implementacao de ECharts/Plotly. Nao afirmar compatibilidade byte a byte.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 43 entradas inventariadas; 6 arquivos selecionados
Manifesto completo de membros: `evidence/quickchart-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `quickchart-master/index.js` | 492 | `a9eb89639804ca8073ed2ff408f71eb33f575874f37ae12cf56297431fba336f` |
| `quickchart-master/lib/charts.js` | 438 | `c94219b2418a8b05c73f1e12098b4395ca28f025a3a62a40cebf0cd3758c00fd` |
| `quickchart-master/lib/svg.js` | 20 | `ab7d3a0ebf3c5d1e190d539d000468cf5f0c1c6948a370280e0e9ad65ca58b2a` |
| `quickchart-master/lib/pdf.js` | 63 | `41aa037367111832af3403f6985a82e1be2567fbaadce712bf00e53bfb6e9644` |
| `quickchart-master/README.md` | 145 | `766161e50834a70cce29d7d7e7e1df600776ee60b4e0d80394bca2245b802e5d` |
| `quickchart-master/test/ci/app.js` | 263 | `8811eb0ac16faca3d318bf71bc8ebc2fe312e34460954019b37be5188a70bedf` |
