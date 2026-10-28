"""Port the complete original minimap camera/settings and Windows pointer hooks."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapCamera'
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    inputs=[];outputs=[]
    def emit(name,text):
        path=DEST/name;write(path,text.encode());meta(path,'MonoImporter' if path.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(path))
    def source(path):inputs.append(record(path));return path.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def wrap(text):
        for line in ['using Common;','using Framework;','using Framework.DependencyInjection;','using Framework.Logging;','using Framework.Logging.InterpolatedStringHandler;']:text=text.replace(line+'\n','')
        at=text.index('public ')
        # Keep attributes attached to the class inside the migrated namespace.
        attributes=text.rfind('\n[',0,at)
        if attributes>=0:at=attributes+1
        return text[:at]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.World.NativeTerrain.Common;\n'+text[at:]+'\n}\n'
    text=source(CODE.parent/'common-source/MiniMapSettings.cs').replace('[ScriptableObjectConfig]\n','')
    text=text.replace('JsonUtils.ToJson(tileSetting.MapClassIds)','("[" + string.Join(",", tileSetting.MapClassIds) + "]")')
    text=text.replace('GLoggerErrorInterpolatedStringHandler','MiniMapSettingsErrorHandler').replace('Log.Error(','MiniMapSettingsDiagnostic.Error(')
    emit('MiniMapSettings.cs',wrap(text))
    diagnostic=source(ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMap/MiniMapDiagnostic.cs')
    diagnostic=diagnostic.replace('GLoggerErrorInterpolatedStringHandler','MiniMapSettingsErrorHandler').replace('class Log','class MiniMapSettingsDiagnostic')
    emit('MiniMapSettingsDiagnostic.cs',diagnostic)
    text=source(CODE/'MiniMapCameraController.cs')
    text=text.replace('\t[Inject]\n\tprivate IScriptableObjectConfig<MiniMapSettings> mMiniMapSettings { get; set; }','\tpublic MiniMapSettings Settings { get; set; }').replace('mMiniMapSettings.Value.','Settings.')
    text=text.replace('UiCamera.TryGetCullingParameters(ref val3)','UiCamera.TryGetCullingParameters(out val3)').replace('mMiniMapCamera.TryGetCullingParameters(ref val3)','mMiniMapCamera.TryGetCullingParameters(out val3)')
    text=text.replace('RenderTargetIdentifier.op_Implicit((Texture)(object)renderTexture)','new RenderTargetIdentifier(renderTexture)')
    emit('MiniMapCameraController.cs',wrap(text))
    emit('IFullScrMiniMapStateListener.cs',wrap(source(CODE/'IFullScrMiniMapStateListener.cs')))
    text=source(CODE/'LineRendererDashedLine.cs')
    text=text.replace('Color32.op_Implicit(', '(Color32)(').replace('Vector4.op_Implicit(', '(Vector4)(')
    emit('LineRendererDashedLine.cs',wrap(text))
    # The original controller consumes click/drag/zoom events, while Windows
    # supplies UGUI pointer events and wheel deltas in place of mobile pinch.
    source(CODE/'UITouchInput.cs')
    emit('UITouchInput.cs','''using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.World.Map13MiniMap
{
    [DefaultExecutionOrder(-999)]
    public sealed class UITouchInput : DragControllerBase,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IScrollHandler
    {
        private bool mIsDragging;
        public event Action<Vector3> OnClick;
        public event Action<float,Vector3> OnZoom;
        public void OnPointerClick(PointerEventData eventData)
        {
            if(mIsDragging)mIsDragging=false;
            else OnClick?.Invoke(eventData.position);
        }
        public void OnBeginDrag(PointerEventData eventData){mIsDragging=true;OnDragBegin();}
        public void OnDrag(PointerEventData eventData){base.OnDrag();}
        public void OnEndDrag(PointerEventData eventData){OnDragEnd();}
        public void OnScroll(PointerEventData eventData){OnZoomBegin();OnZoom?.Invoke(-eventData.scrollDelta.y,eventData.position);}
    }
}
''')
    emit('Zjcs.Official.World.Map13MiniMapCamera.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapCamera',references=['Zjcs.Official.World.NativeTerrain','Zjcs.Official.WorldUI','Unity.ugui','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime'],autoReferenced=True)).decode())
    meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-camera-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),operation='Copy the complete original MiniMapCameraController, full MiniMapSettings lookup/cache/tile-selection methods and IFullScrMiniMapStateListener. Preserve camera render-target quality calculation, render-scale multiplication, truncation, zoom clamp/order/events, culling, sort/shader pass selection and render-target lifecycle. Repair decompiler IL cast/out syntax; replace only injected settings access and logging backend. Adapt original UITouchInput pointer hooks to Windows UGUI drag/click/wheel events, reusing the migrated original DragControllerBase physics without changing UI layout.',limitations=['Camera/settings code only. Original selected MiniMapSettings asset and full-screen prefab bindings, original root/managers, formal input/save and Windows Player remain required.'])))
    print('Original minimap camera/settings migrated:',len(outputs),'files')


if __name__=='__main__':build()
