# Amostras de saida - Ploteus 0.1.1

Arquivos reais produzidos no teste HTTP contra o publish ASP.NET Core 10, sem credenciais, com Chromium Linux 144.0.7559.96. Incluem PNG, SVG e PDF para ECharts, Plotly e Mermaid. Nao sao imagens geradas artificialmente por testes mock. ECharts usou barras; Plotly usou scatter/line; Mermaid usou flowchart. As 18 combinacoes sao exercitadas por `tests/Ploteus.HttpTests`, e os SHA256 estao em `evidence/0.6.0/validation.json`.

O browser foi iniciado com sandbox desabilitado somente para Desenvolvimento nesse contêiner isolado; isso nao valida o ambiente de producao.
