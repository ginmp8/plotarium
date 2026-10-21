# Operacao - Plotarium 0.1.1

Requisitos: .NET SDK 10 para compilar; ASP.NET Core Runtime 10 para executar publish framework-dependent; instalacao local de Chrome/Chromium; fontes locais. Nao precisa PuppeteerSharp, Node, npm ou qualquer PackageReference externo. Os assets sao redistribuidos junto ao projeto; nao ha downloads de bibliotecas na inicializacao.

## Windows (PowerShell)

```powershell
$env:Plotarium__Browser__ExecutablePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
dotnet restore Plotarium.slnx
dotnet build Plotarium.slnx -c Release
dotnet run --project tests/Plotarium.ContractTests -c Release
dotnet run --project src/interfaces/Plotarium.Api -c Release
```

Configure o Chrome no ambiente do processo Visual Studio quando executar com F5. A API **nao possui autenticacao** e nao precisa de chave. Use `GET /v1/capabilities` sem cabecalho e `POST /v1/render` com `Content-Type: application/json`.

## Linux

```sh
export Plotarium__Browser__ExecutablePath=/usr/bin/chromium
export ASPNETCORE_URLS=http://127.0.0.1:5000
dotnet restore Plotarium.slnx
dotnet build Plotarium.slnx -c Release
dotnet run --project src/interfaces/Plotarium.Api -c Release
```

## HTTP/integracao

Com a API em execucao, `dotnet run --project tests/Plotarium.HttpTests -c Release -- http://127.0.0.1:5000/` verifica acesso sem credenciais, 3 engines x 6 formatos, Content-Type, bytes e cabeçalhos, erros de entrada. Grava arquivos/hashes em `artifacts/integration/`; conferir imagens visualmente continua importante.

`dotnet publish src/interfaces/Plotarium.Api -c Release -o artifacts/publish` copia os assets para `artifacts/publish/assets`. O executable path e externo e nao e incluido no ZIP.

## Diagnostico

- 404: confira **POST /v1/render**, nao `/v1/render/echarts`; confira o binario executado e a porta do processo.
- 400 / 422: JSON invalido, ou uso de `definition` no lugar de `spec` / `source`.
- 503: caminho/execucao do Chromium ou assets faltando.
- 429: admissao/concorrencia esgotada.
- 504: limite de tempo de render.

Readiness so confere presenca do executavel e manifestos, nao executa grafico. Execute smoke HTTP real apos o deploy.

## Sandbox do Chromium

Use Chromium sem privilegios com o sandbox nativo. `Plotarium__Browser__DisableSandbox=true` existe **somente para smoke de desenvolvimento em contêiner isolado**, e e rejeitado fora de `ASPNETCORE_ENVIRONMENT=Development`. Esse ajuste desabilita uma camada de protecao e nunca deve ser usado para servir requisições nao confiaveis em rede. Endpoints nao possuem autenticacao: aplique restricao de rede antes de publicar.
