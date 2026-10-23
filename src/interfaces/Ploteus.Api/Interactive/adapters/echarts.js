/* Trusted engine adapter: capabilities come from the server, specifications remain JSON-only. */
window.PloteusAdapters ??= Object.create(null);
window.PloteusAdapters.echarts = Object.freeze({
  async mount({host,assets,request,loadScript,selection}) {
    await loadScript(assets+'echarts/echarts.min.js');
    const useGl = Boolean(request.spec?.globe || request.spec?.grid3D || [request.spec?.series].flat().some(x =>
      x && ['bar3D','scatter3D','line3D','lines3D','surface','map3D','polygons3D','graphGL'].includes(x.type)));
    if(useGl) await loadScript(assets+'echarts-gl/echarts-gl.min.js');
    const opts=structuredClone(request.spec);
    const maps=new Set();
    function add(value){ if(value && typeof value.map==='string')maps.add(value.map); }
    for(const part of [opts.geo,opts.series]) Array.isArray(part)?part.forEach(add):add(part);
    for(const name of maps){
      const r=await fetch(assets+'geojson/'+encodeURIComponent(name)+'.geojson',{cache:'no-store'});
      if(!r.ok)throw new Error('Mapa local indisponível');
      echarts.registerMap(name,await r.json());
    }
    const chart=echarts.init(host,request.theme==='dark'?'dark':null,{renderer:'canvas'});
    try {
      opts.animation=false;
      if(!opts.tooltip)opts.tooltip={trigger:'item'};
      chart.setOption(opts,{notMerge:true});
      chart.on('click',event=>{selection.textContent='Selecionado: '+String(event.name??'elemento');});
    }catch(e){chart.dispose();throw e;}
    return {resize:()=>chart.resize(),reset:()=>chart.dispatchAction({type:'dataZoom',start:0,end:100}),dispose:()=>chart.dispose()};
  }
});
