"""Recover battle portrait photography and the actual scene timeline prefab bindings."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path

OUTPUT = ROOT / 'unity/Assets/Official/Combat/Portraits/Resources/Combat/battle-portraits.json'
RECEIPT = ROOT / 'assets/reference/battle-hud/portrait-source-manifest.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build():
    schemas = ScriptSchemas()
    inputs = []

    def record(apk, obj):
        inputs.append(dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), selected_manifest=apk.version))
        return obj

    versions = []
    for version in ('854_156383', '871_156598'):
        apk = Apk(version)
        config = schemas.read(record(apk, apk.asset('scriptableconfig_modules_fight', '/GamePlayManagerConfig.asset')))
        settings = schemas.read(record(apk, apk.asset('scriptableconfig_modules_playerportrait', '/PlayerPortraitSettings.asset')))
        parameters = next(row for row in settings['mPhotographyParameters'] if row['MyTypes'] == [0])
        versions.append(dict(manifest=version, etc2=bool(config['EnablePlayerPortraitETC2']),
                             scene=bool(config['EnablePlayerPortraitETC2_Scene']), head=parameters))
    if {k: v for k, v in versions[0].items() if k != 'manifest'} != {k: v for k, v in versions[1].items() if k != 'manifest'}:
        raise ValueError('Current portrait settings differ from the historical battle resources')

    apk = Apk()
    prefabs, requestors = [], []
    for key, bundle, suffix in (
        ('studio', 'prefabs_appearance_portrait', '/CharacterPortraitStudio.prefab'),
        ('round_scene', 'prefabs_ui_fight_gameroundscene', '/GameRoundScene.prefab'),
        ('round_character_scene', 'prefabs_ui_fight_gameroundcharacteritem_scene', '/GameRoundCharacterItem_Scene.prefab')):
        root = record(apk, apk.asset(bundle, suffix))
        nodes, components = [], []

        def walk(go, parent):
            record(apk, go.object_reader)
            transform = next(record(apk, c.component.deref()).read() for c in go.m_Component
                             if c.component.type.name in ('Transform', 'RectTransform'))
            index = len(nodes)
            node = dict(name=go.m_Name, parent=parent, active=bool(go.m_IsActive), layer=go.m_Layer,
                        position=[getattr(transform.m_LocalPosition, a) for a in 'xyz'],
                        rotation=[getattr(transform.m_LocalRotation, a) for a in 'xyzw'],
                        scale=[getattr(transform.m_LocalScale, a) for a in 'xyz'])
            nodes.append(node)
            for component in go.m_Component:
                obj = component.component.deref()
                if obj.type.name == 'Camera':
                    record(apk, obj)
                    camera = obj.read_typetree()
                    node['camera'] = {k: camera[k] for k in ('near clip plane', 'far clip plane', 'field of view',
                                                            'orthographic', 'orthographic size', 'm_ClearFlags', 'm_BackGroundColor')}
                if obj.type.name != 'MonoBehaviour':
                    continue
                basic = obj.read(check_read=False)
                name = basic.m_Script.read().m_ClassName
                if name not in ('GameRoundScene', 'CircleSpriteRenderer', 'HeadCharacterPortraitETC2Scene',
                                'GameRoundCharacterItemScene', 'GameRoundPlayerPortrait',
                                'GameRoundPortraitETC2SceneHandler', 'CharacterPortraitPhotographer'):
                    continue
                record(apk, obj)
                fields = schemas.read(obj)
                components.append(dict(node=index, type=name, fields=fields))
                if 'mRequestor' in fields:
                    path, _ = object_path(basic.m_GameObject.deref())
                    requestors.append(dict(prefab=key, path=path, animation=fields['mRequestor']['mAnimationName']))
            for child in transform.m_Children:
                walk(child.read().m_GameObject.read(), index)

        walk(root.read(), -1)
        prefabs.append(dict(key=key, nodes=nodes, components=components))
    if len(requestors) != 1 or requestors[0]['animation'] != 'home/idle_01':
        raise ValueError('Unexpected official scene portrait animation request')
    head = versions[0]['head']
    camera = next(n['camera'] for p in prefabs if p['key'] == 'studio' for n in p['nodes'] if 'camera' in n)
    data = dict(manifest=apk.version, etc2=versions[0]['etc2'], scene=versions[0]['scene'],
                width=head['TextureWidth'], height=head['TextureHeight'], size=head['Size'],
                offset=[head['mOffset'][a] for a in 'xyz'], depth=head['mDepthBuffer'],
                captureParticles=bool(head['mCaptureParticle']), excludes=head['mExcludeSkinTypes'],
                animation=requestors[0]['animation'], near=camera['near clip plane'], far=camera['far clip plane'],
                prefabs=prefabs)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes((json.dumps(data, ensure_ascii=False, separators=(',', ':')) + '\n').encode())
    OUTPUT.with_name('battle-portraits-source-manifest.json').write_bytes((json.dumps(dict(manifest=apk.version, output_sha256=sha(OUTPUT.read_bytes())))+'\n').encode())
    code = ['CharacterPortraitPhotographer', 'CharacterPortraitUtils', 'HeadCharacterPortraitBuilder',
            'HeadCharacterPortraitETC2Scene', 'CircleSpriteRenderer', 'GameRoundUI', 'GameRoundScene',
            'GameRoundPlayerPortrait', 'GameRoundPortraitETC2SceneHandler']
    RECEIPT.write_text(json.dumps(dict(manifest=apk.version, settings=versions, inputs=inputs,
        requestors=requestors, generator_sha256=sha(Path(__file__).read_bytes()),
        official_code={name:sha((ROOT/'.tools/source-packages/official-code/client-source'/f'{name}.cs').read_bytes()) for name in code},
        output=OUTPUT.relative_to(ROOT).as_posix(), output_sha256=sha(OUTPUT.read_bytes()),
        runtime_bindings={name:sha((ROOT/name).read_bytes()) for name in (
            'unity/Assets/Game/Runtime/Presentation/Character/CharacterNativePortraitCapture.cs',
            'unity/Assets/Game/Runtime/Presentation/Character/CharacterNativeAttachmentGeometry.cs')},
        operation='APK-first photography configuration and serialized studio/scene timeline hierarchy extraction. Current and historical head photography parameters are independently verified equal. Scene sprites, materials, controllers and particles require native binding before the scene timeline is complete.'), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Recovered portrait settings, studio and scene bindings:',len(inputs),'official objects')


if __name__ == '__main__':
    build()
