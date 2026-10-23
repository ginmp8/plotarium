#!/usr/bin/env python3
"""Render standalone GET HTML entirely from memory in real Chromium (no second fetch)."""
import json
from html import escape
import os
from pathlib import Path
import socket
import subprocess
import time
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import urlopen
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parent.parent
APP=Path(os.environ.get('PLOTEUS_API_DLL', str(ROOT/'src/interfaces/Ploteus.Api/bin/Release/net10.0/Ploteus.Api.dll'))).resolve()
DOTNET=os.environ.get('PLOTEUS_DOTNET','dotnet')

def free_port():
    with socket.socket() as s:
        s.bind(('127.0.0.1',0));return s.getsockname()[1]

port=free_port()
base=f'http://127.0.0.1:{port}'
env=os.environ.copy()
env.update({'ASPNETCORE_URLS':base,'ASPNETCORE_ENVIRONMENT':'Development',
            'Ploteus__Browser__ExecutablePath':'/usr/bin/chromium',
            'Ploteus__Browser__DisableSandbox':'true',
            'Ploteus__Browser__EnableSoftwareWebGl':'true',
            'Ploteus__Browser__AssetsDirectory':str(ROOT/'assets')})
result=[]
with (ROOT/'artifacts-060-html-server.log').open('wb') as log:
    process=subprocess.Popen([DOTNET,str(APP)],env=env,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
    try:
        for _ in range(100):
            if process.poll() is not None:raise RuntimeError('API failed during startup')
            try:
                with urlopen(base+'/health/live',timeout=1) as r:
                    if r.status==200:break
            except OSError:time.sleep(.15)
        else:raise RuntimeError('API did not start')
        cases=[
            ('echarts',{'engine':'echarts','format':'html','spec':{'xAxis':{'type':'category','data':['A','B']},'yAxis':{'type':'value'},'series':[{'type':'bar','data':[2,4]}]}},'canvas'),
            ('plotly',{'engine':'plotly','format':'html','spec':{'data':[{'type':'scatter','mode':'lines+markers','x':[1,2,3],'y':[3,2,5]}],'layout':{'title':{'text':'Plotly offline'}}}},'.js-plotly-plot'),
            ('mermaid',{'engine':'mermaid','format':'html','source':'flowchart LR\nA[Start] --> B[Finish]'},'#chart img'),
            ('echarts-gl',{'engine':'echarts','format':'html','spec':{'xAxis3D':{'type':'category','data':['A','B']},'yAxis3D':{'type':'category','data':['C']},'zAxis3D':{'type':'value'},'grid3D':{},'series':[{'type':'bar3D','data':[[0,0,3],[1,0,7]]}]}},'canvas')]
        with sync_playwright() as playwright:
            browser=playwright.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox','--disable-dev-shm-usage','--enable-unsafe-swiftshader','--use-angle=swiftshader','--enable-webgl'])
            for name,payload,selector in cases:
                print("START CASE",name,flush=True)
                try:
                    query=urlencode({'c':json.dumps(payload,separators=(',',':')),'format':'html'})
                    with urlopen(base+'/v1/render?'+query,timeout=45) as http:
                        html=http.read().decode()
                        csp=http.headers.get('Content-Security-Policy','')
                    if not csp.startswith("default-src 'none'"):raise ValueError('Missing restrictive CSP')
                    if '<script src=' in html or '<link rel="stylesheet" href=' in html:
                        raise ValueError('HTML contains external script/style fetch')
                    page=browser.new_page(viewport={'width':1000,'height':650})
                    errors=[];requests=[]
                    page.on('pageerror',lambda error:errors.append(str(error)[:200]))
                    page.on('request',lambda request:requests.append(request.url))
                    html_with_csp=html.replace('<head>', '<head><meta http-equiv="Content-Security-Policy" content="'+escape(csp,quote=True)+'">',1)
                    page.set_content(html_with_csp,wait_until='domcontentloaded',timeout=15000)
                    print('SET CONTENT',name,flush=True)
                    if name=='echarts-gl':
                        page.locator('canvas, #chart img').first.wait_for(state='attached',timeout=15000)
                    else:
                        page.locator(selector).first.wait_for(state='attached',timeout=15000)
                    print('LOCATOR FOUND',name,flush=True)
                    if name=='mermaid':
                        page.wait_for_function("document.querySelector('#chart img')?.naturalWidth>0",timeout=15000)
                    else:
                        page.wait_for_timeout(900)
                    initial=page.locator('#chart').evaluate('(el)=>el.style.transform')
                    page.get_by_role('button',name='+').click()
                    zoomed=page.locator('#chart').evaluate('(el)=>el.style.transform')
                    page.get_by_role('button',name='Reset',exact=True).click()
                    reset=page.locator('#chart').evaluate('(el)=>el.style.transform')
                    state=page.locator('#state').inner_text()
                    good=zoomed!=initial and 'scale(1)' in reset and (not state or name=='echarts-gl' and 'static preview' in state) and (not errors or name=='echarts-gl' and 'static preview' in state) and not requests
                    path=ROOT.parent/'html-preview'/f'{name}.png';path.parent.mkdir(parents=True,exist_ok=True)
                    page.screenshot(path=str(path),full_page=True,timeout=10000)
                    print('SCREENSHOT DONE',name,flush=True)
                    result.append({'case':name,'status':'pass' if good else 'fail','errors':errors,'requests':requests,'state':state,'bytes':len(html),'screenshot':str(path)})
                    page.close()
                except Exception as err:
                    result.append({'case':name,'status':'fail','error':str(err)})
                    print('CASE ERROR',name,str(err)[:200],flush=True)
                    try:
                        print('STATE',page.locator('#state').text_content()[:500],flush=True)
                        print('PAGEERRORS',errors,flush=True)
                        print('requests',requests,flush=True)
                        page.screenshot(path=str(ROOT.parent/'html-preview'/f'{name}-failure.png'),timeout=8000)
                    except Exception as debug_ex:
                        print('DIAGNOSTIC FAILED',str(debug_ex)[:200],flush=True)
            browser.close()
    finally:
        process.terminate()
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:process.kill()
failed=sum(x['status']!='pass' for x in result)
report={'release':'0.6.0','passed':len(result)-failed,'failed':failed,'results':result,'scope':'Chromium set_content with fully inlined assets; no HTTP navigation to live host'}
folder=ROOT/'evidence/0.6.0';folder.mkdir(parents=True,exist_ok=True)
(folder/'html-browser-tests.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if failed==0 else 1)
