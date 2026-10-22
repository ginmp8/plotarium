#!/usr/bin/env python3
"""Plotarium 0.4.0 real HTTP regression: GL, maps, policy, hosted viewers.

Usage: python3 scripts/feature_040_smoke.py http://127.0.0.1:51441 OUT
No API credentials or external network are required. Run only against a local trusted server.
"""
import hashlib
import io
import json
from pathlib import Path
import sys
import urllib.request
import urllib.error

from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parent.parent
BASE = (sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:51441').rstrip('/')
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / 'artifacts' / 'feature-040'
OUT.mkdir(parents=True, exist_ok=True)
RESULTS = []


def exchange(path, payload=None):
    body = json.dumps(payload, ensure_ascii=False, separators=(',', ':')).encode('utf8') if payload is not None else None
    request = urllib.request.Request(BASE + path, data=body, headers={'Content-Type': 'application/json'} if body else {}, method='POST' if body else 'GET')
    try:
        with urllib.request.urlopen(request, timeout=110) as r:
            return r.status, r.headers.get('content-type', '').split(';')[0], r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.headers.get('content-type', '').split(';')[0], e.read()


def check(name, callback):
    try:
        info = callback() or {}
        RESULTS.append({'name': name, 'status': 'pass', **info})
    except Exception as e:
        RESULTS.append({'name': name, 'status': 'fail', 'error': str(e)[:350]})
    print(RESULTS[-1], flush=True)


def sample(name, fmt):
    obj = json.loads((ROOT / 'examples' / 'rendered-0.4.0' / (name + '.json')).read_text(encoding='utf8'))
    obj['format'] = fmt
    return obj


def render(name, fmt):
    code, mime, raw = exchange('/v1/render', sample(name, fmt))
    assert (code, mime) == (200, {'png': 'image/png', 'svg': 'image/svg+xml', 'pdf': 'application/pdf'}[fmt]), (code, mime, raw[:180])
    if fmt == 'png':
        assert raw.startswith(b'\x89PNG\r\n\x1a\n')
        image = Image.open(io.BytesIO(raw)).convert('RGB')
        assert image.size == (800, 540), image.size
        deviation = ImageStat.Stat(image.resize((80, 54))).stddev
        assert max(deviation) > 20, ('blank or uniformly colored graphic', deviation)
    elif fmt == 'svg':
        assert b'<svg' in raw
        if name == 'echarts-bar3D':
            assert b'data:image/png;base64,' in raw, 'ECharts GL must embed a raster'
        else:
            assert b'<path' in raw or b'<circle' in raw, 'Plotly geographic fallback must preserve vector paths'
    elif fmt == 'pdf':
        assert raw.startswith(b'%PDF-')
    (OUT / f'{name}.{fmt}').write_bytes(raw)
    return {'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}


for chart in ['echarts-bar3D', 'plotly-choroplethmap', 'plotly-scattermap']:
    check(f'{chart}/png_visual_nonblank', lambda name=chart: render(name, 'png'))
    check(f'{chart}/svg_embeds_raster', lambda name=chart: render(name, 'svg'))
    check(f'{chart}/pdf', lambda name=chart: render(name, 'pdf'))


def density_rejected():
    obj = json.loads((ROOT / 'examples' / 'coverage' / 'plotly-densitymap.json').read_text())
    obj['format'] = 'png'
    obj.get('spec', {}).get('layout', {}).get('map', {}).pop('style', None)
    status, _, raw = exchange('/v1/render', obj)
    assert status == 422, (status, raw[:200])
    prob = json.loads(raw)
    assert prob.get('code') == 'offline_densitymap', prob
    return {'http': status, 'code': prob['code']}


def unsafe_map_style_rejected():
    obj = sample('plotly-scattermap', 'png')
    obj['spec']['layout']['map']['style'] = 'https://example.com/style.json'
    status, _, raw = exchange('/v1/render', obj)
    assert status == 422, (status, raw[:180])
    assert json.loads(raw).get('code') == 'map_style'
    return {'http': status, 'code': 'map_style'}


def gl_viewer():
    status, _, raw = exchange('/v1/charts', sample('echarts-bar3D', 'png'))
    assert status == 201, (status, raw[:200])
    body = json.loads(raw)
    id = body['id']
    assert len(id) == 32
    view_status, view_mime, html = exchange(f'/v1/charts/{id}/view')
    assert (view_status, view_mime) == (200, 'text/html')
    assert id.encode() in html
    adapter_status, asset_mime, js = exchange('/v1/viewer/adapters/echarts.js')
    assert adapter_status == 200 and 'javascript' in asset_mime
    assert b'echarts-gl' in js
    gl_status, _, asset = exchange('/v1/assets/echarts-gl/echarts-gl.min.js')
    assert gl_status == 200 and len(asset) > 100_000
    return {'viewer': view_status, 'adapter': adapter_status, 'glAssetBytes': len(asset)}


def capabilities():
    status, mime, raw = exchange('/v1/capabilities')
    assert (status, mime) == (200, 'application/json')
    j = json.loads(raw)
    assert j['geography']['mapLibreTiles'] is False
    assert 'scattermap' in j['geography']['plotlyMapTypes']
    assert j['interactive']['enabled'] is True
    return {'mapLibreTiles': False, 'interactive': True}


check('densitymap_rejected_explicitly', density_rejected)
check('external_maplibre_style_rejected', unsafe_map_style_rejected)
check('echarts_gl_interactive_view_and_asset', gl_viewer)
check('capabilities_report_limits_honestly', capabilities)
report = {'version': '0.4.0', 'passed': sum(x['status'] == 'pass' for x in RESULTS),
          'failed': sum(x['status'] == 'fail' for x in RESULTS), 'results': RESULTS,
          'scope': 'Actual HTTP bytes, local chart pixel variance and guarded capabilities. Does not prove native Teams/WhatsApp/ChatGPT integration or browser-hosted MapLibre worker.'}
(OUT / 'feature-smokes.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({'passed': report['passed'], 'failed': report['failed']}), flush=True)
sys.exit(0 if report['failed'] == 0 else 1)
