"""Migrate original thin client components with native shared component queries."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-client-components-source-manifest.json'
NAMES=('MyEntitySource','MyWorldEntityLevel','MyStageBehTouch','MyStageResultAwardExtraProp','MyStageResultAwardStatusEffect','MyStageResultModifyHpPercent','MyStageConPlayerPeriodicityInteraction','MyStageResultPeriodicityRandomAward')
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Original component version differs')
    assemblies=[CODE/'managed/decoded'/name for name in ('Assembly-CSharp.dll','Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes())!=manifest['assemblies'][path.name]['decoded_sha256']:raise ValueError('Original component IL changed')
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityClientComponents';inputs=[];shared_names=set()
    for name in NAMES:
        source=CODE/'client-source'/(name+'.cs');inputs.append(source);text=source.read_text(encoding='utf-8-sig')
        component=re.search(r'MyComponent<(\w+)',text)[1];shared_names.add(component)
        text=text.replace('using Common;','using Zjcs.Official.EntityLifecycle;\nnamespace Zjcs.Official.EntityClientComponents\n{')+'\n}\n'
        # Preserve original returned nullable/value or reference object, boxed
        # across the native ABI, including the actual original list identities.
        text=text.replace('public AwardExtraPropInfo? GetAwardExtraPropInfo','public object GetAwardExtraPropInfo')
        text=text.replace('public StageResultAwardStatusEffectComponentData GetAwardStatusEffectInfo','public object GetAwardStatusEffectInfo')
        text=re.sub(r'\b'+component+r'Info\b','object',text)
        text=re.sub(r'\b'+component+r'\b',component+'Data',text)
        write(output/(name+'.cs'),('// Original client component; see client component source receipt.\n'+text).encode())
    for name in sorted(shared_names):
        inputs.append(CODE/'common-source/Common'/(name+'.cs'))
        info=CODE/'common-source/Common'/(name+'Info.cs')
        if info.exists():inputs.append(info)
    inputs += [CODE/'common-source/Common'/(name+'.cs') for name in ('EcComponent','LevelComponent','AwardExtraPropInfo','AwardExtraProp','StageResultAwardStatusEffectComponentData')]
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientComponents'
    editor=ROOT/'unity/Assets/Game/Editor/Map14/ClientComponents';editor.mkdir(parents=True,exist_ok=True)
    common=['Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityViews','Zjcs.Official.EntityTipDotViews']
    outputs=[]
    for folder,name,refs,platforms in (
        (output,'Zjcs.Official.EntityClientComponents',common,[]),
        (native,'Zjcs.Offline.World.NativeMap14ClientComponents',common+['Zjcs.Official.EntityClientComponents','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14SharedEntities'],[]),
        (editor,'Zjcs.Editor.Map14.ClientComponents',common+['Zjcs.Official.EntityContainers','Zjcs.Official.EntityLoading','Zjcs.Official.EntityClientComponents','Zjcs.Offline.World.NativeMap14ClientComponents','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Editor.Map14.EntityIndex'],['Editor'])):
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=refs,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    dependencies=[ROOT/'unity/Assets/Game/Runtime'/name for name in ('Official/EntityLifecycle/MyComponent.cs','Offline/World/NativeMap14SharedViews/NativeMap14SharedViewEntity.cs','Offline/World/NativeMap14ClientEntities/NativeMap14ClientEntityState.cs')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=[record(p) for p in inputs],tools=[record(Path(__file__).resolve())],dependencies=[record(p) for p in dependencies],outputs=[record(p) for p in outputs],
        operation='Copy eight complete original client component classes. Their generic component types consume native data abstractions; original info is returned without reconstruction. Query methods invoke unchanged original Console components. Preserve original nullable reward boxing and reference/list identity instead of translating reward data. Additional component bindings are explicit, cached by consumed type and retain original component identity; instantiate through original shared methods.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14ClientComponentsAcceptance.Run',graphicalPlayMode=True,checks=['All eight client components bind original shared instances/info','Original ownership and click queries','Original configured/mutable reward and periodicity lookups','Native component binding identity and lifecycle rebinding','Entity index/shared state/client container/loading/four-statue regression']),
        limitations=['WorldStageInput, award hook, radar and remaining root/prefab components require migration. Level projections expose native state; original level formula and player parser/session bindings are not replaced or claimed validated. Formal native prefab loading, map input/UI/save, full ProjectTools.Validate and Windows Player remain pending.'])))
    print('Migrated original client component classes:',len(NAMES))
if __name__=='__main__':prepare()
