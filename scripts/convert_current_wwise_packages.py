"""Convert all verified current Wwise packages to Windows without changing payloads."""
import hashlib
import json
from pathlib import Path
import struct

from official_apk import ROOT
from official_source import OfficialSource
from convert_wwise_android_package import convert_android_package_to_windows


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def languages(raw, encoding):
    length = struct.unpack_from('<I', raw, 12)[0]
    block = raw[28:28+length]
    count = struct.unpack_from('<I', block)[0]
    result = []
    for index in range(count):
        offset, identity = struct.unpack_from('<2I', block, 4+index*8)
        width = 2 if encoding == 'utf-16-le' else 1
        end = offset
        while block[end:end+width] != bytes(width):
            end += width
            if end+width > len(block):
                raise ValueError('Package language string is unterminated')
        result.append(dict(id=identity, name=block[offset:end].decode(encoding)))
    return result


def entries(raw):
    language, banks, media, external = struct.unpack_from('<4I', raw, 12)
    cursor, records = 28+language, []
    for kind, size, id_width in [('bank', banks, 4), ('media', media, 4), ('external', external, 8)]:
        count = struct.unpack_from('<I', raw, cursor)[0]
        stride = id_width+16
        if size != 4+count*stride:
            raise ValueError('Package lookup table size differs')
        for index in range(count):
            at = cursor+4+index*stride
            identity = int.from_bytes(raw[at:at+id_width], 'little')
            block, length, start, language_id = struct.unpack_from('<4I', raw, at+id_width)
            offset = block*start
            payload = raw[offset:offset+length]
            if len(payload) != length:
                raise ValueError('Package payload is truncated')
            records.append(dict(kind=kind, id=identity, language=language_id, bytes=length, sha256=sha(payload)))
        cursor += size
    return records


def convert():
    source = OfficialSource()
    base = source.prepared_path/'native-conversion'
    input_path = base/'source-manifest.json'
    raw_path = base/'raw-resources-source-manifest.json'
    inputs = json.loads(input_path.read_bytes())
    raw_receipt = json.loads(raw_path.read_bytes())
    if inputs['manifest'] != source.version or raw_receipt['manifest'] != source.version or not raw_receipt['completed']:
        raise ValueError('Current original package extraction is incomplete')
    rows = {row['name']: row for row in inputs['inputs'] if row.get('package') == 'WwiseBnk'}
    destination = base/'windows-wwise'
    destination.mkdir(exist_ok=True)
    packages = []
    for package in raw_receipt['packages']:
        if package['kind'] != 'wwise-package':
            continue
        row = rows[package['name']]
        original = (base/row['output']).read_bytes()
        if sha(original) != row['raw_sha256'] or sha(original) != package['sha256']:
            raise ValueError('Original Wwise package changed: '+package['name'])
        windows = convert_android_package_to_windows(original)
        expected_languages = languages(original, 'ascii')
        if languages(windows, 'utf-16-le') != expected_languages:
            raise ValueError('Windows language lookup differs from its original')
        records = entries(original)
        if entries(windows) != records:
            raise ValueError('Windows package changed an original bank, media or external payload')
        relative = package['name'].removeprefix('wwisebnk_assets_wwisedata_android_').removesuffix('_pck.rawfile')+'.pck'
        path = destination/relative
        if path.exists() and path.read_bytes() != windows:
            raise ValueError('Existing Windows package changed: '+str(path))
        if not path.exists():
            temporary = path.with_suffix('.pending')
            temporary.write_bytes(windows)
            temporary.replace(path)
        packages.append(dict(name=package['name'], source=row['source'], original_sha256=sha(original),
                             path=relative, sha256=sha(windows), bytes=len(windows),
                             header_growth=len(windows)-len(original), languages=expected_languages, entries=records))
        print('Verified Windows Wwise package:', len(packages), relative, flush=True)
    result = dict(manifest=source.version, apk_sha256=source.apk_sha256, completed=True,
                  scope='All current Wwise package language/index conversion; native engine playback and per-module event bindings remain separate.',
                  input_manifest_sha256=sha(input_path.read_bytes()), raw_manifest_sha256=sha(raw_path.read_bytes()),
                  packages=packages, generator_sha256=sha(Path(__file__).read_bytes()),
                  converter_sha256=sha((ROOT/'scripts/convert_wwise_android_package.py').read_bytes()))
    path = destination/'source-manifest.json'
    temporary = destination/'source-manifest.pending.json'
    temporary.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    temporary.replace(path)
    print('Completed Windows Wwise packages:', len(packages), flush=True)


if __name__ == '__main__':
    convert()
