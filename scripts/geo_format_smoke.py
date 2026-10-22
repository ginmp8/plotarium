#!/usr/bin/env python3
"""Live geographic / 3D cross-format checks; no credentials.
Usage: python3 scripts/geo_format_smoke.py http://127.0.0.1:55820 [output-folder]
"""
import base64
import hashlib
import json
import pathlib
import re
import sys
import urllib.request
import urllib.error

ROOT=pathlib.Path(__file__).resolve().parent.parent
ORIGIN=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:55820').rstrip('/')
OUT=pathlib.Path(sys.argv[2] if len(sys.argv)>2 else ROOT/'artifacts'/'geo-smoke')
OUT.mkdir(parents=True,exist_ok=True)
TASKS=[('echarts-map-world','png'),('echarts-map-world','svg'),('plotly-choropleth','png'),
       ('plotly-choropleth','pdf'),('plotly-scatter3d','png'),('plotly-scatter3d','svg')]
results=[]
for name,fmt in TASKS:
    case=json.loads((ROOT/'examples'/'coverage'/(name+'.json')).read_bytes());case['format']=fmt
    request=urllib.request.Request(ORIGIN+'/v1/render', data=json.dumps(case,separators=(',',':')).encode(),headers={'Content-Type':'application/json'},method='POST')
    try:
        with urllib.request.urlopen(request,timeout=75) as response:
            data=response.read();content_type=response.headers.get('Content-Type','').split(';')[0]
            if response.status!=200:raise ValueError('HTTP non-200')
        mime={'png':'image/png','svg':'image/svg+xml','pdf':'application/pdf'}[fmt]
        if content_type!=mime:raise ValueError('Incorrect MIME: '+content_type)
        if fmt=='png' and not data.startswith(b'\x89PNG\r\n\x1a\n'):raise ValueError('PNG signature')
        if fmt=='pdf' and not data.startswith(b'%PDF-'):raise ValueError('PDF signature')
        if fmt=='svg' and b'<svg' not in data:raise ValueError('SVG element')
        if fmt=='svg' and name=='plotly-scatter3d' and not re.search(rb'data:image/png;base64,[A-Za-z0-9+/=]+',data):raise ValueError('WebGL raster not embedded')
        out=OUT/(name+'.'+fmt);out.write_bytes(data)
        result={'case':name,'format':fmt,'status':'pass','bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}
    except Exception as error:
        detail=str(error)
        if isinstance(error,urllib.error.HTTPError):detail=f'HTTP {error.code}'
        result={'case':name,'format':fmt,'status':'fail','error':detail}
    results.append(result)
    print(result,flush=True)
report={'schemaVersion':1,'passed':sum(x['status']=='pass' for x in results),'failed':sum(x['status']=='fail' for x in results),'results':results,
        'limit':'signature, MIME and self-contained payload; independent visual check required'}
(OUT/'results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
sys.exit(0 if not report['failed'] else 1)
