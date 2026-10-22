# Plotarium 0.2.0

API ASP.NET Core 10 para gerar ECharts 6.1.0, Plotly.js 4.1.2 e Mermaid 12.1.0 em PNG, JPEG, WebP, SVG, PDF ou envelope JSON. **Sem qualquer autenticacao:** nao existe chave, token, usuario, middleware de auth nem cabecalho obrigatorio.

Runtime de imagem: Chromium/Chrome local controlado pelo protocolo CDP diretamente em C#, sem PuppeteerSharp, Node.js ou pacotes NuGet de terceiros. Bibliotecas JavaScript estao incluidas em `assets/` com manifesto de integridade. JSON funciona sem navegador.

## Iniciar no Windows (PowerShell)

```powershell
$env:Plotarium__Browser__ExecutablePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
dotnet restore Plotarium.slnx
dotnet build Plotarium.slnx -c Release
dotnet run --project src/interfaces/Plotarium.Api -c Release
```

No Visual Studio, coloque `Plotarium__Browser__ExecutablePath` nas variaveis de ambiente do perfil ativo ou configure o mesmo caminho na maquina; o Chrome/Chromium precisa estar instalado. Em Linux, configure `/usr/bin/chromium`. O browser deve ser executado sem privilegios, com sandbox. **A API nao exige `Plotarium__ApiKey`.**

## Contrato e exemplos diretos de curl

**Rota unica:** `POST /v1/render`. Campos `engine`, `format`, `width`, `height`, `spec` (ECharts/Plotly) ou `source` (Mermaid). `definition` nao e um campo aceito pela API. Os exemplos a seguir usam curl (Bash/Git Bash) e HTTPS local com certificado de desenvolvimento (`-k` apenas para desenvolvimento).

```bash
curl -k -X POST 'https://localhost:55731/v1/render' -H 'Content-Type: application/json' -d '{"engine":"echarts","format":"svg","width":800,"height":450,"spec":{"xAxis":{"type":"category","data":["Jan","Fev","Mar"]},"yAxis":{"type":"value"},"series":[{"type":"bar","data":[10,20,15]}]}}' -o echarts.svg
curl -k -X POST 'https://localhost:55731/v1/render' -H 'Content-Type: application/json' -d '{"engine":"plotly","format":"png","spec":{"data":[{"type":"bar","x":["A","B"],"y":[1,2]}]}}' -o plotly.png
curl -k -X POST 'https://localhost:55731/v1/render' -H 'Content-Type: application/json' -d '{"engine":"mermaid","format":"svg","source":"flowchart LR\nA[Inicio] --> B[Fim]"}' -o mermaid.svg
```

Use `curl.exe` no PowerShell (a interpretacao das aspas varia entre shells); os comandos Bash/Git Bash acima nao devem ser colados diretamente em Windows PowerShell antigo. `GET /v1/capabilities`, `/health/live` e `/health/ready` tambem sao publicos.

## Testar e publicar

```powershell
dotnet run --project tests/Plotarium.ContractTests -c Release
dotnet run --project tests/Plotarium.HttpTests -c Release -- http://localhost:5000/
dotnet publish src/interfaces/Plotarium.Api -c Release -o artifacts/publish
```

O teste HTTP precisa de uma API executando e verifica as 18 combinacoes de engine x formato, incluindo imagens reais. Veja [operacao](docs/operations.md), [contrato](docs/api-contract.md), [arquitetura](docs/architecture.md), [validacao](docs/validation.md), [dependencias](docs/dependencies.md) e [seguranca](docs/security.md).

**Aviso:** sem autenticacao significa que qualquer cliente com acesso de rede pode solicitar renders. Mantenha a API na interface de loopback ou em uma rede privada protegida pelo ambiente. Nao a exponha diretamente a Internet; cargas complexas ainda podem consumir CPU/memoria apesar dos limites da aplicacao.

## Novidades 0.2.0: mapas geográficos e cobertura ampliada

- ECharts core `map` e componentes `geo` com mapas locais verificados: `world`, `world-50m`, `africa`, `asia`, `europe`, `north-america`, `south-america`, `oceania`, `usa` (nomes dos estados dos EUA). Suporte adicional a `chord` do ECharts 6.
- Plotly `choropleth` e `scattergeo`: TopoJSON local do Plotly 4.1.2 servido por interceptação CDP de **URLs fixas internas**, sem CDN, URLs arbitrárias ou rede externa. A biblioteca inclui diversos outros traces nativos já fornecidos pelo pacote strict, inclusive 3D/WebGL, dependendo do Chromium.
- Gráficos Plotly WebGL e alguns canvas têm uma saída SVG **rasterizada**: o SVG embute um PNG gerado pela engine, não é um SVG vetorial puro. PNG/JPEG/WebP/PDF continuam disponíveis e dependem das condições locais do Chromium.
- MapLibre (`scattermap`, `choroplethmap`, `densitymap`) permanece **não suportado** no modo offline, com erro explícito HTTP 422 `offline_maplibre`. Também permanecem sem suporte ECharts GL (`bar3D`, `globe`, `surface`, etc., exigem biblioteca de extensão não incluída), `custom` callbacks arbitrários, plugins externos e provedores de mapa remoto.
- Sem autenticação, sem PuppeteerSharp e sem runtime Node.js. Requer apenas .NET 10 e Chrome/Chromium instalado.

### cURLs diretos para importar no Postman

Mundo ECharts em PNG:

```bash
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"echarts","format":"png","width":1000,"height":600,"spec":{"visualMap":{"min":0,"max":100,"left":20,"bottom":20},"series":[{"type":"map","map":"world","data":[{"name":"Brazil","value":80},{"name":"China","value":55},{"name":"India","value":75}]}]}}'
```

Plotly Choropleth em SVG:

```bash
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"plotly","format":"svg","width":1000,"height":600,"spec":{"data":[{"type":"choropleth","locations":["BRA","USA","CHN","IND"],"z":[80,30,55,75]}],"layout":{"geo":{"projection":{"type":"natural earth"}}}}}'
```

Plotly 3D em PNG (requer suporte WebGL no Chromium):

```bash
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"plotly","format":"png","width":1000,"height":600,"spec":{"data":[{"type":"scatter3d","mode":"markers","x":[0,1,2,3],"y":[2,3,1,4],"z":[1,2,3,2]}]}}'
```

**No PowerShell:** para evitar problemas de aspas do shell, veja `docs/operations.md`; os comandos de Postman acima são para importação em Raw Text, Git Bash ou shell Unix. `-k` é necessário apenas para `curl` local com certificado de desenvolvimento não confiável.

Veja `docs/chart-compatibility.md`, `docs/geographic-data.md` e `evidence/0.2.0/` para os tipos testados, limites e resultados executados.
