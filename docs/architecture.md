# Arquitetura e decisoes

## Objetivo e limite

Uma unica API ASP.NET Core 10 recebe configuracoes nativas e emite definicoes/imagens. Nao e uma reimplementacao dos algoritmos graficos. Mantem ECharts 6.1.0, Plotly 4.1.2 e Mermaid 12.1.0 como bibliotecas JavaScript reais, carregadas localmente no Chromium.

## Fluxo

`HTTP -> autenticacao -> limite de corpo -> RequestValidator -> RenderService -> definicao JSON OU BoundedRenderGate -> ChromiumRenderer -> assets locais -> engine -> bytes -> politica SVG -> HTTP`

Domain nao conhece browser ou HTTP. Application depende apenas de Domain. Infrastructure implementa `IChartRenderer` e usa Application. API compoe tudo. Nao foram adicionados EF, banco, MediatR, AutoMapper, Redis, filas externas ou gateway intermediario.

## ADR-001: Chromium controlado por .NET

Adotado como denominador comum de layout DOM de Plotly/Mermaid e captura de raster/PDF. PuppeteerSharp e biblioteca .NET; nao instala servidor Node. ECharts poderia usar SSR SVG em outro runtime, mas isso nao resolve sozinho Plotly e Mermaid. ClearScript/V8 + rasterizador nao foi validado e nao integra esta versao.

## ADR-002: processo por trabalho

O primeiro candidato abre um processo Chromium por renderizacao admitida, em vez de compartilhar browser/paginas. Isto reduz estados compartilhados e torna o kill dono-especifico; aumenta cold-start e custo. Nao ha alegacao de alto throughput. Concorrencia padrao 2, espera maxima de 4 requisicoes. Medir antes de introduzir pooling.

Prazo padrao 30s inclui fila e operacoes. `WaitAsync` sozinho nao cancela JavaScript: o `finally` fecha/mata o processo pertencente ao job. Cancelamento durante launch pode aguardar a janela limitada de finalizacao do launch (10s por padrao) e fechamento (2s). Nao e uma garantia de resposta precisamente em 30s; esse pior caso e um teste de integracao obrigatorio.

## ADR-003: configuracao nativa, politica do servidor

ECharts usa `spec` option; Plotly usa `spec.data`/`spec.layout`; Mermaid usa `source`. Funcoes, carregamento remoto, config arbitraria de browser, custom series e HTML nao entram no contrato. Dimensoes, animacao, tooltip e politicas de seguranca sao propriedades do servidor. Plotly config nao e exposto.

## ADR-004: significado do JSON

JSON e envelope com schemaVersion, engineVersion, normalization=envelope-only e validation=structural-only. Nao converte Mermaid em AST, nao popula defaults internos das engines, nao inclui imagem Base64 e nao garante que uma definicao estruturalmente valida renderiza semanticamente. A versao do engine permite o consumidor localizar o runtime correto.

## ADR-005: recursos locais e identidade

Manifesto lista hash e versao de cada arquivo browser. AssetCatalog carrega/verifica os bytes; o browser recebe apenas respostas interceptadas na origem virtual `https://plotarium.invalid/`. Nao existe servidor real nesse dominio. O input nunca define arquivos/caminhos de carregamento. Os hashes protegem contra alteracao acidental apos o intake; nao sao assinatura independente do fornecedor.

## ADR-006: formatos e extensibilidade

SVG usa exportacao da engine; PNG/JPEG/WebP capturam a pagina final; PDF imprime uma pagina com dimensoes definidas. WebGL pode aparecer rasterizado dentro de SVG/PDF. Novas engines implementariam adaptacao e capacidades explicitas; nao se promete converter qualquer biblioteca ou linguagem arbitraria.

## Reprodutibilidade

Fixar SDK, pacote e lock, Chromium, sistema operacional, fontes e assets e necessario para comparacoes de pixels. A aplicacao desativa animacoes suportadas e espera fontes/frames, mas nao promete determinismo pixel-identico para todos os layouts. Core compilou/testou com SDK 10.0.302, runtime 10.0.10. Atualizacao de servicing e homologacao de browser sao gates distintos.
