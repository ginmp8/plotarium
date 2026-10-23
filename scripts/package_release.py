#!/usr/bin/env python3
"""Deterministically package Ploteus 0.6.0 source and current observed GET/stateless evidence.

Usage: python3 scripts/package_release.py /absolute/path/Ploteus-0.6.0.zip
Requires current build/contract/HTTP/GET/browser/publish receipts. No program execution.
"""
import hashlib
import json
from pathlib import Path
import stat
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
VERSION = '0.6.0'
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
        if path.name in IGNORED_FILES or (path.parent == ROOT and path.name.startswith('artifacts-060-')):
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
    brand = json.loads((evidence / 'brand-verification.json').read_text())
    if brand.get('passed') is not True or brand.get('obsolete_identity_occurrences') != 0:
        raise ValueError('Brand identity verification gate failed')
    feature = json.loads((evidence / 'feature-smokes.json').read_text())
    interactive = json.loads((evidence / 'interactive-ui-component-tests.json').read_text())
    required = [(contract, 116), (http, 35), (stateless, 26), (browser, 4), (feature, 13), (interactive, 3)]
    for result, count in required:
        if (result.get('passed'), result.get('failed')) != (count, 0):
            raise ValueError(f'Release gate failed; expected {count}/0 but got {result.get("passed")}/{result.get("failed")}')
    build = (evidence / 'build-final.log').read_text()
    if '0 Error(s)' not in build or '0 Warning(s)' not in build:
        raise ValueError('Build gate lacks zero errors and warnings')
    publish = (evidence / 'publish-final.log').read_text()
    if 'Ploteus.Api -> ' not in publish or 'Ploteus.Infrastructure ->' not in publish:
        raise ValueError('Publish gate not observed')
    validation = json.loads((evidence / 'validation.json').read_text())
    if validation.get('release') != VERSION or validation.get('status') != 'passed':
        raise ValueError('Validation receipt is missing or not final')
    assets = json.loads((ROOT / 'assets' / 'manifest.json').read_text())
    if not (ROOT / 'assets' / 'echarts-gl' / 'echarts-gl.min.js').is_file():
        raise ValueError('ECharts GL runtime is not included')
    return {'contract': 116, 'httpRegression': 35, 'statelessHttp': 26, 'browserHtml': 4, 'featureGlGeo': 13, 'interactiveUi': 3,
            'buildErrors': 0, 'buildWarnings': 0, 'publish': 'passed',
            'browserVisualCases': ['echarts', 'plotly', 'mermaid', 'echarts-gl-static-fallback'],
            'assetManifestPresent': isinstance(assets, dict),
            'limitations': ['No remote public source, real Teams, WhatsApp, ChatGPT or native MapLibre worker certification',
                            'GL interactive needs WebGL-capable browser',
                            'Sources private-origin test uses explicit local development allowlist']}


def main(dest: Path):
    if not dest.is_absolute() or dest.name != f'Ploteus-{VERSION}.zip':
        raise ValueError('Expected absolute output path ending in Ploteus-0.6.0.zip')
    if ROOT in dest.parents or dest.parent == ROOT:
        raise ValueError('Output must be outside the source tree')
    stats = check_evidence()
    entries = source_files()
    banned = (b'plot' + b'arium')
    for path in entries:
        label = path.relative_to(ROOT).as_posix()
        if banned in label.lower().encode('utf8') or banned in path.read_bytes().lower():
            raise ValueError('Non-current product identity in release file: ' + label)
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
                info = zipfile.ZipInfo('Ploteus/' + path.relative_to(ROOT).as_posix(), DATE)
                info.create_system = 3
                info.external_attr = (stat.S_IFREG | 0o644) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
        with zipfile.ZipFile(temporary) as archive:
            names = archive.namelist()
            if archive.testzip() is not None:
                raise ValueError('ZIP CRC mismatch')
            if any(banned in name.lower().encode('utf8') for name in names):
                raise ValueError('Non-current product identity in ZIP member name')
            if len(names) != len(entries) or len({name.casefold() for name in names}) != len(entries):
                raise ValueError('ZIP entry duplication or incomplete archive')
            expected = [
                'Ploteus/Directory.Build.props',
                'Ploteus/assets/manifest.json',
                'Ploteus/assets/echarts-gl/echarts-gl.min.js',
                'Ploteus/src/application/Ploteus.Application/StatelessPayloadDecoder.cs',
                'Ploteus/src/interfaces/Ploteus.Api/Stateless/RemotePayloadFetcher.cs',
                'Ploteus/src/interfaces/Ploteus.Api/Stateless/StatelessQuery.cs',
                'Ploteus/src/interfaces/Ploteus.Api/Stateless/StatelessHtmlBuilder.cs',
                'Ploteus/docs/release-notes-0.6.0.md',
                'Ploteus/docs/stateless-get.md',
                'Ploteus/scripts/stateless_smoke.py',
                'Ploteus/scripts/run_http_regression.py',
                'Ploteus/scripts/stateless_ui_smoke.py',
                'Ploteus/examples/postman/Ploteus-0.6.0.postman_collection.json',
                'Ploteus/examples/rendered-0.6.0/echarts.png',
                'Ploteus/examples/rendered-0.6.0/plotly.png',
                'Ploteus/examples/rendered-0.6.0/mermaid.png',
                'Ploteus/examples/rendered-0.6.0/echarts-gl.png',
                'Ploteus/evidence/0.6.0/build-final.log',
                'Ploteus/evidence/0.6.0/publish-final.log',
                'Ploteus/evidence/0.6.0/contract-tests.json',
                'Ploteus/evidence/0.6.0/http-regression.json',
                'Ploteus/evidence/0.6.0/stateless-http.json',
                'Ploteus/evidence/0.6.0/html-browser-tests.json',
                'Ploteus/evidence/0.6.0/feature-smokes.json',
                'Ploteus/evidence/0.6.0/interactive-ui-component-tests.json',
                'Ploteus/evidence/0.6.0/brand-verification.json',
                'Ploteus/scripts/verify_brand.py',
                'Ploteus/examples/interactive/previews/echarts-interactive.png',
            ]
            if any(name not in names for name in expected):
                raise ValueError('Missing required 0.6.0 source or evidence: ' + str([name for name in expected if name not in names]))
            for m in manifest:
                actual = archive.read('Ploteus/' + m['path'])
                if banned in actual.lower():
                    raise ValueError('Non-current product identity in ZIP member bytes: ' + m['path'])
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
        raise SystemExit('Usage: python3 scripts/package_release.py /absolute/output/Ploteus-0.6.0.zip')
    main(Path(sys.argv[1]))
