"""Migrate the complete original combined radar tip, repairing its async syntax."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-radar-combined-tip-source-manifest.json'
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly=CODE/'managed/decoded/Assembly-CSharp.dll'
    if manifest['manifest']!='871_156598' or sha(assembly.read_bytes())!=manifest['assemblies'][assembly.name]['decoded_sha256']:raise ValueError('Original combined tip IL changed')
    source=CODE/'client-source/MapControllerEntityRadarTipCombinate.cs'
    text=source.read_text(encoding='utf-8-sig')
    start=text.index('\t[StructLayout(');end=text.index('\t[SerializeField]',start)
    state_machine=text[start:end]
    for original in ('UniTask.WaitForSeconds(mapControllerEntityRadarTipCombinate.mHideTime, false, (PlayerLoopTiming)8, cancellationToken, false)',
                     'mapControllerEntityRadarTipCombinate.ClearTips();','mapControllerEntityRadarTipCombinate.OnHideAnimCompleted();',
                     'mapControllerEntityRadarTipCombinate.mIsFadeOut = false;','mapControllerEntityRadarTipCombinate.ClearToken();'):
        if original not in state_machine:raise ValueError('Original async state machine differs: '+original)
    text=text[:start]+text[end:]
    start=text.index('\t[AsyncStateMachine(');end=text.index('\tprivate void ClearToken()',start)
    text=text[:start]+'''    // Async syntax reconstructed directly from the original MoveNext above.
    private async UniTask DelayHide(CancellationToken cancellationToken, Action<MapControllerEntityRadarTipBase> hideCallback = null)
    {
        mOnHideAnimCompletedCallback = hideCallback;
        mIsFadeOut = true;
        await UniTask.WaitForSeconds(mHideTime, false, (PlayerLoopTiming)8, cancellationToken, false);
        ClearTips();
        OnHideAnimCompleted();
        mIsFadeOut = false;
        ClearToken();
    }

'''+text[end:]
    text=re.sub(r'(?m)^using [^\n]+;\n','',text)
    text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','\n',text)
    text='''// Original combined radar tip; see map14 combined-tip source receipt.
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityRadar;
namespace Zjcs.Official.EntityRadarCombined
{
'''+text+'\n}\n'
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityRadarCombined'
    editor=ROOT/'unity/Assets/Game/Editor/Map14/RadarCombined'
    output.mkdir(parents=True,exist_ok=True)
    write(output/source.name,text.encode())
    refs=['Zjcs.Official.EntityRadar','Zjcs.Official.EntityLifecycle','UniTask']
    outputs=[]
    for folder,name,references,platforms in (
        (output,'Zjcs.Official.EntityRadarCombined',refs,[]),
        (editor,'Zjcs.Editor.Map14.RadarCombined',refs+['Zjcs.Official.EntityRadarCombined','Zjcs.Official.EntityViews','Zjcs.Official.StoryPlayables','Zjcs.Official.StoryPlayableManager','Zjcs.Official.World.NativeTerrain','Zjcs.Editor.Map14.EntityRadar'],['Editor'])):
        folder.mkdir(parents=True,exist_ok=True)
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    dependencies=[ROOT/'unity/Assets/Game/Runtime/Official'/name for name in ('EntityRadar/MapControllerEntityRadarTipBase.cs','EntityRadar/MapControllerEntityRadarTipManager.cs','StoryPlayableManager/GameObjectPool.cs')]
    unitask=ROOT/'unity/Packages/com.cysharp.unitask'
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(assembly)],inputs=[record(source)],dependencies=[record(p) for p in dependencies],tools=[record(Path(__file__).resolve())],outputs=[record(p) for p in outputs],
        operation='Copy complete original combined tip. Reconstruct async/await directly from original compiler-generated MoveNext, preserving scaled WaitForSeconds, timing 8, deferred cancellation and cleanup/callback order. Preserve immediate hide, disable/re-enable, show cancellation, child filtering and original pooled children.',
        asyncRuntime=dict(package=record(unitask/'package.json'),delay=record(unitask/'Runtime/UniTask.Delay.cs'),version='2.5.10',origin='Existing MIT UniTask package; external runtime, not claimed as official APK payload.'),
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14RadarCombinedAcceptance.Run',graphicalPlayMode=True),
        limitations=['Original combined-tip prefab, specialized child tips and formal map/HUD/player/input/save/Windows Player are not completed by component fixtures.'])))
    print('Original combined radar tip recorded:',len(outputs),'outputs')
if __name__=='__main__':prepare()
