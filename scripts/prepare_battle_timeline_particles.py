"""Preserve the original, initially disabled scene-timeline particle hierarchy."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import re
import sys
import uuid
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_shader_programs import gles_programs
from prepare_fruit_tree_unity_assets import parse_documents, property_id, game_object_name

STAGE = ROOT/'.tools/battle-system-migration/particle-ripper'
EXPORT = STAGE/'export/ExportedProject/Assets'
DEST = ROOT/'unity/Assets/Official/Combat/Timeline/Source/Particles'
OUTPUT = DEST/'Eff_UI_Icon_Death01.prefab'
RECEIPT = ROOT/'assets/reference/battle-hud/timeline-particles-source-manifest.json'
SHADER = ROOT/'unity/Assets/Game/Runtime/Presentation/Combat/Shaders/TimelineParticleMainTex.shader'
BUNDLE = 'defaultpackage_assets_prefabs_ui_fight_gameroundcharacteritem_scene.bundle'
GUID = re.compile(r'guid: ([0-9a-f]{32})')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def stable_meta(path, template):
    meta = path.with_name(path.name+'.meta')
    guid = GUID.search(meta.read_text()).group(1) if meta.exists() else uuid.uuid5(uuid.NAMESPACE_URL, path.relative_to(ROOT).as_posix()).hex
    meta.write_bytes(GUID.sub('guid: '+guid, template).encode())
    return guid


def build():
    apk = Apk()
    original = EXPORT/'Prefabs/UI/Fight/GameRoundCharacterItem_Scene.prefab'
    header, docs = parse_documents(original.read_text(encoding='utf-8'))
    root_go = next(key for key, (kind, text) in docs.items() if kind == 1 and game_object_name(text) == 'Eff_UI_Icon_Death01')
    transforms = {key: text for key, (kind, text) in docs.items() if kind in (4, 224)}
    root = next(key for key, text in transforms.items() if property_id(text, 'm_GameObject') == root_go)
    descendants = {root}
    while True:
        expanded = descendants | {key for key, text in transforms.items() if property_id(text, 'm_Father') in descendants}
        if expanded == descendants:
            break
        descendants = expanded
    gameobjects = {property_id(transforms[key], 'm_GameObject') for key in descendants}
    keep = descendants | gameobjects | {key for key, (kind, text) in docs.items()
        if kind in (198, 199) and property_id(text, 'm_GameObject') in gameobjects}
    if sum(docs[key][0] == 198 for key in keep) != 2 or sum(docs[key][0] == 199 for key in keep) != 2:
        raise ValueError('The original timeline particle system coverage changed')
    selected = []
    for key, (kind, text) in docs.items():
        if key not in keep:
            continue
        if kind == 1:
            text = re.sub(r'(?m)^  - component: \{fileID: (-?\d+)\}\r?\n',
                          lambda match: match[0] if match[1] in keep else '', text)
        elif key == root:
            text = re.sub(r'm_Father: \{fileID: -?\d+\}', 'm_Father: {fileID: 0}', text)
        if kind == 199:
            # Preserve the same UI layer order in this project's flattened layers.
            layer_id = int(re.search(r'(?m)^  m_SortingLayerID: (-?\d+)', text)[1]) & 0xffffffff
            layers = json.loads((ROOT/'unity/Assets/Game/Resources/Combat/battle-scene-bindings.json').read_bytes())['layers']
            index = next(i for i, row in enumerate(layers) if row['uniqueID'] == layer_id)
            order = int(re.search(r'(?m)^  m_SortingOrder: (-?\d+)', text)[1]) + index * 1000
            for prop, value in (('m_SortingLayerID', 0), ('m_SortingLayer', 0), ('m_SortingOrder', order)):
                text = re.sub(r'(?m)^(  '+prop+r': )-?\d+', lambda m: m[1]+str(value), text)
        selected.append(text)
    text = header+''.join(selected)
    for ref in re.findall(r'\{fileID: (-?\d+)\}', text):
        if ref != '0' and ref not in keep:
            raise ValueError('Particle hierarchy has an unresolved internal reference: '+ref)

    DEST.mkdir(parents=True, exist_ok=True)
    shader_guid = stable_meta(SHADER, 'fileFormatVersion: 2\nguid: 00000000000000000000000000000000\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n')
    exported_guids = {}
    for meta in EXPORT.rglob('*.meta'):
        match = GUID.search(meta.read_text(encoding='utf-8'))
        if match:
            exported_guids[match[1]] = meta.with_name(meta.name[:-5])
    mapping, exports = {}, []

    def copy_guid(guid):
        if guid in mapping:
            return mapping[guid]
        source = exported_guids[guid]
        if source.suffix == '.shader':
            if source.name != 'Noise_Mask_Pro.shader':
                raise ValueError('Restore this original particle shader: '+str(source))
            mapping[guid] = shader_guid
            return shader_guid
        if source.suffix not in ('.mat', '.png'):
            raise ValueError('Restore this original particle dependency: '+str(source))
        target = DEST/source.name
        meta_text = source.with_name(source.name+'.meta').read_text(encoding='utf-8')
        mapping[guid] = stable_meta(target, meta_text)
        raw = source.read_bytes()
        target.write_bytes(GUID.sub(lambda m: 'guid: '+copy_guid(m[1]), raw.decode()).encode() if source.suffix == '.mat' else raw)
        exports.append(dict(path=source.relative_to(ROOT).as_posix(), sha256=sha(raw)))
        return mapping[guid]

    text = GUID.sub(lambda match: 'guid: '+copy_guid(match[1]), text)
    OUTPUT.write_bytes(text.encode())
    stable_meta(OUTPUT, 'fileFormatVersion: 2\nguid: 00000000000000000000000000000000\nPrefabImporter:\n  externalObjects: {}\n')
    inputs, shaders = [], set()
    for obj in apk.objects(BUNDLE):
        if obj.type.name not in ('ParticleSystem', 'ParticleSystemRenderer'):
            continue
        inputs.append(dict(apk.provenance(obj), type=obj.type.name, object_sha256=sha(obj.get_raw_data())))
        if obj.type.name != 'ParticleSystemRenderer':
            continue
        for pointer in obj.read().m_Materials:
            material = pointer.deref()
            raw = material.read()
            if raw.m_ValidKeywords != ['USE_MAINTEX']:
                raise ValueError('Restore the actual particle material shader variant: '+str(raw.m_ValidKeywords))
            inputs.append(dict(apk.provenance(material), type='Material', object_sha256=sha(material.get_raw_data())))
            shader = raw.m_Shader.deref()
            if shader.path_id not in shaders:
                shaders.add(shader.path_id)
                inputs.append(dict(apk.provenance(shader), type='Shader', object_sha256=sha(shader.get_raw_data())))
                programs = [p for p in gles_programs(shader.read(), with_keywords=True) if set(p['keywords']) == {'USE_MAINTEX'}]
                if len(programs) != 2:
                    raise ValueError('The original particle shader pass coverage changed')
                for i, program in enumerate(programs):
                    (DEST/f'USE_MAINTEX-pass-{i}.glsl.txt').write_bytes(program['source'].encode())
            for _, env in raw.m_SavedProperties.m_TexEnvs:
                if env.m_Texture.path_id:
                    texture = env.m_Texture.deref()
                    inputs.append(dict(apk.provenance(texture), type='Texture2D', object_sha256=sha(texture.get_raw_data())))
    RECEIPT.write_text(json.dumps(dict(manifest=apk.version, inputs=inputs,
        decoded_bundle_inputs=json.loads((STAGE/'inputs.json').read_bytes()),
        exporter=dict(name='AssetRipper 1.3.14', executable_sha256=sha((ROOT/'.tools/assetripper-1.3.14/AssetRipper.GUI.Free.exe').read_bytes()),
                      prefab_sha256=sha(original.read_bytes()), dependency_exports=exports),
        generator_sha256=sha(Path(__file__).read_bytes()), shader_conversion=dict(path=SHADER.relative_to(ROOT).as_posix(),sha256=sha(SHADER.read_bytes())),
        outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in sorted(DEST.iterdir()) if p.is_file()],
        operation='Preserve the exact native ParticleSystem/Renderer serialization and four original transforms. Restore material/texture closure and the original USE_MAINTEX first-pass GLES variant. Preserve the disabled UIParticle parent; no activation or substitute effect is introduced.'),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Original timeline particles restored: 2 systems, 2 renderers, original disabled parent preserved.')


if __name__ == '__main__':
    build()
