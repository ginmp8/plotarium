window.PlotariumAdapters ??= Object.create(null);
window.PlotariumAdapters.plotly = Object.freeze({
  async mount({host,assets,request,loadScript,selection}) {
    await loadScript(assets+'plotly/plotly.min.js');
    const figure=structuredClone(request.spec);
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
