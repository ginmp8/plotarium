# Plotarium 0.5.0 - operation

GET usage and source origin configuration: [stateless-get.md](stateless-get.md). Verify source allowlists before network exposure. For production use native Chromium sandbox and a gateway with authentication and egress policy. Do not enable software WebGL or disable the browser sandbox outside isolated Development tests.

Run `python3 scripts/stateless_050_smoke.py <path-to-current-Plotarium.Api.dll>` for one-call GET/source tests (requires local loopback and .NET 10). The suite explicitly allows only its own local payload server, rejects other origins, and writes evidence to `evidence/0.5.0/stateless-http.json`. Run `python3 scripts/run_050_http_regression.py` for the existing full HTTP matrix. Set `PLOTARIUM_API_DLL` to the exact tested publish DLL when using a solution build with a non-default platform output path. `python3 scripts/stateless_050_ui_smoke.py` requires Python Playwright and a locally installed Chromium; it checks standalone page interactions without external HTTP fetches.

# Historical operation instructions (0.2.0)

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

## Renderizadores WebGL, mapas e compatibilidade em 0.2.0

Os exemplos completos estão em `README.md` e em `examples/coverage/*.json` (copie o corpo de cada arquivo em Postman Body → raw → JSON). Para testar todos os exemplos com um servidor local, execute:

```sh
python3 scripts/coverage_smoke.py http://127.0.0.1:55731
```

No Windows com HTTPS `https://localhost:55731`, configure corretamente o certificado de desenvolvimento; o script stdlib valida HTTPS por padrão (não desativa TLS). É possível configurar outro bind HTTP de loopback só para o teste; **não exponha a porta publicamente**. Erros `offline_maplibre` são esperados para os três cases MapLibre.

Para Chromium sem GPU, `Plotarium__Browser__EnableSoftwareWebGl=true` somente em `ASPNETCORE_ENVIRONMENT=Development`, em ambiente isolado de validação. Em produção mantenha WebGL de hardware/sandbox suportado, ou aceite que esses gráficos podem falhar controladamente.
