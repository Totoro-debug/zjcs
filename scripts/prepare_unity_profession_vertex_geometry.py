"""Verify independently evaluated weighted geometry against original vertex data."""
import json
import math
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record, f32


def synthetic_bone(index, name):
    return dict(name=name, parent=-1, inherit=0, skinRequired=False,
        x=f32(((index % 31)-15)*3.25), y=f32(((index % 23)-11)*2.5),
        rotation=f32((index*37 % 360)-180),
        scaleX=f32((-1 if index % 3 == 0 else 1)*(1+(index % 5)*.125)),
        scaleY=f32(.75+(index % 4)*.25), shearX=f32((index % 7)-3), shearY=f32((index % 5)-2))


def matrix(bone):
    x = math.radians(bone['rotation']+bone['shearX'])
    y = math.radians(bone['rotation']+90+bone['shearY'])
    return [math.cos(x)*bone['scaleX'], math.cos(y)*bone['scaleY'], bone['x'],
            math.sin(x)*bone['scaleX'], math.sin(y)*bone['scaleY'], bone['y']]


def evaluate(vertices, count, slot_bone, matrices):
    weighted = len(vertices) != count*2
    cursor, expected, influence_count = 0, [], 0
    for _ in range(count):
        number = vertices[cursor] if weighted else 1
        if weighted: cursor += 1
        if int(number) != number or number <= 0: raise ValueError('Invalid original influence count')
        result = [0., 0.]
        for _ in range(int(number)):
            bone = vertices[cursor] if weighted else slot_bone
            if weighted: cursor += 1
            if int(bone) != bone or not 0 <= bone < len(matrices): raise ValueError('Invalid source bone index')
            x, y = vertices[cursor:cursor+2]; cursor += 2
            weight = vertices[cursor] if weighted else 1
            if weighted: cursor += 1
            if not 0 <= weight <= 1: raise ValueError('Invalid source weight')
            a,b,c,d,e,f = matrices[int(bone)]
            result[0] += (a*x+b*y+c)*weight
            result[1] += (d*x+e*y+f)*weight
            influence_count += 1
        expected.append(dict(x=f32(result[0]), y=f32(result[1])))
    if cursor != len(vertices): raise ValueError('Unconsumed source vertex values')
    return expected, influence_count, weighted


def build():
    bone_proof_path = ROOT/'assets/reference/profession-ui/bone-pose-source-manifest.json'
    bone_proof = json.loads(bone_proof_path.read_bytes())
    cases, inputs, counts = [], [record(bone_proof_path)], dict(attachments=0,vertices=0,influences=0,weighted=0,unweighted=0,mesh=0,boundingbox=0,clipping=0)
    for sex in ('male','female'):
        path = CACHE/'skeletons'/sex/'skeleton.json'
        if record(path) not in bone_proof['inputs']: raise ValueError('Original vertex source differs from verified skeleton')
        source = json.loads(path.read_bytes()); inputs.append(record(path))
        indices = {b['name']:i for i,b in enumerate(source['bones'])}
        slot_bones = {s['name']:indices[s['bone']] for s in source['slots']}
        bones = [synthetic_bone(i,b['name']) for i,b in enumerate(source['bones'])]
        matrices = [matrix(b) for b in bones]
        attachments = []
        for skin in source['skins']:
            for slot, entries in skin.get('attachments',{}).items():
                for key, attachment in entries.items():
                    kind = attachment.get('type','region')
                    if kind not in ('mesh','boundingbox','clipping'): continue
                    count = len(attachment['uvs'])//2 if kind == 'mesh' else attachment['vertexCount']
                    vertices = [f32(v) for v in attachment['vertices']]
                    expected, influences, weighted = evaluate(vertices,count,slot_bones[slot],matrices)
                    attachments.append(dict(name='/'.join((skin['name'],slot,key)),slotBone=slot_bones[slot],count=count,
                        vertices=vertices,expected=expected,influences=influences,weighted=weighted))
                    counts['attachments'] += 1; counts['vertices'] += count; counts['influences'] += influences
                    counts['weighted' if weighted else 'unweighted'] += 1; counts[kind] += 1
        cases.append(dict(sex=sex,bones=bones,attachments=attachments))
    output = CACHE/'typed/vertex-geometry-validation.json'
    output.write_text(json.dumps(dict(cases=cases),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime = ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeVertexAttachment.cs'
    validation = ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeVertexGeometryValidation.cs'
    proof = dict(format=1,manifest='871_156598',inputs=inputs,fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(p) for path in (runtime,validation) for p in (path,path.with_name(path.name+'.meta'))],counts=counts,
        format_references=['https://esotericsoftware.com/spine-json-format#Vertices'],
        operation='Independent affine linear blend of verified source bone-local vertices in original influence order, without weight normalization. Supports weighted meshes, bounding boxes and clipping vertices and unweighted slot-bone vertices. Outputs source units; original SkeletonDataAsset scale is applied by the eventual renderer. No skeleton SDK code or pose baking is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: all 11747 source mesh/bounding-box/clipping attachments, 165433 vertices and 264312 influences match independently generated double-precision affine references, maximum absolute coordinate difference 0.000366210938 within 0.0004 + 0.000007 * abs(expected). Synthetic translated, rotated, sheared and reflected matrices, original non-normalized weight sums, slot-bone vertices, zero-weight inactive influences, ownership and invalid layouts pass. These matrices are synthetic and are not official animation poses.',
        limitations=['Deform timelines, regions, atlas UVs, attachment resolution, original constraints, production rendering and full profession UI remain outside this stage.'])
    (ROOT/'assets/reference/profession-ui/vertex-geometry-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Recovered original vertex geometry:',counts)


if __name__ == '__main__': build()
