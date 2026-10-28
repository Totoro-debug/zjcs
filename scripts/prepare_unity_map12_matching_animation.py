"""Restore the original Map12 matching Legacy clip and AnimationSoundPlayer binding."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT/'.tools/character-art-python'), str(ROOT/'scripts')]
import yaml
from UnityPy.classes.PPtr import PPtr
from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_dungeon_ui_bindings import CODE, VERSION, relative_transform_path
from commerce_clip_time import ENGINE_SHA256, legacy_duration

NAME = 'DailyDungeonDetailContent_GameMatch_Show'
NATIVE = ROOT/'unity/Assets/Official/Dungeons/Prefabs/UI/DailyDungeon'/(NAME+'.anim')
DEST = ROOT/'unity/Assets/Official/World/Map/12/Resources/Map12/Matching'
RECEIPT = ROOT/'assets/reference/official-map/map12-matching-animation-source-manifest.json'
AUDIO = ROOT/'assets/reference/official-map/map12-ui-audio-source-manifest.json'
SCRIPTS = {'Image': ('UnityEngine.UI', 'fe87c0e1cc204ed48ad3b37840f39efc'),
           'TextMeshProUGUI': ('TMPro', 'f4688fdb7df04437aeb418b961361dc5')}

def sha(data): return hashlib.sha256(data).hexdigest()
def encode(value): return (json.dumps(value,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf8')
def write(path, data, update):
    old=path.read_bytes() if path.exists() else None
    if old is not None and old!=data and not update: raise ValueError('Refusing changed matching output: '+str(path))
    if old!=data:
        path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)

def equal(source, native, path):
    if isinstance(source,dict):
        for key,value in source.items(): equal(value,native[key],path+'/'+key)
    elif isinstance(source,list):
        if len(source)!=len(native): raise ValueError('Original animation list changed: '+path)
        for index,(a,b) in enumerate(zip(source,native)): equal(a,b,path+'/'+str(index))
    elif isinstance(source,float):
        value=float(native) if not isinstance(native,str) else float(native.replace('Infinity','inf'))
        if struct.pack('<f',source)!=struct.pack('<f',value): raise ValueError('Original float32 changed: '+path)
    elif source!=native: raise ValueError('Original animation field changed: '+path)

def source_record(apk,obj): return dict(source=apk.provenance(obj),object_raw_sha256=sha(obj.get_raw_data()))

def build(native_path=NATIVE, update=False):
    apk=Apk.current()
    if apk.version!=VERSION: raise ValueError('Unexpected matching source version')
    schemas=ScriptSchemas(code_root=CODE)
    root=apk.asset('prefabs_ui_dailydungeon','/DailyDungeonDialogPrivateMap.prefab').read()
    matches=[row for row in relative_transform_path(root) if row[2][-3:]==('NormalGo','GameMatch','Content')]
    if len(matches)!=1: raise ValueError('Original matching animation host is ambiguous')
    go,indices,names,_=matches[0]
    animation=next(pair.component.deref() for pair in go.m_Component if pair.component.type.name=='Animation')
    settings=animation.read_typetree()
    if len(settings['m_Animations'])!=1 or settings['m_Animation']!=settings['m_Animations'][0]:
        raise ValueError('Original matching clip list changed')
    clip=PPtr(**settings['m_Animation'],assetsfile=animation.assets_file).deref();original=clip.read_typetree()
    duration=legacy_duration(clip.read())
    engine=ROOT/'.tools/source-packages/official-code/libunity.so'
    if sha(engine.read_bytes())!=ENGINE_SHA256: raise ValueError('Original AnimationClip duration engine changed')
    if original['m_Name']!=NAME or not original['m_Legacy'] or len(original['m_FloatCurves'])!=31 or len(original['m_ScaleCurves'])!=5:
        raise ValueError('Original matching animation identity changed')
    native_path=native_path.resolve();payload=native_path.read_bytes();text=payload.decode('utf8').replace('\r\n','\n')
    exported=yaml.safe_load('\n'.join(text.splitlines()[3:]))['AnimationClip']
    fields=('m_Name','m_Legacy','m_Compressed','m_UseHighQualityCurve','m_RotationCurves','m_CompressedRotationCurves',
        'm_EulerCurves','m_PositionCurves','m_ScaleCurves','m_PPtrCurves','m_SampleRate','m_WrapMode','m_Bounds',
        'm_ClipBindingConstant','m_HasGenericRootTransform','m_HasMotionFloatCurves')
    for field in fields: equal(original[field],exported[field],field)
    mappings={};targets=set();script_sources={}
    if len(exported['m_FloatCurves'])!=len(original['m_FloatCurves']): raise ValueError('Matching float channels changed')
    for a,b in zip(original['m_FloatCurves'],exported['m_FloatCurves']):
        for field in ('path','attribute','classID','flags','curve'): equal(a[field],b[field],field)
        target={224:'RectTransform',225:'CanvasGroup'}.get(a['classID'])
        if a['classID']==114:
            script=PPtr(**a['script'],assetsfile=clip.assets_file).deref();data=script.read();target=data.m_ClassName
            namespace,guid=SCRIPTS[target]
            if data.m_Namespace!=namespace: raise ValueError('Unknown original matching animation script')
            previous=b['script']['guid']
            if previous in mappings and mappings[previous]!=guid: raise ValueError('Ambiguous native matching script mapping')
            mappings[previous]=guid;script_sources[target]=dict(**source_record(apk,script),namespace=namespace,native_guid=guid)
        elif a['script']['m_PathID'] or b['script']['fileID']: raise ValueError('Unexpected native animation script')
        if target is None: raise ValueError('Unsupported original matching animation channel')
        targets.add((a['path'],target))
    for a in original['m_ScaleCurves']: targets.add((a['path'],'Transform'))
    hierarchy=[dict(path='/'.join(child_names[1:]),indices=list(child_path),objects=list(child_names))
               for _,child_path,child_names,_ in relative_transform_path(go)]
    source_paths={row['path'] for row in hierarchy}
    absent_targets=[dict(path=path,type=kind) for path,kind in sorted(targets) if path not in source_paths]
    absent_curves=[dict(path=row['path'],attribute=row['attribute'],classID=row['classID'])
                   for row in original['m_FloatCurves'] if row['path'] not in source_paths]
    targets={target for target in targets if target[0] in source_paths}
    source_events=original['m_Events']
    if len(source_events)!=1 or source_events[0]['functionName']!='PostEvent' or source_events[0]['intParameter']!=0:
        raise ValueError('Original matching animation event changed')
    if len(exported['m_Events'])!=len(source_events): raise ValueError('Native animation event count changed')
    for a,b in zip(source_events,exported['m_Events']):
        if any(a['objectReferenceParameter'].values()) or b['objectReferenceParameter']['fileID']:
            raise ValueError('Unconverted animation event object reference')
        for field in ('time','functionName','floatParameter','intParameter','messageOptions'): equal(a[field],b[field],field)
        equal(a['data'],b['data'] or '', 'event/data')
    for field,value in original['m_MuscleClip'].items():
        if field in exported['m_AnimationClipSettings']: equal(value,exported['m_AnimationClipSettings'][field],field)
    for previous,current in mappings.items(): text=text.replace('guid: '+previous+', type: 3','guid: '+current+', type: 3')
    components={}
    for pair in go.m_Component:
        obj=pair.component.deref()
        if obj.type.name=='MonoBehaviour': components[obj.read(check_read=False).m_Script.read().m_ClassName]=obj
    sound=components['AnimationSoundPlayer'];sound_fields=schemas.read(sound)
    if len(sound_fields['mEvents'])!=1: raise ValueError('Original matching sound event list changed')
    event=sound_fields['mEvents'][0]
    if event['idInternal'] or event['valueGuidInternal']: raise ValueError('Unverified inline matching event')
    reference=PPtr(**event['WwiseObjectReference'],assetsfile=sound.assets_file).deref();event_fields=schemas.read(reference)
    if (event_fields['objectName'],event_fields['id'])!=('TeamMatch_Waiting_1',1386996041): raise ValueError('Original matching event changed')
    audio=json.loads(AUDIO.read_bytes());record=next(row for row in audio['events'] if row['id']==event_fields['id'])
    if audio['manifest']!=VERSION or sha((ROOT/record['path']).read_bytes())!=record['sha256']: raise ValueError('Original matching PCM changed')
    canvas=[]
    for child,path,objects,_ in relative_transform_path(go):
        for pair in child.m_Component:
            obj=pair.component.deref()
            if obj.type.name!='CanvasGroup':continue
            fields=obj.read_typetree()
            canvas.append(dict(path='/'.join(map(str,path)),objects=list(objects),enabled=bool(fields['m_Enabled']),
                alpha=fields['m_Alpha'],interactable=fields['m_Interactable'],blocksRaycasts=fields['m_BlocksRaycasts'],
                ignoreParentGroups=fields['m_IgnoreParentGroups'],**source_record(apk,obj)))
    native_scripts=[]
    package_metas={'Image':'com.unity.ugui@1.0.0/Runtime/UI/Core/Image.cs.meta',
                   'TextMeshProUGUI':'com.unity.textmeshpro@3.0.6/Scripts/Runtime/TextMeshProUGUI.cs.meta'}
    for name,relative in package_metas.items():
        meta=ROOT/'unity/Library/PackageCache'/relative;raw=meta.read_bytes()
        guid=re.search(rb'^guid: ([0-9a-f]{32})\r?$',raw,re.M)
        if guid is None or guid.group(1).decode()!=SCRIPTS[name][1]:
            raise ValueError('Project UI script target GUID changed')
        native_scripts.append(dict(type=name,path=meta.relative_to(ROOT).as_posix(),sha256=sha(raw),guid=SCRIPTS[name][1]))
    native=DEST/(NAME+'.anim');guid=sha(('zjcs-map12-matching-animation:'+NAME).encode())[:32]
    folder_guid=sha(b'zjcs-map12-matching-animation-folder')[:32]
    outputs=[]
    for path,data in ((native,text.encode('utf8')),(native.with_name(native.name+'.meta'),
        ('fileFormatVersion: 2\nguid: '+guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 7400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()),
        (DEST.with_name(DEST.name+'.meta'),('fileFormatVersion: 2\nguid: '+folder_guid+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())):
        write(path,data,update);outputs.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(data),bytes=len(data)))
    binding=dict(path='/'.join(map(str,indices)),objects=list(names),enabled=bool(settings['m_Enabled']),
        wrapMode=settings['m_WrapMode'],playAutomatically=settings['m_PlayAutomatically'],animatePhysics=settings['m_AnimatePhysics'],cullingType=settings['m_CullingType'],
        clip=dict(name=NAME,resource='Map12/Matching/'+NAME,duration=duration,sampleRate=original['m_SampleRate'],**source_record(apk,clip)),
        sound=dict(enabled=bool(sound_fields['m_Enabled']),events=[dict(name=record['name'],id=record['id'],resource=record['resource'],**source_record(apk,reference))],**source_record(apk,sound)),
        canvasGroups=canvas,absentSourceTargets=absent_targets,targets=[dict(path=path,type=kind) for path,kind in sorted(targets)],**source_record(apk,animation))
    code=CODE/'launch-source/AnimationSoundPlayer.cs'
    receipt=dict(manifest=VERSION,mapClassId=12,binding=binding,events=source_events,script_sources=script_sources,
        source_hierarchy=hierarchy,absent_source_curves=absent_curves,
        native_input=dict(path=native_path.relative_to(ROOT).as_posix(),sha256=sha(payload)),
        code_sources=[dict(path=code.relative_to(ROOT).as_posix(),sha256=sha(code.read_bytes()))],native_script_targets=native_scripts,
        duration_rule=dict(engine_path=engine.relative_to(ROOT).as_posix(),engine_sha256=ENGINE_SHA256,
            icall='UnityEngine.AnimationClip::get_length',icall_address='0x5755e0',range_kernel='0xf9c434',
            port_path='scripts/commerce_clip_time.py',port_sha256=sha((ROOT/'scripts/commerce_clip_time.py').read_bytes())),
        audio_source_receipt=dict(path=AUDIO.relative_to(ROOT).as_posix(),sha256=sha(AUDIO.read_bytes())),
        verified=dict(float_curves=31,scale_curves=5,float32_keys_exact=True,event_time_exact=True,source_sample_rate=original['m_SampleRate'],present_target_groups=len(targets),absent_float_curves=len(absent_curves)),
        operation='Compare all original Legacy curve fields, float32 key values/slopes/weights, script identities, clip settings and events; reuse native export with only Image/TMP script GUID mapping. Local audio adapter replaces Wwise Post; preserve event indexing and Animation settings.',outputs=outputs)
    write(RECEIPT,encode(receipt),update)
    print(json.dumps(dict(clip=NAME,float_curves=31,scale_curves=5,events=source_events,targets=len(targets),outputs=len(outputs)),ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--native-source',type=Path,default=NATIVE);parser.add_argument('--update',action='store_true')
    args=parser.parse_args();build(args.native_source,args.update)
