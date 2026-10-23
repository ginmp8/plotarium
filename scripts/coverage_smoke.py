#!/usr/bin/env python3
"""Run every bundled Ploteus type specimen through the live HTTP endpoint.
Usage: python3 scripts/coverage_smoke.py http://127.0.0.1:55820 [output-dir] [workers]
No credentials. Python 3.10+ stdlib only. Requires a running API and Chromium.
"""
import concurrent.futures
import hashlib
import json
import pathlib
import sys
import time
import urllib.request
import urllib.error
import xml.etree.ElementTree as ET

PROJECT=pathlib.Path(__file__).resolve().parent.parent
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:55820').rstrip('/')
OUT=pathlib.Path(sys.argv[2] if len(sys.argv)>2 else PROJECT/'artifacts'/'coverage-smoke')
WORKERS=int(sys.argv[3]) if len(sys.argv)>3 else 2
OUT.mkdir(parents=True,exist_ok=True)
FIXTURES=sorted((PROJECT/'examples'/'coverage').glob('*.json'))
UNSUPPORTED={'plotly-scattermap', 'plotly-densitymap', 'plotly-choroplethmap'}

def test(path):
    payload=path.read_bytes()
    started=time.monotonic()
    req=urllib.request.Request(BASE+'/v1/render',data=payload,headers={'Content-Type':'application/json'},method='POST')
    try:
        with urllib.request.urlopen(req,timeout=75) as response:
            content=response.read()
            status=response.status
            mime=response.headers.get('Content-Type','').split(';')[0]
        if status != 200 or mime!='image/svg+xml': raise ValueError(f'status={status} mime={mime}')
        svg=ET.fromstring(content)
        if svg.tag != '{http://www.w3.org/2000/svg}svg':raise ValueError('missing SVG root')
        if len(content) < 450:raise ValueError('suspiciously small SVG')
        if len(content)>16_777_216:raise ValueError('output oversize')
        (OUT/(path.stem+'.svg')).write_bytes(content)
        return {'case':path.stem,'status':'pass','bytes':len(content),'sha256':hashlib.sha256(content).hexdigest(),'duration_ms':round((time.monotonic()-started)*1000)}
    except urllib.error.HTTPError as error:
        detail=error.read(400).decode('utf-8','replace')
        if path.stem in UNSUPPORTED and error.code==422 and 'offline_maplibre' in detail:
            return {'case':path.stem,'status':'expected_unsupported','http':422,'code':'offline_maplibre',
                    'duration_ms':round((time.monotonic()-started)*1000)}
        return {'case':path.stem,'status':'fail','http':error.code,'error':detail[:300],'duration_ms':round((time.monotonic()-started)*1000)}
    except Exception as error:
        return {'case':path.stem,'status':'fail','error':f'{type(error).__name__}: {str(error)[:220]}','duration_ms':round((time.monotonic()-started)*1000)}

results=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=WORKERS) as pool:
    future_map={pool.submit(test,f):f for f in FIXTURES}
    for fut in concurrent.futures.as_completed(future_map):
        result=fut.result();results.append(result)
        print(f"{result['status'].upper():4} {result['case']:34} {result.get('bytes','-'):>8} bytes ({result['duration_ms']} ms)",flush=True)
results.sort(key=lambda r:r['case'])
failed=[x['case'] for x in results if x['status']=='fail']
unsupported=[x['case'] for x in results if x['status']=='expected_unsupported']
passed=[x['case'] for x in results if x['status']=='pass']
report={'schemaVersion':1,'scenarios':len(results),'rendered':len(passed),'expectedUnsupported':len(unsupported),'failed':len(failed),
        'unsupportedCases':unsupported,'failedCases':failed,
        'claim':'HTTP 200+XML SVG structure for rendered cases; external MapLibre types must return 422; visual correctness checked separately',
        'results':results}
(OUT/'results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='results'},ensure_ascii=False,indent=2),flush=True)
sys.exit(0 if not failed else 1)
