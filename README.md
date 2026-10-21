# Plotarium

API .NET 10 para definicoes e imagens de graficos ECharts, Plotly e diagramas Mermaid. Sem servico Node.js, Python, Java ou Go em runtime. JavaScript das engines e executado em Chromium controlado por PuppeteerSharp.

**Versao: 0.1.0-alpha.1. Estado: implementacao candidata; nucleo testado, integracao grafica bloqueada por dependencia indisponivel. Nao liberar em producao com base apenas neste pacote.**

## O que foi entregue

Contrato unico `POST /v1/render`, engines e assets fixados, formatos PNG/SVG/JSON/JPEG/WebP/PDF, controles de entrada, fila limitada, prazo, processo de browser por job, definicao JSON sem browser, exemplos, testes executaveis e mapeamento de sete referencias. A existencia do codigo de um formato nao significa que sua exportacao foi validada aqui.

Veja [estado de validacao](docs/validation.md), [dependencias](docs/dependencies.md), [contrato](docs/api-contract.md), [arquitetura](docs/architecture.md), [operacao](docs/operations.md), [seguranca](docs/security.md) e [referencias](docs/research/INDEX.md).

## Estrutura

- `src/domain`: contratos e limites basicos, sem pacotes externos.
- `src/application`: validacao, definicao JSON, admissao e politica SVG.
- `src/infrastructure`: assets locais e adaptador PuppeteerSharp/Chromium.
- `src/interfaces/Plotarium.Api`: HTTP, autenticacao, erros e metricas.
- `assets`: bundles browser pinados e manifesto SHA-256; nao executar scripts originais dos servidores.
- `tests`: 75 verificacoes executaveis de nucleo e assets; teste HTTP grafico separado.
- `docs/research` e `evidence`: decisao, fontes, inventario, rastreabilidade e evidencias reais.

## Iniciar

Instale um SDK .NET 10 suportado e Chromium/Chrome compativel com PuppeteerSharp 25.14.0. Nao ha download automatico de browser em runtime. As bibliotecas JS ja acompanham o pacote.

```powershell
$env:Plotarium__Browser__ExecutablePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
# Defina Plotarium__ApiKey por seu gerenciador de segredos/ambiente; pelo menos 32 bytes.
dotnet restore Plotarium.slnx
dotnet build Plotarium.slnx -c Release --no-restore
dotnet run --project tests/Plotarium.ContractTests -c Release --no-build
dotnet run --project src/interfaces/Plotarium.Api -c Release --no-build
```

O comando de teste do nucleo pode executar isoladamente sem o pacote PuppeteerSharp: `dotnet run --project tests/Plotarium.ContractTests -c Release`. Execute a partir da raiz do projeto, pois verifica `assets/`.

## Exemplo de chamada

```powershell
$headers = @{ 'X-Api-Key' = $env:Plotarium__ApiKey }
Invoke-WebRequest -Uri 'http://localhost:5000/v1/render' -Method Post -Headers $headers -ContentType 'application/json' -InFile 'examples/echarts.json' -OutFile 'chart.png'
```

A URL depende de `ASPNETCORE_URLS`; consulte os logs de inicializacao. Para JSON, use `examples/definition.json`. O campo `request` devolvido pode ser reenviado com outro `format`.

## O que falta comprovar

Restore completo e compilacao da Infra/API, execucao real dos tres motores, 18 combinacoes basicas motor/formato, isolamento de rede/processo, cancelamento durante launch, fidelity visual, carga e Windows/macOS. O bloqueio observado foi `NU1301` no NuGet; veja comandos e lista de dependencias, sem testes simulados que encubram o bloqueio.

O workspace persistente esperado e `/Projetos/Plotarium`; referencias reutilizaveis ficam em `/_external/visualization/2026-10-21`. A confirmacao efetiva de persistencia esta no recibo Library, separado da validacao do software.
