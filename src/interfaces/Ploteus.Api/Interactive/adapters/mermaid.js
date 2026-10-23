window.PloteusAdapters ??= Object.create(null);
window.PloteusAdapters.mermaid = Object.freeze({
  async mount({host,assets,request}) {
    const module=await import(assets+'mermaid/mermaid.esm.min.mjs');
    const diagram=module.default;
    diagram.initialize({startOnLoad:false,securityLevel:'strict',htmlLabels:false,
      theme:request.theme==='dark'?'dark':'default',maxTextSize:50000,maxEdges:2000,
      flowchart:{htmlLabels:false,useMaxWidth:false},
      secure:['securityLevel','startOnLoad','htmlLabels','maxTextSize','maxEdges']});
    const result=await diagram.render('ploteus-'+Math.random().toString(36).slice(2),request.source);
    // An image object (not active inline SVG markup) ensures that links/callbacks cannot execute.
    const url=URL.createObjectURL(new Blob([result.svg],{type:'image/svg+xml'}));
    const img=document.createElement('img');img.alt='Diagrama Mermaid';img.src=url;host.append(img);
    return {resize:()=>{},reset:()=>{},dispose:()=>URL.revokeObjectURL(url)};
  }
});
