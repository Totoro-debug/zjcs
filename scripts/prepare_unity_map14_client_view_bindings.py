"""Compose already migrated native client inputs without changing view identity."""
import json
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta

def record(p):
    raw=p.read_bytes();entry=dict(path=source_path(p))
    if p.suffix in ('.cs','.asmdef','.py'):
        raw=raw.replace(b'\r\n',b'\n');entry['hashLineEndings']='LF'
    return dict(entry,sha256=sha(raw))

def build():
    outputs=[]
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientViewBindings'
    refs=['Zjcs.Official.MapDisplays','Zjcs.Official.EntityActions','Zjcs.Official.EntityVisibilityHandlers','Zjcs.Official.World.Map13EntityVisibilityViews','Zjcs.Official.EntityViews','Zjcs.Offline.World.NativeMap14ClientComponents','Zjcs.Offline.World.NativeMap14DisplayData','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14Visibility','Zjcs.Offline.World.NativeMap14VisibilityHandlers']
    write(runtime/'Zjcs.Offline.World.NativeMap14ClientViewBindings.asmdef',encode(dict(name='Zjcs.Offline.World.NativeMap14ClientViewBindings',autoReferenced=False,references=refs)))
    editor=ROOT/'unity/Assets/Game/Editor/Map14/ClientViewBindings'
    refs=json.loads((editor.parent/'VisibilityHandlers/Zjcs.Editor.Map14.VisibilityHandlers.asmdef').read_bytes())['references']
    refs=sorted(set(refs+['Zjcs.Offline.World.NativeMap14ClientViewBindings','Zjcs.Editor.Map14.VisibilityHandlers']))
    write(editor/'Zjcs.Editor.Map14.ClientViewBindings.asmdef',encode(dict(name='Zjcs.Editor.Map14.ClientViewBindings',autoReferenced=False,references=refs,includePlatforms=['Editor'])))
    for folder in (runtime,editor):
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
        for p in folder.iterdir():
            if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    changes={
        'map14-map-display-runtime-source-manifest.json':['unity/Assets/Game/Runtime/Official/MapDisplays/MapDisplayBindings.cs','scripts/prepare_unity_map_display_runtime.py'],
        'map14-native-display-data-source-manifest.json':['unity/Assets/Game/Runtime/Offline/World/NativeMap14DisplayData/NativeMap14DisplayEntityState.cs']}
    for name,paths in changes.items():
        receipt=ROOT/'assets/reference/official-map'/name;value=json.loads(receipt.read_bytes())
        for path in paths:
            rows=[entry for section in ('outputs','tools') for entry in value.get(section,[]) if entry['path']==path]
            if len(rows)!=1:raise ValueError('Existing source receipt record differs: '+path)
            rows[0].update(record(ROOT/path));outputs.append(ROOT/path)
        write(receipt,encode(value));outputs.append(receipt)
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Console.dll','Assembly-CSharp.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original client view source version differs')
        assemblies.append(record(p))
    sources=[CODE/'client-source'/(name+'.cs') for name in ('MyMapDisplay','MyEntity','MyComponent','MyAction')]
    write(ROOT/'assets/reference/official-map/map14-client-view-bindings-source-manifest.json',encode(dict(manifest='871_156598',mapClassId=14,assemblies=assemblies,inputs=[record(p) for p in sources],outputs=[record(p) for p in sorted(set(outputs))],tools=[record(Path(__file__))],operation='Install previously migrated ordinary component, native visibility, action, award and map display state projections on the one original stable client view before consumer binding. Resolve consumed display state directly or through that same view component query. Keep original MyMapDisplay/MyEntity code, BindPre/remove order and source lookup unchanged; no new shared rules, identities or default state.',validation_entry='Zjcs.Editor.Map14.OfficialMap14ClientViewBindingsAcceptance.Run',validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile/Play Mode passed: actual original placement 201 LoGridTransform into original 6106 template, registration/Awake/container Start, original logical tag/area/ground/position through one stable view and MyEntity BindPre to unchanged MyMapDisplay; repeated identity and foreign-scope rejection, ordinary/visibility/action/award prebinding, cached recycle arguments and removing/destroying branches. Original visibility handler/action/display/statue/radar/input/container regressions passed; current rendered frames viewed and root static metadata check passed. Complete ProjectTools.Validate and Windows Player remain pending.',limitations=['The display manager is a call recorder in this input-boundary check, not a formal rendered map or dungeon gameplay.','Formal map host, original action/effect prefabs, player/UI/simulated pointer/save, complete ProjectTools.Validate and Windows Player remain required.'])))
    print('Stable native client view composition prepared:',len(set(outputs)),'outputs')

if __name__=='__main__':build()
