# Seguranca e limites da avaliacao

Revisao estatica de um candidato nao e certificacao de seguranca. Nao foram executados testes de browser, egress, sandbox, carga ou invasao. A aplicacao nao deve ser exposta publicamente antes desses gates.

## Controles implementados

Entrada JSON tratada como dados, sem eval ou interpolacao executavel. Politicas de engine sao fixas. API key fora dos arquivos, comparacao de SHA-256 em tempo constante; sem log de corpo/chave/erro bruto da engine. Ausencia de chave bloqueia startup, exceto modo Development anonimo explicitamente habilitado e restrito a clientes loopback.

Manifesto SHA-256 de assets locais; recusa de path traversal e links nos assets. Intercepcao de requisicoes instalada antes da navegacao, respostas somente para origem virtual e nomes existentes no catalogo. CSP recusa conexoes, frames, workers e formulários. Sem carregamento de CDN em runtime. O caminho do browser e configuracao do operador, nao input do cliente.

Mermaid strict com maxTextSize/maxEdges do servidor. Saida SVG recusa DTD, processing instructions, namespaces ativos, script, foreignObject, on*, xml:base, URLs externas e animacao. Este perfil nao e um sanitizador universal e pode recusar SVG legitimo com funcionalidades nao suportadas.

## Requisitos operacionais ainda nao comprovados

Execute Chromium como usuario sem privilegios com sandbox ativo. Nenhuma flag --no-sandbox/--disable-setuid-sandbox e incluida. Egress de processo deve ser negado por politica de rede do ambiente: request interception/CSP nao controlam todo o trafego interno do Chromium. Mantenha arquivos de assets somente leitura, temporarios limitados, memoria/CPU/PIDs com limites de SO e filesystem sem dados de outros usuarios.

Nao injete credenciais AWS/banco/desenvolvimento no processo do renderizador. A chave Plotarium__ApiKey e esvaziada no ambiente especifico de launch, mas isto nao substitui isolamento do processo e minimo privilegio. Outros segredos nao devem coexistir nesse runtime.

TLS, rotacao/armazenamento de chaves, quotas por consumidor, protecao do proxy, WAF e rate limit por identidade sao responsabilidades do ambiente a definir antes de publicacao. A fila global protege concorrencia, nao e um sistema completo antiautomacao.

## Fronteiras e riscos residuais

Cada trabalho abre seu proprio Chromium e o encerra; ainda e necessario provar ausencia de processo orfao durante cancelamento de launch e falha de pagina. O limite de bytes de saida e verificado depois de a biblioteca produzir o resultado: memoria e tempo precisam de limites externos. O Chrome instalado no sandbox (144) nao foi homologado com PuppeteerSharp 25.14.0 nem declarado atualizado.

As fontes recebidas nao foram autenticadas contra um release assinado independente. Hashes fixam exatamente o material enviado, nao sua autenticidade upstream. Antes de producao, conferir origem, SBOM, avisos de licenca e vulnerabilidades do conjunto realmente restaurado, incluindo pacotes transitivos e browser.
