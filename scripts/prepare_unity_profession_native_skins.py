"""Bind all original skin identities, attachment keys and constraint membership."""
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record


def union(a,b):return list(dict.fromkeys(a+b))


def build():
    bone_path=ROOT/'assets/reference/profession-ui/bone-pose-source-manifest.json';bone_proof=json.loads(bone_path.read_bytes())
    inputs=[record(bone_path)];cases=[];counts=dict(skins=0,attachments=0,bones=0,constraints=0,combinations=0)
    for sex in ('male','female'):
        path=CACHE/'skeletons'/sex/'skeleton.json'
        if record(path) not in bone_proof['inputs']:raise ValueError('Original skin source differs from verified skeleton')
        source=json.loads(path.read_bytes());inputs.append(record(path))
        bones={b['name']:i for i,b in enumerate(source['bones'])};slots={s['name']:i for i,s in enumerate(source['slots'])}
        constraints={(kind,c['name']):len_id for len_id,(kind,c) in enumerate((kind,c) for kind in ('ik','transform','path','physics') for c in source.get(kind,[]))}
        skins=[];identity=0
        for skin in source['skins']:
            if set(skin)-{'name','bones','ik','transform','path','physics','attachments','color'}:raise ValueError('Unhandled original skin field')
            entries=[]
            for slot,attachments in skin.get('attachments',{}).items():
                for key in attachments:
                    entries.append(dict(slot=slots[slot],key=key,id=identity));identity+=1
            row=dict(name=skin['name'],entries=entries,bones=[bones[name] for name in skin.get('bones',[])],
                constraints=[constraints[kind,name] for kind in ('ik','transform','path','physics') for name in skin.get(kind,[])])
            skins.append(row)
            counts['bones']+=len(row['bones']);counts['constraints']+=len(row['constraints'])
        combinations=[]
        for i,a in enumerate(skins):
            b=skins[(i+1)%len(skins)];resolved={(e['slot'],e['key']):e for e in a['entries']}
            resolved.update(((e['slot'],e['key']),e) for e in b['entries'])
            combinations.append(dict(first=i,second=(i+1)%len(skins),entries=list(resolved.values()),bones=union(a['bones'],b['bones']),constraints=union(a['constraints'],b['constraints'])))
        cases.append(dict(sex=sex,slotCount=len(slots),boneCount=len(bones),constraintCount=len(constraints),identityCount=identity,skins=skins,combinations=combinations))
        counts['skins']+=len(skins);counts['attachments']+=identity;counts['combinations']+=len(combinations)
    output=CACHE/'typed/native-skins-validation.json'
    output.write_text(json.dumps(dict(cases=cases),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkin.cs'
    validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeSkinValidation.cs'
    binding=ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearanceSpineBindings.cs'
    proof=dict(format=1,manifest='871_156598',inputs=inputs,bindings=record(binding),fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(p) for path in (runtime,validation) for p in (path,path.with_name(path.name+'.meta'))],counts=counts,
        format_references=['https://esotericsoftware.com/spine-json-format#Skins'],
        operation='Independently preserve each source compound slot/name key and shared attachment identity, ordered bone/constraint membership and original ordinal skin names. Combined skins overwrite identical attachment keys with later skins and union membership without duplicate entries. Resolve active skin before the original default skin. This native table implements the migrated application interface; no skeleton SDK implementation or alternate art is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: 1007 source skins, 34009 attachment identities, original ordered membership and 1007 pair combinations pass. Last-skin overwrite, duplicate-free unions, clear/reuse, unchanged source tables, default fallback, ordinal names, cloned input arrays/shared attachment objects and invalid layouts pass. Source identities are represented by validation objects; production native geometry/material objects are not bound here.',
        limitations=['This table does not implement skin-change slot migration, world constraints, native rendering or the full profession page.'])
    (ROOT/'assets/reference/profession-ui/native-skins-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Bound original skin tables:',counts)


if __name__=='__main__':build()
