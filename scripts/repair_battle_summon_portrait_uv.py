"""Bake the original packed UV transform into the existing native rock portrait."""
import hashlib
import json
import re
import struct
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    receipt_path = ROOT / 'assets/reference/battle-rules/summon-models-source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    portraits = json.loads((ROOT / 'assets/reference/battle-hud/status-graph-portraits-source-manifest.json').read_bytes())
    source = portraits['portraits']['3181']
    original, = [row for row in receipt['inputs'] if row['bundle'] == source['source']['bundle']
        and row['path_id'] == source['source']['path_id']]
    if (original['object_sha256'] != source['sprite_object_sha256']
            or original['bundle_sha256'] != source['source']['bundle_sha256']):
        raise ValueError('The selected source differs from the previously recovered original summon Sprite')
    relative = 'unity/Assets/Official/Combat/Summons/G_3181.sprite.asset'
    path = ROOT / relative
    before = path.read_bytes()
    expected, = [row for row in receipt['outputs'] if row['path'] == relative]
    if sha(before.replace(b'\r\n', b'\n')) != expected['sha256']:
        raise ValueError('Preserve the changed original summon portrait')
    text = re.sub(r'(?m)^(\s*(?:m_IndexBuffer|_typelessdata):) ([0-9a-fA-F]+)$', r'\1 "\2"', before.decode())
    sprite = yaml.safe_load(re.sub(r'(?m)^--- !u!\d+ &-?\d+', '---', re.sub(r'(?m)^%.*\n', '', text)))['Sprite']
    rd = sprite['m_RD']
    vd = rd['m_VertexData']
    raw = bytes.fromhex(vd['_typelessdata'])
    native = source['native_sprite']['expected']
    count = len(native['vertices'])
    offset = (count * 12 + 15) // 16 * 16
    if (vd['m_VertexCount'] != count or len(raw) != offset + count * 8
            or [(c['stream'], c['offset'], c['format'], c['dimension'])
                for c in vd['m_Channels'] if c['dimension']] != [(0, 0, 0, 3), (1, 0, 0, 2)]):
        raise ValueError('Original summon portrait native streams differ')
    positions = b''.join(struct.pack('<fff', v['x'], v['y'], 0) for v in native['vertices'])
    indices = b''.join(struct.pack('<H', value) for value in native['triangles'])
    if raw[:count * 12] != positions or bytes.fromhex(rd['m_IndexBuffer']) != indices:
        raise ValueError('Preserve original summon position/index bytes')
    restored = raw[:offset] + b''.join(struct.pack('<ff', uv['x'], uv['y']) for uv in native['uv'])
    vd.update(serializedVersion=3, m_DataSize=len(restored), _typelessdata=restored.hex())
    sprite['m_Rect']['serializedVersion'] = 2
    rd['serializedVersion'] = 3
    rd['textureRect']['serializedVersion'] = 2
    for submesh in rd['m_SubMeshes']:
        submesh['serializedVersion'] = 2
    class Dumper(yaml.SafeDumper):
        pass
    def represent(dumper, value):
        return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(), flow_style=bool(value)
            and all(not isinstance(v, (dict, list)) for v in value.values()))
    Dumper.add_representer(dict, represent)
    updated = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n'
        + yaml.dump({'Sprite': sprite}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)).encode()
    settings_path = ROOT / 'unity/Assets/Official/Combat/Summons/3181.json'
    settings_before = settings_path.read_bytes()
    settings_entry, = [row for row in receipt['outputs'] if row['path'] == settings_path.relative_to(ROOT).as_posix()]
    if sha(settings_before.replace(b'\r\n', b'\n')) != settings_entry['sha256']:
        raise ValueError('Preserve changed original summon settings')
    settings = json.loads(settings_before)
    settings['portraitData']['uv'] = native['uv']
    from prepare_unity_map13_sources import encode
    settings_updated = encode(settings)
    proof = dict(source_manifest=source['manifest'], source=source['source'],
        sprite_object_sha256=source['sprite_object_sha256'],
        original_input_manifest=receipt['manifest'], identical_original_bundle=True,
        position_bytes_sha256=sha(positions), index_bytes_sha256=sha(indices), uv=native['uv'],
        generator_sha256=sha(Path(__file__).read_bytes()),
        full_generator_sha256=sha((ROOT / 'scripts/prepare_battle_summon_models.py').read_bytes()),
        operation='Bake the original runtime uvTransform only; preserve original logical Sprite, positions, indices, '
            'texture binding, full RGBA texture and existing asset GUID. Restore native serialization versions.')
    outputs = {path: updated, settings_path: settings_updated}
    for output, data in outputs.items():
        output.write_bytes(data)
        entry, = [row for row in receipt['outputs'] if row['path'] == output.relative_to(ROOT).as_posix()]
        entry['sha256'] = sha(data)
    receipt['portrait_uv_recovery'] = proof
    receipt.pop('limitations', None)
    receipt['operation'] = ('APK-first verified source recovery, lossless skeleton decoding, original geometry, '
        'curves, transforms, full high-resolution texture mips and material/shader references. Preserve native '
        'Sprite positions/indices and logical fields; bake the original packed UV transform for Windows. '
        'No Spine runtime loaded or sampled.')
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Restored original rock portrait UVs; positions, indices, texture and GUID unchanged:', count, 'vertices')


if __name__ == '__main__':
    main()
