# Operacao e comandos

## Pre-requisitos

SDK .NET 10 para build; runtime ASP.NET Core 10 para publish framework-dependent; PuppeteerSharp e transitivas restaurados; Chrome/Chromium instalado; fontes locais padronizadas. Nao iniciar Chromium como root nem adicionar flags que removam sandbox. Nao ha instalacao automatica de pacotes ou browser ao atender requests.

## Linux

```sh
export Plotarium__Browser__ExecutablePath=/usr/bin/chromium
# Exporte Plotarium__ApiKey usando seu mecanismo de segredos, sem registra-la em historico/logs.
export ASPNETCORE_URLS=http://127.0.0.1:5000
dotnet restore Plotarium.slnx
dotnet build Plotarium.slnx -c Release --no-restore
dotnet run --project tests/Plotarium.ContractTests -c Release --no-build
dotnet run --project src/interfaces/Plotarium.Api -c Release --no-build
```

Para publish: `dotnet publish src/interfaces/Plotarium.Api -c Release -o artifacts/publish`. Os assets sao copiados pelo csproj. Run: `dotnet artifacts/publish/Plotarium.Api.dll` com as variaveis configuradas. Build/publish da API ainda precisam ser executados e validados fora do bloqueio NuGet observado.

## Windows

Use comandos separados no PowerShell; nao requer &&. Configure o caminho completo do Chrome e a chave no ambiente. `scripts/verify.ps1` verifica restore/build/nucleo; `scripts/collect-dependencies.ps1` prepara dependencia offline quando houver acesso ao NuGet.

## Teste HTTP

Com API realmente iniciada e autenticacao habilitada:

```sh
dotnet run --project tests/Plotarium.HttpTests -c Release -- http://localhost:5000/
```

O teste tenta 18 combinacoes basicas (3 engines x 6 formatos) e erros/autenticacao. Grava bytes e hashes em artifacts/integration. Assinatura/dimensao nao provam qualidade visual; abrir os resultados e aplicar a matriz de validacao e obrigatorio. Nao ha resultado HTTP grafico executado nesta entrega.

## Diagnostico

NU1301: transporte/feed NuGet indisponivel; nao desativar auditoria ou suprimir dependencia. Browser ausente: verificar caminho/ABI/usuario. Falha de sandbox: corrigir host/usuario; nao remover isolamento. Assets ausentes/hash incorreto: restabelecer pacote de assets pinado, sem recalcular manifesto para aceitar adulteracao. 429: reduzir admissao/concentracao de clientes ou dimensionar depois de medir. 504: identificar tipo/volume problematico sem logar dados do cliente.

Liveness nao executa graficos. Readiness desta versao faz checks de presenca; um 200 nela nao e homologacao do browser. Para readiness operacional forte use smoke real no processo de implantacao e nao publique antes de passar.

## Sem dependencias ocultas

Nenhum CDN em runtime; sem Node ou Python; sem permissao de endereco de browser remoto; sem execucao de payload JavaScript do cliente. Fontes, sandbox Chromium, protecao TLS, egress e limites de memoria sao responsabilidade explicita do deployment.
