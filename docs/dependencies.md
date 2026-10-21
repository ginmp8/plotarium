# Dependencias e lacunas - 21/10/2026

## Leitura correta do estado

Disponivel na Library != materializado no sandbox != compativel em execucao. O NuGet da solucao completa falhou com NU1301; portanto nao existe fechamento transitivo restaurado nem packages.lock.json validado. A lista abaixo cobre as dependencias diretas e as fronteiras transitivas conhecidas, explicitando o que nao pode ser completado sem restore.

## Runtime e build

| Dependencia | Estado observado | Necessidade |
|---|---|---|
| SDK .NET 10 | 10.0.302 linux-x64 materializado da Library; SHA-512 conferido com metadata Microsoft; compilou e executou testes | Build. Atualizacao de servicing deve ser tratada antes de producao |
| Runtime .NET/ASP.NET Core 10 | 10.0.10 acompanha o SDK e esta no ambiente | Execucao da API; nao e o servicing mais recente |
| PuppeteerSharp 25.14.0 | Ausente no sandbox e caminho esperado do catalogo; download e restore bloqueados | Unico PackageReference externo direto da implementacao |
| Chromium/Chrome | /usr/bin/chromium, versao 144.0.7559.96, presente no sandbox | Engine DOM/captura. Compatibilidade e sandbox de execucao nao homologados; nao distribuir este binario como aprovado |
| Bibliotecas nativas do Chromium | ldd local sem dependencia ausente no binario inspecionado | Instalacao do browser no SO-alvo; conjunto depende de distro/arquitetura |
| Fontes locais | fontconfig resolve Noto Sans no sandbox | Operador deve padronizar e licenciar fontes no ambiente; arquivos de fontes nao sao redistribuidos |
| ECharts 6.1.0 | Bundle disponivel no ZIP recebido e copiado para assets, hash fixado | Runtime browser; nao precisa npm/Node |
| Plotly 4.1.2 | Bundle strict disponivel no ZIP recebido e copiado para assets | Runtime browser; nao precisa Python/Kaleido |
| Mermaid 12.1.0 | 108 modulos ESM encontrados no Kroki e copiados com estrutura preservada | Runtime browser; nao precisa mermaid-cli/Node |

## Dependencias declaradas por PuppeteerSharp 25.14.0 para net10.0

Fonte: https://www.nuget.org/packages/PuppeteerSharp/25.14.0 (consultada em 21/10/2026).

| Dependencia declarada | Estado de disponibilidade |
|---|---|
| Microsoft.Extensions.Logging >=8.0.0 | Catalogo possui 5.0.0 e 10.0.12. 8.0.0 nao apareceu na pasta completa consultada. Nao escolher silenciosamente 10.0.12 para contornar o sandbox |
| Microsoft.IO.RecyclableMemoryStream >=3.0.1 | 3.0.1.nupkg, nuspec e hash ja existem em /_external/dotnet/nuget/packages/microsoft.io.recyclablememorystream/3.0.1 |
| ReactiveExtensionsSharp >=0.3.0 | Caminho esperado nao encontrado na Library; bytes e fechamento ainda indisponiveis |
| WebDriverBiDi >=0.0.61 | Caminho esperado nao encontrado na Library; bytes indisponiveis; metadata net10 consultada informa sem dependencias adicionais |

Logging 8.0.0 declara DependencyInjection, Logging.Abstractions e Options >=8.0.0 para net8 (asset compativel com net10). Estes, por sua vez, podem introduzir DI.Abstractions e Primitives. A dependencia ReactiveExtensionsSharp nao teve seu metadata transitivo confirmado. Nao ha alegacao de que esta enumeracao seja um lock completo ou lista exata de todos os nupkgs resolvidos.

**Para eliminar essa lacuna sem adivinhar:** executar `scripts/collect-dependencies.ps1` em um ambiente com NuGet acessivel. O script usa o projeto real, gera lock files, enumera transitivas resolvidas e preserva nupkgs/hashes em um pacote offline. Esse resultado e a lista exata que falta; nenhuma dependencia foi substituida por stub ou implementacao ficticia para simular build.

## O que nao e necessario

Node/npm, Python/Kaleido, Java/Maven, Go, node-canvas, Sharp, SkiaSharp, ClearScript/V8, banco, Redis, PostgreSQL, S3 ou filas externas nao sao dependencias do runtime deste candidato. Tambem nao ha framework NuGet de testes: as verificacoes de contrato sao executaveis .NET com codigo de saida e relatorio JSON.

## Plataformas

Compilacao/execucao de nucleo: somente Linux x64 testado. Windows e macOS exigem .NET 10 e Chrome/Chromium apropriados e continuam nao testados. Browser .NET nao e Native AOT nem implementacao grafica nativa C#. Docker/ Kubernetes nao foram configurados ou testados; nao ha imagem de container homologada neste pacote.

## Atualidade e confianca

Metadata Microsoft consultada informava SDK 10.0.402/runtime 10.0.13 como servicing mais recente em 13/10/2026. O SDK reutilizado 10.0.302 foi usado apenas para o escopo de validacao relatado. Fontes: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json ; https://www.puppeteersharp.com/ .

Arquivos de entrada foram pinados por hash, nao autenticados como releases upstream assinados. Antes de producao: restore com lock, verificador de vulnerabilidades, licencas/SBOM, browser atualizado compativel, smoke tests, rede negada e limites de SO. Nenhum desses gates foi marcado como aprovado por inferencia.
