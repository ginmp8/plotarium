# plotly: mapa de engenharia reversa

**Identidade:** `plotly.js-main.zip`; SHA-256 `c093c85dda97ae664a843af9339c8e96b04c57e3997d91b5c06a1609b4a5df89`.
**Versao observada:** package e bundle 4.1.2; bundle strict selecionado.
**Licenca declarada:** MIT e avisos incorporados.

## Fluxo principal
`data/layout/config -> newPlot -> calculos/layout/DOM/SVG/Canvas/WebGL -> toImage/clonePlot -> exportacao ou captura`

## Evidencias e comportamento
### plotly-01 - `src/plot_api/to_image.js:64-75`
toImage cria um div e o anexa ao document; ha dependencia real de ambiente DOM neste caminho.

### plotly-02 - `src/plot_api/to_image.js:106-148`
SVG/full-json/raster possuem ramificacoes distintas, com Canvas para imagens. full-json e especifico do Plotly e nao uma gramatica unificada.

### plotly-03 - `src/snapshot/tosvg.js; src/snapshot/cloneplot.js`
Exportacao reconstrui/serializa o grafico. Camadas WebGL em SVG podem ser rasterizadas; extensao SVG nao significa vetor puro.

### plotly-04 - `src/plot_api/validate.js`
Validador do engine e usado no script do candidato antes de newPlot. Validacao estrutural C# nao substitui essa etapa.

### plotly-05 - `dist/README.md; dist/plotly-strict.min.js`
Distribuicao pronta evita build npm. Bundle strict reduz necessidades de avaliacao dinamica; a compatibilidade CSP precisa ser testada em runtime.

## Decisao para Ploteus
**Reutilizar:** Aceitar data/layout nativos; configuracao de browser e conexoes ficam sob autoridade do servidor. Esperar newPlot e fontes.
**Nao transportar:** Nao usar Kaleido/Python apenas para controlar o browser quando a API ja e .NET. Nao misturar full-json com AST de Mermaid. Mapas remotos desabilitados.

## Limites da conclusao
WebGL, 3D, mapas, matematica e fidelidade tipografica nao foram executados. Suporte amplo do bundle nao comprova cobertura ampla desta API.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 4367 entradas inventariadas; 9 arquivos selecionados
Manifesto completo de membros: `evidence/plotly-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `plotly.js-main/README.md` | 199 | `fea6ce7917d644bad473e21be6828c07877aa5e76a9b7bdcfbe70b3ab917e4e5` |
| `plotly.js-main/src/plot_api/to_image.js` | 177 | `76710fadccd9c30282a63daa5a370a37e41e5c62c8029782c76cd97a59eb235c` |
| `plotly.js-main/src/plot_api/to_image_attributes.ts` | 66 | `ba7f43df99080ee6c15224dd6a2bd7c97a8d2dde5d509d9e66528ac308eba8da` |
| `plotly.js-main/src/plot_api/plot_config.js` | 462 | `2f45b5306853ad79717b7fa29fa9c0f936b31be77c3ae31f39d1250566df0d57` |
| `plotly.js-main/src/snapshot/tosvg.js` | 183 | `e8e349eec917adf76a368a4bdbf8ebaa49cd92ffea268918bf52e43625198a97` |
| `plotly.js-main/src/snapshot/cloneplot.js` | 155 | `d7f75562b681d87800d5af372a86069d0f66ae1aaa9f1cfbee654f8650e5f48c` |
| `plotly.js-main/src/plot_api/validate.js` | 389 | `8da403a052e7c9a25dd42b5140b3144825f243875e44d18e8936774a7e683c28` |
| `plotly.js-main/dist/README.md` | 271 | `3043e6a66549bc74f90a059bad980ad12b350e3051a5e46bda7936f5a89f557a` |
| `plotly.js-main/package.json` | 184 | `6b987c5ae366ded1513ac05c1d934e0ffc51dcb8f393f9fbf309149eab0bff2f` |
