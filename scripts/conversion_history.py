"""Shared helpers for existing asset tools; no conversion command entry point."""

from __future__ import annotations

import hashlib

import json

from official_apk import ROOT

def sha(data):
    return hashlib.sha256(data).hexdigest()

def preserve(path):
    raw = path.read_bytes()
    backup = ROOT / '.tools/source-packages/history/converted' / (sha(raw) + '.bin')
    backup.parent.mkdir(parents=True, exist_ok=True)
    if backup.exists() and backup.read_bytes() != raw:
        raise ValueError('Historical conversion hash collision: ' + str(path))
    if not backup.exists():
        backup.write_bytes(raw)
    return {'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(raw),
            'preserved': backup.relative_to(ROOT).as_posix()}

def load_skeleton(version, sex='male'):
    root = ROOT / '.tools/source-packages/current' / version / 'skeletons' / sex
    raw = (root / 'skeleton.json').read_bytes()
    receipt = json.loads((root / 'source-manifest.json').read_text('utf8'))
    if receipt['manifest'] != version or receipt['output_sha256'] != sha(raw):
        raise ValueError('Decoded skeleton source hash differs: ' + version)
    for record in receipt['inputs']:
        if sha((root / record['file']).read_bytes()) != record['sha256']:
            raise ValueError('Preserved raw skeleton input differs')
    return json.loads(raw), {'path': (root / 'source-manifest.json').relative_to(ROOT).as_posix(),
                             'sha256': sha((root / 'source-manifest.json').read_bytes())}
