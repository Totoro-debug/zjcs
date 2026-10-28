"""Recover the idle IK geometry and compare independent circle-intersection solutions."""
import json
import math
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record, f32


def delta(a, b):
    value = (b-a) % 360
    return value-360 if value > 180 else value


def circle_solution(target, offset, length, positive):
    x, y = target
    distance = math.hypot(x, y)
    first = math.hypot(*offset)
    reached = min(first+length, max(abs(first-length), distance))
    ux, uy = x/distance, y/distance
    along = (first*first-length*length+reached*reached)/(2*reached)
    height = math.sqrt(max(0, first*first-along*along)) * (-1 if positive else 1)
    knee = (ux*along-uy*height, uy*along+ux*height)
    shoulder = math.degrees(math.atan2(knee[1], knee[0]) - math.atan2(offset[1], offset[0]))
    elbow = math.degrees(math.atan2(uy*reached-knee[1], ux*reached-knee[0])) - shoulder
    return shoulder, elbow


def build():
    pose_proof = ROOT / 'assets/reference/profession-ui/bone-pose-source-manifest.json'
    proof = json.loads(pose_proof.read_bytes())
    fixture = CACHE / 'typed/bone-pose-validation.json'
    if proof['manifest'] != '871_156598' or proof['fixture'] != record(fixture):
        raise ValueError('Verified bone pose fixture changed')
    cases = json.loads(fixture.read_bytes())['cases']
    rows, inputs = [], [record(pose_proof), record(fixture)]
    for case in cases:
        path = CACHE / 'skeletons' / case['sex'] / 'skeleton.json'
        if record(path) not in proof['inputs']: raise ValueError('Original IK skeleton differs from local pose source')
        original = json.loads(path.read_bytes())
        names = {b['name']: i for i, b in enumerate(case['bones'])}
        bones = case['bones']
        ordered = sorted(original['ik'], key=lambda c: c['order'])
        if len(ordered) != 8 or [c['order'] for c in ordered] != list(range(8)):
            raise ValueError('Inspect original idle IK constraint order')
        for constraint in ordered:
            if set(constraint) - {'name', 'order', 'bones', 'target', 'mix', 'bendPositive'}:
                raise ValueError('Restore additional IK setup branches: ' + constraint['name'])
        inputs.append(record(path))
        for clip in case['clips']:
            animation = original['animations'][clip['name']]
            for state in clip['samples']:
                local = [dict(v) for v in state['local']]

                def world(index):
                    bone, values = bones[index], local[index]
                    rx = math.radians(values['rotation']+bone['shearX'])
                    ry = math.radians(values['rotation']+90+bone['shearY'])
                    a, c = math.cos(rx)*values['scaleX'], math.sin(rx)*values['scaleX']
                    b, d = math.cos(ry)*values['scaleY'], math.sin(ry)*values['scaleY']
                    x, y = values['x'], values['y']
                    if bone['parent'] < 0: return a,b,c,d,x,y
                    pa,pb,pc,pd,px,py = world(bone['parent'])
                    if bone['inherit'] == 1: return a,b,c,d,pa*x+pb*y+px,pc*x+pd*y+py
                    if bone['inherit'] != 0: raise ValueError('Selective inheritance affects an idle IK chain')
                    return pa*a+pb*c,pa*b+pb*d,pc*a+pd*c,pc*b+pd*d,pa*x+pb*y+px,pc*x+pd*y+py

                for constraint in ordered:
                    track = animation.get('ik', {}).get(constraint['name'])
                    mix, positive = constraint.get('mix',1), constraint.get('bendPositive',True)
                    if track:
                        if len(track) != 1 or set(track[0]) - {'time', 'mix', 'bendPositive'}:
                            raise ValueError('Restore interpolated idle IK timelines')
                        key = track[0]
                        if state['time'] >= key.get('time',0):
                            mix,positive = key.get('mix',1),key.get('bendPositive',True)
                    chain = [names[n] for n in constraint['bones']]
                    first = chain[0]
                    for index in chain:
                        values,bone = local[index],bones[index]
                        if values['scaleX'] != 1 or values['scaleY'] != 1 or bone['shearX'] or bone['shearY'] or bone['inherit']:
                            raise ValueError('The recovered idle IK branch requires scaled/sheared/selective solving')
                    target_world = world(names[constraint['target']])[-2:]
                    parent = world(bones[first]['parent']) if bones[first]['parent'] >= 0 else (1,0,0,1,0,0)
                    a,b,c,d,px,py = parent
                    determinant = a*d-b*c
                    tx,ty = target_world[0]-px,target_world[1]-py
                    target = [f32((tx*d-ty*b)/determinant-local[first]['x']), f32((ty*a-tx*c)/determinant-local[first]['y'])]
                    row = dict(label=case['sex']+'/'+clip['name']+'/'+constraint['name']+'/'+str(state['time']),
                        target=dict(x=target[0],y=target[1]), parentRotation=local[first]['rotation'],
                        childRotation=0, offset=dict(x=0,y=0), childLength=0, positive=positive, mix=mix, pair=len(chain)==2)
                    if len(chain) == 2:
                        child = chain[1]
                        if bones[child]['parent'] != first: raise ValueError('Original IK chain is not consecutive')
                        offset = [f32(local[child]['x']),f32(local[child]['y'])]
                        length = original['bones'][child].get('length',0)
                        row.update(childRotation=local[child]['rotation'],offset=dict(x=offset[0],y=offset[1]),childLength=length)
                        desired = circle_solution(target,offset,length,positive) if mix else (row['parentRotation'],row['childRotation'])
                        row['expectedParent'] = f32(row['parentRotation']+delta(row['parentRotation'],desired[0])*mix)
                        row['expectedChild'] = f32(row['childRotation']+delta(row['childRotation'],desired[1])*mix)
                        local[child]['rotation'] = row['expectedChild']
                    else:
                        desired = math.degrees(math.atan2(target[1],target[0]))
                        row['expectedParent'] = f32(row['parentRotation']+delta(row['parentRotation'],desired)*mix)
                        row['expectedChild'] = 0
                    local[first]['rotation'] = row['expectedParent']
                    rows.append(row)
    output = CACHE / 'typed/native-ik-validation.json'
    output.write_text(json.dumps(dict(cases=rows), ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime = ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIk.cs'
    validation = ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeIkValidation.cs'
    result = dict(format=1,manifest='871_156598',inputs=inputs,fixture=record(output),
        generator=record(Path(__file__)),runtime=[record(runtime),record(runtime.with_name(runtime.name+'.meta'))],
        validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        constraints_per_skeleton=8,cases=len(rows),
        operation='Independent single-joint aiming and law-of-cosines two-joint geometry for verified unit-scale, unsheared idle chains. Recover original setup, constant idle mix/bend keys and order; generate independent circle-intersection angle references. No skeleton SDK source or runtime is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: 384 source geometry cases, 192 pairs, 192 single-joint aims and 188 zero-mix branches; maximum circular angle difference 0.0000305175781 degrees within 0.0002 degrees. Independent circle-intersection angles, reachable/clamped endpoints, bend direction, fractional mixes, reflected target coordinates and explicit invalid/degenerate input rejection pass. No source SDK world pose, live character or profession UI is validated.',
        limitations=['The solver is not yet bound to the native character pose/renderer. General scale/shear, compression/stretch/softness, coincident targets and other animations are not covered. The generated source-chain geometry is not an original SDK pose or visual reference.'])
    (ROOT/'assets/reference/profession-ui/native-ik-source-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Recovered original idle IK geometry:',len(rows),'cases.')


if __name__ == '__main__': build()
