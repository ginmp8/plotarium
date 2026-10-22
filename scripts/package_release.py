#!/usr/bin/env python3
"""Produce a deterministic source release, never executing any project code.

Usage: python3 scripts/package_release.py /absolute/output/Plotarium-0.2.0.zip
Source root is the parent of this script. Requires existing passing receipts.
"""
import datetime
import hashlib
import json
import pathlib
import stat
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
VERSION = '0.2.0'
EXCLUDED_DIRS = {'bin','obj','artifacts','.git','.vs','.nuget','.rhapsodia','__pycache__'}
EXCLUDED_FILES = {'Thumbs.db','.DS_Store'}
DT = (2026, 10, 21, 0, 0, 0)

def sha(b): return hashlib.sha256(b).hexdigest()
def gather():
    source=[]
    for path in sorted(ROOT.rglob('*')):
        if set(path.relative_to(ROOT).parts) & EXCLUDED_DIRS: continue
        if path.name in EXCLUDED_FILES:continue
        if path.is_symlink():raise ValueError('Symlink is forbidden: '+str(path))
        if not path.is_file():continue
        if path.relative_to(ROOT).as_posix().startswith('releases/') and path.suffix=='.zip':continue
        source.append(path)
    return source

def receipts():
    d=ROOT/'evidence'/'0.2.0'
    c=json.loads((d/'contract-final.json').read_text())
    h=json.loads((d/'http-tests.json').read_text())
    g=json.loads((d/'geo-format-results.json').read_text())
    x=json.loads((d/'coverage-results.json').read_text())
    assert (c['passed'],c['failed'])==(90,0)
    assert (h['passed'],h['failed'])==(24,0)
    assert (g['passed'],g['failed'])==(6,0)
    assert (x['scenarios'],x['rendered'],x['expectedUnsupported'],x['failed'])==(69,66,3,0)
    assert '0 Error(s)' in (d/'build-final.log').read_text()
    assert '0 Warning(s)' in (d/'build-final.log').read_text()
    assert 'Plotarium.Api -> ' in (d/'publish-final.log').read_text()
    return {'contract':{'passed':90,'failed':0},'http':{'passed':24,'failed':0},'geo':{'passed':6,'failed':0},'coverage':{'scenarios':69,'rendered':66,'expectedUnsupported':3,'failed':0},'platform':'Linux x64 .NET 10.0.302 / Chromium 144.0.7559.96','windowsValidated':False}

if __name__=='__main__':
    if len(sys.argv)!=2:raise SystemExit('Expected absolute output ZIP path')
    dest=pathlib.Path(sys.argv[1]);assert dest.is_absolute()
    assert str(dest)!=''
    assert ROOT not in dest.parents and ROOT!=dest.parent
    stats=receipts()
    src=gather()
    assert len(src)>200, len(src)
    file_manifest=[{'path':x.relative_to(ROOT).as_posix(),'bytes':x.stat().st_size,'sha256':sha(x.read_bytes())} for x in src if x.name!='release-content-hashes.json']
    m=ROOT/'evidence'/'0.2.0'/'release-content-hashes.json'
    m.write_text(json.dumps({'release':VERSION,'files':file_manifest,'tests':stats},indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    src=gather()
    dest.parent.mkdir(parents=True,exist_ok=True)
    stage=dest.with_name(dest.name+'.tmp')
    try:
        with zipfile.ZipFile(stage,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9,strict_timestamps=True) as z:
            for x in src:
                rel='Plotarium/'+x.relative_to(ROOT).as_posix()
                data=x.read_bytes()
                zi=zipfile.ZipInfo(rel,DT)
                zi.create_system=3
                zi.external_attr=(stat.S_IFREG|0o644)<<16
                zi.compress_type=zipfile.ZIP_DEFLATED
                z.writestr(zi,data,compress_type=zipfile.ZIP_DEFLATED,compresslevel=9)
        with zipfile.ZipFile(stage) as z:
            assert z.testzip() is None
            assert len(z.infolist()) == len(src)
            assert len({i.filename.casefold() for i in z.infolist()}) == len(src)
            for n in ('Plotarium/Directory.Build.props','Plotarium/docs/release-notes-0.2.0.md','Plotarium/assets/geojson/world.geojson','Plotarium/assets/topojson/world_110m.json','Plotarium/evidence/0.2.0/coverage-results.json','Plotarium/examples/rendered-0.2.0/plotly-scatter3d.png'):
                assert n in z.namelist(),n
        stage.replace(dest)
    finally:
        stage.unlink(missing_ok=True)
    report={'path':str(dest),'version':VERSION,'sha256':sha(dest.read_bytes()),'bytes':dest.stat().st_size,'entries':len(src),'tests':stats}
    dest.with_suffix('.release.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    dest.with_suffix('.sha256').write_text(report['sha256']+'  '+dest.name+'\n',encoding='ascii')
    print(json.dumps(report,indent=2))
