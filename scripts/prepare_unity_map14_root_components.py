"""Port the original thin map-root client components over unchanged native state."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta
from prepare_unity_map14_client_view_bindings import record

NAMES=dict(MyMap='MapComponent',MyWorld='WorldComponent',MyMapCharacterSpawner='MapCharacterSpawnerComponent',
           MyGridGround='GridGroundComponent',MyAreaAgent='AreaAgentComponent',MyEntityVisibilityAgent='EntityVisibilityAgentComponent',
           MyAreaExploreSpawner='AreaExploreSpawnerComponent',MyMapStoryEntitySpawner='MapStoryEntitySpawnerComponent')

def build():
    official=ROOT/'unity/Assets/Game/Runtime/Official/EntityRootComponents'
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14RootComponents'
    editor=ROOT/'unity/Assets/Game/Editor/Map14/RootComponents'
    inputs=[];outputs=[]
    for name in (*NAMES,'MyGridGroundBase'):
        p=CODE/'client-source'/(name+'.cs');inputs.append(p);s=p.read_text(encoding='utf-8-sig')
        s=s.replace('using Common;','using Zjcs.Official.EntityLifecycle;\nnamespace Zjcs.Official.EntityRootComponents\n{')+'\n}\n'
        for component in NAMES.values():
            s=re.sub(r'\b'+component+r'Info\b','object',s)
            s=re.sub(r'\b'+component+r'\b',component+'Data',s)
        s=s.replace(' where TGridGroundInfo : object','')
        write(official/(name+'.cs'),('// Complete original thin root client class; see Map14 root component receipt.\n'+s).encode())
    definitions=['using Zjcs.Official.EntityLifecycle;','namespace Zjcs.Official.EntityRootComponents','{',
                 'public interface IRootClientComponentData:IEntityComponentInfoSource { object Original {get;} }']
    for component in NAMES.values():
        parent='EntityGridGroundData, IRootClientComponentData' if component=='GridGroundComponent' else 'IRootClientComponentData'
        definitions.append('public abstract class '+component+'Data : '+parent+' { public abstract object Original {get;} public abstract object ComponentInfo {get;} }')
        p=CODE/'common-source/Common'/(component+'.cs');inputs.append(p)
        p=p.with_name(component+'Info.cs')
        if p.exists():inputs.append(p)
    definitions.append('}')
    write(official/'RootClientComponentData.cs','\n'.join(definitions).encode())
    code='''using System;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityRootComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14RootComponents
{
    // No shared values or rules are reconstructed. Each projection retains the
    // original component and returns the original live _Info object.
    public sealed class NativeMap14RootComponentBindings
    {
        private readonly NativeMap14SharedAssemblies shared;
        public NativeMap14RootComponentBindings(NativeMap14SharedAssemblies shared)
        {this.shared=shared??throw new ArgumentNullException(nameof(shared));}
        public void Bind(NativeMap14SharedViewEntity view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
'''
    for component in NAMES.values():
        code+='            Bind<'+component+'Data>(view,"'+component+'",value=>new '+component+'Projection(value));\n'
    code+='''            var ground=view.GetComponent<GridGroundComponentData>();
            if(ground!=null)view.BindAdditionalComponent<EntityGridGroundData>(ground);
        }
        private void Bind<T>(NativeMap14SharedViewEntity view,string name,Func<object,T> create) where T:class,IRootClientComponentData
        {
            var original=view.Original.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(view.Original,new object[]{shared.CommonType(name)});
            if(original==null)return;
            var previous=view.GetComponent<T>();
            if(previous!=null)
            {if(!ReferenceEquals(previous.Original,original))throw new InvalidOperationException("Original map root component identity differs.");return;}
            view.BindAdditionalComponent<T>(create(original));
        }
'''
    for component in NAMES.values():
        code+='        private sealed class '+component+'Projection:'+component+'Data\n        {\n            public override object Original {get;}\n            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");\n            public '+component+'Projection(object original){Original=original;}\n        }\n'
    code+='    }\n}\n'
    write(native/'NativeMap14RootComponentBindings.cs',code.encode())
    base=['Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityViews','Zjcs.Official.StoryPlayables']
    for folder,name,refs,platforms in (
        (official,'Zjcs.Official.EntityRootComponents',base,[]),
        (native,'Zjcs.Offline.World.NativeMap14RootComponents',base+['Zjcs.Official.EntityRootComponents','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'],[]),
        (editor,'Zjcs.Editor.Map14.RootComponents',base+['Zjcs.Official.EntityRootComponents','Zjcs.Official.EntityContainers','Zjcs.Official.EntityLoading','Zjcs.Official.World.Map14DisplayTags','Zjcs.Offline.World.NativeMap14RootComponents','Zjcs.Offline.World.NativeMap14ClientViewBindings','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Offline.World.NativeMap14DisplayData','Zjcs.Editor.Map14.BufferedEvents'],['Editor'])):
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=refs,includePlatforms=platforms)))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
        for p in folder.iterdir():
            if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    # Extend the common composition before any original MyComponent.Bind runs.
    from prepare_unity_map14_client_view_bindings import build as view_build
    view_build()
    changes=[ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientViewBindings'/n for n in ('NativeMap14ClientViewBindings.cs','Zjcs.Offline.World.NativeMap14ClientViewBindings.asmdef')]
    changes.append(ROOT/'scripts/prepare_unity_map14_client_view_bindings.py')
    receipt=ROOT/'assets/reference/official-map/map14-client-view-bindings-source-manifest.json'
    value=json.loads(receipt.read_bytes())
    for p in changes:
        rows=[r for section in ('outputs','tools') for r in value.get(section,[]) if r['path']==source_path(p)]
        if len(rows)!=1:raise ValueError('Root composition source receipt differs: '+source_path(p))
        rows[0].update(record(p))
    write(receipt,encode(value));outputs.extend((*changes,receipt))
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original root assembly version differs')
        assemblies.append(record(p))
    inputs.extend(CODE/'client-source'/(n+'.cs') for n in ('MyComponent','MyEntity'))
    inputs.append(CODE/'common-source/Common/EcComponent.cs')
    write(ROOT/'assets/reference/official-map/map14-root-components-source-manifest.json',encode(dict(manifest='871_156598',mapClassId=14,
        assemblies=assemblies,inputs=[record(p) for p in inputs],outputs=[record(p) for p in sorted(set(outputs))],tools=[record(Path(__file__))],
        operation='Copy all eight complete original thin root client classes and the empty generic ground base. Adapt native generic component/info type constraints to consumed typed projections only; no behavior is introduced. Bind each original Console root component and its live info by stable identity before original MyEntity component binding. The ground projection also supplies the existing EntityGridGroundData consumption alias.',
        validation_entry='Zjcs.Editor.Map14.OfficialMap14RootComponentsAcceptance.Run',validation='Unity 2022.3.57f1c2 / internal-display D3D11 isolated import/compile and Play Mode passed: all eight unchanged native root components/info through the stable client composition, original MyEntity bind/start/end/remove/reset and rebinding, repeated projection identity, live info, ground alias and absent ordinary-entity components. Original buffered events/stable views/factory/display/statue/radar/input/container regressions passed; current frames inspected. The owning map page distinguishes the root static check and remaining full-project/Player checks.',
        limitations=['The root fog controller/visibility agent, full original root prefab, map input, formal map host and Windows Player are still required.'])))
    print('Original thin root components prepared:',len(outputs),'outputs')

if __name__=='__main__':build()
