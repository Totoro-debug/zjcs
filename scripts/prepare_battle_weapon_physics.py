"""Record original primary-weapon constraint inputs for independent playback.

Consumes serialized game data and application code identities, never SDK code.
"""
import json
from pathlib import Path

from prepare_battle_constraint_channel_fixtures import skeleton
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed
from validation_workspace import validation_directory


def main():
    high_receipt = ROOT / 'assets/reference/battle-animation/weapon-high-textures-source-manifest.json'
    high = json.loads(high_receipt.read_bytes())
    assert high['manifest'] == '884_156692'
    payload = ROOT / high['model']['native']['path']
    assert identity(payload) == high['model']['native']
    data = skeleton(payload.read_bytes())
    assert len(data['bones']) == 364 and len(data['skins']) == 399
    assert len(data['ik']) == len(data['transform']) == 1 and len(data['physics']) == 222
    assert data['ik'][0]['order'] == 0 and data['transform'][0]['order'] == 1
    assert data['transform'][0]['mixRotate'] == -1
    assert all(row['order'] >= 2 and row.get('scaleX', 0) == row.get('shearX', 0) == 0 for row in data['physics'])
    assert len(data['animations']) == 6 and all(set(value) == {'bones'} for value in data['animations'].values())
    indices = {row['name']: index for index, row in enumerate(data['bones'])}
    physical = {row['name']: row for row in data['physics']}
    scene_skins = json.loads((validation_directory() / 'weapon-high-texture-cases.json').read_bytes())['skins']
    rows = []
    for skin in data['skins']:
        group = '200' if skin['name'].startswith('pWeapon_200') else '100'
        rows.append(dict(name=skin['name'], scene=skin['name'] in scene_skins,
            constraints=skin.get('ik', []) + skin.get('transform', []) + skin.get('physics', []),
            physics_bones=[indices[physical[name]['bone']] for name in skin.get('physics', [])],
            motions=[name for name in data['animations'] if name.startswith(group + '/')]))
    constrained = [row for row in rows if row['constraints']]
    assert len(constrained) == 56 and all(len(row['motions']) == 3 for row in rows)
    fixture = json.dumps(dict(skins=rows), ensure_ascii=False, separators=(',', ':')) + '\n'
    write_changed(validation_directory() / 'weapon-physics-cases.json', fixture)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    editor = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    probe = editor / 'OfficialBattlePlayerModelClipValidation.WeaponPhysics.cs'
    meta(probe)
    files = [runtime / (name + '.cs') for name in (
        'ProfessionNativeSceneWeaponPlayback', 'ProfessionNativeConstraintPose', 'ProfessionNativeSceneWeaponRenderer')]
    files += [editor / 'OfficialBattlePlayerModelClipValidation.cs', probe, Path(str(probe) + '.meta')]
    for path in files:
        raw = path.read_bytes()
        canonical = raw.replace(b'\r\n', b'\n')
        if raw != canonical: path.write_bytes(canonical)
    app = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    proof = dict(manifest=high['manifest'], native_payload=identity(payload), current_source_reuse=identity(high_receipt),
        original_payload_manifest='871_156598',
        application=[identity(app / name) for name in ('AppearanceSystem.Runtime/AppearanceAnimUtils.cs', 'AppearanceSkeletonPart.cs')],
        constraints={kind: data[kind] for kind in ('ik', 'transform', 'physics')},
        skins=len(rows), constrained_skins=len(constrained), scene_constrained=sum(row['scene'] for row in constrained),
        original_motions=list(data['animations']), fixture_sha256=__import__('hashlib').sha256(fixture.encode()).hexdigest(),
        outputs=[identity(path) for path in files],
        dependencies=[identity(runtime / (name + '.cs')) for name in (
            'ProfessionNativePlayerPhysicsPose', 'ProfessionNativeSpringConstraint', 'ProfessionNativePhysicsAnimation',
            'ProfessionNativeConstraintAnimation', 'ProfessionNativeAnimationChannels', 'ProfessionNativeIdleTransform')],
        numerical_reference=[identity(ROOT / name) for name in (
            'scripts/test_partner_player_physics.py', 'scripts/fixtures/partner_player_physics_reference.txt')],
        helper=identity(ROOT / 'scripts/prepare_battle_constraint_channel_fixtures.py'), generator=identity(Path(__file__)),
        binding='Each original weapon skeleton owns its physical clock and histories. Each Advance completes the original motion and ordered IK/transform/physics once. Apply and same-skeleton track-zero replay use the retained Physics.Pose history at zero delta. A new skeleton starts a new history. Original negative transform mix and concave clipping are retained.',
        limits=['Original full prefabs, their particles/sprites/bone override callbacks, production player factory and battle/Windows Player integration remain separate.',
            'Probe requests original motions explicitly for internal model skins; it does not invent additional prefab animation mappings.',
            'This uses the existing independent geometric and spring solvers; no SDK implementation/source or DLL methods are read or copied.'])
    write_changed(ROOT / 'assets/reference/battle-animation/weapon-physics-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Original primary-weapon physics:', len(rows), 'skins,', len(constrained), 'constrained,', proof['scene_constrained'], 'scene skins; six original motions.')


if __name__ == '__main__':
    main()
