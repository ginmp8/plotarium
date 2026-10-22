"""Reproducible conversion of packaged Plotly TopoJSON into GeoJSON used by ECharts.
Reads only pre-approved members of the provided plotly.js archive; no execution of source.
"""
from pathlib import Path
import zipfile,json,hashlib,sys
ROOT=Path(__file__).resolve().parent.parent/'assets'
ARCHIVE=sys.argv[1] if len(sys.argv)==2 else sys.exit('Usage: python3 scripts/generate_geodata.py /path/to/plotly.js-main.zip')
STATE_NAMES=dict(zip(
    'AL AK AZ AR CA CO CT DE FL GA HI ID IL IN IA KS KY LA ME MD MA MI MN MS MO MT NE NV NH NJ NM NY NC ND OH OK OR PA RI SC SD TN TX UT VT VA WA WV WI WY DC'.split(),
    'Alabama|Alaska|Arizona|Arkansas|California|Colorado|Connecticut|Delaware|Florida|Georgia|Hawaii|Idaho|Illinois|Indiana|Iowa|Kansas|Kentucky|Louisiana|Maine|Maryland|Massachusetts|Michigan|Minnesota|Mississippi|Missouri|Montana|Nebraska|Nevada|New Hampshire|New Jersey|New Mexico|New York|North Carolina|North Dakota|Ohio|Oklahoma|Oregon|Pennsylvania|Rhode Island|South Carolina|South Dakota|Tennessee|Texas|Utah|Vermont|Virginia|Washington|West Virginia|Wisconsin|Wyoming|District of Columbia'.split('|')
))
MAPPINGS={'world':'world_110m','world-50m':'world_50m','africa':'africa_110m','asia':'asia_110m','europe':'europe_110m','north-america':'north-america_110m','south-america':'south-america_110m','oceania':'oceania_110m','usa':'usa_110m'}
with zipfile.ZipFile(ARCHIVE) as z:
    names={v['iso3cd']:v['name'] for v in json.loads(z.read('plotly.js-main/topojson/dist/country_names_iso_codes.json'))}
    for name in sorted(set(MAPPINGS.values()) | {"usa_50m", "world_50m"}):
        out=ROOT/'topojson'/f'{name}.json';out.parent.mkdir(parents=True,exist_ok=True)
        out.write_bytes(z.read('plotly.js-main/dist/topojson/'+name+'.json'))
    for dest,src in MAPPINGS.items():
        topo=json.loads((ROOT/'topojson'/f'{src}.json').read_bytes())
        scalex,scaley=topo['transform']['scale'];offx,offy=topo['transform']['translate']
        cache={}
        def points(i):
            reverse=i<0;index=(~i if reverse else i)
            if index not in cache:
                x=y=0;a=[]
                for dx,dy in topo['arcs'][index]:
                    x+=dx;y+=dy;a.append([round(x*scalex+offx,6),round(y*scaley+offy,6)])
                cache[index]=a
            pts=cache[index]
            return list(reversed(pts)) if reverse else pts
        def join(indices):
            ring=[]
            for idx in indices:
                frag=points(idx)
                if ring:frag=frag[1:]
                ring.extend(frag)
            if ring and ring[-1]!=ring[0]:ring.append(ring[0])
            return ring
        collection=topo['objects']['subunits' if dest=='usa' else 'countries']['geometries']
        features=[]
        for g in collection:
            if g['type']=='Polygon':coords=[join(r) for r in g['arcs']]
            elif g['type']=='MultiPolygon':coords=[[join(r) for r in poly] for poly in g['arcs']]
            else:continue
            fid=g.get('id', '');props=dict(g.get('properties',{}))
            props['name']=props.get('name') or (STATE_NAMES.get(fid) if dest=='usa' else names.get(fid)) or str(fid)
            props['iso3']=str(fid)
            features.append({'type':'Feature','properties':props,'geometry':{'type':g['type'],'coordinates':coords}})
        out=ROOT/'geojson'/f'{dest}.geojson';out.parent.mkdir(parents=True,exist_ok=True)
        out.write_text(json.dumps({'type':'FeatureCollection','features':features},ensure_ascii=False,separators=(',',':')),encoding='utf-8')
        print(dest,len(features),out.stat().st_size)
manifest_path=ROOT/'manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
manifest['files']=[i for i in manifest['files'] if not (i['path'].startswith('geojson/') or i['path'].startswith('topojson/'))]
for folder in ('geojson','topojson'):
    for p in sorted((ROOT/folder).rglob('*')):
        if not p.is_file():continue
        raw=p.read_bytes()
        manifest['files'].append({'path':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw),'sourceArchive':'plotly.js-main.zip','sourceMember':('derived from plotly.js-main/dist/topojson' if folder=='geojson' else 'plotly.js-main/dist/topojson/'+p.name),'version':'4.1.2','provenance':'local derived GeoJSON, geographic boundaries from bundled Plotly data (Natural Earth-derived); see THIRD-PARTY-NOTICES.md'})
manifest['files'].sort(key=lambda i:i['path'])
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
