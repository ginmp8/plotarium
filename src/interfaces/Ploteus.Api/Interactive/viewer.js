/* Generic Ploteus viewer; individual engine behavior lives in packaged adapters. */
(() => {
  'use strict';
  const id=document.body.dataset.chartId;
  const host=document.getElementById('chart');
  const state=document.getElementById('state');
  const selection=document.getElementById('selection');
  const status=document.getElementById('status');
  const themeButton=document.getElementById('theme');
  const assets='/v1/assets/';
  let request,mounted,scale=1,disposed=false;
  const scripts=new Map();
  function show(message,isError=false){state.textContent=message;state.classList.remove('hidden');state.classList.toggle('error',isError);}
  function loadScript(src){
    if(!scripts.has(src))scripts.set(src,new Promise((resolve,reject)=>{
      const script=document.createElement('script');script.async=true;script.src=src;
      script.addEventListener('load',resolve,{once:true});script.addEventListener('error',()=>reject(new Error('Não foi possível carregar um componente local.')),{once:true});document.head.append(script);
    }));
    return scripts.get(src);
  }
  function adjustScale(){host.style.transform=`scale(${scale})`;status.textContent=Math.round(scale*100)+'%';}
  async function cleanup(){if(mounted){await mounted.dispose();mounted=undefined;}host.replaceChildren();}
  async function draw(){
    if(disposed)return;
    show('Carregando visualização interativa…');await cleanup();
    await loadScript('/v1/viewer/adapters/'+request.engine+'.js');
    const adapter=window.PloteusAdapters?.[request.engine];
    if(!adapter||typeof adapter.mount!=='function')throw new Error('Adaptador indisponível');
    mounted=await adapter.mount({host,request,assets,selection,loadScript});
    if(!disposed)state.classList.add('hidden');
  }
  document.getElementById('zoomin').addEventListener('click',()=>{scale=Math.min(3,Math.round((scale+.2)*10)/10);adjustScale();});
  document.getElementById('zoomout').addEventListener('click',()=>{scale=Math.max(.5,Math.round((scale-.2)*10)/10);adjustScale();});
  document.getElementById('reset').addEventListener('click',()=>{scale=1;adjustScale();mounted?.reset?.();});
  themeButton.addEventListener('click',async()=>{if(!request)return;request.theme=request.theme==='dark'?'light':'dark';document.documentElement.dataset.theme=request.theme;themeButton.textContent=request.theme==='dark'?'Tema claro':'Tema escuro';try{await draw();}catch(e){show(e.message,true);}});
  document.getElementById('download').addEventListener('click',()=>{const link=document.createElement('a');link.href=`/v1/charts/${id}/preview?format=png`;link.download=`ploteus-${request?.engine??'chart'}.png`;link.click();});
  document.getElementById('definition').addEventListener('click',()=>{const link=document.createElement('a');link.href=`/v1/charts/${id}/definition`;link.download='chart.json';link.click();});
  document.getElementById('share').addEventListener('click',async()=>{
    const link=new URL(`/v1/charts/${id}/view`,location.origin).href;
    try{await navigator.clipboard.writeText(link);selection.textContent='Link copiado para a área de transferência';}
    catch{selection.textContent=link;}
  });
  const observer=new ResizeObserver(()=>{mounted?.resize?.();});observer.observe(host);
  window.addEventListener('pagehide',()=>{disposed=true;observer.disconnect();void cleanup();},{once:true});
  (async()=>{
    try{
      if(!/^[a-f0-9]{32}$/.test(id||''))throw new Error('Identificador inválido');
      const response=await fetch(`/v1/charts/${id}/definition`,{cache:'no-store'});
      if(!response.ok)throw new Error(response.status===404?'Gráfico expirado ou inexistente':`HTTP ${response.status}`);
      const data=await response.json();request=data.request;
      document.getElementById('engine').textContent=request.engine.toUpperCase();
      document.documentElement.dataset.theme=request.theme;
      themeButton.textContent=request.theme==='dark'?'Tema claro':'Tema escuro';
      document.getElementById('expires').textContent='Expira: '+new Date(data.expiresAt).toLocaleString();
      await draw();
    }catch(e){show(e.message||'Não foi possível abrir o gráfico',true);}
  })();
})();
