# kroki: mapa de engenharia reversa

**Identidade:** `kroki-main.zip`; SHA-256 `9e075d68d641023b1447cbd5ef2451e86c2e24be196aa21d19d35d7a8c7af235`.
**Versao observada:** gateway pom 0.33.0, Java 21; Mermaid companion com puppeteer 25.1.0 e assets Mermaid 12.1.0.
**Licenca declarada:** MIT no projeto, motores e ativos com licencas proprias.

## Fluxo principal
`Gateway Vert.x -> DiagramRegistry/DiagramRest -> service/companion -> validacao e fila -> Chromium/pagina -> SVG ou PNG -> gateway HTTP`

## Evidencias e comportamento
### kroki-01 - `server/src/main/java/io/kroki/server/Server.java:68-71,99-165`
BodyHandler, registro de motores, POST raiz, health e tratamento de erros. Descoberta de companions nao e requisito para uma API de tres engines.

### kroki-02 - `mermaid/src/index.js`
Companion valida tipo/formato/tamanho e distingue timeout, sintaxe e falhas internas. Codigo fonte, nao runtime observado.

### kroki-03 - `mermaid/src/worker.js`
Adquire semaforo; conexao/newPage antecedem o try/finally no caminho lido. Falha antes do try pode reter uma permissao: risco inferido, nao reproduzido.

### kroki-04 - `mermaid/src/config.js:1-19`
Bloqueia alteracoes diretas de maxTextSize, maxEdges, securityLevel e outras politicas; essa separacao entre dado e autoridade e relevante.

### kroki-05 - `lib/browser-instance/index.js:101-163`
Memoriza a promessa de launch para evitar multiplos browsers em corridas; redefine estado apos falha.

### kroki-06 - `lib/browser-instance/index.js:175-209`
Intercepcao e aplicada antes de navegar; permite file/data/origens configuradas. Ploteus usa apenas assets interceptados conhecidos, sem file URLs.

### kroki-07 - `lib/browser-instance/index.js:11-66`
Ha --no-sandbox e --disable-setuid-sandbox. Nao foram transportados para o candidato .NET.

### kroki-08 - `mermaid/assets/mermaid/`
Distribuicao ESM com 108 arquivos .mjs disponivel no ZIP; pode ser carregada sem instalar Node/npm na API.

## Decisao para Ploteus
**Reutilizar:** Catalogo de engines, isolamento da conversao, limites, tratamento de timeout e controle de recursos antes da navegacao.
**Nao transportar:** Nao portar dezenas de motores, gateway Java, companions Node, configuracao remota ou flags sem sandbox. Nao afirmar que abortar uma promise interrompe CPU.

## Limites da conclusao
A analise de Kroki se concentrou no gateway, Mermaid e caminhos de renderizacao; outros motores foram inventariados, nao auditados semanticamente.
Nenhum servidor original foi instalado ou executado. Testes originais nao foram executados. Leitura estatica nao comprova explorabilidade, performance, equivalencia ou ausencia de defeitos.

## Cobertura: 3793 entradas inventariadas; 15 arquivos selecionados
Manifesto completo de membros: `evidence/kroki-members.json`. Arquivos lidos, hashes e linhas: `evidence/source-read-index.json`. Excertos numerados estao no pacote de referencias compartilhado, nao sao codigo executavel do produto.

| Arquivo | Linhas | SHA-256 |
|---|---:|---|
| `kroki-main/README.adoc` | 225 | `93bd01bb23434ef3c2606cbb802e486029c31ee80f7d936b8848010aad453210` |
| `kroki-main/mermaid/src/index.js` | 100 | `16642b8a7f315509d4eb4238e7a9ce3e5fcc81053ecb9cb3548073ceb3e1081c` |
| `kroki-main/mermaid/package.json` | 38 | `bc719028dbb4228e4b8a03685666d76ea3d67ecee5dc849620e8f482c367da7d` |
| `kroki-main/mermaid/assets/index.html` | 20 | `08782b3be8fd52310c7a640de61f59f23d4aa35b35b3a379909045fb6c5fac17` |
| `kroki-main/server/src/main/java/io/kroki/server/Server.java` | 357 | `afe586f23ef69f6c6535a01d551c26d7e0f6f91d49b7e2d5eb257575d5c647ee` |
| `kroki-main/server/src/main/java/io/kroki/server/service/Mermaid.java` | 69 | `e048c6d8bdd8d7cad2340fff942002f5930b3069bd36084323e5755f9d17d46b` |
| `kroki-main/server/src/main/java/io/kroki/server/service/Vega.java` | 81 | `f4393a911426ea542d72da7ef9adb910655a7cc972e3675a9aa77a451264b299` |
| `kroki-main/vega/src/convert.js` | 144 | `a006526d49001e68b7658542e0391ff3c06615d469f02a4183a9dd159d72d003` |
| `kroki-main/mermaid/src/worker.js` | 221 | `43b25048e849a5650cf283b0c31dfb26cde5bec7262f2c295cdfa2d0be9e12eb` |
| `kroki-main/mermaid/src/task.js` | 41 | `8bdb0c0b3e7581c7cc4f49e8e349a125208be4c788be22d497f354044de5a630` |
| `kroki-main/mermaid/src/config.js` | 67 | `592ac689a8d10444ba39ba3dbbe0f9c8f35f9552818ff0db64f52dca0d666515` |
| `kroki-main/mermaid/src/browser-instance.js` | 10 | `c43fc959bcdca163a817633ba2eb99751f95bed4f10a79076e6ce2439c3d9ec7` |
| `kroki-main/lib/browser-instance/index.js` | 212 | `f0113e2c0cf9b335a2525f623f19b984759cc1f61a835e52c552dc945431e6f9` |
| `kroki-main/server/src/main/java/io/kroki/server/service/DiagramRegistry.java` | 119 | `36bc23b062187eabdec0736a6246494ef1c8f5cbc6c24212244692ca6c661ab3` |
| `kroki-main/server/src/main/java/io/kroki/server/service/DiagramRest.java` | 62 | `b83e739c9b52384abfec3496eab7b4e1b4c5cd548d7f7a2e294156127b016415` |
