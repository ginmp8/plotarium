# echarts-export-server: mapa de engenharia reversa

**Identidade:** `echarts-node-export-server-master.zip`; SHA-256 `08db1d9da8b5276c904b3c8b78cf0db99e00d9b791a4250c92c74cc7eba1be55`.
**Versao observada:** bundle ECharts 5.6.0; nao confundir com a versao do servidor.
**Licenca declarada:** GPL-3.0-or-later (cabecalhos do servidor).

## Fluxo principal
`HTTP POST / -> acumulo do corpo -> JSON.parse -> ssr.render -> SVG string ou node-canvas PNG -> resposta binaria`

## Evidencias e comportamento
### echarts-export-server-01 - `export-server/server.js:43-116`
Rota raiz, POST, OPTIONS, limite de corpo, formato e dimensoes por X-Image-*; formato SVG por header, PNG como alternativa.

### echarts-export-server-02 - `export-server/ssr.js:37-53`
SVG usa echarts.init(null, ..., renderer=svg, ssr=true), setOption e renderToSVGString. Este caminho nao usa Chromium.

### echarts-export-server-03 - `export-server/ssr.js:69-80`
PNG usa createCanvas e toBuffer; a dependencia nativa e Canvas, nao um navegador.

### echarts-export-server-04 - `export-server/ssr.js:100-137`
Dimensoes possuem defaults, mas o trecho lido nao demonstra teto de pixels. Nao ha dispose nos caminhos de renderizacao inspecionados; risco de ciclo de vida, nao vazamento medido.

### echarts-export-server-05 - `export-server/tests/server.test.js`
Ha testes de varios tipos, incluindo boxplot, candlestick, sankey e sunburst. Eles foram lidos, nao executados.

## Decisao para Plotarium
**Reutilizar:** Aceitar option nativa e devolver MIME binario; separar exportacao de contrato HTTP.
**Nao transportar:** Nao copiar implementacao GPL; nao transportar headers proprietarios como unico contrato; controlar pixels e descarte explicitamente.

## Limites da conclusao
A existencia de fixture nao prova fidelidade no ECharts 6.1.0 nem equivalencia do novo servico.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 47 entradas inventariadas; 5 arquivos selecionados
Manifesto completo de membros: `evidence/echarts-export-server-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `echarts-node-export-server-master/export-server/server.js` | 128 | `270ee46f6f5c8240f3cc86b7da9e58fa6ec58eb11db8fe0e10488465331ff74d` |
| `echarts-node-export-server-master/export-server/ssr.js` | 137 | `6fcd79a98e172da650f19567d271f65a765f7c3bf5a6b7d74e763d5f798216f0` |
| `echarts-node-export-server-master/export-server/package.json` | 30 | `65bfc27057249b81a76d5c9cb107031e22f7b8cb7fc8e3cc26e2007aa59fd7ae` |
| `echarts-node-export-server-master/readme.md` | 198 | `23d04b94be011eb5a525d6ddab369e442ccae37a1dd8b1f31ebb4625a03fb739` |
| `echarts-node-export-server-master/export-server/tests/server.test.js` | 389 | `1104548a3c1a3d1f6f48c69c0a9768a111e3655143787574b983878d554c398e` |
