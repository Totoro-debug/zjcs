"""Decode verified official skeletons and split animations without a Spine runtime.

The MIT SkelToJson format reader is retained as a pinned wheel in .tools. Raw
TextAssets and their bundle provenance remain beside the lossless JSON output.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import urllib.request
import zipfile

from official_apk import Apk, ROOT

WHEEL_SHA256 = 'e6a0c0c5a38c811c7b23074fb22381615edbbc0be42db2cd12468764228e8897'
TOOL_ROOT = ROOT / '.tools/skeltojson-1.0.1'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def reader_type():
    wheel = TOOL_ROOT / 'skeltojson-1.0.1-py3-none-any.whl'
    TOOL_ROOT.mkdir(parents=True, exist_ok=True)
    if not wheel.exists():
        with urllib.request.urlopen('https://pypi.org/pypi/SkelToJson/1.0.1/json', timeout=30) as response:
            index = json.load(response)
        record = next(r for r in index['urls'] if r['filename'] == wheel.name)
        if record['digests']['sha256'] != WHEEL_SHA256:
            raise ValueError('Published format reader differs from the pinned wheel')
        with urllib.request.urlopen(record['url'], timeout=30) as response:
            data = response.read()
        if digest(data) != WHEEL_SHA256:
            raise ValueError('Format reader wheel SHA256 mismatch')
        wheel.write_bytes(data)
    if digest(wheel.read_bytes()) != WHEEL_SHA256:
        raise ValueError('Cached format reader wheel SHA256 mismatch')
    # Import directly from the verified wheel; preserve the upstream MIT license.
    sys.path.insert(0, str(wheel))
    import SkelToJson.converter as converter
    from SkelToJson.binary_input import BinaryInput
    converter.rf = lambda value, *args: value  # Keep every source float32 value.

    class LosslessReader(converter.SkelConverter):
        def _read_attachment(self, attachment_name):
            # Upstream drops the binary name override when path equals that name.
            # Preserve it so a slot attachment key does not replace its image path.
            original_byte = self.inp.read_byte
            original_string = self.inp.read_string_ref
            flags, names = [], []

            def byte():
                value = original_byte()
                flags.append(value)
                return value

            def string():
                value = original_string()
                names.append(value)
                return value

            self.inp.read_byte = byte
            self.inp.read_string_ref = string
            try:
                result = super()._read_attachment(attachment_name)
            finally:
                self.inp.read_byte = original_byte
                self.inp.read_string_ref = original_string
            if result is not None and flags[0] & 8 and names[0] != attachment_name:
                result['name'] = names[0]
            return result

        def _read_ik_constraint_data(self, name):
            # Upstream 1.0.1 incorrectly defaults an absent mix flag to one.
            # Record the final flag byte read by the upstream field reader.
            # No additional primitive bytes follow it (only optional floats).
            original = self.inp.read_byte
            flags = []

            def byte():
                value = original()
                flags.append(value)
                return value

            self.inp.read_byte = byte
            try:
                result = super()._read_ik_constraint_data(name)
            finally:
                self.inp.read_byte = original
            if not flags[-1] & 32:
                result['mix'] = 0.0
            return result

    return LosslessReader, BinaryInput


def convert(manifest=None, sex='male', output=None):
    reader_class, binary_input = reader_type()
    apk = Apk(manifest=manifest)
    destination = output or ROOT / '.tools/source-packages/current' / apk.source.version / 'skeletons' / sex
    destination.mkdir(parents=True, exist_ok=True)
    records = []
    model = f'defaultpackage_assets_appearanceassets_character_model_{sex}_1.bundle'
    split = model.removesuffix('.bundle') + '_splitanimations.bundle'

    def text_asset(obj, prefix):
        value = obj.read()
        raw = value.m_Script.encode('utf8', 'surrogateescape')
        relative = prefix + '/' + digest(raw) + '.bytes'
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes() != raw:
            raise ValueError('Content-addressed raw skeleton cache differs: ' + str(target))
        if not target.exists():
            target.write_bytes(raw)
        records.append(dict(apk.provenance(obj), file=relative, sha256=digest(raw), name=value.m_Name))
        return raw

    skeletons = [obj for obj in apk.objects(model) if obj.type.name == 'TextAsset'
                 and obj.read().m_Name.lower() == f'character_{sex}_1.skel']
    if len(skeletons) != 1:
        raise ValueError('Expected exactly one character skeleton: ' + model)
    reader = reader_class()
    raw = text_asset(skeletons[0], 'raw')
    data = reader.convert(raw)
    if reader.inp.index != len(raw):
        raise ValueError('Skeleton binary was not fully consumed')
    strings = reader.inp.strings
    seen = set()
    for obj in apk.objects(split):
        if obj.type.name != 'TextAsset':
            continue
        raw = text_asset(obj, 'split')
        reader.inp = binary_input(raw)
        reader.inp.strings = strings
        name = reader.inp.read_string()
        if not name or name in seen:
            raise ValueError('Empty or duplicate split animation name: ' + str(name))
        seen.add(name)
        data.setdefault('animations', {})[name] = reader._read_animation()
        if reader.inp.index != len(raw):
            raise ValueError('Split animation binary was not fully consumed: ' + name)
        records[-1]['animation'] = name
    payload = json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8')
    (destination / 'skeleton.json').write_bytes(payload)
    source = {'manifest': apk.source.version, 'sex': sex, 'output_sha256': digest(payload),
              'tool': {'name': 'SkelToJson', 'version': '1.0.1', 'wheel_sha256': WHEEL_SHA256,
                       'license': 'MIT', 'float_rounding': False,
                       'correction': 'Absent IK setup mix flag decodes to zero; binary attachment name overrides are preserved.'},
              'converter_sha256': digest(Path(__file__).read_bytes()), 'inputs': records,
              'counts': {key: len(data.get(key, [])) for key in
                         ('bones', 'slots', 'skins', 'animations', 'ik', 'transform', 'path', 'physics')}}
    (destination / 'source-manifest.json').write_text(json.dumps(source, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return source


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest')
    parser.add_argument('--sex', choices=('male', 'female'), default='male')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    print(json.dumps(convert(args.manifest, args.sex, args.output)['counts']))
