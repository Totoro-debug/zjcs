"""Migrate the complete original minimap click and camera-frame path."""
import hashlib,json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import write,encode,sha,meta
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_minimap_mark_core import method

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkClick'

def build():
    inputs=[];outputs=[]
    def read(relative):
        p=SOURCE/relative;inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());m=Path(str(p)+'.meta');guid=hashlib.md5(('zjcs-official:'+p.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()
        if m.exists() and not re.search(r'(?m)^guid: '+guid+r'\s*$',m.read_text(encoding='utf-8-sig')):raise ValueError('Unrelated click metadata')
        importer='AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter'
        write(m,('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode());outputs.append(dict(**record(p),meta_sha256=sha(m.read_bytes())))
    text=read('client-source/MiniMapMarkClick.cs')
    text=text.replace('using Common;\n','').replace('using Framework.DependencyInjection;\n','').replace('using MessagePipe;\n','')
    text=text.replace('public record struct OnClickEventData(Pos2d TargetPos);',EVENT)
    text=text.replace('public MiniMap MiniMap;','public MiniMapManagedContext MiniMap;')
    text=text.replace('[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }','private MiniMapManagedContext MapRootManagerProxy => MiniMap;')
    for old,new in [('IMapInputSupportHandlerProxy mMapInput','IMiniMapClickInput mMapInput'),('ICameraControlManager CameraControlManager','IMiniMapClickCameraControl CameraControlManager'),('IPublisher<OnClickEventData> OnClickEventPublisher','IMiniMapClickPublisher OnClickEventPublisher')]:
        text=text.replace('[Inject]\n\tprivate '+old+' { get; set; }','public '+new+' { get; set; }')
    text=text.replace('MapRootManagerProxy.MyGridVisibilityAgent','((IMiniMapClickContext)MiniMap).MyGridVisibilityAgent')
    text=text.replace('MapUtils.MapPosToGrid(MiniMap.MapClassId, val)','MiniMapClickRules.MapPosToGrid(MiniMap, MiniMap.MapClassId, val)').replace('BroadcastManager.ShowErrText(num, null, false)','MiniMap.ShowError(num)').replace('), ref val, float.MaxValue', '), out val, float.MaxValue')
    at=text.index('[RequireComponent');text=text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using MapUIBase=Zjcs.Official.World.NativeMap13Labels.MapUIBase;
using Zjcs.Official.World.NativeTerrain.Common;
'''+text[at:]+'\n}\n';emit('MiniMapMarkClick.cs',text)
    parser=read('common-source/Common/MapRootEntityLiteInfoPackParser.cs');helper=method(parser,'public Pos2d MapPosToGrid(int mapClassId, Vector3 pos)').replace('public Pos2d MapPosToGrid(','public static Pos2d MapPosToGrid(MiniMapMaskContext context, ').replace('TryGetMapLiteData(','context.TryGetMapLiteData(')
    emit('MiniMapClickPorts.cs','''using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapClickContext { Object MyGridVisibilityAgent { get; } }
    public interface IMiniMapClickInput { int CanTeamClickPosCheck(int map,Pos2d position); }
    public interface IMiniMapClickCameraBrain { void SetViewPos(Vector3 position); }
    public interface IMiniMapClickCameraControl { IMiniMapClickCameraBrain ControlBrain { get; } }
    public interface IMiniMapClickPublisher { void Publish(MiniMapMarkClick.OnClickEventData value); }
    public static class MiniMapClickRules
    {
'''+helper+'\n}\n}\n')
    assembly=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkManagers/Zjcs.Official.World.Map13MiniMapMarkManagers.asmdef';inputs.append(record(assembly));refs=list(dict.fromkeys(['Zjcs.Official.World.Map13MiniMapMarkManagers','Zjcs.Official.World.Map13MiniMap']+json.loads(assembly.read_bytes())['references']))
    emit('Zjcs.Official.World.Map13MiniMapMarkClick.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarkClick',references=refs,autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official/World'/p) for p in ['NativeMap13Labels/MapUIBase.cs','Map13MiniMapCamera/UITouchInput.cs','Map13MiniMapCamera/MiniMapCameraController.cs','Map13MiniMapMarkManagers/MiniMapMarkManager.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-click-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy the complete original minimap click component, including all mark manager lookup order, click publication before query, interactive/team error branches, actual 3D raycast collider-transform position, main-camera viewport fallback, minimap transform subtraction, original frame size and XY/Z preservation, visibility-agent guard and full-screen input subscription. Copy the complete original map-position parser method; replace injected services with explicit local ports, adapt record struct generated members for Unity C# and repair decompiled Raycast out syntax.',limitations=['Original frame subtree/root prefab binding, actual input/camera/session ports, native Play Mode mouse/render checks and formal map/save/Windows Player remain required.'])))
    print('Original minimap click:',len(outputs),'outputs')

EVENT='''public struct OnClickEventData : System.IEquatable<OnClickEventData>
    {
        public Pos2d TargetPos { get; set; }
        public OnClickEventData(Pos2d targetPos){TargetPos=targetPos;}
        public void Deconstruct(out Pos2d targetPos){targetPos=TargetPos;}
        public bool Equals(OnClickEventData other)=>System.Collections.Generic.EqualityComparer<Pos2d>.Default.Equals(TargetPos,other.TargetPos);
        public override bool Equals(object value)=>value is OnClickEventData other&&Equals(other);
        public override int GetHashCode()=>System.Collections.Generic.EqualityComparer<Pos2d>.Default.GetHashCode(TargetPos);
        public static bool operator ==(OnClickEventData left,OnClickEventData right)=>left.Equals(right);
        public static bool operator !=(OnClickEventData left,OnClickEventData right)=>!left.Equals(right);
        public override string ToString()=>"OnClickEventData { TargetPos = "+TargetPos.ToString()+" }";
    }'''

if __name__=='__main__':build()
