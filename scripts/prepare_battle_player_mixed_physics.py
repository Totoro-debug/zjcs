"""Check current original physics fields and derive independent MixNext expectations.

Only verified game payloads and public 4.2 behavior are consumed, never SDK code.
"""
import bisect
import json
from pathlib import Path

from prepare_battle_constraint_channel_fixtures import skeleton, interval_values
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed
from validation_workspace import validation_directory


def sample(keys, time):
    index = bisect.bisect_right([row.get('time', 0) for row in keys], time) - 1
    if index < 0: return None
    if index == len(keys) - 1: return keys[index].get('value', 0)
    return interval_values(keys[index], keys[index + 1], ('value',), (0,), time)[0]


def main():
    core = ROOT / 'assets/reference/battle-animation/scene-core-source-manifest.json'
    proof = json.loads(core.read_bytes())
    assert proof['manifest'] == '884_156692'
    checked = {row['path']: row['sha256'] for row in proof['outputs']}
    cases, inputs, statistics = [], [], []
    for sex in ('male', 'female'):
        path = ROOT / ('unity/Assets/Official/Combat/SceneCharacterSources/' + sex + '.bytes')
        assert identity(path)['sha256'] == checked[path.relative_to(ROOT).as_posix()]
        data = skeleton(path.read_bytes()); inputs.append(identity(path))
        definitions = {row['name']: row for row in data['physics']}
        animations = data['animations']
        tracks = []
        for animation, fields in animations.items():
            for constraint, channels in fields.get('physics', {}).items():
                assert constraint in definitions and set(channels) == {'mix'}
                assert channels['mix']
                tracks.append((animation, constraint, channels['mix']))
        outgoing, _, _ = tracks[0]
        absent = next(name for name, row in animations.items() if not row.get('physics'))
        for incoming, constraint, keys in tracks:
            first = animations[outgoing].get('physics', {}).get(constraint, {}).get('mix')
            before = sample(first, .37) if first else None
            if before is None: before = definitions[constraint].get('mix', 1)
            times = {row.get('time', 0) for row in keys} | {keys[-1].get('time', 0) + .1}
            if keys[0].get('time', 0) > 0: times.add(keys[0]['time'] / 2)
            for a, b in zip(keys, keys[1:]):
                for fraction in (.25, .5, .75):
                    times.add(a.get('time', 0) + (b.get('time', 0) - a.get('time', 0)) * fraction)
            for time in sorted(times):
                value = sample(keys, time)
                for weight in (0, .25, .75, 1):
                    cases.append(dict(sex=sex, outgoing=outgoing, incoming=incoming, constraint=constraint,
                        **{'from':.37, 'to':time}, weight=weight,
                        expected=before if value is None else before + (value - before) * weight))
            cases.append(dict(sex=sex, outgoing=outgoing, incoming=absent, constraint=constraint,
                **{'from':.37, 'to':.37}, weight=.75, expected=before))
        statistics.append(dict(sex=sex, animations=len(animations), physics_tracks=len(tracks), constraints=len(definitions)))
    fixture = json.dumps(dict(cases=cases), ensure_ascii=False, separators=(',', ':')) + '\n'
    write_changed(validation_directory() / 'mixed-physics-cases.json', fixture)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    validation = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    extra = validation / 'OfficialBattlePlayerModelClipValidation.Physics.cs'; meta(extra)
    paths = [runtime / (name + '.cs') for name in ('ProfessionNativePhysicsAnimation', 'ProfessionNativePlayerPhysicsPose', 'ProfessionNativeAnimatorPose', 'ProfessionNativeSpringConstraint')]
    paths += [validation / 'OfficialBattlePlayerModelClipValidation.cs', extra, extra.with_name(extra.name + '.meta')]
    for path in paths:
        canonical = ('\n'.join(row.rstrip() for row in path.read_text(encoding='utf-8').splitlines()) + '\n').encode('utf-8')
        if path.read_bytes() != canonical: path.write_bytes(canonical)
    receipt = dict(manifest=proof['manifest'], inputs=inputs, core=identity(core), statistics=statistics,
        translator=identity(ROOT / 'assets/reference/battle-animation/native-mix-next-source-manifest.json'),
        dependencies=[identity(ROOT / ('unity/Assets/Game/Runtime/Offline/ProfessionUI/' + name + '.cs')) for name in
            ('ProfessionNativeAnimatorPose', 'ProfessionNativeConstraintPose', 'ProfessionNativeSpringConstraint',
             'ProfessionNativeAnimationChannels', 'ProfessionNativeCurve')],
        helper=identity(ROOT / 'scripts/prepare_battle_constraint_channel_fixtures.py'),
        numerical_reference=[identity(ROOT / path) for path in (
            'scripts/test_partner_player_physics.py', 'scripts/fixtures/partner_player_physics_validation.cs',
            'scripts/fixtures/partner_player_physics_reference.txt')],
        scalar_cases=len(cases), fixture_sha256=__import__('hashlib').sha256(fixture.encode()).hexdigest(),
        outputs=[identity(path) for path in paths], generator=identity(Path(__file__).resolve()),
        documentation=['https://esotericsoftware.com/spine-api-reference-4.2#MixBlend',
            'https://esotericsoftware.com/spine-api-reference-4.2#Physics',
            'https://esotericsoftware.com/spine-api-reference-4.2#PhysicsConstraint',
            'https://en.esotericsoftware.com/spine-unity-main-components-4.2#Animation-Blending-Control'],
        binding='Outgoing original physics parameters followed by reached incoming Replace mix keys at the actual clip weight; solve mixed local/geometric pose once, advance the same physical clock/history once. Replay does not reset physical time. Explicit updatePhysics=false selects public Physics.Pose behavior and requires zero delta, reapplying stored displacements without changing history. Existing unmixed/setup entry uses the same integration stage.',
        limits=['This extends the existing independent player solver domain (physics after IK/transform, translation/rotation). Additional parameter kinds and interleaved constraints remain explicitly rejected.',
            'The fixture exercises current four player controllers and original complete Game prefab. Independent weapon animation clocks, production player factory/battle lifecycle and Windows Player remain separate. No SDK implementation is read or copied.'])
    write_changed(ROOT / 'assets/reference/battle-animation/player-mixed-physics-source-manifest.json', json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
    print('Current original mixed physics:', len(cases), 'scalar expectations;', statistics)


if __name__ == '__main__': main()
