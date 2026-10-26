"""Convert original Mecanim controllers used by story entity performances."""
import hashlib,json,math,traceback
from prepare_map_regions import Regions,ART,REF,ROOT,save
from map_timeline_assets import TimelineAssets,identity,pointer
from official_manifest import read_asset_manifest

def build():
    adventure=json.loads((ART/'adventure.json').read_text(encoding='utf8'));tables=adventure['tables']
    tags={r['Tag']:r for r in tables['Map/map_logic_entity_tag']}
    entities={tags[n['Tag']]['EntityClass'] for s in adventure['stories'].values() if any(n['_type']=='PlayPlayableNode' for n in s['nodes'].values()) for n in s['nodes'].values() if n['_type'] in ['GetEntityNode','GetNearestEntityNode'] and n.get('Tag') in tags}
    entities.update(int(n['EntityClassId']) for s in adventure['stories'].values() for n in s['nodes'].values() if n['_type'] in ['AddMapNpcNode','AddMapNpcAtPosNode','AddMapExclusiveNpcAroundPlayerNode','AddMapExclusiveNpcAtPosNode'] and int(n.get('EntityClassId',0))>0)
    paths={r['DisplayPath'] for r in tables['Map/entity_map_display'] if r['EntityClassId'] in entities and r['DisplayPath']}
    paths.update(r['FigureModel'] for r in json.loads((ART/'rules.json').read_text(encoding='utf8'))['npc'] if r['Id'] in entities and r['FigureModel'])
    legacy=json.loads((ART/'map-legacy-displays.json').read_text(encoding='utf8'))
    paths.update(path for group in legacy.values() for id,path in group.items() if int(id) in entities)
    # The map's display table is keyed by aliases as well as the concrete NPC
    # classes referenced by nodes. Retain every already converted native actor.
    previous_path=ART/'map-story-actors.json'
    if previous_path.exists():paths.update(json.loads(previous_path.read_text(encoding='utf8')).get('scenes',{}))
    previous_receipt=REF/'story-actors-source-manifest.json'
    if previous_receipt.exists():paths.update(json.loads(previous_receipt.read_text(encoding='utf8')).get('failures',{}))
    e=Regions();assets=TimelineAssets(e);assets.folder=ART/'map-story-actors';assets.folder.mkdir(exist_ok=True)
    native={r['path']:r for name in e.apk.archive.namelist() if name.startswith('assets/yoo/') and name.endswith('_854_156383.v3.bytes') for r in read_asset_manifest(e.apk.archive.read(name))}
    output={};failures={}
    for path in sorted(paths):
        full='Assets/Prefabs/'+path+'.prefab';row=native.get(full)
        if not row:failures[path]='Original AssetTable path unavailable';continue
        try:
            root=e.apk.asset(row['bundle'],'/'+full.rsplit('/',1)[-1]);tree={};animators=[];agents=[];object_paths={};e.active_overrides={}
            def visit(obj,parent=''):
                e.input(obj);go=obj.read();path=parent+'/'+go.m_Name
                cs=[c.component.deref() for c in go.m_Component];t=next(c.read() for c in cs if c.type.name in ['Transform','RectTransform']);q=t.m_LocalRotation
                tree[path]=dict(position=dict(x=t.m_LocalPosition.x,y=t.m_LocalPosition.y,z=t.m_LocalPosition.z),quaternion=dict(x=q.x,y=q.y,z=q.z,w=q.w),rotation=math.atan2(2*(q.w*q.z+q.x*q.y),1-2*(q.y*q.y+q.z*q.z)),scale=dict(x=t.m_LocalScale.x,y=t.m_LocalScale.y,z=t.m_LocalScale.z),active=bool(go.m_IsActive))
                e.active_overrides[identity(obj)]=True
                object_paths[identity(obj)]=path
                for c in cs:
                    object_paths[identity(c)]=path
                    if c.type.name=='MonoBehaviour' and c.read(check_read=False).m_Script.read().m_ClassName=='AttachAgent':agents.append((path,c))
                animators.extend((path,c) for c in cs if c.type.name=='Animator')
                for child in t.m_Children:visit(child.read().m_GameObject.deref(),path)
            visit(root)
            for binding,obj in agents:
                e.input(obj);cfg=e.schemas.read(obj);bones={}
                for name,ref in zip(cfg['myBoneNameList'],cfg['myBoneList']):
                    target=pointer(ref,obj)
                    if target is not None and identity(target) in object_paths:bones[name]=object_paths[identity(target)]
                tree[binding]['attach_bones']=bones
            guid=hashlib.sha256(full.encode()).hexdigest()[:24];visuals=assets.scene_geometry(guid,row,tree)
            for binding,obj in animators:tree[binding]['animator']=assets.animator(obj,binding,tree,visuals)
            assets.convert_skeletons();output[path]=dict(asset=row,scene=tree,visuals=visuals,sequences=[])
            print('Story actor',len(output),'/',len(paths),path,flush=True)
        except Exception as ex:traceback.print_exc();failures[path]=str(ex)
    save(ART/'map-story-actors.json',dict(scenes=output,skeletons=assets.skeletons))
    save(REF/'story-actors-source-manifest.json',dict(manifest='854_156383',entities=sorted(entities),converted=len(output),failures=failures,inputs=e.source_records,resources=e.records,schemas=e.schemas.records,unresolved=assets.unresolved,outputs={'map-story-actors.json':hashlib.sha256((ART/'map-story-actors.json').read_bytes()).hexdigest(),**{p.relative_to(ART).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in assets.folder.iterdir() if p.suffix in ['.geom','.json']}},operation='Original story actor transforms, geometry, Animator parameters and transitions, native animation curves and original Spine animation frames.'))
    print('Story actors',len(output),'failures',failures,flush=True)

if __name__=='__main__':build()
