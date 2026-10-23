# mermaid-ink: mapa de engenharia reversa

**Identidade:** `mermaid.ink-main.zip`; SHA-256 `392cc91ea9097cf4bb099740c0163771f634aba538b2fcb7411d635796e54d9b`.
**Versao observada:** package 16.0.0; mermaid ^11.12.2; puppeteer ^25.0.2.
**Licenca declarada:** MIT.

## Fluxo principal
`GET /img|svg|pdf/:encodedCode -> decodificacao/base64/pako -> opcoes -> fila -> pagina Mermaid -> screenshot/SVG/PDF -> cache/resposta`

## Evidencias e comportamento
### mermaid-ink-01 - `src/app.js:47-50,75-85`
Rotas por formato, PQueue e instancia Puppeteer. Dependencia opcional de banco/cache nao e inerente ao desenho Mermaid.

### mermaid-ink-02 - `src/static/mermaid.mjs:36-59`
Espera fontes, inicializa Mermaid, renderiza SVG e vincula funcoes. securityLevel loose e fixado neste caminho.

### mermaid-ink-03 - `src/helpers/getOptionsFromCode.js`
Dados codificados e opcoes podem influenciar a configuracao; entrada nativa precisa ser tratada como nao confiavel.

### mermaid-ink-04 - `src/app.js:133-160`
Ha particularidades de flags em CI, reconexao e fechamento de browser/banco. Nao transportar flags de desativacao de sandbox.

### mermaid-ink-05 - `CLAUDE.md`
Entrada de symlink no ZIP foi registrada e nao seguida; instrucoes de agentes nos repositorios nao comandaram esta analise.

## Decisao para Ploteus
**Reutilizar:** Renderizar a partir de Mermaid fonte, esperar fontes e separar SVG de screenshot/PDF.
**Nao transportar:** Nao usar securityLevel loose, callbacks ou HTML de usuario. Nao adotar PostgreSQL, pnpm/Node ou GET codificado sem necessidade.

## Limites da conclusao
Mermaid 11 no servidor doador nao e a versao 12.1.0 vendorizada do Kroki. Plugins de layout/diagramas sao contratos separados.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 53 entradas inventariadas; 12 arquivos selecionados
Manifesto completo de membros: `evidence/mermaid-ink-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `mermaid.ink-main/src/app.js` | 184 | `459c9b0df116d39d180a14598cd1523d0faf034b7582b0c37b7be3e97ce3b74e` |
| `mermaid.ink-main/src/helpers/renderImgOrSvg.js` | 109 | `f847ce973116b18d6b8e5532cea2c3d8790c8d5a267afea64ee50ed0a32ff28c` |
| `mermaid.ink-main/src/helpers/renderSVG.js` | 24 | `b7f3824b52f97b63d315991682b93ba72b3b2caed25cf65b8ab1e814f3061773` |
| `mermaid.ink-main/src/helpers/openMermaidPage.js` | 16 | `93b5757b9eaefaf477dfcfa558971afbd78d4a77447850566a3d88109ab1075a` |
| `mermaid.ink-main/src/helpers/utils.js` | 297 | `4214b2cc1ce8b2827de21b5877d1b8578cc955906533cb90d438e6b74cf87965` |
| `mermaid.ink-main/src/helpers/createCacheKey.js` | 79 | `ba83b0057519c9e1b91164c2608d617bb37d1c4c8b1db83b2847149ac30064ba` |
| `mermaid.ink-main/src/helpers/getOptionsFromCode.js` | 101 | `fcf721966310cc72172f2d839091a6a85d370f6496611e2bb2690217326166d9` |
| `mermaid.ink-main/src/static/mermaid.mjs` | 81 | `b8294eed74b657f444b8c703918f8f5ee9be7b3613a52b02de8fef824dd3af94` |
| `mermaid.ink-main/src/views/img.js` | 72 | `f996a52d0457bf0184c34d0acbe55bf507551d39915ce8621155c7423d1a5aaa` |
| `mermaid.ink-main/src/views/svg.js` | 98 | `863d1dc3f700ec7e8a93d13c68a07ebf47659eb43e4e51e375f912f8949c4499` |
| `mermaid.ink-main/src/views/pdf.js` | 93 | `07ac5e62423907266ed4f67c86b25180406f63a4b8c3bfe0be5610bc5584a2c2` |
| `mermaid.ink-main/README.md` | 97 | `ee9e2923e33e3bf50740d3d1ed5b34024d622e2524c0dfb82f3ad4834389134d` |
