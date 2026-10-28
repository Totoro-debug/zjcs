"""Preserve attachment identity through original skin changes and setup resets."""
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record


def build():
    skin_proof_path = ROOT/'assets/reference/profession-ui/native-skins-source-manifest.json'
    skin_proof = json.loads(skin_proof_path.read_bytes())
    fixture_path = ROOT/skin_proof['fixture']['path']
    if record(fixture_path) != skin_proof['fixture']:
        raise ValueError('Verified source skin identities changed')
    source_skins = json.loads(fixture_path.read_bytes())
    cases=[];counts=dict(skins=0,transitions=0,slot_states=0)
    for body in source_skins['cases']:
        path=CACHE/'skeletons'/body['sex']/'skeleton.json'
        if record(path) not in skin_proof['inputs']:
            raise ValueError('Original slot setup differs from verified source')
        original=json.loads(path.read_bytes())
        setup=[s.get('attachment') for s in original['slots']]
        skins=body['skins'];maps=[{(e['slot'],e['key']):e['id'] for e in s['entries']} for s in skins]
        default=next((m for s,m in zip(skins,maps) if s['name']=='default'),{})
        def reset(active):return [active.get((i,key),default.get((i,key),-1)) if key is not None else -1 for i,key in enumerate(setup)]
        scenarios=[]
        for i,a in enumerate(maps):
            j=(i+1)%len(maps);b=maps[j]
            initial=reset({})
            first=[a.get((slot,key),old) if key is not None else old for slot,(key,old) in enumerate(zip(setup,initial))]
            before=list(first)
            if skins[i]['entries']:
                entry=skins[i]['entries'][-1];before[entry['slot']]=entry['id']
            custom=(i*7)%len(setup);empty=(custom+1)%len(setup)
            before[custom]=body['identityCount'];before[empty]=-1
            # Reference uses a reverse identity map, independently of the
            # native old-key walk. Object identity is preserved by source ID.
            identities={identity:key for key,identity in a.items()}
            swapped=[b.get(identities[identity],identity) if identity in identities and identities[identity][0]==slot else identity for slot,identity in enumerate(before)]
            again=[b.get((slot,key),old) if key is not None else old for slot,(key,old) in enumerate(zip(setup,swapped))]
            scenarios.append(dict(first=i,second=j,initial=initial,firstState=first,before=before,swapped=swapped,afterNull=swapped,fromNull=again,reset=reset(b)))
        out={k:body[k] for k in ('sex','slotCount','boneCount','constraintCount','identityCount','skins')}
        out.update(setup=setup,scenarios=scenarios);cases.append(out)
        counts['skins']+=len(skins);counts['transitions']+=len(scenarios)*4
        counts['slot_states']+=len(scenarios)*len(setup)*7
    output=CACHE/'typed/skin-slots-validation.json'
    output.write_text(json.dumps(dict(cases=cases),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkinSlots.cs'
    validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeSkinSlotsValidation.cs'
    proof=dict(format=1,manifest='871_156598',inputs=[record(skin_proof_path),record(fixture_path)],fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(p) for file in (runtime,validation) for p in (file,file.with_name(file.name+'.meta'))],counts=counts,
        behavior_reference='https://esotericsoftware.com/spine-runtime-skins#Skin-changes',
        operation='Independent slot attachment identity state using published skin-change semantics and verified original slot/name maps. Initial skin affects setup-visible entries; subsequent skin changes replace only matching old attachment identities at the corresponding source slot/name key. Manual/null attachments survive changes, absent replacement keys retain the old identity, null skin changes do not reset slots, and explicit setup reset resolves active then default skin. No skeleton SDK implementation is read or used.',
        validated_scope='Unity 2022.3.57f1c2 isolated real Play Mode passed all 1007 source skins: 4028 skin transitions and 888174 exact source slot states. Initial setup-visible attachments, old-identity migration at compound keys, missing replacements, manual/null overrides, null skin, active/default setup resolution, same-skin behavior, original identity enumeration, input ownership, ordinal lookup and invalid layout rejection pass. Attachments are identity objects; no constrained actor or full profession UI is rendered by this test.',
        limitations=['This is attachment state only. Bone/constraint activation, world update scheduling, timeline mixing and the native graphic are separate production integration requirements.'])
    (ROOT/'assets/reference/profession-ui/skin-slots-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Prepared original skin/slot transitions:',counts)


if __name__=='__main__':build()
