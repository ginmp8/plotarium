#!/usr/bin/env python3
"""Optional Playwright component smoke for the interactive viewer.

Uses a real Chromium executing the actual bundled engine+adapter+viewer scripts,
but injects the HTML and fixture responses via set_content: managed environments
can forbid localhost page navigation. This does NOT test network navigation or
Teams/WhatsApp/ChatGPT host integration. HTTP chart creation and the viewer HTML
are still fetched from the real Ploteus server.
"""
import argparse
import json
import re
import shutil
import sys
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.parse import urlparse

try:
    from playwright.sync_api import sync_playwright
except ImportError:
    sys.exit("Optional UI tests require Python Playwright: python -m pip install playwright")

ROOT=Path(__file__).resolve().parents[2]
UI=ROOT/'src/interfaces/Ploteus.Api/Interactive'

def get(url):
    with urlopen(url, timeout=15) as r:
        return r.read().decode('utf-8')

def post(url, data):
    req=Request(url,data=json.dumps(data).encode(),headers={'Content-Type':'application/json'},method='POST')
    with urlopen(req,timeout=15) as r:
        assert r.status==201,(url,r.status)
        return json.loads(r.read())

def run_one(browser,base,engine,request,geo,output):
    chart=post(base+'/v1/charts',request)
    page_html=get(base+chart['viewUrl'])
    page_html=re.sub(r'<script[^>]+src="/v1/viewer/viewer.js"[^>]*></script>','',page_html)
    page_html=re.sub(r'<link[^>]+viewer.css[^>]*>','',page_html)
    page=browser.new_page(viewport={'width':1320,'height':830})
    errors=[]
    page.on('pageerror',lambda e: errors.append(str(e)))
    page.set_content(page_html)
    page.add_style_tag(content=(UI/'viewer.css').read_text())
    file='echarts/echarts.min.js' if engine=='echarts' else 'plotly/plotly.min.js'
    page.add_script_tag(content=(ROOT/'assets'/file).read_text())
    adapter=(UI/'adapters'/f'{engine}.js').read_text()
    if engine=='plotly':
        # about:blank has opaque origin; supply the real API origin for URL construction in this fixture.
        adapter=adapter.replace('location.origin',json.dumps(base))
    page.add_script_tag(content=adapter)
    page.evaluate('''args=>{
       const [config,geojson]=args;
       window.fetch=async url=>({ok:true,status:200,json:async()=>String(url).endsWith('.geojson')?geojson:config});
       document.head.append=(...elements)=>{for(const el of elements)if(el.tagName==='SCRIPT')queueMicrotask(()=>el.dispatchEvent(new Event('load')));};
    }''',[{'request':request,'id':chart['id'],'expiresAt':chart['expiresAt']},geo])
    page.add_script_tag(content=(UI/'viewer.js').read_text())
    page.locator('#state').wait_for(state='hidden',timeout=30000)
    assert page.locator('#chart').inner_html(),f'{engine}: empty DOM'
    page.locator('#zoomin').click()
    assert page.locator('#status').inner_text()=='120%'
    if engine=='plotly':
        page.locator('#theme').click();page.locator('#state').wait_for(state='hidden',timeout=30000)
        assert page.evaluate("document.querySelector('#chart')._fullLayout.plot_bgcolor")=='#17232c'
    page.screenshot(path=str(output/(engine+('-world' if geo is not None else '')+'-interactive.png')))
    if errors:raise AssertionError(errors)
    page.close()
    return {'engine':engine,'world_map':geo is not None,'status':'pass','zoom':'120%','page_errors':errors}

def main():
    arg=argparse.ArgumentParser();arg.add_argument('--base-url',default='http://localhost:5000');arg.add_argument('--chromium',default=None);arg.add_argument('--output-dir',default=str(ROOT/'artifacts/ui'));args=arg.parse_args()
    base=args.base_url.rstrip('/')
    if urlparse(base).scheme not in ('http','https'):sys.exit('base-url must be http(s)')
    chromium=args.chromium or shutil.which('chromium') or shutil.which('google-chrome')
    if not chromium:sys.exit('Supply --chromium path for operator-installed Chromium')
    out=Path(args.output_dir);out.mkdir(parents=True,exist_ok=True)
    simple_e={'engine':'echarts','theme':'light','spec':{'xAxis':{'type':'category','data':['Jan','Fev','Mar']},'yAxis':{'type':'value'},'series':[{'type':'bar','data':[10,25,18]}]}}
    simple_p={'engine':'plotly','theme':'light','spec':{'data':[{'type':'scatter','mode':'lines+markers','x':[1,2,3],'y':[6,12,10]}]}}
    map_e=json.loads((ROOT/'examples/interactive/echarts-world.json').read_text())
    world=json.loads((ROOT/'assets/geojson/world.geojson').read_text())
    with sync_playwright() as p:
        browser=p.chromium.launch(executable_path=chromium,headless=True)
        results=[run_one(browser,base,'echarts',simple_e,None,out),run_one(browser,base,'plotly',simple_p,None,out),run_one(browser,base,'echarts',map_e,world,out)]
        browser.close()
    print(json.dumps({'passed':len(results),'failed':0,'mode':'component fixture; not a real host navigation', 'results':results},indent=2))
if __name__=='__main__':main()
