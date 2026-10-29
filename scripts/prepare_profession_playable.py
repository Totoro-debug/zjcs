"""Convert the original map promotion config and its referenced MyPlayable."""
import collections
import hashlib
import json
import math
from prepare_map_regions import Regions,ROOT,ART,REF,save
from map_timeline_assets import TimelineAssets,pointer,identity
from prepare_story_playables import convert_playable
from official_resource_manifest import write_resources


def player_hierarchy(e):
    root=e.apk.asset('defaultpackage_assets_prefabs_ecentity_mapworld_worldmapcharacter.bundle','/3001.prefab')
    tree={};paths={};agents=[]
    def visit(obj,parent=''):
        e.input(obj);go=obj.read();path=parent+'/'+go.m_Name
        cs=[c.component.deref() for c in go.m_Component]
        transform=next(c for c in cs if c.type.name in ['Transform','RectTransform']);e.input(transform)
        t=transform.read();q=t.m_LocalRotation
        # The root's serialized map placement is replaced by the local player spawn.
        tree[path]=dict(position=dict(x=t.m_LocalPosition.x if parent else 0,y=t.m_LocalPosition.y if parent else 0,z=t.m_LocalPosition.z if parent else 0),
            quaternion=dict(x=q.x,y=q.y,z=q.z,w=q.w),rotation=math.atan2(2*(q.w*q.z+q.x*q.y),1-2*(q.y*q.y+q.z*q.z)),
            scale=dict(x=t.m_LocalScale.x,y=t.m_LocalScale.y,z=t.m_LocalScale.z),active=bool(go.m_IsActive))
        paths[identity(obj)]=path
        for c in cs:
            paths[identity(c)]=path
            if c.type.name=='MonoBehaviour' and e.mono(c)[0]=='AttachAgent':agents.append((path,c))
        for child in t.m_Children:visit(child.read().m_GameObject.deref(),path)
    visit(root)
    for path,obj in agents:
        e.input(obj);cfg=e.schemas.read(obj);bones={}
        for name,ref in zip(cfg['myBoneNameList'],cfg['myBoneList']):
            target=pointer(ref,obj)
            if target is not None:bones[name]=paths[identity(target)]
        tree[path]['attach_bones']=bones
    if not any('IntEffectSlot' in node.get('attach_bones',{}) for node in tree.values()):raise ValueError('Original player effect slot absent')
    return dict(scene=tree)


def build():
    e=Regions()
    obj=e.apk.asset('defaultpackage_assets_scriptableconfig_modules_mapcommon.bundle','/PlayerWorldProfessionUpgradeConfig.asset');e.input(obj)
    settings=e.schemas.read(obj);root=pointer(settings.pop('Playable'),obj);e.input(root)
    row=dict(bundle=e.apk.provenance(root)['bundle'],path='Assets/ScriptableConfig/Modules/MapCommon/PlayerWorldProfessionUpgradeConfig.asset',embedded_game_object=root.read().m_Name,embedded_path_id=str(root.path_id))
    assets=TimelineAssets(e);assets.folder=ART/'profession-upgrade';assets.folder.mkdir(exist_ok=True)
    counts=collections.Counter()
    scene=convert_playable(e,assets,'profession-upgrade',row,root,counts)
    from official_particle_codec import effects
    scene['emitters']=effects(e,root,include_inactive=True)
    scene['sequences']=[]
    if e.emitter_failures:raise ValueError(e.emitter_failures)
    from profession_upgrade_audio import convert
    audio=convert({t['config']['SoundEvent']['WwiseObjectReference']['event'] for t in scene['tracks'] if t['type']=='MyPlayableSound'},assets.folder/'audio')
    e.records.update(audio['resources'])
    save(ART/'profession-upgrade.json',dict(settings=settings,scene=scene,player=player_hierarchy(e),skeletons=assets.skeletons,audio=audio['events']))
    sources=['client-source/'+name+'.cs' for name in ['PlayerWorldProfessionUpgradeModule','PlayerWorldProfessionUpgradeConfig','ProfessionPromotionPanel','ProfessionChangePanel','GlobalStoryPlayEntry','StoryPlayManager']]
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    save(REF/'profession-upgrade-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,resources=e.records,
        schemas=e.schemas.records,unresolved=assets.unresolved,components=dict(counts),
        unconverted_modules=sorted({m for emitter in scene['emitters'] for m in emitter['unconverted_modules']}),
        audio_inputs=audio['inputs'],
        code={n:sha(ROOT/'.tools/source-packages/official-code'/n) for n in sources},
        outputs={'profession-upgrade.json':sha(ART/'profession-upgrade.json'),**{p.relative_to(ART).as_posix():sha(p) for p in assets.folder.iterdir() if p.suffix in ['.geom','.json']}},
        operation='Original PlayerWorldProfessionUpgradeConfig PPtr; original MyPlayable tracks, transforms, time keys, animation and geometry; no substitute effects'))
    central=ROOT/'assets/reference/official-city/source-manifest.json';manifest=json.loads(central.read_text(encoding='utf8'));manifest['resources'].update(e.records)
    manifest['data_sha256']['profession-upgrade.json']=sha(ART/'profession-upgrade.json')
    resources=manifest.setdefault('explicit_data_resources',[])
    if 'profession-upgrade.json' not in resources:resources.append('profession-upgrade.json')
    write_resources(manifest);save(central,manifest)
    print('Original profession upgrade',settings,dict(counts),'unresolved',assets.unresolved,flush=True)


if __name__=='__main__':build()
