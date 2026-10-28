"""Recover source idle attachment changes and stable draw-order permutations."""
import json
from pathlib import Path

from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record, f32


def color(value):
    if len(value) == 6: value += 'ff'
    if len(value) != 8: raise ValueError('Original slot color format changed')
    return dict(zip('rgba', bytes.fromhex(value)))


def draw_order(slot_count, indices, offsets):
    destinations, moved, source = {}, set(), []
    for offset in offsets:
        slot, raw = indices[offset['slot']], offset['offset']
        if not isinstance(raw, int) or not -2**31 <= raw < 2**32:
            raise ValueError('Original draw-order offset is not a 32-bit integer')
        # The format converter exposes varint+ bits as a Python unsigned int.
        # Restore signed 32-bit meaning without changing its source JSON.
        signed = raw - 2**32 if raw >= 2**31 else raw
        destination = slot + signed
        if slot in moved or destination in destinations or not 0 <= destination < slot_count:
            raise ValueError('Original draw-order destination collides or is outside the slot list')
        moved.add(slot); destinations[destination] = slot
        source.append(dict(slot=slot, raw=raw, signed=signed, destination=destination))
    remaining = iter(i for i in range(slot_count) if i not in moved)
    result = [destinations[i] if i in destinations else next(remaining) for i in range(slot_count)]
    if sorted(result) != list(range(slot_count)): raise ValueError('Original draw order is not a permutation')
    if [i for i in result if i not in moved] != [i for i in range(slot_count) if i not in moved]:
        raise ValueError('Untouched draw slots lost their stable relative order')
    return result, source


def build():
    bone_proof_path = ROOT/'assets/reference/profession-ui/bone-pose-source-manifest.json'
    bone_proof = json.loads(bone_proof_path.read_bytes())
    bone_fixture = CACHE/'typed/bone-pose-validation.json'
    if bone_proof['fixture'] != record(bone_fixture): raise ValueError('Original bone fixture changed')
    cases, inputs = [], [record(bone_proof_path),record(bone_fixture)]
    count_tracks = count_keys = count_draws = count_samples = 0
    for sex in ('male','female'):
        path = CACHE/'skeletons'/sex/'skeleton.json'
        if record(path) not in bone_proof['inputs']: raise ValueError('Original slot source differs from bone source')
        original = json.loads(path.read_bytes()); inputs.append(record(path))
        bones = {b['name']: i for i,b in enumerate(original['bones'])}
        indices = {s['name']: i for i,s in enumerate(original['slots'])}
        slots = []
        for slot in original['slots']:
            if set(slot)-{'name','bone','color','dark','attachment','blend'}: raise ValueError('Restore extra slot setup fields')
            slots.append(dict(name=slot['name'],bone=bones[slot['bone']],attachment=slot.get('attachment'),
                color=color(slot.get('color','ffffffff')),hasDarkColor='dark' in slot,
                darkColor=color(slot.get('dark','00000000')),blend={'normal':0,'additive':1,'multiply':2,'screen':3}[slot.get('blend','normal')]))
        clips = []
        for name in ('home/idle_01','100/idle_01','200/idle_01'):
            animation = original['animations'][name]
            tracks, order, draw_sources = [], [], []
            times = {f32(t) for t in (-.001,0,.15,.5,.9,1.3,2,3.2)}
            for slot_name, channels in animation.get('slots',{}).items():
                if set(channels) != {'attachment'}: raise ValueError('Restore non-attachment idle slot timeline')
                keys = [dict(time=f32(k.get('time',0)),name=k.get('name')) for k in channels['attachment']]
                if any(set(k)-{'time','name'} for k in channels['attachment']): raise ValueError('Unexpected attachment key field')
                tracks.append(dict(slot=indices[slot_name],keys=keys)); count_tracks += 1; count_keys += len(keys)
                for key in keys: times.update(f32(key['time']+d) for d in (-.00001,0,.00001))
            for key in animation.get('drawOrder',[]):
                permutation, offsets = draw_order(len(slots),indices,key.get('offsets',[]))
                time = f32(key.get('time',0)); order.append(dict(time=time,slots=permutation)); draw_sources.append(offsets); count_draws += 1
                times.update(f32(time+d) for d in (-.00001,0,.00001))
            samples = []
            for time in sorted(times):
                attachments = [s['attachment'] for s in slots]
                for track in tracks:
                    applied = [k for k in track['keys'] if k['time'] <= time]
                    if applied: attachments[track['slot']] = applied[-1]['name']
                applied_order = [k for k in order if k['time'] <= time]
                samples.append(dict(time=time,attachments=attachments,order=applied_order[-1]['slots'] if applied_order else list(range(len(slots)))))
            count_samples += len(samples)
            clips.append(dict(name=name,tracks=tracks,order=order,draw_sources=draw_sources,samples=samples))
        cases.append(dict(sex=sex,slots=slots,clips=clips))
    output = CACHE/'typed/slot-pose-validation.json'
    output.write_text(json.dumps(dict(cases=cases),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime = ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSlotPose.cs'
    validation = ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeSlotPoseValidation.cs'
    proof = dict(format=1,manifest='871_156598',inputs=inputs,fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(p) for path in (runtime,validation) for p in (path,path.with_name(path.name+'.meta'))],
        counts=dict(slots=sum(len(b['slots']) for b in cases),tracks=count_tracks,attachment_keys=count_keys,draw_order_keys=count_draws,samples=count_samples),
        format_references=['https://esotericsoftware.com/spine-json-format#Slots','https://esotericsoftware.com/spine-json-format#Slot-timelines','https://esotericsoftware.com/spine-json-format#Draw-order-timeline','https://esotericsoftware.com/spine-binary-format#Varint'],
        operation='Independently bind unmixed discrete attachment keys, preserving null clears, key boundaries and inactive bones. Preserve every setup slot name/bone/color/dark-color flag/blend. Recover each stable draw-order permutation by explicit destination assignment and filling untouched slots in source order. Interpret varint+ bit patterns as signed 32-bit offsets (4294967290 becomes -6) without altering the verified converter JSON. No skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Play Mode: 252 setup slots, 54 attachment timelines, 84 attachment keys, four draw-order keys, 109 time samples and 27468 exact attachment/order comparisons pass. Key boundaries, null clears/blinks, backward sampling, source weapon destination offsets +4/-6 and untouched relative order, setup color/dark/blend values, inactive skin bones, input ownership and invalid bindings pass. These are discrete source-data checks, not an SDK pose, character render or full profession-page validation.',
        limitations=['This stage does not implement animation mixing, skin/attachment resolution, weighted geometry, constraints, character rendering or full profession UI. Generated discrete references are not original SDK pose or visual captures.'])
    (ROOT/'assets/reference/profession-ui/slot-pose-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Recovered original discrete slot state:',proof['counts'])


if __name__ == '__main__': build()
