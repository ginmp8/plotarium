# instachart: mapa de engenharia reversa

**Identidade:** `instachart-master.zip`; SHA-256 `719335f945a5b9462584c0451049d443033e73f00c75ac2e27cd33995ade51dc`.
**Versao observada:** Go 1.21.3; echo/v4 4.12.0; go-charts/v2 2.6.6.
**Licenca declarada:** MIT.

## Fluxo principal
`main.go -> registro de rotas -> handler do tipo -> bind/defaults/validator -> renderizador Go -> PNG ou SVG`

## Evidencias e comportamento
### instachart-01 - `go.mod`
O servico e Go; go-charts e go-chart fazem parte das dependencias. O nome go-echarts em dependencia nao torna toda a API uma exportacao do Apache ECharts.

### instachart-02 - `pkg/chart.go:44-61`
Envelope de dados/titulo/tema/dimensoes; formato PNG/SVG. Cache longo deve ser reconsiderado para graficos privados.

### instachart-03 - `pkg/chart.go:93-108`
Entrada pode referenciar URL de dados e tem allowlist configuravel; esse caminho remoto e desnecessario no perfil offline do Plotarium.

### instachart-04 - `pkg/routes.go`
Rotas especializadas por familia tornam a API simples, mas tendem a restringir a variedade a handlers previamente implementados.

### instachart-05 - `pkg/bar_chart_handler_test.go`
Exemplos de validacao e testes do handler sao referencia de contrato, sem execucao nesta analise.

## Decisao para Plotarium
**Reutilizar:** Envelope pequeno, defaults explicitos e tipos MIME; evitar servicos de infraestrutura que nao participam da renderizacao.
**Nao transportar:** Nao criar uma gramatica generica de todas as familias nem baixar dados de URLs fornecidas por clientes. Nao transportar Go como dependencia.

## Limites da conclusao
Nao reimplementar a biblioteca grafica Go; objetivo e aprender a divisao de responsabilidades da API.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 83 entradas inventariadas; 8 arquivos selecionados
Manifesto completo de membros: `evidence/instachart-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `instachart-master/main.go` | 65 | `e888a8e5eab44af1db7e92021605b3c929f8f52f1462764cf6669de4cc45181d` |
| `instachart-master/pkg/chart.go` | 153 | `4086fe17f6c3595b720703ac0cdde5646eb6741105a25150ff2d64fb1a55dd7a` |
| `instachart-master/pkg/routes.go` | 123 | `74742e48c0afc30be8c741185e50c4051dda901743e1a7c26612b8f1f1187d06` |
| `instachart-master/pkg/bar_chart_handler.go` | 93 | `e5a8c12b623f1a3bd3344555961bd04d5bbd8fa2d468694b6d9a001d87f5ce51` |
| `instachart-master/pkg/util.go` | 80 | `e313ec7aa6dd98566324f1def68b7d9076d35be581093b9fcf1f505796c3041a` |
| `instachart-master/pkg/validator.go` | 42 | `f02d3c3c18b3471526954382ba378a977688f3b56ba990ff3c2311c7e1d19e11` |
| `instachart-master/pkg/bar_chart_handler_test.go` | 62 | `d6372487174e354eaca3b22fcd5869bef77892e8d5327f2a016423488e0f4fd5` |
| `instachart-master/README.md` | 241 | `f137beea4229623b4d39438abbbda177f15fdf6074459c4ea279fcd23f0c1d24` |
