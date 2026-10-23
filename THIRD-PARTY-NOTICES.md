# Avisos de terceiros

O codigo C# e o adaptador JavaScript do Ploteus foram escritos independentemente para esta entrega. Nao houve port/copia de implementacao dos servidores QuickChart (AGPL) ou echarts-node-export-server (GPL). Seus fluxos foram usados como referencias de comportamento.

Os assets JavaScript sao codigo de terceiros, nao reimplementacao: Apache ECharts 6.1.0 (Apache-2.0), Plotly.js 4.1.2 (MIT) e Mermaid 12.1.0 (MIT), com avisos originais preservados em assets/licenses e nos bundles. Dependencias incorporadas podem conter avisos adicionais; auditar o bundle/SBOM antes de redistribuicao publica.

Os arquivos originais, licencas e excertos de engenharia reversa mantem suas licencas upstream e sao armazenados separadamente como referencias. O MIT do codigo novo nao relicencia esse material. Esta separacao nao substitui revisao juridica das condicoes reais de distribuicao.

O projeto 0.2.0 nao utiliza PuppeteerSharp ou pacotes NuGet externos. Chromium e fontes nao sao redistribuidos no ZIP. Origem, versao, plataforma e licencas devem ser registradas pelo operador.

## 0.2.0: dados geográficos

Foram adicionados TopoJSON de `plotly.js-main/dist/topojson/`, com checksum e origem por membro no `assets/manifest.json`. GeoJSON de ECharts foi derivado deterministicamente a partir desses mesmos dados por `scripts/generate_geodata.py`. Os dados geográficos são derivados de Natural Earth por upstream Plotly, com possíveis ajustes do upstream; não afirmamos que a transformação autônoma do Ploteus altere os direitos/licenças da base original. Preservar a licença MIT e os avisos do pacote Plotly distribuídos em `assets/licenses`. Consulte `docs/geographic-data.md`.

## ECharts GL 2.1.0

The user-provided ECharts GL distribution (`echarts-gl-2.1.0.zip`) is bundled as `assets/echarts-gl/echarts-gl.min.js`. Its license is preserved in `assets/licenses/echarts-gl-LICENSE`; render support requires WebGL in the host browser.

## Protomaps and MCP source archives

Protomaps and MCP SDK archives live separately in the shared `/_external` Library and are **not** bundled or executed in this Ploteus source release. No tiled geodata or unpublished license/provenance is implied.
