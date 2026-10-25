"""Recover battle renderer ordering and scene camera bindings from pinned official prefabs."""
from __future__ import annotations
import hashlib,json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
from official_apk import Apk,ROOT
from official_unity_schema import ScriptSchemas

OUTPUT=ROOT/'unity/Assets/Game/Resources/Combat/battle-scene-bindings.json'
RECEIPT=ROOT/'assets/reference/battle-scenes/layout-source-manifest.json'

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def build():
 apk=Apk();schemas=ScriptSchemas();inputs=[]
 def record(o):
  r=apk.provenance(o);r['sha256']=hashlib.sha256(o.get_raw_data()).hexdigest();inputs.append(r)
 apk.load_builtin('assets/bin/Data/data.unity3d')
 tag=next(o for o in apk.env.objects if o.type.name=='TagManager')
 layers=tag.read_typetree()['m_SortingLayers'];record(tag)
 identities_path=ROOT/'unity/Assets/Official/Combat/Presentation/model-identities.json'
 identities=json.loads(identities_path.read_bytes())
 units=[]
 for identity in [3100]+sorted({int(x['monster_id']) for x in identities['monster_models']}):
  kind='fightcharacter' if identity==3100 else 'fightmonster'
  objects=apk.objects(f'prefabs_ecentity_fight_{kind}_{identity}')
  skeleton_gos=set()
  for o in objects:
   if o.type.name=='MonoBehaviour':
    d=o.read(check_read=False)
    if d.m_Script.read().m_ClassName in ['SkeletonAnimation','SkeletonMecanim']:
     skeleton_gos.add(d.m_GameObject.path_id)
  roots=[ptr.read() for path,ptr in apk.container(f'prefabs_ecentity_fight_{kind}_{identity}') if path.endswith('/'+str(identity)+'.prefab') and ptr.deref().type.name=='GameObject']
  if len(roots)!=1:raise ValueError('Expected one root prefab for '+str(identity))
  root_transform=roots[0].m_Component[0].component.path_id
  model=[];hud=[]
  for o in objects:
   if o.type.name!='MeshRenderer':continue
   d=o.read();go=d.m_GameObject.read();raw=o.read_typetree()
   r={'name':go.m_Name,'sortingLayer':int(raw['m_SortingLayer']),'sortingLayerId':int(raw['m_SortingLayerID']),'sortingOrder':int(raw['m_SortingOrder'])}
   if d.m_GameObject.path_id in skeleton_gos:
    transforms=[];t=go.m_Component[0].component.read()
    while t.object_reader.path_id!=root_transform:
     pos=t.m_LocalPosition;rot=t.m_LocalRotation;scale=t.m_LocalScale
     transforms.insert(0,{'name':t.m_GameObject.read().m_Name,'position':[pos.x,pos.y,pos.z],'rotation':[rot.x,rot.y,rot.z,rot.w],'scale':[scale.x,scale.y,scale.z]})
     record(t.object_reader)
     if t.m_Father.path_id==0:raise ValueError('Model not below original prefab root: '+str(identity))
     t=t.m_Father.read()
    r['transforms']=transforms
    model.append(r);record(o)
   elif go.m_Name in ['HpBar','HpSubBar','HpAddBar','ShieldAddBar','ResilienceSlider','HpBg','HpBarBg']:
    hud.append(r);record(o)
  if len(model)!=1:raise ValueError(f'Expected one official skeletal renderer for {identity}: {model}')
  units.append({'entityId':identity,'model':model[0],'bars':hud})
 cameras=[]
 scene_catalog_path=ROOT/'unity/Assets/Official/Combat/Scenes/Resources/BattleScenes/catalog.json'
 scene_catalog=json.loads(scene_catalog_path.read_bytes())
 scene_cameras={}
 for scene in scene_catalog['scenes']:
  name=scene['sceneName']
  if name not in scene_cameras:
   objects=apk.objects('prefabs_ecentity_fight_fightstage_'+name.removeprefix('game_'))
   main=[o for o in objects if o.type.name=='Camera' and o.read().m_GameObject.read().m_Name=='Main Camera']
   if len(main)!=1:raise ValueError('Expected one Main Camera in '+name+': '+str(len(main)))
   camera=main[0];d=camera.read();t=d.m_GameObject.read().m_Component[0].component.read();record(camera)
   p=t.m_LocalPosition;record(t.object_reader)
   ctrls=[o for o in objects if o.type.name=='MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName=='GameCameraController']
   if len(ctrls)!=1:raise ValueError('Expected one GameCameraController in '+name)
   cfg=schemas.read(ctrls[0]);record(ctrls[0])
   if not d.orthographic:raise ValueError('Perspective battle camera needs its full native projection: '+name)
   scene_cameras[name]={'position':[p.x,p.y,p.z],'orthographicSize':float(d.orthographic_size),'defaultOrthographicSize':cfg['CameraDefaultOrthoSize'],'resetDistanceDamping':cfg['ResetDistanceDamping'],'yOffset':cfg['myYOffset']}
  cameras.append({'gameId':scene['gameId'],'sceneName':name,**scene_cameras[name]})
 data={'manifest':apk.version,'layers':layers,'units':units,'cameras':cameras}
 OUTPUT.parent.mkdir(parents=True,exist_ok=True);OUTPUT.write_bytes((json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8'))
 sidecar=OUTPUT.with_name('battle-scene-bindings-source-manifest.json')
 sidecar.write_bytes((json.dumps({'manifest':apk.version,'output_sha256':digest(OUTPUT)},separators=(',',':'))+'\n').encode('utf-8'))
 RECEIPT.parent.mkdir(parents=True,exist_ok=True)
 RECEIPT.write_text(json.dumps({'manifest':apk.version,'inputs':inputs,'identity_input':{'path':identities_path.relative_to(ROOT).as_posix(),'sha256':digest(identities_path)},'scene_catalog_input':{'path':scene_catalog_path.relative_to(ROOT).as_posix(),'sha256':digest(scene_catalog_path)},'output':OUTPUT.relative_to(ROOT).as_posix(),'output_sha256':digest(OUTPUT),'generator_sha256':digest(__file__),'schema_sha256':digest(ROOT/'scripts/unity_schema_types.cs'),'runtime_bindings':{path:digest(ROOT/path) for path in ['unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneBindings.cs','unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleEnvironmentView.cs','unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleNativeModelView.cs','unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.BattleScene.cs']},'operation':'Read original TagManager, model Transform chains, MeshRenderer sorting layers/orders, native Camera and strict GameCameraController fields. Preserve internal native draw order with a SortingGroup; express source layer index/order in the existing flattened scene range. Restore legacy scene export downward Y and PNG atlas V to Unity coordinates, retain exported source depth. Legacy mesh samples are repaired, not claimed as the completed native Spine migration. APK-first verified cloud fallback through Apk; no images synthesized.'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Official battle scene bindings:',len(units),'units,',len(cameras),'cameras')
if __name__=='__main__':build()
