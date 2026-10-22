# Frozen acceptance contract - Plotarium 0.1.0-alpha.1

Source reconstruction is independent; donor server compatibility is not promised.

- ASP.NET Core 10 API, no Node or Python runtime. Chromium explicitly required for binary outputs.
- Engines: ECharts 6.1.0, Plotly 4.1.2, Mermaid 12.1.0 from supplied pinned browser assets.
- POST /v1/render; PNG, SVG, JSON, JPEG, WebP, PDF. JSON is a versioned reusable definition envelope, not an engine AST.
- Native ECharts JSON and Plotly data/layout JSON; Mermaid source text. Never evaluate client JavaScript.
- Validate shape, duplicate properties, budgets and forbidden policy overrides before rendering. Reject unsupported formats.
- Per-job owned Chromium process, bounded concurrency/queue and deadline, cleanup in all outcomes. No pooling in this release.
- Local pinned assets, no remote content or user URLs. Browser sandbox must remain enabled. Explicit operator-installed browser.
- No database, stored chart links, billing, GUI, 3D extension promises, remote data sources, callbacks, HTML labels or public service deployment.
- Verify core with executed .NET tests. Full API/browser integration is a distinct gate requiring actual packages and runtime.
- Deliver independent implementation, source evidence, limitations, acquisition/validation receipts and persistent Library verification.

These acceptance conditions were recorded before implementation of validation/rendering logic. Later changes must be recorded, not silently made to bless failures.

Nota de versao: 0.2.0 altera controle de acesso e renderer por solicitacao explicita; use docs/validation.md e docs/security.md para os gates atuais.


## Criterios adicionais para 0.3.0

- HTML interativo servido com assets locais e definicao validada; zoom, tema, reset e visualizacoes ECharts/Plotly confirmados em teste de componente isolado; Mermaid segue as restricoes da engine (sem callbacks arbitrarios).
- `POST /v1/charts` 201 cria links aleatorios temporarios; `GET /view|embed|definition|preview` retornam conteudos estruturados e binarios correspondentes; IDs desconhecidos retornam 404.
- `GET /share/teams|whatsapp|chatgpt` descreve canais, sem fazer entrega a terceiros; `/mcp` responde aos metodos do subconjunto documentado.
- Preservar seis formatos de renderizacao preexistentes para as 3 engines, sem autenticacao, e documentar riscos de publicacao.
