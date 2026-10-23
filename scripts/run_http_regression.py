#!/usr/bin/env python3
"""Run the existing HTTP integration suite against the exact current release DLL."""
from pathlib import Path
import json
import os
import socket
import subprocess
import time
from urllib.request import urlopen

ROOT=Path(__file__).resolve().parent.parent
DOTNET=os.environ.get('PLOTEUS_DOTNET','dotnet')
APP=Path(os.environ.get('PLOTEUS_API_DLL', str(ROOT/'src/interfaces/Ploteus.Api/bin/Release/net10.0/Ploteus.Api.dll'))).resolve()

def port():
    with socket.socket() as s:
        s.bind(('127.0.0.1',0))
        return s.getsockname()[1]

p=port();base='http://127.0.0.1:'+str(p)
env=os.environ.copy()
env.update({'ASPNETCORE_URLS':base,'ASPNETCORE_ENVIRONMENT':'Development',
            'Ploteus__Browser__ExecutablePath':'/usr/bin/chromium',
            'Ploteus__Browser__DisableSandbox':'true',
            'Ploteus__Browser__EnableSoftwareWebGl':'true',
            'Ploteus__Browser__AssetsDirectory':str(ROOT/'assets'),
            'DOTNET_CLI_TELEMETRY_OPTOUT':'1'})
assert APP.is_file(),str(APP)
with (ROOT/'artifacts-060-regression-server.log').open('wb') as log:
    process=subprocess.Popen([DOTNET,str(APP)],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
    try:
        for _ in range(100):
            if process.poll() is not None:raise RuntimeError('API did not start')
            try:
                with urlopen(base+'/health/live',timeout=1) as response:
                    if response.status==200:break
            except OSError:time.sleep(.15)
        else:raise RuntimeError('API not ready')
        test_dll=ROOT/'tests/Ploteus.HttpTests/bin/Any CPU/Release/net10.0/Ploteus.HttpTests.dll'
        if not test_dll.is_file():
            test_dll=ROOT/'tests/Ploteus.HttpTests/bin/Release/net10.0/Ploteus.HttpTests.dll'
        if not test_dll.is_file():
            raise RuntimeError('Build the Ploteus.HttpTests project before running HTTP regression')
        r=subprocess.run([DOTNET,str(test_dll),base],cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,timeout=300)
        response=json.loads(r.stdout)
        d=ROOT/'evidence/0.6.0';d.mkdir(parents=True,exist_ok=True)
        (d/'http-regression.json').write_text(json.dumps(response,indent=2)+'\n')
        print(json.dumps({'exit':r.returncode,'passed':response.get('passed'),'failed':response.get('failed'),'failures':[x for x in response.get('results',[]) if x['status']!='pass'],'stderr':r.stderr[-500:]},indent=2))
        if r.returncode!=0:raise SystemExit(1)
    finally:
        process.terminate()
        try:process.wait(5)
        except subprocess.TimeoutExpired:process.kill()
