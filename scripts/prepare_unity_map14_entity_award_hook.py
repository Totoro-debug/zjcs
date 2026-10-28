"""Copy the original entity award hook and bind unchanged shared AwardManager IL."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-entity-award-hook-source-manifest.json'
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Original award hook version differs')
    assemblies=[CODE/'managed/decoded'/name for name in ('Assembly-CSharp.dll','Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes())!=manifest['assemblies'][path.name]['decoded_sha256']:raise ValueError('Original award assembly changed')
    source=CODE/'client-source/EntityAwardItemHook.cs';manager=CODE/'common-source/AwardManager.cs'
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityAwardHook'
    text=source.read_text(encoding='utf-8-sig').replace('using Common;','using System;\nusing Zjcs.Official.EntityLifecycle;')
    text=text.replace('AwardManager.AwardHandlerPriority','EntityAwardHandlerPriority').replace('AwardManager.Instance','AwardManager')
    text=re.sub(r'\bPlayerAwardMsg\b','EntityAwardMessageData',text)
    text=re.sub(r'\bAsMapEntity\b','EntityMapAwardSourceData',text)
    text=text.replace('public class EntityAwardItemHook : MonoBehaviour\n{','public class EntityAwardItemHook : MonoBehaviour\n{\n    public IEntityAwardManager AwardManager { get; set; }')
    write(output/'EntityAwardItemHook.cs',('// Complete original client award hook; see source receipt.\nnamespace Zjcs.Official.EntityAwardHook\n{\n'+text+'\n}\n').encode())
    original=manager.read_text(encoding='utf-8-sig')
    priority=re.search(r'public enum AwardHandlerPriority\s*\{[^}]+\}',original)[0].replace('AwardHandlerPriority','EntityAwardHandlerPriority')
    write(output/'EntityAwardHandlerPriority.cs',('namespace Zjcs.Official.EntityAwardHook\n{\n'+priority+'\n}\n').encode())
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14EntityAwardHook'
    editor=ROOT/'unity/Assets/Game/Editor/Map14/EntityAwardHook'
    outputs=[]
    refs=['Zjcs.Official.EntityLifecycle','Zjcs.Official.StoryPlayables']
    for folder,name,references,platforms in (
        (output,'Zjcs.Official.EntityAwardHook',refs,[]),
        (native,'Zjcs.Offline.World.NativeMap14EntityAwardHook',refs+['Zjcs.Official.EntityAwardHook','Zjcs.Offline.World.NativeMap14SharedEntities'],[]),
        (editor,'Zjcs.Editor.Map14.EntityAwardHook',refs+['Zjcs.Official.EntityViews','Zjcs.Official.EntityAwardHook','Zjcs.Offline.World.NativeMap14EntityAwardHook','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Editor.Map14.WorldInput'],['Editor'])):
        folder.mkdir(parents=True,exist_ok=True)
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    inputs=[source,manager]+[CODE/'common-source/Common'/(name+'.cs') for name in ('PlayerAwardMsg','AsMapEntity','AwardSourceData')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=[record(p) for p in inputs],tools=[record(Path(__file__).resolve())],outputs=[record(p) for p in outputs],
        operation='Copy complete original EntityAwardItemHook and priority enum. Explicit local injection supplies the unchanged Console AwardManager; preserve native message/source identities, original handler ordering/history processing and deferred replay. Do not apply or synthesize player rewards.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14EntityAwardHookAcceptance.Run',graphicalPlayMode=True),
        limitations=['Formal award/player/inventory/window services, original root prefab binding, pointer/UI/save and Windows Player remain required. Hook tests consume original messages and original manager IL, not server reward generation.'])))
    print('Original entity award hook recorded:',len(outputs),'outputs')
if __name__=='__main__':prepare()
