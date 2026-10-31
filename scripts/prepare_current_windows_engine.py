"""Prove native Windows engine counterparts against the original GPU programs."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

import UnityPy
from official_apk import ROOT
from official_engine_shader_equivalence import prove
from prepare_current_asset_inventory import sha
from prepare_unity_bundle_inputs import serialized_files


def projected(original, native, path=''):
    if isinstance(original, dict):
        for key, value in original.items():
            if key not in native:
                raise ValueError('Windows engine omitted field: ' + path + '/' + key)
            projected(value, native[key], path + '/' + key)
        for key in native.keys() - original.keys():
            value = native[key]
            if key == 'compilerPlatform' and isinstance(value, int):
                continue
            if key == 'needsReflectionData' and isinstance(value, bool):
                continue
            if key == 'isCompiled' and value is True:
                continue
            if key == 'keywords' and value == []:
                continue
            raise ValueError('Unexpected added engine field: ' + path + '/' + key)
    elif isinstance(original, (list, tuple)):
        if len(original) != len(native):
            raise ValueError('Windows engine array differs: ' + path)
        for index, (a, b) in enumerate(zip(original, native)):
            projected(a, b, path + '/' + str(index))
    elif original != native:
        raise ValueError('Windows engine value differs: ' + path)


def build(cache):
    inventory = json.loads((cache / 'all-assets/inventory.json').read_bytes())
    editor = Path(json.loads((ROOT / 'unity-tools.json').read_bytes())['editorExecutable'])
    original_path = cache / 'inputs/unity default resources'
    windows_path = editor.parent / 'Data/Resources/unity default resources'
    original = UnityPy.load(str(original_path))
    windows = UnityPy.load(str(windows_path))
    native = {}
    for obj in windows.objects:
        if obj.type.name in ('Shader', 'ComputeShader'):
            value = obj.read()
            name = value.m_ParsedForm.m_Name if obj.type.name == 'Shader' else value.m_Name
            native[obj.type.name, name] = obj
    proofs = []
    for obj in original.objects:
        if obj.type.name not in ('Shader', 'ComputeShader'):
            continue
        value = obj.read()
        name = value.m_ParsedForm.m_Name if obj.type.name == 'Shader' else value.m_Name
        counterpart = native[obj.type.name, name]
        record = dict(type=obj.type.name, name=name, original_file_id=obj.path_id,
                      original_object_sha256=sha(obj.get_raw_data()),
                      windows_file_id=counterpart.path_id, windows_object_sha256=sha(counterpart.get_raw_data()))
        if obj.type.name == 'Shader':
            try:
                state, programs = prove(obj, counterpart)
            except ValueError:
                continue  # The raster converter restores these two original programs.
            record.update(programs=programs, state_sha256=sha(json.dumps(state, sort_keys=True).encode()))
        else:
            a = obj.read_typetree()['variants']
            b = counterpart.read_typetree()['variants']
            variants = []
            for variant in a:
                candidates = [w for w in b if (w['targetRenderer'], w['targetLevel']) == (variant['targetRenderer'], variant['targetLevel'])]
                if len(candidates) != 1:
                    raise ValueError('No unique matching engine compute API')
                projected(variant, candidates[0])
                variants.append(dict(renderer=variant['targetRenderer'], level=variant['targetLevel'], kernels=len(variant['kernels']),
                                     sha256=sha(json.dumps(variant, sort_keys=True).encode())))
            record.update(original_variants=variants, windows_apis=[v['targetRenderer'] for v in b],
                          windows_kernel_variants=sum(len(k['uniqueVariants']) for v in b for k in v['kernels']))
        proofs.append(record)
    if sum(r['type'] == 'ComputeShader' for r in proofs) != 6:
        raise ValueError('Incomplete original engine compute closure')
    target = cache / 'windows-engine'
    (target / 'inputs').mkdir(parents=True, exist_ok=True)
    destination = target / 'inputs/unity default resources'
    raw = windows_path.read_bytes()
    if destination.exists() and destination.read_bytes() != raw:
        raise ValueError('Existing Windows engine input changed')
    destination.write_bytes(raw)
    receipt = dict(manifest=inventory['manifest'], generator_sha256=sha(Path(__file__).read_bytes()),
                   source=dict(path=str(windows_path), sha256=sha(raw), role='matching-windows-unity-engine', version='2022.3.57f1c2'),
                   original_source=dict(path=original_path.relative_to(ROOT).as_posix(), sha256=sha(original_path.read_bytes())),
                   inputs=[dict(output='inputs/unity default resources', format='builtin', raw_sha256=sha(raw), decoded_sha256=sha(raw))],
                   counterparts=proofs,
                   backend_note='The engine VRS array compute shader has only its original Vulkan backend; no D3D-only algorithm is invented.')
    (target / 'source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Proved native engine counterparts:', len(proofs), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    build(parser.parse_args().cache.resolve())
