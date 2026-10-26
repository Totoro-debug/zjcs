"""Convert original MyPlayable story sequences and their attached visuals."""
import collections, hashlib, json, math, traceback, zipfile
from official_manifest import read_asset_manifest
from fetch_official_assets import APK
from prepare_map_regions import Regions, ART, REF, ROOT, save
from map_timeline_assets import TimelineAssets, pointer, identity


def convert_playable(e,assets,guid,row,root,counts):
    scene={};paths={};behaviours={};animators=[];apply={};extras=[];e.active_overrides={}
    def visit(obj,parent=''):
        e.input(obj);go=obj.read();path=parent+'/'+go.m_Name
        cs=[c.component.deref() for c in go.m_Component];t=next(c.read() for c in cs if c.type.name in ['Transform','RectTransform'])
        q=t.m_LocalRotation
        scene[path]=dict(position=dict(x=t.m_LocalPosition.x,y=t.m_LocalPosition.y,z=t.m_LocalPosition.z),quaternion=dict(x=q.x,y=q.y,z=q.z,w=q.w),rotation=math.atan2(2*(q.w*q.z+q.x*q.y),1-2*(q.y*q.y+q.z*q.z)),scale=dict(x=t.m_LocalScale.x,y=t.m_LocalScale.y,z=t.m_LocalScale.z),active=bool(go.m_IsActive))
        paths[(obj.assets_file.name,obj.path_id)]=path;e.active_overrides[identity(obj)]=True
        for c in cs:
            paths[(c.assets_file.name,c.path_id)]=path
            if c.type.name=='Animator':animators.append((path,c))
            if c.type.name=='MonoBehaviour':
                kind=c.read(check_read=False).m_Script.read().m_ClassName
                if kind.startswith(('MyPlayable','Apply')):
                    e.input(c);cfg=e.schemas.read(c);behaviours[identity(c)]=(kind,c,cfg,path)
                    if kind.startswith('Apply'):apply.setdefault(path,[]).append(dict(type=kind,config=cfg))
                if kind in ['CinemachineImpulseSource','SkeletonRendererFollower','AttachmentFollower']:
                    extras.append((path,kind,c))
        for child in t.m_Children:visit(child.read().m_GameObject.deref(),path)
    visit(root)
    for path,kind,obj in extras:
        e.input(obj);cfg=e.schemas.read(obj)
        scene[path]['impulse' if kind=='CinemachineImpulseSource' else 'spine_follower']=assets.payload({k:v for k,v in cfg.items() if k not in ['m_GameObject','m_Script','m_Enabled','m_Name']},obj,path,scene,paths)
        if kind=='CinemachineImpulseSource':
            signal=pointer(cfg['m_ImpulseDefinition']['m_RawSignal'],obj)
            if signal is not None:
                e.input(signal);scene[path]['impulse']['noise_profile']=e.schemas.read(signal)
    visuals=assets.scene_geometry(guid,row,scene,root=root)
    for path,obj in animators:scene[path]['animator']=assets.animator(obj,path,scene,visuals)
    roots=[v for v in behaviours.values() if v[0]=='MyPlayableSequence' and v[3].count('/')==1]
    if len(roots)!=1:raise ValueError('Expected one native root sequence')
    tracks=[]
    def flatten(value,start=0,ancestors=()):
        kind,obj,cfg,path=value
        if identity(obj) in ancestors:raise ValueError('Cyclic MyPlayable sequence')
        if kind=='MyPlayableSequence':
            ends=[]
            for item in cfg['myPlayables']:
                child=pointer(item['myPlayable'],obj)
                if child is not None:ends.append(flatten(behaviours[identity(child)],start+float(item['myTime']),ancestors+(identity(obj),)))
            return -1 if any(x<0 for x in ends) else max(ends+[start])
        duration=float(cfg.get('myDuration',cfg.get('mDuration',cfg.get('Duration',0))))
        if kind=='MyPlayableTimeLine':
            director=pointer(cfg.get('myDirector',{}),obj)
            if director is not None:
                e.input(director);director_data=director.read_typetree()
                timeline=pointer(director_data['m_PlayableAsset'],director)
                if timeline is not None:
                    e.input(timeline);timeline_data=e.schemas.read(timeline)
                    if int(timeline_data['m_DurationMode'])==1:duration=float(timeline_data['m_FixedDuration'])
                    else:
                        ends=[]
                        for track_pointer in timeline_data['m_Tracks']:
                            track=pointer(track_pointer,timeline)
                            if track is not None:
                                e.input(track);track_data=e.schemas.read(track)
                                ends.extend(float(c['m_Start'])+float(c['m_Duration']) for c in track_data.get('m_Clips',[]))
                        duration=max(ends+[0])
        config=assets.payload({k:v for k,v in cfg.items() if k not in ['m_GameObject','m_Script','m_Enabled','m_Name']},obj,path,scene,paths)
        tracks.append(dict(type=kind,path=path,start=start,duration=duration,speed=float(cfg.get('mySpeed',1)),config=config,apply=apply.get(path,[])))
        counts[kind]+=1
        return -1 if duration<0 else start+duration/max(float(cfg.get('mySpeed',1)),.000001)
    duration=flatten(roots[0]);assets.convert_skeletons()
    return dict(asset=row,scene=scene,visuals=visuals,tracks=tracks,duration=duration)


def build():
    stories=json.loads((ART/'adventure.json').read_text(encoding='utf8'))['stories']
    media=json.loads((ART/'map-story-media.json').read_text(encoding='utf8'))
    requested={p['mAssetGuid'] for s in stories.values() for n in s['nodes'].values() if n['_type']=='PlayPlayableNode' for p in n['PlayablesProxy'] if p['mAssetGuid']}
    e=Regions()
    register=e.apk.asset('defaultpackage_assets_scriptableconfig_modules_story.bundle','/StoryPlayableRegisterConfig.asset');e.input(register)
    registry={r['Key']:r['Value']['mProxyRes']['mAssetGuid'] for r in e.schemas.read(register)['StoryPlayableRegisterItems']['mSerializeBuffer']}
    def selections(value):
        if isinstance(value,dict):
            if 'CustomAssetProxy' in value and 'PreDefineKey' in value:
                guid=value['CustomAssetProxy']['mProxyRes']['mAssetGuid'] if int(value['Type'])==1 else registry.get(value['PreDefineKey'],'') if int(value['Type'])==2 else ''
                if guid:yield guid
            for child in value.values():yield from selections(child)
        elif isinstance(value,list):
            for child in value:yield from selections(child)
    requested.update(selections(stories))
    archive=zipfile.ZipFile(APK)
    for name in archive.namelist():
        if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes'):
            for row in read_asset_manifest(archive.read(name)):
                if row['guid'] in requested:media['assets'][row['guid']]=row
    assets=TimelineAssets(e);assets.folder=ART/'map-playables';assets.folder.mkdir(exist_ok=True)
    output={};failures={};counts=collections.Counter()
    for guid in sorted(requested):
        row=media['assets'].get(guid)
        if row is None:failures[guid]='GUID absent from original five-package AssetTable';continue
        try:
            root=e.apk.asset(row['bundle'],'/'+row['path'].rsplit('/',1)[-1])
            output[guid]=convert_playable(e,assets,guid,row,root,counts)
            print('Story playable',len(output),row['path'],flush=True)
        except Exception as ex:
            traceback.print_exc();failures[guid]=dict(asset=row,error=str(ex))
    save(ART/'map-playables.json',dict(scenes=output,skeletons=assets.skeletons,registry=registry))
    save(REF/'playables-source-manifest.json',dict(manifest='854_156383',requested=len(requested),registry=registry,converted=len(output),failures=failures,components=dict(counts),inputs=e.source_records,resources=e.records,schemas=e.schemas.records,unresolved=assets.unresolved,outputs={'map-playables.json':hashlib.sha256((ART/'map-playables.json').read_bytes()).hexdigest(),**{p.relative_to(ART).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in assets.folder.iterdir() if p.suffix in ['.geom','.json']}},schema_code_sha256=hashlib.sha256((ROOT/'scripts/unity_schema_types.cs').read_bytes()).hexdigest(),operation='Original MyPlayableSequence children, start times, durations, attachment and animator configuration, scene geometry and original native animation curves. Missing references remain explicit.'))
    print('Story playables',len(output),'failures',len(failures),dict(counts),flush=True)


if __name__=='__main__':build()
