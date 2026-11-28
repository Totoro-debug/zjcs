"""Shared helpers for existing asset tools; no conversion command entry point."""

import hashlib

import os

from pathlib import Path

import tempfile

from official_apk import ROOT

from prepare_current_inventory_sprites import PAYLOAD

PROOF = Path('assets/reference/inventory-current-sprites-source-manifest.json')

def sha(raw):
    return hashlib.sha256(raw).hexdigest()

def verify_output(row):
    raw = (ROOT/row['path']).read_bytes()
    if sha(raw) != row['sha256'] and (not row.get('sha256_lf') or sha(raw.replace(b'\r\n', b'\n')) != row['sha256_lf']):
        raise ValueError('Installed current inventory asset changed: '+row['path'])

def atomic_write(path, raw):
    if path.exists() and path.read_bytes() == raw:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, name = tempfile.mkstemp(prefix='.current-inventory-', suffix='.tmp', dir=path.parent)
    temporary = Path(name)
    try:
        with os.fdopen(handle, 'wb') as output:
            output.write(raw)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)
