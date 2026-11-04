"""Verify original weapon movement settings and prepare native regression inputs.

Consumes verified original prefab exports and recorded public-call observations.
Does not load or inspect an SDK implementation.
"""
import hashlib
import json
from pathlib import Path

from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    source_manifest = ROOT / 'assets/reference/battle-animation/weapon-prefabs-source-manifest.json'
    source = json.loads(source_manifest.read_bytes())
    assert source['manifest'] == '884_156692'
    verified = {row['path']: row['sha256'] for row in source['outputs']}
    rows, cases = [], []
    for entry in source['entries']:
        path = ROOT / ('unity/Assets/Official/Combat/WeaponPrefabs/Sources/' + entry['Skin'] + '.json')
        assert hashlib.sha256(path.read_bytes()).hexdigest() == verified[path.relative_to(ROOT).as_posix()]
        original = json.loads(path.read_bytes())
        components = [c for n in original['nodes'] for c in n['components']]
        skeleton, = [c for c in components if c['type'] == 'Spine.Unity.SkeletonAnimation']
        settings = {key: skeleton['fields'][key] for key in (
            'physicsPositionInheritanceFactor', 'physicsRotationInheritanceFactor', 'physicsMovementRelativeTo')}
        assert settings == dict(physicsPositionInheritanceFactor=dict(x=1., y=1.),
            physicsRotationInheritanceFactor=1., physicsMovementRelativeTo=dict(m_FileID=0, m_PathID=0))
        rows.append(dict(skin=entry['Skin'], source=identity(path), component=skeleton['source'], settings=settings))
        indices = {n['game_object']['source']['path_id']: i for i, n in enumerate(original['nodes'])}
        nodes = []
        for n in original['nodes']:
            transform = next(c['fields'] for c in n['components'] if c['type'] == 'Transform')
            nodes.append(dict(name=n['name'], parent=indices.get(n['parent'], -1), position=transform['m_LocalPosition'],
                rotation=transform['m_LocalRotation'], scale=transform['m_LocalScale'],
                active=bool(n['game_object']['fields']['m_IsActive']), layer=n['game_object']['fields']['m_Layer']))
        kinds = [c['type'] for c in components]
        fields = skeleton['fields']
        cases.append(dict(name=entry['Skin'], initial_skin=fields['initialSkinName'], initial_animation=fields['_animationName'],
            z_spacing=fields['zSpacing'], nodes=nodes, particles=kinds.count('ParticleSystem'),
            particle_renderers=kinds.count('ParticleSystemRenderer'), sprites=kinds.count('SpriteRenderer'),
            followers=kinds.count('SkeletonRendererFollower'), rotations=kinds.count('AppearanceSystem.SkeletonBoneRotOverride')))
    assert len(rows) == 390
    write_changed(work / 'weapon-native-prefab-cases.json', json.dumps(dict(cases=cases), ensure_ascii=False, separators=(',', ':')) + '\n')
    observations = ROOT / 'scripts/fixtures/battle_weapon_host_motion_reference.json'
    numerical = json.loads(observations.read_bytes())
    assert len(numerical['cases']) == 15 and sum(len(c['frames']) for c in numerical['cases']) == 480
    write_changed(work / 'weapon-host-motion-observations.json', observations.read_text(encoding='utf8'))
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    editor = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    paths = [runtime / (name + '.cs') for name in (
        'ProfessionNativeSpringConstraint', 'ProfessionNativePlayerPhysicsPose', 'ProfessionNativeScenePhysicsMovement',
        'ProfessionNativeSceneRendererSettings', 'ProfessionNativeSceneWeaponPlayback', 'ProfessionNativeSceneWeaponRenderer')]
    paths += [editor / (name + '.cs') for name in ('OfficialBattlePlayerModelClipValidation',
        'OfficialBattlePlayerModelClipValidation.NativeWeaponPrefabs', 'OfficialBattlePlayerModelClipValidation.WeaponHostMovement',
        'OfficialBattleWeaponHostMotionNumericalValidation', 'OfficialBattleWeaponHostMotionReferenceCapture')]
    for path in paths:
        if not Path(str(path) + '.meta').exists(): meta(path)
        raw = path.read_bytes()
        if b'\r\n' in raw: path.write_bytes(raw.replace(b'\r\n', b'\n'))
    proof = dict(manifest=source['manifest'], input=identity(source_manifest), original_settings=rows,
        public_documentation='https://esotericsoftware.com/spine-unity-main-components-4.2#Setting-Advanced-Parameters',
        numerical_observations=identity(observations),
        reference_binary=dict(path='unity/Library/ScriptAssemblies/spine-unity.dll', sha256=numerical['reference_binary_sha256']),
        reference_core_binary=dict(path='unity/Library/ScriptAssemblies/spine-csharp.dll', sha256=numerical['reference_core_binary_sha256']),
        generator=identity(Path(__file__)), outputs=[identity(p) for path in paths for p in (path, Path(str(path) + '.meta'))],
        binding='The independent weapon animation owner samples its actual Transform once in Update when its configured update mode permits. It converts world translation to original skeleton units, applies the original position factors, then the wrapped world Z rotation factor. Mesh/material redraw and same-skeleton replay retain the sample and physical clock/history; reinitialization binds a new sample.',
        limits=['All selected weapon prefabs use absolute world movement. Non-null relative-transform references are explicitly rejected.',
            'Reference capture is an optional editor-only public-call observation on an existing binary. Native playback and regression checks do not load that binary or use SDK source.',
            'Production player factory/battle integration and Windows Player validation remain separate.'])
    write_changed(ROOT / 'assets/reference/battle-animation/weapon-host-movement-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Original weapon host movement:', len(rows), 'verified prefabs; 15 recorded cases, 480 frames.')


if __name__ == '__main__': main()
