"""Record independently implemented weapon clocks from original application data.

Only application source, serialized assets and public enum constants are used;
no animation SDK implementation/source or DLL methods are inspected.
"""
import gzip
import hashlib
import json
import struct
from pathlib import Path

from official_metadata import Metadata
from prepare_battle_scene_appearance_runtime import ROOT, identity, write_changed


def main():
    original = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    metadata = ROOT / '.tools/source-packages/current/884_156692/official-code/global-metadata.dat'
    parsed = Metadata(metadata.read_bytes())
    timing = next(row for row in parsed.types if row['enum'] and row['name'] == 'UpdateTiming')
    constants = {field['name']: parsed.default_int(field['index']) for field in timing['fields'] if field['name'] != 'value__'}
    assert constants['InUpdate'] == 1
    prefabs = []
    for group in ('100', '200'):
        path = ROOT / f'unity/Assets/Official/ProfessionUI/SceneWeaponSources/pWeapon_{group}_G_007_Integrated-source.json'
        source = json.loads(path.read_bytes())
        fields = next(component['fields'] for node in source['nodes'] for component in node['components']
                      if component['type'] == 'Spine.Unity.SkeletonAnimation')
        clock = {key: fields[key] for key in ('_animationName', 'loop', 'timeScale', 'updateTiming', 'unscaledTime', 'updateWhenInvisible', 'clearStateOnDisable')}
        assert clock == dict(_animationName=f'{group}/{group}_loop', loop=True, timeScale=1.0,
                             updateTiming=1, unscaledTime=0, updateWhenInvisible=0, clearStateOnDisable=0)
        prefabs.append(dict(source=identity(path), clock=clock))
    payload = ROOT / 'unity/Assets/Official/ProfessionUI/WeaponResources/primary-weapon.bytes'
    raw = payload.read_bytes()
    assert raw[:8] == b'ZJCSPROF' and struct.unpack_from('<I', raw, 8)[0] == 1
    cursor = 12
    strings = []
    for _ in range(2):
        length = struct.unpack_from('<I', raw, cursor)[0]
        cursor += 4
        strings.append(raw[cursor:cursor + length].decode())
        cursor += length
    assert strings == ['871_156598', 'primary-weapon']
    cursor += 4  # Original data scale.
    length = struct.unpack_from('<I', raw, cursor)[0]
    cursor += 36  # Original decoded length and SHA-256.
    compressed = struct.unpack_from('<I', raw, cursor)[0]
    cursor += 4
    decoded = gzip.decompress(raw[cursor:cursor + compressed])
    assert len(decoded) == length and hashlib.sha256(decoded).digest() == raw[cursor - 36:cursor - 4]
    animations = json.loads(decoded)['animations']

    def last_time(value):
        if isinstance(value, dict):
            return max((float(child) if key == 'time' else last_time(child) for key, child in value.items()), default=0)
        if isinstance(value, list):
            return max(map(last_time, value), default=0)
        return 0

    assert len(animations) == 6 and all(set(value) == {'bones'} for value in animations.values())
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    editor = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    files = [runtime / (name + '.cs') for name in (
        'ProfessionNativeSceneWeaponPlayback', 'ProfessionNativeSceneWeaponRenderer',
        'ProfessionNativeSceneRendererSettings', 'ProfessionNativeSceneWeapon', 'OfflineProfessionScenePartPoolBindings')]
    files += [editor / (name + '.cs') for name in (
        'OfficialBattleAnimatorPlaybackAcceptance', 'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance',
        'OfficialBattleAnimatorPlaybackAcceptance.SceneWeaponPlayback')]
    outputs = [identity(path) for source in files for path in (source, source.with_name(source.name + '.meta'))]
    proof = dict(manifest='884_156692', application=[identity(original / path) for path in (
        'AppearanceSystem.Runtime/AppearanceAnimUtils.cs', 'AppearanceSkeletonPart.cs')],
        metadata=identity(metadata), public_update_timing=constants, prefabs=prefabs,
        reused_payload=identity(payload), original_payload_manifest=strings[0],
        motions={name: dict(duration=last_time(value), sections=list(value)) for name, value in animations.items()},
        dependencies=[identity(ROOT / ('assets/reference/battle-animation/' + name + '-source-manifest.json'))
                      for name in ('scene-weapon', 'scene-native-renderer', 'scene-mecanim-parts', 'scene-skill-motions')],
        outputs=outputs, generator=identity(Path(__file__).resolve()),
        documentation=['https://esotericsoftware.com/spine-api-reference-4.2#AnimationState-setAnimation',
                       'https://esotericsoftware.com/spine-api-reference-4.2#TrackEntry-mixDuration'],
        binding='Original application requests restart track zero with explicit zero mix and immediately apply/render. Each assembled native weapon now owns an independent clock, advanced only by scaled Unity Update while visible according to its exact prefab settings. Apply/redraw after skin resets does not advance time. Explicit diagnostic producers remain available; the default pool/assembly entry uses live playback.',
        limits=['Only original bone-only weapon motions with no active constraints and the two verified G_007 prefabs are supported by this entry. Other update settings are rejected explicitly.',
                'The reused historical weapon payload remains labeled 871_156598; current exact-byte reuse evidence is preserved in the scene-weapon receipt. No replacement or relabeling of animation data.',
                'Current Game body motions have no original part-animation mapping. The live test explicitly requests existing loop_L/loop_R weapon motions and does not invent a Game mapping.',
                'Body poses remain before physics. Production battle host wiring, complete physics/mixing/FX/audio and Windows Player integration are still incomplete.',
                'The portrait fixture fits a camera around original geometry; it does not establish official host scale, HUD alignment, UI input or save restoration.'])
    write_changed(ROOT / 'assets/reference/battle-animation/scene-weapon-playback-source-manifest.json',
                  json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded original application zero-mix requests, exact prefab clocks, six native motions and independent playback bindings.')


if __name__ == '__main__':
    main()
