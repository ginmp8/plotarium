async function (payload) {
  "use strict";
  const r = JSON.parse(payload);
  const chart = document.getElementById("chart");
  chart.style.width = `${r.width}px`;
  chart.style.height = `${r.height}px`;
  chart.style.background = r.background;
  document.body.style.background = r.background;
  const dark = r.theme === "dark";
  const foreground = dark ? "#eeeeee" : "#333333";
  const load = url => new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = url; script.onload = resolve; script.onerror = () => reject(new Error("asset_load"));
    document.head.appendChild(script);
  });
  await document.fonts.ready;
  const frame = () => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  if (r.engine === "echarts") {
    await load("/assets/echarts/echarts.min.js");
    const instance = echarts.init(chart, dark ? "dark" : null, {renderer:"svg",width:r.width,height:r.height});
    const option = {...r.spec,animation:false,backgroundColor:r.background};
    // Inputs cannot supply functions. Tooltips are disabled because this is a static renderer.
    option.tooltip = {show:false};
    instance.setOption(option,{notMerge:true,lazyUpdate:false});
    await frame();
    const element = chart.querySelector("svg");
    if (!element) throw new Error("svg_missing");
    const svg = new XMLSerializer().serializeToString(element);
    if (r.format === "svg") instance.dispose();
    return svg;
  }
  if (r.engine === "plotly") {
    await load("/assets/plotly/plotly.min.js");
    const layout = {...r.spec.layout,width:r.width,height:r.height,autosize:false,
      paper_bgcolor:r.background,plot_bgcolor:r.background,
      font:{color:foreground,...(r.spec.layout?.font??{})}};
    const config = {staticPlot:true,responsive:false,displayModeBar:false,scrollZoom:false,showLink:false,showSendToCloud:false};
    const problems=Plotly.validate(r.spec.data,layout);
    if(problems?.length) throw new Error("plotly_spec");
    await Plotly.newPlot(chart,r.spec.data,layout,config);
    await document.fonts.ready; await frame();
    if(r.format === "svg") {
      const uri=await Plotly.toImage(chart,{format:"svg",width:r.width,height:r.height});
      const comma=uri.indexOf(",");
      if(comma<0 || !uri.startsWith("data:image/svg+xml")) throw new Error("svg_encoding");
      const svg=uri.slice(0,comma).includes(";base64")?atob(uri.slice(comma+1)):decodeURIComponent(uri.slice(comma+1));
      Plotly.purge(chart);return svg;
    }
    return ""; // Binary formats capture the completed page, including canvas/WebGL layers.
  }
  if(r.engine === "mermaid") {
    const {default:mermaid}=await import("/assets/mermaid/mermaid.esm.min.mjs");
    mermaid.initialize({startOnLoad:false,securityLevel:"strict",theme:dark?"dark":"default",
      deterministicIds:true,deterministicIDSeed:"plotarium",maxTextSize:50000,maxEdges:2000,
      htmlLabels:false,flowchart:{htmlLabels:false,useMaxWidth:false}});
    await mermaid.parse(r.source);
    const rendered=await mermaid.render("plotarium-diagram",r.source);
    const xml=new DOMParser().parseFromString(rendered.svg,"image/svg+xml");
    if(xml.querySelector("parsererror")) throw new Error("svg_xml");
    const svg=xml.documentElement;
    svg.setAttribute("width",String(r.width));svg.setAttribute("height",String(r.height));
    svg.setAttribute("preserveAspectRatio","xMidYMid meet");svg.style.maxWidth="none";
    svg.style.background=r.background;
    chart.replaceChildren(document.importNode(svg,true));
    await document.fonts.ready;await frame();
    return new XMLSerializer().serializeToString(chart.firstElementChild);
  }
  throw new Error("engine");
}
