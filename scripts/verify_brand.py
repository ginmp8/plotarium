#!/usr/bin/env python3
"""Ensure a release carries one Ploteus product identity across paths and bytes.

Checks names and source bytes; screenshots require separate visual verification.
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parent.parent
EXCLUDED = {'obj', 'bin', 'artifacts', '__pycache__', '.git', '.vs', '.nuget'}
# Concatenated to avoid embedding an obsolete product name in delivered sources.
OLD = b'plot' + b'arium'


def check():
    files = [p for p in ROOT.rglob('*') if p.is_file() and not set(p.relative_to(ROOT).parts) & EXCLUDED]
    violations = []
    for p in files:
        name = p.relative_to(ROOT).as_posix()
        if OLD in name.lower().encode('utf-8'):
            violations.append({'path': name, 'cause': 'obsolete path component'})
        if OLD in p.read_bytes().lower():
            violations.append({'path': name, 'cause': 'obsolete bytes'})
    required = {
        'Ploteus.slnx',
        'src/interfaces/Ploteus.Api/Ploteus.Api.csproj',
        'src/application/Ploteus.Application/Ploteus.Application.csproj',
        'src/infrastructure/Ploteus.Infrastructure/Ploteus.Infrastructure.csproj',
        'src/domain/Ploteus.Domain/Ploteus.Domain.csproj',
        'docs/release-notes-0.6.0.md',
        'examples/postman/Ploteus-0.6.0.postman_collection.json',
    }
    missing = sorted(required - set(p.relative_to(ROOT).as_posix() for p in files))
    metadata = {
        'version_props': '<Version>0.6.0</Version>' in (ROOT/'Directory.Build.props').read_text(),
        'api_section': '"Ploteus"' in (ROOT/'src/interfaces/Ploteus.Api/appsettings.json').read_text(),
        'mcp_ui_resource': 'ui://ploteus/chart.html' in (ROOT/'src/interfaces/Ploteus.Api/Interactive/McpEndpoints.cs').read_text(),
        'http_entrypoint': 'MapGet("/v1/render"' in (ROOT/'src/interfaces/Ploteus.Api/Program.cs').read_text(),
        'post_entrypoint': 'MapPost("/v1/render"' in (ROOT/'src/interfaces/Ploteus.Api/Program.cs').read_text(),
    }
    report = {
        'release': '0.6.0', 'scanned_files': len(files), 'obsolete_identity_occurrences': len(violations),
        'violations': violations, 'missing': missing, 'invariants': metadata,
        'passed': not violations and not missing and all(metadata.values()),
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report['passed'] else 1

if __name__ == '__main__':
    sys.exit(check())
