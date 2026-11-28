"""Shared helpers for existing asset tools; no conversion command entry point."""

from __future__ import annotations

import hashlib

import json

import struct

from official_apk import ROOT

def sha(raw):
    return hashlib.sha256(raw).hexdigest()

def language(directory):
    records = []
    result = {}
    index = json.loads((directory / 'bundle-index.json').read_bytes())
    sources = json.loads((directory / 'bundle-sources.json').read_bytes())
    for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
        raw = (directory / name).read_bytes()
        if hashlib.md5(raw).hexdigest() != index[name]:
            raise ValueError('Official language rawfile differs from manifest: ' + name)
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from('<Qi', raw, 4 + 12*i)
            position = start + offset
            size = struct.unpack_from('<H', raw, position)[0]
            if offset < 0 or position + 2 + size > len(raw):
                raise ValueError('Official language entry is out of bounds')
            result[identity] = raw[position+2:position+2+size].decode('utf8')
        records.append({'path': (directory / name).relative_to(ROOT).as_posix(),
                        'sha256': sha(raw), 'entries': count, 'source': sources[name]})
    return result, records
