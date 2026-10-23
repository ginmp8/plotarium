# Ploteus 0.6.0 — validation record

The authoritative validation for this distribution is generated from the current Ploteus assemblies and lives under `evidence/0.6.0/`, not historical release snapshots. All historical documentation is contextual; it is not evidence that this renamed build was tested.

Applicable proof categories:

- .NET 10 restore/build of the renamed complete solution (`Ploteus.slnx`, `Any CPU`).
- Contract suite including Ploteus assembly identity and version, JSON parser, schema validation and GET payload handling.
- HTTP legacy regression suite covering image formats, temporary chart/viewer routes, sharing descriptors and MCP.
- HTTP stateless suite covering inline JSON, compressed JSON, allowlisted remote URL, configured provider and SSRF/error cases.
- Real-Chromium, CSP-constrained standalone HTML browser exercises for ECharts, Plotly, Mermaid and GL static fallback.
- Standalone GL/geographic PNG/SVG/PDF feature-smoke checks when executable in the available browser environment.
- An explicit brand-identity and release ZIP member/byte scan, separate from the build and tests.
- `dotnet publish` from the renamed API project, new ZIP CRC/content hash validation, and Library-side persistence verification.

The latest execution counts, status and any unavailable validation are recorded in `evidence/0.6.0/validation.json` and the published release report. Passing local tests does not prove an external vendor integration or Windows/macOS compatibility.
