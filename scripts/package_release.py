#!/usr/bin/env python3
"""Deterministically package Plotarium 0.5.0 source and current observed GET/stateless evidence.

Usage: python3 scripts/package_release.py /absolute/path/Plotarium-0.5.0.zip
Requires current build/contract/HTTP/GET/browser/publish receipts. No program execution.
"""
import hashlib
import json
from pathlib import Path
import stat
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
VERSION = '0.5.0'
DATE = (2026, 10, 22, 0, 0, 0)
IGNORED_DIRS = {'bin', 'obj', 'artifacts', '.git', '.vs', '.nuget', '.rhapsodia', '__pycache__'}
IGNORED_FILES = {'.DS_Store', 'Thumbs.db'}


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def source_files():
    entries = []
    for path in sorted(ROOT.rglob('*')):
        if set(path.relative_to(ROOT).parts).intersection(IGNORED_DIRS):
            continue
        if path.name in IGNORED_FILES or (path.parent == ROOT and path.name.startswith('artifacts-050-')):
            continue
        if path.is_symlink():
            raise ValueError(f'Symlink not permitted in source release: {path}')
        if not path.is_file():
            continue
        if path.relative_to(ROOT).as_posix().startswith('releases/') and path.suffix == '.zip':
            continue
        entries.append(path)
    return entries


def check_evidence():
    evidence = ROOT / 'evidence' / VERSION
    contract = json.loads((evidence / 'contract-tests.json').read_text())
    http = json.loads((evidence / 'http-regression.json').read_text())
    stateless = json.loads((evidence / 'stateless-http.json').read_text())
    browser = json.loads((evidence / 'html-browser-tests.json').read_text())
    required = [(contract, 113), (http, 35), (stateless, 26), (browser, 4)]
    for result, count in required:
        if (result.get('passed'), result.get('failed')) != (count, 0):
            raise ValueError(f'Release gate failed; expected {count}/0 but got {result.get("passed")}/{result.get("failed")}')
    build = (evidence / 'build-final.log').read_text()
    if '0 Error(s)' not in build or '0 Warning(s)' not in build:
        raise ValueError('Build gate lacks zero errors and warnings')
    publish = (evidence / 'publish-final.log').read_text()
    if 'Plotarium.Api -> ' not in publish or 'Plotarium.Infrastructure ->' not in publish:
        raise ValueError('Publish gate not observed')
    validation = json.loads((evidence / 'validation.json').read_text())
    if validation.get('release') != VERSION or validation.get('status') != 'passed':
        raise ValueError('Validation receipt is missing or not final')
    assets = json.loads((ROOT / 'assets' / 'manifest.json').read_text())
    if not (ROOT / 'assets' / 'echarts-gl' / 'echarts-gl.min.js').is_file():
        raise ValueError('ECharts GL runtime is not included')
    return {'contract': 113, 'httpRegression': 35, 'statelessHttp': 26, 'browserHtml': 4,
            'buildErrors': 0, 'buildWarnings': 0, 'publish': 'passed',
            'browserVisualCases': ['echarts', 'plotly', 'mermaid', 'echarts-gl-static-fallback'],
            'assetManifestPresent': isinstance(assets, dict),
            'limitations': ['No remote public source, real Teams, WhatsApp, ChatGPT or native MapLibre worker certification',
                            'GL interactive needs WebGL-capable browser',
                            'Sources private-origin test uses explicit local development allowlist']}


def main(dest: Path):
    if not dest.is_absolute() or dest.name != f'Plotarium-{VERSION}.zip':
        raise ValueError('Expected absolute output path ending in Plotarium-0.5.0.zip')
    if ROOT in dest.parents or dest.parent == ROOT:
        raise ValueError('Output must be outside the source tree')
    stats = check_evidence()
    entries = source_files()
    if len(entries) < 300:
        raise ValueError(f'Unexpectedly short source release ({len(entries)} files)')
    # Preserve release identity without constructing a self-referential checksum.
    manifest_path = ROOT / 'evidence' / VERSION / 'release-content-hashes.json'
    manifest = [{'path': path.relative_to(ROOT).as_posix(), 'bytes': path.stat().st_size,
                 'sha256': sha256(path.read_bytes())} for path in entries if path != manifest_path]
    manifest_path.write_text(json.dumps({'release': VERSION, 'files': manifest, 'tests': stats},
                                        ensure_ascii=False, indent=2) + '\n')
    entries = source_files()
    dest.parent.mkdir(parents=True, exist_ok=True)
    temporary = dest.with_name(dest.name + '.tmp')
    try:
        with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for path in entries:
                data = path.read_bytes()
                info = zipfile.ZipInfo('Plotarium/' + path.relative_to(ROOT).as_posix(), DATE)
                info.create_system = 3
                info.external_attr = (stat.S_IFREG | 0o644) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
        with zipfile.ZipFile(temporary) as archive:
            names = archive.namelist()
            if archive.testzip() is not None:
                raise ValueError('ZIP CRC mismatch')
            if len(names) != len(entries) or len({name.casefold() for name in names}) != len(entries):
                raise ValueError('ZIP entry duplication or incomplete archive')
            expected = [
                'Plotarium/Directory.Build.props',
                'Plotarium/assets/manifest.json',
                'Plotarium/assets/echarts-gl/echarts-gl.min.js',
                'Plotarium/src/application/Plotarium.Application/StatelessPayloadDecoder.cs',
                'Plotarium/src/interfaces/Plotarium.Api/Stateless/RemotePayloadFetcher.cs',
                'Plotarium/src/interfaces/Plotarium.Api/Stateless/StatelessQuery.cs',
                'Plotarium/src/interfaces/Plotarium.Api/Stateless/StatelessHtmlBuilder.cs',
                'Plotarium/docs/release-notes-0.5.0.md',
                'Plotarium/docs/stateless-get.md',
                'Plotarium/scripts/stateless_050_smoke.py',
                'Plotarium/scripts/run_050_http_regression.py',
                'Plotarium/scripts/stateless_050_ui_smoke.py',
                'Plotarium/examples/postman/Plotarium-0.5.0.postman_collection.json',
                'Plotarium/examples/rendered-0.5.0/echarts.png',
                'Plotarium/examples/rendered-0.5.0/plotly.png',
                'Plotarium/examples/rendered-0.5.0/mermaid.png',
                'Plotarium/examples/rendered-0.5.0/echarts-gl.png',
                'Plotarium/evidence/0.5.0/build-final.log',
                'Plotarium/evidence/0.5.0/publish-final.log',
                'Plotarium/evidence/0.5.0/contract-tests.json',
                'Plotarium/evidence/0.5.0/http-regression.json',
                'Plotarium/evidence/0.5.0/stateless-http.json',
                'Plotarium/evidence/0.5.0/html-browser-tests.json',
            ]
            if any(name not in names for name in expected):
                raise ValueError('Missing required 0.5.0 source or evidence: ' + str([name for name in expected if name not in names]))
            for m in manifest:
                actual = archive.read('Plotarium/' + m['path'])
                if len(actual) != m['bytes'] or sha256(actual) != m['sha256']:
                    raise ValueError('Source bytes differ from manifest: ' + m['path'])
        temporary.replace(dest)
    finally:
        temporary.unlink(missing_ok=True)
    report = {'release': VERSION, 'path': str(dest), 'bytes': dest.stat().st_size,
              'sha256': sha256(dest.read_bytes()), 'entries': len(entries), 'tests': stats}
    dest.with_suffix('.release.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')
    dest.with_suffix('.sha256').write_text(report['sha256'] + '  ' + dest.name + '\n')
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('Usage: python3 scripts/package_release.py /absolute/output/Plotarium-0.5.0.zip')
    main(Path(sys.argv[1]))
