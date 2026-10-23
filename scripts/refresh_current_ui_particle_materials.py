"""Bind existing UI particle materials to verified current shaders and samplers."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from activate_current_payloads import activate
from current_conversion_state import activated_receipt
from official_apk import Apk, ROOT


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(activate_outputs=False):
    apk = Apk.current()
    receipt = ROOT / 'assets/reference/official-map/current-ui-particle-materials-source-manifest.json'
    prior = activated_receipt(receipt, apk.version)
    if prior is not None:
        return prior
    runtime = ROOT / 'unity/Assets/Game/Resources/Wishes/wishes-runtime.json'
    targets = {}
    for prefab in json.loads(runtime.read_bytes())['prefabs']:
        for node in prefab.get('nodes', []):
            if not node.get('particle_material_json'):
                continue
            material = json.loads(node['particle_material_json'])
            if 'shader_path_id' not in material:
                source = material['source']
                targets[(source['bundle'], source['path_id'])] = None
    objects, proofs, legacy_objects = {}, {}, {}
    legacy = Apk()

    def enrich(material):
        source = material.get('source', {})
        key = (source.get('bundle'), source.get('path_id'))
        if key not in targets or 'floats' not in material:
            return False
        if key not in proofs:
            if key[0] not in objects:
                objects[key[0]] = {str(o.path_id): o for o in apk.objects(key[0])}
            obj = objects[key[0]][key[1]]
            data = obj.read_typetree()
            shader = obj.read().m_Shader.deref()
            name = shader.read_typetree()['m_ParsedForm']['m_Name']
            if name != 'Effect/Noise_Mask_Pro':
                raise ValueError('Particle shader requires another Windows adapter: ' + name)
            samplers = {}
            for prop, env in obj.read().m_SavedProperties.m_TexEnvs:
                if not env.m_Texture.path_id:
                    continue
                texture = env.m_Texture.deref()
                settings = texture.read_typetree()['m_TextureSettings']
                samplers[prop] = {
                    'settings': {out: settings[field] for out, field in (
                        ('filter_mode', 'm_FilterMode'), ('aniso_level', 'm_Aniso'),
                        ('wrap_u', 'm_WrapU'), ('wrap_v', 'm_WrapV'), ('wrap_w', 'm_WrapW'))},
                    'source': apk.provenance(texture), 'raw_sha256': sha(texture.get_raw_data())}
            keywords = data.get('m_ValidKeywords')
            if keywords is None:
                keywords = data.get('m_ShaderKeywords', '').split()
            proofs[key] = {
                'source': apk.provenance(obj), 'raw_sha256': sha(obj.get_raw_data()),
                'shader': {'name': name, 'source': apk.provenance(shader),
                           'raw_sha256': sha(shader.get_raw_data())},
                'runtimeShader': 'Zjcs/Opening/NoiseMaskPro', 'shaderKeywords': keywords,
                'render_queue': data['m_CustomRenderQueue'],
                'floats': dict(data['m_SavedProperties']['m_Floats']),
                'colors': {name: [value[c] for c in ('r', 'g', 'b', 'a')]
                           for name, value in data['m_SavedProperties']['m_Colors']},
                'samplers': samplers}
        proof = proofs[key]
        if material['floats'] != proof['floats'] or material['colors'] != proof['colors']:
            if key[0] not in legacy_objects:
                legacy_objects[key[0]] = {str(o.path_id): o for o in legacy.objects(key[0])}
            old_obj = legacy_objects[key[0]][key[1]]
            old = old_obj.read_typetree()['m_SavedProperties']
            old_floats = dict(old['m_Floats'])
            old_colors = {name: [value[c] for c in ('r', 'g', 'b', 'a')]
                          for name, value in old['m_Colors']}
            if material['floats'] != old_floats or material['colors'] != old_colors:
                raise ValueError('Particle properties match neither verified official version: ' + str(key))
            proof['previous_version'] = {'source': legacy.provenance(old_obj),
                                         'raw_sha256': sha(old_obj.get_raw_data()),
                                         'floats': old_floats, 'colors': old_colors}
            material['floats'] = proof['floats']
            material['colors'] = proof['colors']
        material.update({name: proof[name] for name in (
            'source', 'runtimeShader', 'shaderKeywords', 'render_queue')})
        for name, texture in material['textures'].items():
            texture.update(proof['samplers'][name]['settings'])
        return True

    def walk(value):
        count = 0
        if isinstance(value, dict):
            count += enrich(value)
            for key, child in list(value.items()):
                if key == 'particle_material_json' and child:
                    material = json.loads(child)
                    if enrich(material):
                        value[key] = json.dumps(material, ensure_ascii=False, separators=(',', ':'))
                        count += 1
                else:
                    count += walk(child)
        elif isinstance(value, list):
            count += sum(walk(child) for child in value)
        return count

    staged = ROOT / '.tools/source-packages/current' / apk.version / 'ui-particle-materials'
    staged.mkdir(parents=True, exist_ok=True)
    outputs = []
    for path in (ROOT / 'assets/official/wish-performance/performance.json', runtime):
        raw = path.read_bytes()
        document = json.loads(raw)
        count = walk(document)
        if not count:
            raise ValueError('No source particle material bindings: ' + str(path))
        payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        destination = staged / (sha(payload) + '.json')
        destination.write_bytes(payload)
        outputs.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                        'sha256': sha(payload), 'bytes': len(payload), 'bindings': count,
                        'staged': destination.relative_to(ROOT).as_posix()})
    result = {'manifest': apk.version, 'operation': __doc__, 'outputs': outputs,
              'materials': list(proofs.values()), 'generator_sha256': sha(Path(__file__).read_bytes())}
    stage_receipt = staged / 'source-manifest.json'
    stage_receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    if activate_outputs:
        return activate(stage_receipt, Path(__file__), receipt)
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    result = build(parser.parse_args().activate)
    print('Verified current UI particle materials:', len(result['materials']))
