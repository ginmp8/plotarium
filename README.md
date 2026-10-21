# Plotarium 0.1.1

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
