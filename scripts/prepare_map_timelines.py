"""Decode original story Timeline tracks and their native scene bindings."""
import hashlib,json,collections,argparse,math,traceback
from pathlib import Path
from UnityPy.classes.PPtr import PPtr
from prepare_map_regions import Regions,ART,REF,ROOT,save
from map_timeline_assets import TimelineAssets


def build(selected=None,extra=None):
    adaptations=json.loads((REF/'timeline-adaptations.json').read_text(encoding='utf8'))
    extra=set(extra or [])|{v['source'] for v in adaptations.values()}
    media=json.loads((ART/'map-story-media.json').read_text(encoding='utf8'))
    stories=json.loads((ART/'adventure.json').read_text(encoding='utf8'))['stories']
    requested=set()
    def visit(value):
        if isinstance(value,dict):
            for k,v in value.items():
                if k in ['StoryPrefabGuid','TimelineProxy'] and v.get('mAssetGuid'):requested.add(v['mAssetGuid'])
                else:visit(v)
        elif isinstance(value,list):
            for v in value:visit(v)
    visit(stories)
    # Some story sequences contain a director inside their MyPlayable prefab.
    # Decode that original director as well; its GUID is not a story node field.
    playables_path=ART/'map-playables.json'
    if playables_path.exists():
        playables=json.loads(playables_path.read_text(encoding='utf8'))
        for guid,scene in playables.get('scenes',{}).items():
            if any(track['type']=='MyPlayableTimeLine' for track in scene['tracks']):
                requested.add(guid)
                media['assets'][guid]=scene['asset']
    total_requested=len(requested)
    if extra:
        from official_manifest import read_asset_manifest
        from official_apk import Apk
        apk=Apk()
        for name in apk.archive.namelist():
            if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes'):
                for row in read_asset_manifest(apk.archive.read(name)):
                    if row['guid'] in extra:media['assets'][row['guid']]=row
        requested.update(extra)
        if selected:selected=set(selected)|set(extra)
    if selected:requested.intersection_update(selected)
    e=Regions();assets=TimelineAssets(e);output={};failures={};counts=collections.Counter();payload_types=collections.Counter()
    previous_receipt={}
    if selected and (ART/'map-timelines.json').exists():
        previous=json.loads((ART/'map-timelines.json').read_text(encoding='utf8'));output.update(previous.get('scenes',{}));assets.skeletons.update(previous.get('skeletons',{}))
        previous_receipt=json.loads((REF/'timelines-source-manifest.json').read_text(encoding='utf8'))
        failures.update({k:v for k,v in previous_receipt.get('failures',{}).items() if k not in requested})
    for guid in sorted(requested):
        row=media['assets'].get(guid)
        if row is None:failures[guid]='Original AssetTable GUID unavailable';continue
        try:
            # Use the AssetTable's bundle name. Dots/spaces inside prefab names
            # are normalized in bundle names and cannot be derived reliably.
            root=e.apk.asset(row['bundle'],'/'+row['path'].rsplit('/',1)[-1]);directors=[];paths={};tree={};e.active_overrides={};components_by_path={};receivers={};animators=[]
            def scene(obj,parent=''):
                e.input(obj);go=obj.read();path=parent+'/'+go.m_Name
                paths[(obj.assets_file.name,obj.path_id)]=path
                components=[c.component.deref() for c in go.m_Component]
                transform=next(c.read() for c in components if c.type.name in ['Transform','RectTransform'])
                q=transform.m_LocalRotation
                tree[path]=dict(position=dict(x=transform.m_LocalPosition.x,y=transform.m_LocalPosition.y,z=transform.m_LocalPosition.z),quaternion=dict(x=q.x,y=q.y,z=q.z,w=q.w),rotation=math.atan2(2*(q.w*q.z+q.x*q.y),1-2*(q.y*q.y+q.z*q.z)),scale=dict(x=transform.m_LocalScale.x,y=transform.m_LocalScale.y,z=transform.m_LocalScale.z),active=bool(go.m_IsActive))
                e.active_overrides[obj.assets_file.name+':'+str(obj.path_id)]=True
                for c in components:
                    paths[(c.assets_file.name,c.path_id)]=path
                    if c.type.name=='PlayableDirector':directors.append(c)
                    if c.type.name=='Animator':animators.append((path,c))
                    if c.type.name=='MonoBehaviour':
                        kind=c.read(check_read=False).m_Script.read().m_ClassName
                        if kind in ['CinemachineVirtualCamera','CinemachineBasicMultiChannelPerlin','SignalReceiver']:
                            e.input(c);components_by_path.setdefault(path,[]).append((kind,c,e.schemas.read(c)))
                for child in transform.m_Children:scene(child.read().m_GameObject.deref(),path)
            scene(root)
            def ptr(value,owner):return PPtr(**value,assetsfile=owner.assets_file).deref() if value.get('m_PathID') else None
            def path_of(value,owner):
                obj=ptr(value,owner)
                return paths.get((obj.assets_file.name,obj.path_id),'') if obj is not None else ''
            for path,components in components_by_path.items():
                for kind,obj,cfg in components:
                    if kind=='CinemachineVirtualCamera':
                        tree[path]['camera']=dict(lens=cfg['m_Lens'],follow=path_of(cfg['m_Follow'],obj),look_at=path_of(cfg['m_LookAt'],obj))
                    elif kind=='CinemachineBasicMultiChannelPerlin':
                        profile=ptr(cfg['m_NoiseProfile'],obj)
                        if profile is not None:
                            e.input(profile);tree[path]['noise']=dict(profile=e.schemas.read(profile),amplitude=cfg['m_AmplitudeGain'],frequency=cfg['m_FrequencyGain'],offsets=cfg['mNoiseOffsets'])
                    elif kind=='SignalReceiver':
                        for signal,event in zip(cfg['m_Events']['m_Signals'],cfg['m_Events']['m_Events']):
                            signal_obj=ptr(signal,obj)
                            if signal_obj is not None:
                                receivers[signal_obj.assets_file.name+':'+str(signal_obj.path_id)]=assets.payload(event,obj,path,tree,paths)
            sequences=[]
            for director in directors:
                e.input(director);d=director.read_typetree();asset=ptr(d['m_PlayableAsset'],director)
                if asset is None:continue
                e.input(asset);timeline=e.schemas.read(asset)
                exposed={str(k):path_of(v,director) for k,v in d['m_ExposedReferences']['m_References']}
                bindings={}
                for binding in d['m_SceneBindings']:
                    target=ptr(binding['key'],director)
                    if target is not None:bindings[(target.assets_file.name,target.path_id)]=path_of(binding['value'],director)
                tracks=[]
                def track(pointer,owner,parent_kind=""):
                    obj=ptr(pointer,owner)
                    if obj is None:return
                    e.input(obj);kind=obj.read(check_read=False).m_Script.read().m_ClassName;cfg=e.schemas.read(obj);counts[kind]+=1
                    binding=bindings.get((obj.assets_file.name,obj.path_id),'')
                    record=dict(type=kind,name=cfg['m_Name'],binding=binding,muted=bool(cfg.get('m_Muted',False)),clips=[],output=kind!='GroupTrack' and kind!=parent_kind)
                    if kind=='AnimationTrack':
                        for key in ['m_InfiniteClip','m_AnimClip']:
                            clip_obj=ptr(cfg.get(key,{}),obj)
                            if clip_obj is not None:
                                record['infinite']=assets.curves(clip_obj,binding,tree);record['time_offset']=float(cfg.get('m_InfiniteClipTimeOffset',0));break
                    if kind=='ActivationTrack':record['post_playback']=int(cfg.get('m_PostPlaybackState',0))
                    record['signals']=[]
                    for pointer in cfg.get('m_Markers',{}).get('m_Objects',[]):
                        marker=ptr(pointer,obj)
                        if marker is None:continue
                        e.input(marker);marker_cfg=e.schemas.read(marker)
                        signal=ptr(marker_cfg.get('m_Asset',{}),marker)
                        if signal is not None:
                            signal_key=signal.assets_file.name+':'+str(signal.path_id)
                            record['signals'].append(dict(time=marker_cfg['m_Time'],event=receivers.get(signal_key,{})))
                    for clip in cfg.get('m_Clips',[]):
                        playable=ptr(clip['m_Asset'],obj)
                        if playable is None:continue
                        e.input(playable);typ=playable.read(check_read=False).m_Script.read().m_ClassName;payload_types[typ]+=1
                        payload=e.schemas.read(playable)
                        for k in ['m_GameObject','m_Script','m_Enabled']:payload.pop(k,None)
                        payload=assets.payload(payload,playable,binding,tree,paths)
                        record['clips'].append(dict(start=clip['m_Start'],duration=clip['m_Duration'],clip_in=clip['m_ClipIn'],speed=clip['m_TimeScale'],name=clip['m_DisplayName'],type=typ,payload=payload))
                    tracks.append(record)
                    for child in cfg.get('m_Children',[]):track(child,obj,kind)
                for pointer in timeline['m_Tracks']:track(pointer,asset)
                output_index=0
                for record in tracks:
                    if record['output']:record['output_index']=output_index;output_index+=1
                duration=timeline['m_FixedDuration'] if int(timeline['m_DurationMode'])==1 else max([c['start']+c['duration'] for t in tracks for c in t['clips']]+[t['infinite']['duration'] for t in tracks if 'infinite' in t]+[0])
                sequences.append(dict(name=timeline['m_Name'],duration=duration,tracks=tracks,exposed=exposed,path=paths.get((director.assets_file.name,director.path_id),''),wrap=int(d.get('m_DirectorWrapMode',0)),play_on_awake=bool(d.get('m_InitialState',0))))
            visuals=assets.scene_geometry(guid,row,tree);assets.bind_spine_skins(sequences,visuals)
            for path,obj in animators:tree[path]['animator']=assets.animator(obj,path,tree,visuals)
            assets.convert_skeletons()
            output[guid]=dict(asset=row,scene=tree,sequences=sequences,visuals=visuals)
            print('Timeline',len(output),'/',len(requested),row['path'],flush=True)
        except Exception as ex:
            traceback.print_exc()
            failures[guid]=dict(asset=row,error=type(ex).__name__+': '+str(ex));print('Timeline failure',row['path'],failures[guid]['error'],flush=True)
    active_adaptations={k:v for k,v in adaptations.items() if k not in output and v['source'] in output}
    save(ART/'map-timelines.json',dict(scenes=output,skeletons=assets.skeletons,adaptations=active_adaptations))
    save(REF/'timelines-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,schemas=e.schemas.records,tracks=dict(counts),clip_types=dict(payload_types),requested=len(requested),converted=len(output),failures=failures,outputs={'map-timelines.json':hashlib.sha256((ART/'map-timelines.json').read_bytes()).hexdigest()},schema_sources=[dict(url='https://download.packages.unity.com/com.unity.timeline/-/com.unity.timeline-1.7.6.tgz',sha256=hashlib.sha256((ROOT/'.tools/unity-timeline-source/com.unity.timeline-1.7.6.tgz').read_bytes()).hexdigest()),dict(url='https://download.packages.unity.com/com.unity.cinemachine/-/com.unity.cinemachine-2.9.7.tgz',sha256=hashlib.sha256((ROOT/'.tools/unity-cinemachine-source/com.unity.cinemachine-2.9.7.tgz').read_bytes()).hexdigest())]))
    print('Timeline scenes',len(output),'failures',len(failures),'tracks',dict(counts),'clips',dict(payload_types),flush=True)
    receipt_path=REF/'timelines-source-manifest.json';receipt=json.loads(receipt_path.read_text(encoding='utf8'))
    for field,values in [('inputs',e.source_records),('resources',e.records),('unresolved',assets.unresolved),('schemas',e.schemas.records)]:
        receipt[field]={**previous_receipt.get(field,{}),**values}
    receipt['requested']=total_requested
    receipt['adaptations']=active_adaptations
    receipt['adaptation_config_sha256']=hashlib.sha256((REF/'timeline-adaptations.json').read_bytes()).hexdigest()
    receipt.update(operation='Original scene transforms, Timeline clips and bindings; native Mecanim curves; original Spine animation frames, including original GUID-resolved split animation binaries, sampled at 12 fps')
    receipt['tracks']=dict(collections.Counter(t['type'] for value in output.values() for s in value['sequences'] for t in s['tracks']))
    receipt['clip_types']=dict(collections.Counter(c['type'] for value in output.values() for s in value['sequences'] for t in s['tracks'] for c in t['clips']))
    receipt['outputs'].update({p.relative_to(ART).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in assets.folder.iterdir() if p.suffix in ['.geom','.json']})
    for name in ['SpineAnimationStateClip.cs','SpineAnimationStateBehaviour.cs']:
        receipt['schema_sources'].append(dict(url='https://raw.githubusercontent.com/EsotericSoftware/spine-runtimes/4.2/spine-unity/Modules/com.esotericsoftware.spine.timeline/Runtime/SpineAnimationState/'+name,sha256=hashlib.sha256((ROOT/'.tools/spine-timeline-source'/name).read_bytes()).hexdigest()))
    receipt['schema_code_sha256']=hashlib.sha256((ROOT/'scripts/unity_schema_types.cs').read_bytes()).hexdigest()
    save(receipt_path,receipt)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--only',nargs='+');parser.add_argument('--include-guid',nargs='+');args=parser.parse_args();build(args.only,args.include_guid)
