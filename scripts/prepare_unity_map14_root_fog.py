"""Migrate the exact Map14 root fog controller, native inputs and GLES programs."""
import hashlib,json,re
from pathlib import Path
from official_apk import Apk,ROOT
from prepare_unity_bundle_inputs import collect,serialized_files
from prepare_unity_map13_water_shaders import translate
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta
from prepare_unity_map14_client_view_bindings import record
from prepare_unity_map14_root_inputs import BUNDLE,workspace

OFFICIAL=ROOT/'unity/Assets/Game/Runtime/Official/MapRootFog'
NATIVE=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14RootFog'
EDITOR=ROOT/'unity/Assets/Game/Editor/Map14/RootFog'
ASSETS=ROOT/'unity/Assets/Official/World/Map/14/RootFog'
RECEIPT=ROOT/'assets/reference/official-map/map14-root-fog-source-manifest.json'

def guid(p):return hashlib.md5(('zjcs-official:'+source_path(p).removeprefix('unity/')).encode()).hexdigest()

def asset_meta(p,kind):
    write(Path(str(p)+'.meta'),('fileFormatVersion: 2\nguid: '+guid(p)+'\n'+kind+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())

def build():
    cache=workspace();export=cache/'export/ExportedProject/Assets'
    proof=json.loads((cache/'export-source-manifest.json').read_bytes())
    input_receipt=ROOT/'assets/reference/official-map/map14-native-root-input-source-manifest.json'
    if sha(input_receipt.read_bytes())!=proof['input_receipt']['sha256']:raise ValueError('Reverify the original root export first')
    inputs=[CODE/'client-source/CloudController2.cs',CODE/'common-source/Common/GridGroundComponent.cs',CODE/'common-source/Common/GridGroundComponentInfo.cs',CODE/'common-source/Common/GridVisibilityAgentComponent.cs',input_receipt]
    outputs=[]
    text=inputs[0].read_text(encoding='utf-8-sig')
    text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','',text)
    text=text.replace('using Common;','using Zjcs.Official.World.NativeTerrain.Common;\nusing Object = UnityEngine.Object;').replace('using GameFramework;','')
    text=text.replace('[DefaultExecutionOrder(101)]','namespace Zjcs.Official.MapRootFog\n{\n[DefaultExecutionOrder(101)]')+'\n}\n'
    text=text.replace('GridGroundComponent','FogGroundData').replace('GridVisibilityAgentComponent','FogVisibilityData')
    text=text.replace('public SpriteRenderer CloudTemplate;','public IRootFogPlatform Platform {get;set;}\n\n\tpublic SpriteRenderer CloudTemplate;')
    text=text.replace('MapCameraManager.Instance.MapCamera','Platform.MapCamera')
    text=text.replace('MySceneManager.Instance.ActiveScene.SceneType != SceneType.Map','!Platform.IsMapScene')
    text=text.replace('Vector2.op_Implicit(mGridGround.GridToPos(playerPos.X, playerPos.Y))','(Vector2)mGridGround.GridToPos(playerPos.X, playerPos.Y)')
    text=text.replace('RenderTargetIdentifier.op_Implicit((Texture)(object)mRenderTexture)','new RenderTargetIdentifier((Texture)mRenderTexture)')
    write(OFFICIAL/'CloudController2.cs',('// Complete original application class; platform services and consumed native types only.\n'+text).encode())
    write(OFFICIAL/'RootFogInputs.cs',b'''using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.MapRootFog
{
 public interface IRootFogPlatform {Camera MapCamera {get;} bool IsMapScene {get;}}
 public abstract class FogGroundInfo {public abstract Pos2d Size {get;} public abstract Vector3 OriginPos {get;}}
 public abstract class FogGroundData
 {
  public abstract object Original {get;} public abstract FogGroundInfo Info {get;}
  public abstract Vector3 GridToPos(float x,float y);
  public abstract Vector3 GridToPos(Pos2d pos);
  public abstract Pos2d PosToGrid(Vector3 pos);
 }
 public abstract class FogVisibilityData {public abstract object Original {get;} public abstract bool GetPosVisibility(int x,int y);}
}
''')
    write(NATIVE/'NativeMap14RootFogInputs.cs',b'''using System;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Offline.World.NativeMap14RootFog
{
 public sealed class NativeMap14RootFogInputs
 {
  public FogGroundData Ground {get;} public FogVisibilityData Visibility {get;}
  public NativeMap14RootFogInputs(NativeMap14SharedAssemblies shared,object root)
  {
   if(shared==null||root==null)throw new ArgumentNullException();
   var get=root.GetType().GetMethod("GetComponent",new[]{typeof(Type)});
   Ground=new GroundInput(get.Invoke(root,new object[]{shared.CommonType("GridGroundComponent")}));
   Visibility=new VisibilityInput(get.Invoke(root,new object[]{shared.CommonType("GridVisibilityAgentComponent")}));
  }
  private static object Get(object value,string name)=>NativeMap14SharedPrefabs.Get(value,name);
  private static Pos2d Position(object value)=>new Pos2d((int)Get(value,"X"),(int)Get(value,"Y"));
  private sealed class GroundInfo:FogGroundInfo
  {
   private readonly object ground;public GroundInfo(object value){ground=value;}
   private object Info=>Get(ground,"_Info");
   public override Pos2d Size=>Position(Get(Info,"Size"));
   public override Vector3 OriginPos=>(Vector3)Get(Info,"OriginPos");
  }
  private sealed class GroundInput:FogGroundData
  {
   public override object Original {get;} public override FogGroundInfo Info {get;}
   private readonly MethodInfo grid,position,toGrid;
   public GroundInput(object value)
   {
    Original=value??throw new ArgumentException("Original root ground missing");Info=new GroundInfo(value);
    grid=value.GetType().GetMethod("GridToPos",new[]{typeof(float),typeof(float)});
    position=Array.Find(value.GetType().GetMethods(),m=>m.Name=="GridToPos"&&m.GetParameters().Length==1);
    toGrid=value.GetType().GetMethod("PosToGrid",new[]{typeof(Vector3)});
   }
   public override Vector3 GridToPos(float x,float y)=>(Vector3)grid.Invoke(Original,new object[]{x,y});
   public override Vector3 GridToPos(Pos2d pos)=>(Vector3)position.Invoke(Original,new[]{Activator.CreateInstance(position.GetParameters()[0].ParameterType,pos.X,pos.Y)});
   public override Pos2d PosToGrid(Vector3 pos)=>Position(toGrid.Invoke(Original,new object[]{pos}));
  }
  private sealed class VisibilityInput:FogVisibilityData
  {
   public override object Original {get;}private readonly MethodInfo visible;
   public VisibilityInput(object value){Original=value??throw new ArgumentException("Original root visibility missing");visible=value.GetType().GetMethod("GetPosVisibility",new[]{typeof(int),typeof(int)});}
   public override bool GetPosVisibility(int x,int y)=>(bool)visible.Invoke(Original,new object[]{x,y});
  }
 }
}
''')
    refs=['Zjcs.Official.World.NativeTerrain','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime']
    for folder,name,references,platforms in (
        (OFFICIAL,'Zjcs.Official.MapRootFog',refs,[]),
        (NATIVE,'Zjcs.Offline.World.NativeMap14RootFog',['Zjcs.Official.MapRootFog','Zjcs.Official.World.NativeTerrain','Zjcs.Offline.World.NativeMap14SharedEntities'],[]),
        (EDITOR,'Zjcs.Editor.Map14.RootFog',['Zjcs.Official.MapRootFog','Zjcs.Official.World.NativeTerrain','Zjcs.Offline.World.NativeMap14RootFog','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Editor.Map14.RootComponents'],['Editor'])):
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
        for p in folder.iterdir():
            if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    apk=Apk.current();assets,missing=collect(apk,[BUNDLE])
    if missing:raise ValueError('Original fog closure incomplete')
    shader_sources=[]
    for bundle in apk.loaded.values():
        for f in serialized_files(bundle):
            for obj in f.objects.values():
                if obj.type.name!='Shader':continue
                shader=obj.read();name=shader.m_ParsedForm.m_Name
                if name not in ('Unlit/Cloud','Unlit/CloudMask','Unlit/CloudDisappear'):continue
                output=ASSETS/'Shaders/Cloud'/(name.split('/')[-1]+'.shader')
                text,programs=translate(shader,name.split('/')[-1]);text=text.replace('Zjcs/Official/World/Map13/','Zjcs/Official/World/Map14/RootFog/')
                write(output,text.encode());asset_meta(output,'ShaderImporter');outputs.extend((output,Path(str(output)+'.meta')))
                shader_sources.append(dict(name=name,source=apk.provenance(obj),raw_sha256=sha(obj.get_raw_data()),programs=[dict(type=p['type'],keywords=p['keywords'],source_sha256=sha(p['source'].encode())) for p in programs]))
    if len(shader_sources)!=3:raise ValueError('Original root fog programs incomplete')
    selected=['Textures/CloudShape02.png','Textures/CloudNoise02.png','Textures/Cloud/CloudMask.png','Textures/Cloud/CloudMask.asset','Materials/Cloud/Cloud.mat','Materials/Cloud/CloudMask.mat','Materials/Cloud/CloudDisappear.mat']
    mapping={}
    for relative in selected+['Shaders/Cloud/'+n+'.shader' for n in ('Cloud','CloudMask','CloudDisappear')]:
        p=export/relative;mapping[re.search(r'(?m)^guid: ([0-9a-f]{32})',Path(str(p)+'.meta').read_text(encoding='utf-8'))[1]]=guid(ASSETS/relative)
    script_guid=re.search(r'(?m)^guid: ([0-9a-f]{32})',Path(str(OFFICIAL/'CloudController2.cs')+'.meta').read_text(encoding='utf-8'))[1]
    mapping['c73f6104834143e48d2d08b9792af530']=script_guid
    def remap(text):return re.sub(r'(?<=guid: )[0-9a-f]{32}',lambda m:mapping.get(m[0],m[0]),text)
    for relative in selected:
        p=export/relative;target=ASSETS/relative
        if p.suffix=='.png':
            write(target,p.read_bytes());meta=Path(str(p)+'.meta').read_text(encoding='utf-8');meta=re.sub(r'(?m)^timeCreated:.*\n','',meta);meta=re.sub(r'(?m)^  assetBundleName:.*','  assetBundleName:',meta)
            write(Path(str(target)+'.meta'),remap(meta).encode())
        else:
            write(target,remap(p.read_text(encoding='utf-8')).encode());asset_meta(target,'NativeFormatImporter')
        outputs.extend((target,Path(str(target)+'.meta')))
    original=export/'Prefabs/EcEntity/Map/MapRoot/MapRoot_14/14.prefab'
    source=original.read_text(encoding='utf-8');blocks=re.split(r'(?m)(?=^--- !u!)',source)[1:]
    gos={'1618682377417806','1703437598541787','1884547391421755'}
    chosen=[block for block in blocks if (re.search(r'^--- !u!1 &(\d+)',block) and re.search(r'^--- !u!1 &(\d+)',block)[1] in gos) or any('m_GameObject: {fileID: '+go+'}' in block for go in gos)]
    if len(chosen)!=10:raise ValueError('Original root fog subtree changed: '+str(len(chosen)))
    text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(chosen)
    text=text.replace('m_Father: {fileID: 4977153420339851}','m_Father: {fileID: 0}')
    target=ASSETS/'Prefabs/RootFog.prefab';write(target,remap(text).encode());asset_meta(target,'PrefabImporter');outputs.extend((target,Path(str(target)+'.meta')))
    for folder in sorted({p.parent for p in outputs if p.is_relative_to(ASSETS)}|{ASSETS}):
        while folder.is_relative_to(ASSETS):
            runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
            folder=folder.parent
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original fog assembly version differs')
        assemblies.append(record(p))
    apk.archive.close()
    tools=[Path(__file__),ROOT/'scripts/prepare_unity_map13_water_shaders.py',ROOT/'scripts/official_shader_programs.py',ROOT/'scripts/prepare_battle_hud_materials.py']
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=assemblies,inputs=[record(p) for p in inputs],tools=[record(p) for p in tools],outputs=[record(p) for p in sorted(set(outputs))],shaders=shader_sources,
        operation='Copy the complete original CloudController2, changing namespace, decompiler operator syntax and the external camera/map-scene service boundary only. Ground/visibility inputs invoke unchanged original Console methods. Copy the original ten-object fog subtree, three materials, three textures and exact serialized mask Sprite; detach only its outer parent and rebind GUIDs/scripts. Convert each exact GLES program with original properties/pass states to Windows HLSL. No replacement fog algorithm or artwork.',
        validation_entry='Zjcs.Editor.Map14.OfficialMap14RootFogAcceptance.Run',validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile and Play Mode passed: exact native root/ground/visibility calls, original ten-object fog subtree/Sprite/materials and three supported Windows shaders, cell/boundary masks, generated cloud/mask/fading meshes, original reveal timing and expiration, add/remove, camera movement and map scene predicate. Rendered concealed/revealing/cleared frames inspected. Earlier root/container/display/statue/radar/input regressions passed. This controller fixture calls original controller methods; no pointer-driven formal map interaction is claimed. Full ProjectTools.Validate and Windows Player remain pending; owning map page records the root static result.',limitations=['MyGridVisibilityAgent event lifecycle, full root prefab and formal map/Player remain separate unfinished work.'])))
    print('Original root fog prepared:',len(set(outputs)),'outputs')

if __name__=='__main__':build()
