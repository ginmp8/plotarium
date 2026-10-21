# Validacao executada - Plotarium 0.1.1

**Versao 0.1.1 aprovada para o smoke funcional executado, nao homologada para exposicao publica.** Receipts e hashes de cada artefato em `evidence/0.1.1/`.

## Executado nesta entrega

- .NET SDK 10.0.302 (Linux x64), Chromium 144.0.7559.96.
- `dotnet restore Plotarium.slnx -p:Platform="Any CPU" --ignore-failed-sources`: aprovado; projeto sem pacotes NuGet de terceiros.
- `dotnet build Plotarium.slnx -p:Platform="Any CPU" -c Release --no-restore`: **aprovado, 0 warnings, 0 errors**.
- `dotnet run --project tests/Plotarium.ContractTests -c Release --no-build`: **75/75**.
- `dotnet publish src/interfaces/Plotarium.Api -p:Platform="Any CPU" -c Release --no-restore -o artifacts/publish`: **aprovado**, DLL e assets locais copiados.
- Publicacao executada via `dotnet artifacts/publish/Plotarium.Api.dll`, sem configurar API Key, em `127.0.0.1:55731` do ambiente de testes.
- `dotnet run --project tests/Plotarium.HttpTests -c Release --no-build -- http://127.0.0.1:55731/`: **24/24**, incluindo 18 combinacoes (ECharts, Plotly, Mermaid x PNG/SVG/JSON/JPEG/WebP/PDF), contrato sem autenticacao, status esperados 400/404/422 e MIME/assinatura/dimensoes.
- Revisao visual: os tres PNGs gerados possuem conteudo grafico visivel. Os tres PDFs foram verificados com `pdfinfo` (1 pagina cada), convertidos para imagem via Poppler; PDF ECharts inspecionado visualmente.
- Artefatos testados verificam seus bytes SHA-256 contra os hashes declarados no JSON de testes.

## Configuracao de sandbox na avaliacao

O browser foi executado com `ASPNETCORE_ENVIRONMENT=Development` e `Plotarium__Browser__DisableSandbox=true` **somente** porque o contêiner de validação nao permite user namespaces apropriados. Isso **nao valida** que o Chromium inicia com sandbox ativo em Windows/Linux de producao. O codigo nao desabilita sandbox por padrao e rejeita esse opt-in fora de Development. Nenhum browser binario esta incluido no ZIP.

## Nao comprovado

Execucao Windows/macOS, browser sandbox de producao, carga/performance de picos, seguranca de exposicao publica anonima, recurso remoto/bypass do navegador, cobertura de todos os tipos nativos e pixels idênticos entre hosts. Um teste de assinatura MIME nao prova equivalencia visual; a revisao manual de 3 PNGs nao cobre todos os estilos/diagramas. O renderer mudou de PuppeteerSharp para CDP direto na 0.1.1; compatibilidade com futuras versoes do Chromium deve ser revalidada.

## Historico

As evidencias de `evidence/` anteriores registram o candidato `0.1.0-alpha.1` e seus impedimentos de restore; foram preservadas sem reescrever os resultados historicos. Evidencias da release atual sao armazenadas em `evidence/0.1.1/`.
