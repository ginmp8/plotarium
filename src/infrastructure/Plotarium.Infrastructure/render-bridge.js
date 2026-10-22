/* Plotarium's owned bridge. User input crosses CDP as JSON values, never source code. */
let plotariumMermaid;
window.Plotarium = Object.freeze({
  async loadMermaid(value) {
    const imports = Object.create(null);
    for (const [name, code] of Object.entries(value.modules)) {
      imports[name] = URL.createObjectURL(new Blob([code], { type: 'text/javascript' }));
    }
    const map = document.createElement('script');
    map.type = 'importmap';
    map.nonce = value.nonce;
    map.textContent = JSON.stringify({ imports });
    document.head.append(map);
    plotariumMermaid = (await import('@plotarium/assets/mermaid/mermaid.esm.min.mjs')).default;
    return { loaded: true };
  },
  loadGeoMaps(value) {
    for (const [name, json] of Object.entries(value)) {
      echarts.registerMap(name, { geoJSON: JSON.parse(json) });
    }
    return { registered: Object.keys(value) };
  },
  async render(request) {
    const host = document.getElementById('chart');
    host.style.width = `${request.width}px`;
    host.style.height = `${request.height}px`;
    document.body.style.background = request.background;
    let svg;
    if (request.engine === 'echarts') {
      const options = structuredClone(request.spec);
      const gl = Boolean(options.globe || options.grid3D || [options.series].flat().some(x => x && ['bar3D','scatter3D','line3D','lines3D','surface','map3D','polygons3D','graphGL'].includes(x.type)));
      const chart = echarts.init(host, request.theme === 'dark' ? 'dark' : null,
        { renderer: gl ? 'canvas' : 'svg', width: request.width, height: request.height });
      try {
        options.animation = false;
        options.backgroundColor = request.background;
        chart.setOption(options, { notMerge: true, lazyUpdate: false });
        if (gl) {
          await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          window.__plotariumActiveChart = chart;
          return { capture: true }; // GL viewport remains alive until CDP captures pixels.
        }
        svg = chart.renderToSVGString();
      } finally { if (!gl) chart.dispose(); }
    } else if (request.engine === 'plotly') {
      const figure = structuredClone(request.spec);
      if (request.offlineWorldGeoJson) {
        // CDP's navigation origin is intentionally opaque and Chromium's administrative network policy
        // prevents MapLibre blob-worker startup. Preserve geographic semantics via Plotly's offline geo engine.
        // This conversion is explicit and visible as an output strategy; densitymap cannot be represented faithfully.
        figure.data = figure.data.map(t => {
          if (t.type === 'scattermap') return { ...t, type: 'scattergeo' };
          if (t.type === 'choroplethmap') return { ...t, type: 'choropleth' };
          if (t.type === 'densitymap') throw new Error('densitymap needs a MapLibre worker and is unavailable in this environment');
          return t;
        });
        figure.layout = { ...(figure.layout || {}) };
        const map = figure.layout.map || {};
        delete figure.layout.map;
        figure.layout.geo = { projection: { type: 'equirectangular', scale: Math.max(0.75,Number(map.zoom || 1)) },
          center: map.center || { lat: 0, lon: 0 }, showland: true, showocean: true,
          landcolor: request.theme === 'dark' ? '#3b5664' : '#d1d8cd',
          oceancolor: request.theme === 'dark' ? '#13222c' : '#dcecf4',
          bgcolor: 'rgba(0,0,0,0)', ...(figure.layout.geo || {}) };
      }
      const layout = Object.assign({}, figure.layout || {}, {
        width: request.width, height: request.height, autosize: false,
        paper_bgcolor: request.background === 'transparent' ? 'rgba(0,0,0,0)' : request.background
      });
      if (request.theme === 'dark') {
        layout.font = Object.assign({ color: '#f0f0f0' }, layout.font);
        if (!layout.plot_bgcolor) layout.plot_bgcolor = '#202124';
      }
      try {
        await Plotly.newPlot(host, figure.data, layout, {
          staticPlot: true, displayModeBar: false, responsive: false,
          topojsonURL: 'https://plotarium.invalid/assets/topojson/'
        });
        // WebGL and MapLibre canvases must be captured as raster pixels. Never return a blank SVG.
        const rasterTraces = new Set(['scattergl', 'scatterpolargl', 'scatter3d', 'surface', 'mesh3d',
          'cone', 'streamtube', 'volume', 'isosurface', 'splom', 'scattermap', 'choroplethmap', 'densitymap']);
        if (figure.data.some(trace => rasterTraces.has(trace.type))) {
          if (!document.createElement('canvas').getContext('webgl'))
            throw new Error('WebGL is unavailable in this Chromium environment');
          await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          try {
            const png = await Plotly.toImage(host, { format: 'png', width: request.width, height: request.height });
            if (png.startsWith('data:image/png;base64,')) return { png: png.slice('data:image/png;base64,'.length) };
          } catch (_) { /* CDP canvas capture is still available */ }
          return { capture: true };
        }
        try {
          const dataUrl = await Plotly.toImage(host, { format: 'svg', width: request.width, height: request.height });
          const comma = dataUrl.indexOf(',');
          svg = dataUrl.slice(0, comma).includes(';base64') ? atob(dataUrl.slice(comma + 1)) : decodeURIComponent(dataUrl.slice(comma + 1));
        } catch (_) {
          // A rendering type may not have an SVG exporter, but live DOM is still rendered.
          return { capture: true };
        }
      } finally { if (svg) Plotly.purge(host); }
    } else if (request.engine === 'mermaid') {
      const mermaid = plotariumMermaid;
      if (!mermaid) throw new Error('Mermaid not initialized');
      mermaid.initialize({
        startOnLoad: false, securityLevel: 'strict', htmlLabels: false,
        theme: request.theme === 'dark' ? 'dark' : 'default',
        maxTextSize: 100000, maxEdges: 2000,
        flowchart: { htmlLabels: false, useMaxWidth: false },
        sequence: { useMaxWidth: false },
        secure: ['securityLevel', 'startOnLoad', 'maxTextSize', 'maxEdges', 'htmlLabels']
      });
      const result = await mermaid.render('plotarium-diagram', request.source);
      const doc = new DOMParser().parseFromString(result.svg, 'image/svg+xml');
      const root = doc.documentElement;
      if (root.localName !== 'svg') throw new Error('Invalid Mermaid SVG');
      root.setAttribute('width', request.width);
      root.setAttribute('height', request.height);
      root.setAttribute('preserveAspectRatio', 'xMidYMid meet');
      root.style.maxWidth = 'none';
      svg = new XMLSerializer().serializeToString(root);
    } else throw new Error('Unknown engine');
    if (!svg || !svg.includes('<svg')) throw new Error('Empty SVG');
    return { svg };
  },
  async present(value) {
    // Rasterize the exact SVG accepted by the server, not the live interactive DOM.
    document.body.replaceChildren();
    document.body.style.background = value.background;
    const image = document.createElement('img');
    image.width = value.width;
    image.height = value.height;
    image.style.display = 'block';
    image.src = 'data:image/svg+xml;base64,' + value.svg;
    document.body.append(image);
    await image.decode();
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    return { ready: true };
  }
});
