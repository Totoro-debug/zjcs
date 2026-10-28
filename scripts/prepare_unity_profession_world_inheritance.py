"""Independent complex-plane references for the supported source world transforms."""
import cmath
import json
import math
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_photographer import record


def world(bones, local, active):
    result = []
    for i, (bone, pose) in enumerate(zip(bones, local)):
        if i not in active:
            result.append((0j, 0j, 0j))
            continue
        u, v, origin = result[bone['parent']] if bone['parent'] >= 0 else (1 + 0j, 1j, 0j)
        position = origin + u * pose['x'] + v * pose['y']
        mode, angle = bone['inherit'], math.radians(pose['rotation'])
        sx, sy = pose['scaleX'], pose['scaleY']
        shx, shy = math.radians(bone['shearX']), math.radians(bone['shearY'])
        x, y = cmath.rect(sx, angle + shx), cmath.rect(sy, angle + math.pi / 2 + shy)
        if bone['parent'] < 0 or mode == 1:
            pass
        elif mode == 0:
            x, y = u * x.real + v * x.imag, u * y.real + v * y.imag
        elif mode == 2:
            size = max(abs(u)**2, abs(v)**2)
            if abs(abs(u)**2 - abs(v)**2) > 2e-5 * size or abs((u.conjugate() * v).real) > 2e-5 * size:
                raise ValueError('Source no-rotation parent is nonuniform: ' + bone['name'])
            x, y = x * abs(u), y * abs(u)
        elif mode == 3:
            direction = u * math.cos(angle) + v * math.sin(angle)
            if abs(direction) <= 1e-7:
                x, y = 0j, 0j
            else:
                reflection = -1 if (u.conjugate() * v).imag < 0 else 1
                x = cmath.rect(sx, cmath.phase(direction) + reflection * shx)
                y = cmath.rect(sy, cmath.phase(direction) + reflection * (math.pi / 2 + shy))
        else:
            raise ValueError('Source inheritance remains unsupported: ' + bone['name'])
        result.append((x, y, position))
    return [dict(bone=i, a=u.real, b=v.real, c=u.imag, d=v.imag, x=p.real, y=p.imag)
        for i, (u, v, p) in enumerate(result) if i in active]


def build():
    proof_path = ROOT / 'assets/reference/profession-ui/bone-pose-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    fixture_path = CACHE / 'typed/bone-pose-validation.json'
    if proof['fixture'] != record(fixture_path):
        raise ValueError('Verified original local pose fixture changed')
    original = json.loads(fixture_path.read_bytes())
    cases, inputs = [], []
    for body in original['cases']:
        sex, bones = body['sex'], body['bones']
        path = CACHE / 'skeletons' / sex / 'skeleton.json'
        if record(path) not in proof['inputs']:
            raise ValueError('Verified original source skeleton changed')
        source = json.loads(path.read_bytes())
        indices = {b['name']: i for i, b in enumerate(bones)}
        skins = {s['name']: s for s in source['skins']}
        gender = 'M' if sex == 'male' else 'F'
        hair = 'hair_G_01' if sex == 'male' else 'hair_F_01'
        for selected in (['default', 'body_100_' + gender + '_01', hair],
                         ['default', 'body_200_' + gender + '_01', hair], ['default', 'map_01']):
            active = {i for i, bone in enumerate(bones) if not bone['skinRequired']}
            for name in selected:
                for name in skins[name].get('bones', []):
                    i = indices[name]
                    while i >= 0:
                        active.add(i); i = bones[i]['parent']
            frames = []
            for clip in body['clips']:
                for sample in clip['samples']:
                    frames.append(dict(animation=clip['name'], time=sample['time'],
                        expected=world(bones, sample['local'], active)))
            cases.append(dict(sex=sex, skins=selected, frames=frames))
        inputs.append(record(path))
    synthetic = []
    for mode in (2, 3):
        parents = [(2, 2, 0), (-2, 2, 0), (-2, -2, 0), (0, 0, 0)]
        if mode == 3:
            parents.extend([(2, .75, 0), (-2, .75, 17)])
        for parent_x, parent_y, shear in parents:
            for rotation in (-93, 0, 27, 185):
                bones = [dict(name='parent', parent=-1, inherit=0, skinRequired=False, x=7, y=-11,
                    rotation=37, scaleX=parent_x, scaleY=parent_y, shearX=shear, shearY=0),
                    dict(name='child', parent=0, inherit=mode, skinRequired=False, x=3, y=9,
                    rotation=rotation, scaleX=-.75, scaleY=1.25, shearX=13, shearY=-7)]
                synthetic.append(dict(bones=bones, expected=world(bones, bones, {0, 1})))
    target = CACHE / 'typed/world-inheritance-validation.json'
    target.write_text(json.dumps(dict(cases=cases, synthetic=synthetic), ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8', newline='\n')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs'
    reader = runtime.with_name('ProfessionNativeSkeletonData.cs')
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionWorldInheritanceValidation.cs'
    result = dict(format=1, manifest='871_156598', inputs=inputs, baseline_pose_adapter=record(proof_path), original_local_fixture=record(fixture_path),
        fixture=record(target), runtime=record(runtime), reader=record(reader), validation=record(validation),
        validation_meta=record(validation.with_name(validation.name + '.meta')), generator=record(Path(__file__)),
        independent_reuse=record(ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13SkeletonView.cs'),
        references=['https://esotericsoftware.com/spine-bones#Transform-inheritance'],
        scope='Native world hierarchy before constraints: normal, translation-only, no-scale retaining parent direction/reflection, '
            'and no-rotation/reflection for conformal (equal-length orthogonal) parent axes. Bind actual packaged idle bone timelines '
            'through the existing independent Unity curves. Nonuniform/sheared no-rotation parents and mode 4 remain explicit gaps.',
        reference_method='Independent double-precision complex-plane transforms; source local poses from the verified independent '
            'Bezier inversion fixture. No SDK pose, runtime implementation or baked animation is used.',
        counts=dict(source_cases=len(cases), frames=sum(len(c['frames']) for c in cases), synthetic=len(synthetic)),
        limitations=['Transform/physics constraints, mixed animation and full character rendering are not evaluated here.',
            'Source world references are pre-constraint mathematical references, not original game or SDK frame captures.'],
        validated_scope='Pending Unity compilation and actual Play Mode inheritance/timeline validation.')
    (ROOT / 'assets/reference/profession-ui/world-inheritance-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Prepared independent world inheritance:', result['counts'])
    return result


if __name__ == '__main__':
    build()
