"""Port complete original private-region border/concave-corner geometry."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapBorders'
SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    inputs=[];outputs=[]
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    for name in ['PrivateRegionUtility','PrivateRegionExtendUtils']:
        p=SOURCE/(name+'.cs');inputs.append(record(p));text=p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
        for ns in ['Common','Framework.Logging','Framework.Logging.InterpolatedStringHandler']:text=text.replace('using '+ns+';\n','')
        text=text.replace('GLoggerDebugInterpolatedStringHandler','MiniMapBorderDebugHandler').replace('Log.Debug(','MiniMapBorderDiagnostic.Debug(')
        start=text.index('public ');emit(name+'.cs',text[:start]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Zjcs.Official.World.NativeTerrain.Common;\n'+text[start:]+'\n}\n')
    emit('MiniMapBorderDiagnostic.cs','''using System.Text;
namespace Zjcs.Official.World.Map13MiniMap
{
    internal struct MiniMapBorderDebugHandler
    {
        private StringBuilder text;
        public MiniMapBorderDebugHandler(int literals,int formatted,out bool enabled){text=new StringBuilder(literals);enabled=true;}
        public void AppendLiteral(string value)=>text.Append(value);
        public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null)=>text.Append(value);
        public override string ToString()=>text.ToString();
    }
    internal static class MiniMapBorderDiagnostic
    {
        public static void Debug(ref MiniMapBorderDebugHandler message,object context,string member,string file,int line)=>UnityEngine.Debug.Log(message.ToString());
    }
}
''')
    emit('Zjcs.Official.World.Map13MiniMapBorders.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapBorders',references=['Zjcs.Official.World.NativeTerrain'],autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official/World/NativeTerrain/Common'/n) for n in ['Pos2d.cs','Direction.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-borders-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy all 767 lines of the original PrivateRegionUtility and PrivateRegionExtendUtils. Preserve one-unit grid and half-cell vertices, boundary direction order, line/point traversal, preferred directions, polygon simplification and original loop guards, ray-crossing containment, offset edge intersections and concave-neighbor extension. Reuse original Pos2d/Direction. Change only namespace and logging backend.',limitations=['Region description/highlight/native root binding, formal map input/save and Windows Player remain required.'])))
    print('Complete original minimap border geometry migrated:',len(outputs),'files')


if __name__=='__main__':build()
