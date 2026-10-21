# Contrato HTTP v1

`POST /v1/render`, `Content-Type: application/json`, `X-Api-Key` configurada. Nao existem GET com dados na URL nem persistencia de graficos nesta versao. `GET /v1/capabilities` lista engines/formats/limites. `/health/live` e liveness sem autenticacao; `/health/ready` autenticado informa presenca dos assets/executavel, nao executa um grafico nem substitui smoke test.

## Campos

| Campo | Regra |
|---|---|
| engine | echarts, plotly ou mermaid; obrigatorio, case-sensitive |
| format | png (default), jpeg, webp, svg, json, pdf |
| width/height | 64..4096, defaults 800x450 |
| scale | inteiro 1..3; somente raster permite valor diferente de 1 |
| theme | light ou dark; padrao light |
| background | #RRGGBB ou transparent; padrao #ffffff; JPEG exige opaco |
| spec | objeto option ECharts ou {data,layout} Plotly; exclusivo de source |
| source | texto Mermaid, exclusivo de spec |

Capacidade raster: width*height*scale^2 <= 16.777.216 pixels. Corpo <=1 MiB; profundidade JSON <=32; no maximo 100.000 valores, 50.000 itens por array, 50.000 caracteres Mermaid. Saida <=16 MiB depois de obtida; limite nao e um substituto para cgroup/memoria do processo.

Propriedades desconhecidas do envelope, duplicatas em qualquer objeto, prototypes e campos de recursos remotos sao rejeitados. Os perfis suportados sao restritivos: uma opcao valida na biblioteca original pode ser recusada pela API.

## Representacao JSON

```json
{"schemaVersion":"1","normalization":"envelope-only","validation":"structural-only","engineVersion":"6.1.0","request":{"engine":"echarts","format":"json","width":800,"height":450,"scale":1,"theme":"light","background":"#ffffff","spec":{"series":[{"type":"pie","data":[1,2]}]}}}
```

Esse formato funciona no nucleo sem browser. Reenvie `request`, alterando format. Nao e a saida `full-json` do Plotly nem um JSON do desenho renderizado. Definicoes invalidas semanticamente podem falhar ao selecionar imagem.

## Saidas

PNG image/png; JPEG image/jpeg; WebP image/webp; SVG image/svg+xml; JSON application/json; PDF application/pdf. Retorno binario, nao JSON com Base64. Content-Disposition attachment, Cache-Control no-store e X-Content-Type-Options nosniff. SVG passa por perfil restritivo de XML; scripts, foreignObject, links externos, handlers e DTD nao sao aceitos.

## Erros

400 JSON/shape/duplicata; 401 autenticacao; 413 corpo/complexidade/saida; 415 content-type; 422 configuracao, perfil ou engine; 429 fila cheia (Retry-After 1); 503 browser ausente/iniciacao; 504 prazo; 500 erro interno. Cancelamento pelo cliente aborta a conexao. ProblemDetails contem codigo controlado/traceId, nao payload, stack, segredos ou erro bruto do navegador.

## Limites de cobertura

ECharts: vinte familias 2D declaradas; custom/map/GL excluidos. Plotly: data/layout nativos e validacao da engine; providers geograficos remotos excluidos. Mermaid: fonte com strict, sem diretivas de configuracao/frontmatter, callbacks e HTML. Plugins nao presentes no bundle podem falhar. Nenhuma equivalencia com QuickChart/Kroki/mermaid.ink e prometida.
