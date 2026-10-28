"""Port all original minimap zoom-dependent size and subscription behavior."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapSizing'
SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    inputs=[];outputs=[]
    def source(path):inputs.append(record(path));return path.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    for name in ['MiniMapKeepSizeHandler','MiniMapKeepSizeObj','MinimapKeepSizeObjHelper']:
        text=source(SOURCE/(name+'.cs'))
        for ns in ['Framework','Framework.Logging','Framework.Logging.InterpolatedStringHandler','R3']:text=text.replace('using '+ns+';\n','')
        text=text.replace('[Rename("开始放大的阈值")]','').replace('typeof(MiniMap)','typeof(MiniMapSizingContext)').replace('MiniMap mMinimap','MiniMapSizingContext mMinimap').replace('MiniMap miniMap','MiniMapSizingContext miniMap')
        text=text.replace('mMinimap.KeepSizeHandler','mMinimap.SizingHandler').replace('mMinimap.OnHideFullScreen','mMinimap.HideFullScreenAction')
        text=text.replace('DisposableBag','MiniMapSizingDisposableBag').replace('Disposable.AddTo<','MiniMapSizingDisposable.AddTo<').replace('GLoggerErrorInterpolatedStringHandler','MiniMapSizingErrorHandler').replace('Log.Error(','MiniMapSizingDiagnostic.Error(')
        start=text.index('public ');at=text.rfind('\n[',0,start)
        if at>=0:start=at+1
        text=text[:start]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object=UnityEngine.Object;\nusing Zjcs.Official.World.NativeMap13Labels;\n'+text[start:]+'\n}\n';emit(name+'.cs',text)
    emit('MiniMapSizingContext.cs','''using System;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Consumed original MiniMap surface. The concrete original root derives from
    // this component and supplies its actual handler and full-screen action.
    public abstract class MiniMapSizingContext:MiniMapRegionContext
    {
        public abstract MiniMapKeepSizeHandler SizingHandler{get;}
        public abstract GAction HideFullScreenAction{get;}
    }
    internal struct MiniMapSizingDisposableBag:IDisposable
    {
        private List<IDisposable> leases;
        public void Add(IDisposable lease){if(leases==null)leases=new List<IDisposable>();leases.Add(lease);}
        public void Dispose(){if(leases==null)return;foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
    internal static class MiniMapSizingDisposable
    {
        public static T AddTo<T>(T lease,ref MiniMapSizingDisposableBag bag) where T:IDisposable {bag.Add(lease);return lease;}
    }
}
''')
    text=source(ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMap/MiniMapDiagnostic.cs').replace('GLoggerErrorInterpolatedStringHandler','MiniMapSizingErrorHandler').replace('class Log','class MiniMapSizingDiagnostic');emit('MiniMapSizingDiagnostic.cs',text)
    emit('Zjcs.Official.World.Map13MiniMapSizing.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapSizing',references=['Zjcs.Official.World.Map13MiniMapRegions','Zjcs.Official.World.Map13MiniMapCamera','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.StoryPlayables'],autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official'/p) for p in ['World/Map13MiniMapRegions/MiniMapRegionContext.cs','World/NativeMap13Labels/UIBase.cs','World/NativeMap13Labels/GAction.cs','StoryPlayables/GAction.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-sizing-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy complete original MiniMapKeepSizeHandler, MiniMapKeepSizeObj and MinimapKeepSizeObjHelper. Preserve exact zoom-threshold formula, uniform replacement scale, original default-scale restoration on hide, strict ratio/threshold guards, event order, subscriptions and lifecycle. Reuse complete original UIBase and GAction classes. Qualify namespaces, remove editor-only Rename annotation and replace only consumed MiniMap root/DI and disposable/logging backends; the required context is the base of the actual original root.',limitations=['Original root and native prefab binding, actual formal minimap input/save and Windows Player remain required.'])))
    print('Complete original minimap size handlers migrated:',len(outputs),'files')


if __name__=='__main__':build()
