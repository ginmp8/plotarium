# echarts: mapa de engenharia reversa

**Identidade:** `echarts-master.zip`; SHA-256 `dcf71c20cf279eb216d38da44f522bf4d4d5cdae39da0b0b75f41467975ba91e`.
**Versao observada:** package e bundle 6.1.0.
**Licenca declarada:** Apache-2.0, NOTICE e avisos incorporados.

## Fluxo principal
`option -> modelo global/componentes/series -> pipeline de layout/visual -> zrender SVG ou Canvas -> exportacao/descarte`

## Evidencias e comportamento
### echarts-01 - `package.json; dist/echarts.min.js`
Bundle pronto esta presente. Nao e necessario compilar TypeScript ou instalar npm para consumi-lo no browser controlado por .NET.

### echarts-02 - `src/core/echarts.ts`
init/setOption/renderToSVGString/getDataURL/dispose definem a fronteira de integracao; SSR nao e sinonimo de renderizacao PNG.

### echarts-03 - `src/model/Global.ts`
Opcoes e componentes formam o modelo; preservar o JSON nativo evita manter um tradutor de todas as familias.

### echarts-04 - `src/core/lifecycle.ts`
Ha fases de ciclo de vida; tirar screenshot antes de concluir layout/animacao pode capturar estado intermediario.

### echarts-05 - `dist/extension/`
Extensoes e mapas nao sao implicitamente incluidos no core. ECharts GL e custom series com funcoes ficam fora do perfil inicial.

## Decisao para Ploteus
**Reutilizar:** option nativa, renderizador SVG, animacao desativada e dimensoes explicitas; separar engine de transporte.
**Nao transportar:** Nao afirmar que ClearScript/V8 ou SSR Node funcionam automaticamente dentro do CLR. Chromium e uma escolha da solucao conjunta, nao exigencia universal do ECharts.

## Limites da conclusao
Layouts dinamicos, fontes, mapas, GL e custom callbacks exigem cobertura especifica; o candidato restringe os que nao suporta.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 2343 entradas inventariadas; 7 arquivos selecionados
Manifesto completo de membros: `evidence/echarts-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `echarts-master/README.md` | 98 | `8cc8864325843d975f082cba0fd4cf208093feffb03568f48f164725dab8ebf9` |
| `echarts-master/src/core/echarts.ts` | 3434 | `330d96079db4b29e6a58be35d651c8933c5646621ecee162b932c6661d88a907` |
| `echarts-master/src/core/lifecycle.ts` | 74 | `1f587ba1b2716935db653e365f9a3bd1bc4c878a8b354967eb2d19e491de9de9` |
| `echarts-master/src/model/Global.ts` | 1100 | `461f9cfad58d62cdec107373cc65c6d6811f6c9db6c184d029a6bf0a543a36fb` |
| `echarts-master/src/export/core.ts` | 134 | `89851304a0614f113ff4c6a2728290378ecaa59ca25577579a57992a8f04f18c` |
| `echarts-master/package.json` | 244 | `3b5f84e135d641960271dd2d7d63b5486896a471fee8e0e841f822fa3846bb28` |
| `echarts-master/NOTICE` | 5 | `d491d358344f842685c1b1585970999db65fe30ecf7ef3867af8814f4016c016` |
