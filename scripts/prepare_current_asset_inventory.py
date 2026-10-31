"""Inventory every verified current object, including unreferenced bundles."""
import argparse
from collections import Counter
import gc
import hashlib
import json
from pathlib import Path

import UnityPy
from UnityPy.enums import TextureFormat
from official_apk import ROOT
from prepare_unity_bundle_inputs import serialized_files


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(cache):
    cache = cache.resolve()
    manifest = cache / 'source-manifest.json'
    document = json.loads(manifest.read_bytes())
    textures, shaders, sources = [], [], []
    counts = Counter()
    identities = {}
    for index, row in enumerate(document['inputs']):
        if row['format'] not in ('UnityFS', 'builtin'):
            continue
        path = cache / row['output']
        raw = path.read_bytes()
        if sha(raw) != row['decoded_sha256']:
            raise ValueError('Input hash differs: ' + row['output'])
        env = UnityPy.load(raw)
        for bundle in env.files.values():
            for file in serialized_files(bundle):
                if row['output'] == 'inputs/unity default resources':
                    file.name = 'unity default resources'
                for obj in file.objects.values():
                    counts[obj.type.name] += 1
                    if obj.type.name not in ('Texture2D', 'Cubemap', 'Texture2DArray', 'CubemapArray', 'Texture3D', 'Shader', 'ComputeShader'):
                        continue
                    key = file.name.casefold(), str(obj.path_id)
                    digest = sha(obj.get_raw_data())
                    if key in identities:
                        if identities[key] != digest:
                            raise ValueError('Conflicting CAB/PathID: ' + repr(key))
                        continue
                    identities[key] = digest
                    item = dict(input=row['output'], cab=file.name, path_id=str(obj.path_id),
                                type=obj.type.name, object_sha256=digest)
                    value = obj.read()
                    item['name'] = value.m_Name
                    if obj.type.name in ('Shader', 'ComputeShader'):
                        if obj.type.name == 'Shader':
                            item.update(name=value.m_ParsedForm.m_Name, platforms=value.platforms,
                                        subshaders=len(value.m_ParsedForm.m_SubShaders),
                                        passes=sum(len(s.m_Passes) for s in value.m_ParsedForm.m_SubShaders))
                        shaders.append(item)
                    else:
                        item.update(width=getattr(value, 'm_Width', None), height=getattr(value, 'm_Height', None),
                                    format=int(value.m_TextureFormat), format_name=TextureFormat(value.m_TextureFormat).name,
                                    mips=getattr(value, 'm_MipCount', None), images=getattr(value, 'm_ImageCount', None),
                                    dimension=getattr(value, 'm_TextureDimension', None), stripped=getattr(value, 'm_MipsStripped', None))
                        textures.append(item)
        sources.append(dict(path=row['output'], sha256=row['decoded_sha256']))
        if index % 128 == 0:
            gc.collect()
            print('Inputs', index, 'textures', len(textures), 'shaders', len(shaders), flush=True)
    output = cache / 'all-assets'
    output.mkdir(exist_ok=True)
    result = dict(manifest=document['manifest'], input_manifest_sha256=sha(manifest.read_bytes()),
                  generator_sha256=sha(Path(__file__).read_bytes()), inputs=sources,
                  types=dict(sorted(counts.items())), textures=textures, shaders=shaders,
                  completed=True)
    (output / 'inventory.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Completed:', len(sources), 'inputs;', len(textures), 'textures;', len(shaders), 'shaders', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    build(parser.parse_args().cache)
