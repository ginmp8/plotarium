

### 1. ECharts — PNG

```
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"echarts","format":"png","width":800,"height":450,"spec":{"xAxis":{"type":"category","data":["Jan","Fev","Mar"]},"yAxis":{"type":"value"},"series":[{"type":"bar","data":[10,20,15]}]}}'
```

### 2. ECharts — SVG

```
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"echarts","format":"svg","width":800,"height":450,"spec":{"series":[{"type":"pie","data":[{"name":"A","value":30},{"name":"B","value":70}]}]}}'
```

### 3. Plotly — PNG

```
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"plotly","format":"png","width":800,"height":450,"spec":{"data":[{"type":"bar","x":["Jan","Fev","Mar"],"y":[10,20,15]}],"layout":{"title":{"text":"Vendas mensais"}}}}'
```

### 4. Mermaid — SVG

```
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"mermaid","format":"svg","width":800,"height":450,"source":"flowchart LR\nA[Inicio] --> B{Valido?}\nB -->|Sim| C[Sucesso]\nB -->|Nao| D[Erro]"}'
```

### 5. Mermaid — PDF

```
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"mermaid","format":"pdf","width":800,"height":450,"source":"sequenceDiagram\nCliente->>API: Requisicao\nAPI->>Renderer: Renderizar\nRenderer-->>API: PDF\nAPI-->>Cliente: Resposta"}'
```

### 6. ECharts — JSON (sem precisar do Chromium)

```
curl --location 'https://localhost:55731/v1/render' --header 'Content-Type: application/json' --data-raw '{"engine":"echarts","format":"json","spec":{"series":[{"type":"bar","data":[10,20,15]}]}}'
```

Todos os exemplos usam a rota correta: `POST /v1/render`.