window.PlotariumAdapters ??= Object.create(null);
window.PlotariumAdapters.plotly = Object.freeze({
  async mount({host,assets,request,loadScript,selection}) {
    await loadScript(assets+'plotly/plotly.min.js');
    const figure=structuredClone(request.spec);
    if(figure.data.some(trace=>['scattermap','densitymap','choroplethmap'].includes(trace.type))) {
      const response=await fetch(assets+'geojson/world.geojson',{cache:'no-store'});
      if(!response.ok)throw new Error('Offline GeoJSON unavailable');
      const world=await response.json();
      const dark=request.theme==='dark';
      const offlineStyle={version:8,sources:{world:{type:'geojson',data:world}},layers:[
        {id:'background',type:'background',paint:{'background-color':dark?'#13222c':'#dcecf4'}},
        {id:'land',type:'fill',source:'world',paint:{'fill-color':dark?'#3b5664':'#d1d8cd','fill-outline-color':dark?'#67808a':'#9bafa6'}}]};
      figure.layout={...(figure.layout??{}),map:{center:{lat:0,lon:0},zoom:0.65,...(figure.layout?.map??{}),style:offlineStyle}};
      for(const t of figure.data)if(t.type==='choroplethmap'&&!t.geojson){t.geojson=world;t.featureidkey=t.featureidkey??'properties.iso3';}
    }
    const layout={...(figure.layout??{}),autosize:true,paper_bgcolor:request.theme==='dark'?'#17232c':'#ffffff'};
    if(request.theme==='dark') {
      layout.font={color:'#eaf0f4',...(layout.font??{})};
      layout.plot_bgcolor=layout.plot_bgcolor??'#17232c';
      for(const axis of ['xaxis','yaxis']) layout[axis]={gridcolor:'#364552',zerolinecolor:'#364552',...(layout[axis]??{})};
    }
    await Plotly.newPlot(host,figure.data,layout,{
      responsive:true,scrollZoom:true,displayModeBar:true,displaylogo:false,
      topojsonURL:new URL(assets+'topojson/',location.origin).href
    });
    if(typeof host.on==='function')host.on('plotly_click',ev=>{
      const point=ev.points?.[0];if(point)selection.textContent='Selecionado: '+String(point.text??point.label??point.x??'elemento');
    });
    return {
      resize:()=>{void Plotly.Plots.resize(host).catch(()=>{});},
      reset:()=>{void Plotly.relayout(host,{'xaxis.autorange':true,'yaxis.autorange':true}).catch(()=>{});},
      dispose:()=>Plotly.purge(host)
    };
  }
});
