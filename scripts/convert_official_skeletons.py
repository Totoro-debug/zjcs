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
        def convert(self, data):
            self._skin_membership = []
            result = super().convert(data)
            self._restore_skin_membership(result)
            return result

        def _restore_skin_membership(self, result):
            # The binary format reader consumes these indices but omits them
            # from JSON. They activate skin-required bones and constraints.
            for skin, references in self._skin_membership:
                for field, indices in references.items():
                    definitions = result.get(field, [])
                    if any(index < 0 or index >= len(definitions) for index in indices):
                        raise ValueError("Skin references an absent " + field + " entry: " + skin["name"])
                    if indices:
                        skin[field] = [definitions[index]["name"] for index in indices]

        def _read_skin(self, default_skin):
            start = self.inp.index
            skin = super()._read_skin(default_skin)
            end = self.inp.index
            if default_skin or skin is None:
                return skin
            try:
                self.inp.index = start
                name = self.inp.read_string()
                if name != skin["name"]:
                    raise ValueError("Skin header replay changed its name")
                if self.nonessential:
                    skin["color"] = format(self.inp.read_color_int() & 0xffffffff, "08x")
                if self.is_43:
                    raise ValueError("Review unified 4.3 skin constraints before conversion")
                references = {field: [self.inp.read_varint(True)
                                      for _ in range(self.inp.read_varint(True))]
                              for field in ("bones", "ik", "transform", "path", "physics")}
                self._skin_membership.append((skin, references))
            finally:
                self.inp.index = end
            return skin

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


def convert(manifest=None, sex='male', output=None, *, model_bundle=None, skeleton_name=None):
    reader_class, binary_input = reader_type()
    apk = Apk(manifest=manifest)
    if (model_bundle is None) != (skeleton_name is None):
        raise ValueError('Custom model bundle and skeleton name must be specified together')
    identity = sex if model_bundle is None else skeleton_name.removesuffix('.skel')
    destination = output or ROOT / '.tools/source-packages/current' / apk.source.version / 'skeletons' / identity
    destination.mkdir(parents=True, exist_ok=True)
    records = []
    model = model_bundle or f'defaultpackage_assets_appearanceassets_character_model_{sex}_1.bundle'
    expected_name = skeleton_name or f'character_{sex}_1.skel'
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
                 and obj.read().m_Name.lower() == expected_name.lower()]
    if len(skeletons) != 1:
        raise ValueError('Expected exactly one original skeleton: ' + model + '/' + expected_name)
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
    source = {'manifest': apk.source.version, 'modelBundle': model, 'skeletonName': expected_name,
              'output_sha256': digest(payload),
              'tool': {'name': 'SkelToJson', 'version': '1.0.1', 'wheel_sha256': WHEEL_SHA256,
                       'license': 'MIT', 'float_rounding': False,
                       'correction': 'Absent IK setup mix flag decodes to zero; binary attachment name overrides and skin bone/constraint memberships are preserved.'},
              'converter_sha256': digest(Path(__file__).read_bytes()), 'inputs': records,
              'counts': {key: len(data.get(key, [])) for key in
                         ('bones', 'slots', 'skins', 'animations', 'ik', 'transform', 'path', 'physics')}}
    if model_bundle is None:
        source['sex'] = sex
    (destination / 'source-manifest.json').write_text(json.dumps(source, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return source


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest')
    parser.add_argument('--sex', choices=('male', 'female'), default='male')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--model-bundle')
    parser.add_argument('--skeleton-name')
    args = parser.parse_args()
    print(json.dumps(convert(args.manifest, args.sex, args.output, model_bundle=args.model_bundle,
                             skeleton_name=args.skeleton_name)['counts']))
