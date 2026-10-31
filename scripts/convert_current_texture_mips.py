"""Restore all original mip levels as native Windows texture assets."""
import argparse
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
import copy
import gc
import hashlib
import json
from pathlib import Path
from types import SimpleNamespace

import UnityPy
from UnityPy.enums import TextureFormat
from UnityPy.export.Texture2DConverter import parse_image_data
import yaml

from prepare_current_asset_inventory import sha
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_native_scene import decode_mips
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header


def decode(texture, obj):
    """Keep native uncompressed data; decode compressed faces and mip levels."""
    if texture.m_PlatformBlob or texture.m_MipsStripped:
        raise ValueError('Platform layout or stripped mip levels need recovery')
    if texture.m_Width == texture.m_Height == 0:
        if texture.image_data or texture.m_StreamData.size:
            raise ValueError('Empty dynamic texture has unexpected pixel data')
        return b'', [], texture.m_TextureFormat, 'original-empty'
    if texture.m_TextureDimension != 2 or texture.m_ImageCount not in (1, 6):
        raise ValueError('Unexpected texture topology')
    original = texture.get_image_data()
    format_ = TextureFormat(texture.m_TextureFormat).name
    per_face = texture.m_CompleteImageSize
    if len(original) != per_face * texture.m_ImageCount:
        raise ValueError('Texture face sizes differ from original descriptor')
    output, records = bytearray(), []
    native_format = int(texture.m_TextureFormat)
    profile = 'original-uncompressed'
    for face in range(texture.m_ImageCount):
        source = original[face * per_face:(face + 1) * per_face]
        if format_.startswith('ASTC_'):
            proxy = SimpleNamespace(m_TextureFormat=texture.m_TextureFormat, m_ColorSpace=texture.m_ColorSpace,
                                    m_Width=texture.m_Width, m_Height=texture.m_Height,
                                    m_MipCount=texture.m_MipCount, get_image_data=lambda: source)
            pixels, mips, profile = decode_mips(proxy)
            for mip in mips:
                records.append(dict(mip, face=face, source_face_offset=face * per_face,
                                    native_offset=len(output) + mip['rgba_offset']))
            output.extend(pixels)
            native_format = 4
            continue
        offset = 0
        for level in range(texture.m_MipCount):
            width, height = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
            if format_ in ('ETC_RGB4', 'ETC2_RGBA8'):
                size = ((width + 3) // 4) * ((height + 3) // 4) * (8 if format_ == 'ETC_RGB4' else 16)
            else:
                size = width * height * {'Alpha8': 1, 'RGBA32': 4, 'RGB24': 3, 'RGBA4444': 2}[format_]
            block = source[offset:offset + size]
            if len(block) != size:
                raise ValueError('Truncated original mip')
            if format_ in ('ETC_RGB4', 'ETC2_RGBA8'):
                pixels = parse_image_data(block, width, height, texture.m_TextureFormat,
                                          obj.version, obj.platform, flip=False).convert('RGBA').tobytes()
                native_format, profile = 4, format_ + '_UNORM'
            else:
                pixels = block
            records.append(dict(face=face, level=level, width=width, height=height,
                                source_offset=face * per_face + offset, source_size=size,
                                source_sha256=sha(block), native_offset=len(output),
                                native_size=len(pixels), native_sha256=sha(pixels)))
            output.extend(pixels)
            offset += size
        if offset != len(source):
            raise ValueError('Unconsumed original mip bytes')
    return bytes(output), records, native_format, profile


def convert_one(cache, output, row, obj):
    if sha(obj.get_raw_data()) != row['object_sha256']:
        raise ValueError('Texture object changed')
    key = hashlib.sha256((row['cab'].casefold() + ':' + row['path_id']).encode()).hexdigest()
    receipt = output / 'Records' / (key + '.json')
    if receipt.is_file():
        previous = json.loads(receipt.read_bytes())
        if previous['source'] != row:
            raise ValueError('Existing texture receipt belongs to a different input')
        for item in previous['outputs']:
            if sha((output / item['path']).read_bytes()) != item['sha256']:
                raise ValueError('Existing native texture changed')
        prior_converter = output / 'Converters' / (previous['generator_sha256'] + '.py')
        if previous['generator_sha256'] == sha(Path(__file__).read_bytes()) or (previous['pixel_bytes'] and prior_converter.is_file() and sha(prior_converter.read_bytes()) == previous['generator_sha256']):
            return previous
    texture = obj.read()
    pixels, mips, native_format, profile = decode(texture, obj)
    original = obj.read_typetree()
    original.pop('image data')
    original_fields = copy.deepcopy(original)
    native = source_header()
    native.update(fields(original))
    images = max(1, texture.m_ImageCount)
    native.update(serializedVersion=2, m_TextureFormat=native_format,
                  m_CompleteImageSize=len(pixels) // images, m_PlatformBlob='',
                  m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
    native['m_TextureSettings']['serializedVersion'] = 2
    native['image data'] = len(pixels)
    # The YAML scalar scanner need not scan millions of already-validated hex
    # characters. Insert the hex after serializing the original field layout.
    native['_typelessdata'] = 'ZJCS_EXACT_PIXEL_HEX'
    kind = row['type']
    class_id = 28 if kind == 'Texture2D' else 89
    file_id = class_id * 100000
    text = yaml.dump({kind: native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    assert text.count('_typelessdata: ZJCS_EXACT_PIXEL_HEX') == 1
    text = text.replace('_typelessdata: ZJCS_EXACT_PIXEL_HEX', '_typelessdata: ' + (pixels.hex() if pixels else "''"))
    payload = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!' + str(class_id) + ' &' + str(file_id) + '\n' + text).encode('utf-8')
    target = output / 'Assets/Textures' / key[:2] / (key + '.asset')
    target.parent.mkdir(parents=True, exist_ok=True)
    guid = hashlib.md5(('zjcs-current-native-texture:' + row['cab'].casefold() + ':' + row['path_id']).encode()).hexdigest()
    metadata = ('fileFormatVersion: 2\nguid: ' + guid + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(file_id) + '\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    outputs = []
    for path, data in [(target, payload), (Path(str(target) + '.meta'), metadata)]:
        if path.exists() and path.read_bytes() != data:
            existing = path.read_bytes()
            if not receipt.is_file() or not any(r['path'] == path.relative_to(output).as_posix() and r['sha256'] == sha(existing) for r in previous['outputs']):
                raise ValueError('Preserved native texture differs: ' + str(path))
            prior = output / 'Previous' / sha(existing)
            prior.parent.mkdir(exist_ok=True)
            if not prior.exists():
                prior.write_bytes(existing)
        path.write_bytes(data)
        outputs.append(dict(path=path.relative_to(output).as_posix(), bytes=len(data), sha256=sha(data)))
    result = dict(source=row, generator_sha256=sha(Path(__file__).read_bytes()),
                  original_fields=original_fields, native_format=int(native_format), profile=profile,
                  guid=guid, file_id=file_id, pixels_sha256=sha(pixels), pixel_bytes=len(pixels),
                  mips=mips, outputs=outputs)
    receipt.parent.mkdir(exist_ok=True)
    receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return result


def build(cache, workers=2):
    inventory_path = cache / 'all-assets/inventory.json'
    inventory = json.loads(inventory_path.read_bytes())
    inputs = {r['output']: r for r in json.loads((cache / 'source-manifest.json').read_bytes())['inputs']}
    grouped = defaultdict(list)
    for row in inventory['textures']:
        grouped[row['input']].append(row)
    output = cache / 'all-assets/windows-textures'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'Records').mkdir(exist_ok=True)
    (output / 'Converters').mkdir(exist_ok=True)
    converter = Path(__file__).read_bytes()
    (output / 'Converters' / (sha(converter) + '.py')).write_bytes(converter)
    results, failures = [], []
    with ThreadPoolExecutor(max_workers=workers) as pool:
        for index, (source, rows) in enumerate(grouped.items()):
            path = cache / source
            raw = path.read_bytes()
            if sha(raw) != inputs[source]['decoded_sha256']:
                raise ValueError('Input bytes changed: ' + source)
            env = UnityPy.load(raw)
            if source == 'inputs/unity default resources':
                for bundle in env.files.values():
                    for file in serialized_files(bundle):
                        file.name = 'unity default resources'
            objects = {(f.name.casefold(), str(o.path_id)): o for b in env.files.values()
                       for f in serialized_files(b) for o in f.objects.values()}
            pending = [(row, pool.submit(convert_one, cache, output, row, objects[row['cab'].casefold(), row['path_id']])) for row in rows]
            for row, future in pending:
                try:
                    results.append(future.result())
                except Exception as error:
                    failures.append(dict(source=row, error=str(error)))
                    print('FAILED', row['name'], str(error)[:200], flush=True)
            if index % 32 == 0:
                gc.collect()
                print('Texture bundles', index, '/', len(grouped), 'textures', len(results), 'unresolved', len(failures), flush=True)
    result = dict(manifest=inventory['manifest'], inventory_sha256=sha(inventory_path.read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), textures=results, unresolved=failures,
                  counts=dict(textures=len(results), mip_levels=sum(len(r['mips']) for r in results),
                              pixel_bytes=sum(r['pixel_bytes'] for r in results), formats=dict(Counter(r['source']['format_name'] for r in results))),
                  completed=not failures)
    (output / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Texture result', result['counts'], 'unresolved', len(failures), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--workers', type=int, default=2)
    args = parser.parse_args()
    build(args.cache.resolve(), args.workers)
