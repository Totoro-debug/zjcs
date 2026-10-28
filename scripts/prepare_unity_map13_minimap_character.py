"""Port the complete original minimap player marker and its native render path."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapCharacter'
SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    inputs=[];outputs=[]
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    p=SOURCE/'MiniMapCharacterManager.cs';inputs.append(record(p));text=p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    text=text.replace('using Framework.DependencyInjection;\n','').replace('\t[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }','\tprivate MiniMapCharacterContext MapRootManagerProxy => (MiniMapCharacterContext)MiniMap;')
    text=text.replace('private MoveAgent mMoveAgent;','private UnityEngine.Object mMoveAgent;')
    text=text.replace('(Object)(object)MapRootManagerProxy.MapRoot != (Object)null','MapRootManagerProxy.HasMapRoot')
    text=text.replace('((Component)myMapController.MyEntity).GetComponent<MoveAgent>()','MapRootManagerProxy.GetMoveAgent(myMapController)').replace('myMapController.WorldPos','MapRootManagerProxy.GetCharacterWorldPosition(myMapController)')
    text=text.replace('UiCamera.TryGetCullingParameters(ref val3)','UiCamera.TryGetCullingParameters(out val3)').replace('RenderTargetIdentifier.op_Implicit((Texture)(object)GetOrAddRenderTexture())','new RenderTargetIdentifier(GetOrAddRenderTexture())')
    at=text.index('public class');emit('MiniMapCharacterManager.cs',text[:at]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object=UnityEngine.Object;\n'+text[at:]+'\n}\n')
    emit('MiniMapCharacterContext.cs','''using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Read-only ports for the original map/controller components. Their
    // lifecycle and world interpolation are owned by the formal map host.
    public abstract class MiniMapCharacterContext:MiniMapMaskContext
    {
        public abstract bool HasMapRoot { get; }
        public abstract Object GetMoveAgent(IMiniMapCharacterController controller);
        public abstract Vector3 GetCharacterWorldPosition(IMiniMapCharacterController controller);
    }
}
''')
    emit('Zjcs.Official.World.Map13MiniMapCharacter.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapCharacter',references=['Zjcs.Official.World.Map13MiniMapMasks','Zjcs.Official.World.Map13MiniMapDescriptions','Zjcs.Official.World.Map13MiniMapSizing','Zjcs.Official.World.Map13MiniMapRegions','Zjcs.Official.World.Map13MiniMapView','Zjcs.Official.World.Map13MiniMapCamera','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.EntityLifecycle','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime'],autoReferenced=True)).decode())
    meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official/World'/p) for p in ['Map13MiniMapMasks/MiniMapManagerBase.cs','Map13MiniMapRegions/MapMeCharacterUIBase.cs','Map13MiniMapView/MiniMapViewInputs.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-character-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy the complete original MiniMapCharacterManager including its URP render hook, shader tags, culling, sorting, command-buffer order, full-screen state, fixed render-target allocation/aspect, player local-position updates and camera recentering. Replace injected map and controller component reads with explicit read-only ports; repair only decompiled out/implicit cast syntax. Preserve original render-target lifetime and null/destroyed-object checks.',limitations=['Native original character marker/camera prefab bindings, actual local map/controller ports, Play Mode rendering/movement/full-screen checks, formal input/save and Windows Player remain required.'])))
    print('Complete original minimap character manager:',len(outputs),'files')


if __name__=='__main__':build()
