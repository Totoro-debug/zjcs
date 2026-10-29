"""Record the original shared visibility component and native consumer bridge."""
import json
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta

def record(p):
    raw=p.read_bytes();entry=dict(path=source_path(p))
    if p.suffix=='.cs' and p.is_relative_to(ROOT/'unity'):
        raw=raw.replace(b'\r\n',b'\n');entry['hashLineEndings']='LF'
    return dict(entry,sha256=sha(raw))
def build():
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14Visibility'
    editor=ROOT/'unity/Assets/Game/Editor/Map14/NativeVisibility'
    refs=['Zjcs.Official.World.Map13EntityVisibilityViews','Zjcs.Official.World.Map13EntityEvents','Zjcs.Official.EntityViews','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews']
    base=ROOT/'unity/Assets/Game/Editor/Map14/StatueEntities/Zjcs.Editor.Map14.StatueEntities.asmdef'
    outputs=[]
    for folder,name,references,platforms in ((runtime,'Zjcs.Offline.World.NativeMap14Visibility',refs,[]),
        (editor,'Zjcs.Editor.Map14.NativeVisibility',sorted(set(refs+json.loads(base.read_bytes())['references']+['Zjcs.Offline.World.NativeMap14Visibility','Zjcs.Editor.Map14.StatueEntities','Zjcs.Official.EntityWorldInput','Zjcs.Official.World.NativeLabelUI'])),['Editor'])):
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for p in folder.iterdir():
            if p.is_file() and p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    data=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13EntityVisibilityViews/Map13VisibilityData.cs'
    generator=ROOT/'scripts/prepare_unity_map13_entity_visibility_views.py'
    proof_path=ROOT/'assets/reference/official-map/map13-entity-visibility-views-source-manifest.json'
    proof=json.loads(proof_path.read_bytes())
    if proof['manifest']!='871_156598':raise ValueError('Original visibility consumer version differs')
    # Preserve other sessions' records; update only this consumed data boundary.
    for section,path in (('outputs',data),('tools',generator)):
        entry,=[r for r in proof[section] if r['path']==source_path(path)]
        raw=path.read_bytes()
        if path==data:entry['hashLineEndings']='LF'
        if entry.get('hashLineEndings')=='LF':raw=raw.replace(b'\r\n',b'\n')
        entry['sha256']=sha(raw)
    proof['native_visibility_boundary']='Map13VisibilityData is an extensible consumed-data projection: virtual accessors/mutations retain the original default flag/event bodies and allow Map14 to delegate to the unchanged original VisibilityComponent and original receiver ABI.'
    write(proof_path,encode(proof))
    outputs.extend((data,generator,proof_path))
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original visibility IL version differs')
        assemblies.append(record(p))
    sources=[CODE/'common-source/Common'/name for name in ('VisibilityComponent.cs','VisibilityGridComponent.cs','ClientVisibilityType.cs','IEcEntityClientVisibilityUpdatedEventReceiver.cs','EcEntityClientVisibilityUpdatedEventDelegate.cs','EventDelegateBasicBase.cs')]
    sources.extend((CODE/'client-source/MyVisibility.cs',CODE/'client-source/ECClientVisibilityView.cs'))
    shared=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedViews/NativeMap14SharedViewEntity.cs'
    subscription=shared.parent.parent/'NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs'
    acceptance=ROOT/'unity/Assets/Game/Editor/Map14/StatueEntities/OfficialMap14StatueEntityAcceptance.cs'
    outputs.extend((shared,subscription,acceptance,ROOT/'scripts/prepare_unity_map14_shared_views.py',ROOT/'assets/reference/official-map/map14-shared-view-source-manifest.json',ROOT/'scripts/prepare_unity_map14_radar_native.py',ROOT/'scripts/prepare_unity_map14_statue_entities_native.py',ROOT/'assets/reference/official-map/map14-native-radar-tips-source-manifest.json',ROOT/'assets/reference/official-map/map14-native-statue-entities-source-manifest.json'))
    write(ROOT/'assets/reference/official-map/map14-native-visibility-source-manifest.json',encode(dict(manifest='871_156598',mapClassId=14,assemblies=assemblies,inputs=[record(p) for p in sources],dependencies=[record(base)],tools=[record(Path(__file__))],outputs=[record(p) for p in sorted(set(outputs))],
        operation='Bind the existing original VisibilityComponent to the same stable native view. Forward flag/fade reads and writes directly; invoke unchanged Console Add/Remove and forward its original event argument identity through the migrated visibility delegate. Keep original default visibility consumption methods; expose only virtual platform data access. The native view owns disposal of its additional component projections and original receiver subscription.',
        validation_entry='Zjcs.Editor.Map14.OfficialMap14NativeVisibilityAcceptance.Run',
        validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile and Play Mode passed: direct original flags/fade reads and writes, unchanged Console Add/Remove including an unchanged-mask event, same projected event owner, duplicate binding identity, absent component, view-owned subscription disposal and repeated disposal. Four assembled statue roots use these original visibility components through actual visibility requests/handlers and real rendered frames; native cost/radar/input/container/loading regressions passed. Current loop/disappear/hidden and regression frames viewed; root static metadata check required separately.',
        limitations=['Visibility flags are explicitly seeded by the integration fixture; actual map visibility handler services and player state remain required. Formal display/tag state, map/player/UI/simulated pointer/save, complete ProjectTools.Validate and Windows Player remain required.'])))
    print('Original native visibility binding prepared:',len(outputs),'outputs')

if __name__=='__main__':build()
