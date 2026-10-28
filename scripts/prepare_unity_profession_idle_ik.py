"""Bind original idle IK order to independently evaluated bone poses."""
import json
import math
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record


def build():
    pose_path = ROOT / 'assets/reference/profession-ui/bone-pose-source-manifest.json'
    ik_path = ROOT / 'assets/reference/profession-ui/native-ik-source-manifest.json'
    pose_proof, ik_proof = [json.loads(p.read_bytes()) for p in (pose_path, ik_path)]
    pose_fixture = CACHE / 'typed/bone-pose-validation.json'
    ik_fixture = CACHE / 'typed/native-ik-validation.json'
    if pose_proof['fixture'] != record(pose_fixture) or ik_proof['fixture'] != record(ik_fixture):
        raise ValueError('Verified pose or IK fixture changed')
    rows = {r['label']: r for r in json.loads(ik_fixture.read_bytes())['cases']}
    cases, inputs = [], [record(p) for p in (pose_path, ik_path, pose_fixture, ik_fixture)]
    for body in json.loads(pose_fixture.read_bytes())['cases']:
        path = CACHE / 'skeletons' / body['sex'] / 'skeleton.json'
        if record(path) not in pose_proof['inputs']: raise ValueError('Original skeleton changed')
        original = json.loads(path.read_bytes())
        inputs.append(record(path))
        bones = body['bones']
        names = {b['name']: i for i, b in enumerate(bones)}
        ordered = sorted(original['ik'], key=lambda c: c['order'])
        for clip in body['clips']:
            for sample in clip['samples']:
                local = [dict(v) for v in sample['local']]
                constraints, changed, observed = [], set(), set()
                label = body['sex'] + '/' + clip['name']
                for constraint in ordered:
                    row = rows[label + '/' + constraint['name'] + '/' + str(sample['time'])]
                    chain = [names[n] for n in constraint['bones']]
                    constraints.append(dict(name=constraint['name'], order=constraint['order'],
                        target=names[constraint['target']], bones=chain, childLength=row['childLength'],
                        mix=row['mix'], bendPositive=row['positive']))
                    local[chain[0]]['rotation'] = row['expectedParent']
                    changed.add(chain[0])
                    if len(chain) == 2:
                        local[chain[1]]['rotation'] = row['expectedChild']
                        changed.add(chain[1])
                    observed.update(chain)
                    observed.add(names[constraint['target']])

                def world(i):
                    b, v = bones[i], local[i]
                    rx, ry = math.radians(v['rotation']+b['shearX']), math.radians(v['rotation']+90+b['shearY'])
                    a,c = math.cos(rx)*v['scaleX'], math.sin(rx)*v['scaleX']
                    bb,d = math.cos(ry)*v['scaleY'], math.sin(ry)*v['scaleY']
                    x,y = v['x'],v['y']
                    if b['parent'] < 0: return [a,bb,c,d,x,y]
                    pa,pb,pc,pd,px,py = world(b['parent'])
                    if b['inherit'] == 1: return [a,bb,c,d,pa*x+pb*y+px,pc*x+pd*y+py]
                    if b['inherit'] != 0: raise ValueError('Unverified selective inheritance in observed chain')
                    return [pa*a+pb*c,pa*bb+pb*d,pc*a+pd*c,pc*bb+pd*d,pa*x+pb*y+px,pc*x+pd*y+py]

                cases.append(dict(sex=body['sex'], animation=clip['name'], time=sample['time'], constraints=constraints,
                    rotations=[dict(bone=i,value=local[i]['rotation']) for i in sorted(changed)],
                    world=[dict(bone=i,values=world(i)) for i in sorted(observed)]))
    output = CACHE / 'typed/idle-ik-validation.json'
    output.write_text(json.dumps(dict(cases=cases),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    paths = ['unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIdleIk.cs',
             'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs',
             'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeIdleIkValidation.cs']
    proof = dict(format=1,manifest='871_156598',inputs=inputs,fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(ROOT/p) for p in paths for p in (p,p+'.meta')],cases=len(cases),
        operation='Bind source order and constant idle mix/bend states to the independent local pose and IK geometry. Lazily evaluate only requested world ancestors, invalidate cached transforms after each rotation. Circle-intersection references come from the independently generated geometry fixture; no skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: 48 source poses, 576 final angles and 960 world matrices; maximum circular angle difference 0.00020724535 degrees within 0.0004 degrees and maximum absolute world difference 0.00008010864 within 0.001 + 0.000007 * abs(expected). Source order, zero mix, parent/descendant cache invalidation, independent geometry references and explicit unsupported chain rejection pass. No original SDK full pose, character rendering or profession UI is validated.',
        limitations=['Only the six verified idle IK branches are bound. General IK scale/shear/softness, selective inheritance, transform/physics constraints, skins/mesh/materials, original appearance components and complete profession UI remain required. These generated references are independent geometry, not original SDK poses or visual references.'])
    (ROOT/'assets/reference/profession-ui/idle-ik-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Bound original idle IK states:',len(cases),'poses.')


if __name__ == '__main__': build()
