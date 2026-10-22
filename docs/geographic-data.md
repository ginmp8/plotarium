# Dados geográficos offline — Plotarium 0.2.0

A distribuição inclui arquivos cartográficos com SHA-256 em `assets/manifest.json`, carregados via `AssetCatalog` com verificação de integridade antes da API aceitar requisições.

## Proveniência e formatos

- **Fonte binária/arquivo**: `plotly.js-main.zip`, membros `plotly.js-main/dist/topojson/{world,usa,africa,asia,europe,north-america,south-america,oceania}_{110m|50m}.json` fornecidos com o projeto. Não inventar equivalência de identidade upstream: o hash original consta no manifesto.
- **ECharts**: `scripts/generate_geodata.py <zip>` converte as geometrias `countries` ou `subunits` do TopoJSON para GeoJSON `FeatureCollection`, descompactando arcos delta (inclusive índices negativos), coordenadas e componentes Polygon/MultiPolygon. Atributos `properties.name` vêm de `topojson/dist/country_names_iso_codes.json`; as unidades dos EUA são traduzidas por uma tabela controlada de códigos de estados e DC. Cada feature também expõe `properties.iso3` com o identificador original (para os EUA é código de estado).
- **Plotly**: TopoJSON original é servido exclusivamente pelo interceptador CDP `Fetch.fulfillRequest` para a origem sintética `https://plotarium.invalid/assets/topojson/`. O domínio `.invalid` nunca é acessado pela rede; o corpo é lido dos assets verificados. Qualquer URL de provedor externo ou tile é bloqueada.
- **Licenças**: MIT do Plotly original preservada em `assets/licenses/plotly-LICENSE`. Os dados do Plotly derivam de Natural Earth, e devem manter atribuição/proveniência da distribuição original. Não usar os mapas para definir fronteiras jurídicas oficiais.

## Mapas ECharts disponíveis

`world`, `world-50m`, `africa`, `asia`, `europe`, `north-america`, `south-america`, `oceania`, `usa`. Para ECharts, `map` é campo obrigatório quando `series.type="map"`. `name` precisa coincidir com as strings da base: "Brazil", "China", "California", etc. `properties.iso3` pode ajudar com integração de códigos caso o spec escolha `nameProperty` adequado. O arquivo é dado estático: não inclui mosaicos/tile imagery nem mapas atualizados dinamicamente.

## Regerar os arquivos

```sh
python3 scripts/generate_geodata.py /path/to/plotly.js-main.zip
```

O script usa apenas Python 3 e a biblioteca padrão; gera os arquivos no diretório `assets/` e refaz as entradas geográficas do manifesto com SHA-256, tamanhos e membros de origem. O ZIP `0.2.0` já inclui os resultados e não precisa do arquivo externo em runtime.
