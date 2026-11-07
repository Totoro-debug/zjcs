"""Restore original 13052/13482 persistent fire graphs and source-death leaf."""
import json
import re
from pathlib import Path
from hashlib import sha256

from official_apk import ROOT
from prepare_battle_status_orbit_assets import build, SCRIPTS
from prepare_battle_cast_assets import EVENT_ASSET
from prepare_battle_status_playable_runtime import CODE, method_body, identity
from prepare_unity_story_sound_runtime import runtime_meta

RECEIPT = ROOT / 'assets/reference/battle-animation/status-burn-assets-source-manifest.json'
RUNTIME_RECEIPT = ROOT / 'assets/reference/battle-animation/grid-playable-runtime-source-manifest.json'
FOLDER = ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemPlayables'


def prepare_runtime():
    source = CODE / 'client-source/AutoStopOnSourceDie.cs'
    target = FOLDER / 'AutoStopOnSourceDie.cs'
    manifest = CODE.parent / 'code-manifest.json'
    metadata = json.loads(manifest.read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if metadata['manifest'] != '884_156692' or identity(assembly)['sha256'] != metadata['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original source-death leaf identity differs from the selected source')
    original, migrated = source.read_text(encoding='utf-8-sig'), target.read_text(encoding='utf-8-sig')
    checks = {}
    for method in ('Component_OnDie', 'StopImpl', 'PlayImpl'):
        bodies = [re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', method_body(text, method)))
                  for text in (original, migrated)]
        assert bodies[0] == bodies[1], method
        checks[method] = sha256(bodies[0].encode()).hexdigest()
    # ILSpy's explicit async state machine is recompiled as its original await.
    assert 'UniTask.Delay((int)(autoStopOnSourceDie.DelayStop * 1000f), false, (PlayerLoopTiming)8, autoStopOnSourceDie.mCancellationTokenSource.Token, false)' in original
    assert re.search(r'await\s+UniTask.Delay\(\(int\)\(DelayStop\s*\*\s*1000f\),\s*false,\s*\(PlayerLoopTiming\)8,\s*mCancellationTokenSource.Token,\s*false\);\s*StopImpl\(\);', migrated)
    assert 'mCancellationTokenSource.Cancel();' in migrated
    asmdef = FOLDER / 'Zjcs.Official.BattleGridItemPlayables.asmdef'
    asmdef.write_text(json.dumps(dict(name='Zjcs.Official.BattleGridItemPlayables',
        rootNamespace='Zjcs.Official.BattleGridItemPlayables',
        references=['Zjcs.Official.StoryPlayables', 'UniTask'], autoReferenced=True), indent=2)+'\n', encoding='utf-8')
    outputs = [target, asmdef,
        ROOT / 'unity/Assets/Game/Runtime/Official/StoryPlayables/IBattleSourceDeathEvents.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.SourceDeathEvents.cs']
    host = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs'
    for path in outputs:
        runtime_meta(path)
    runtime_meta(FOLDER)
    outputs += [Path(str(path)+'.meta') for path in outputs] + [Path(str(FOLDER)+'.meta')]
    RUNTIME_RECEIPT.write_text(json.dumps(dict(manifest='884_156692',
        source=identity(source), assembly=identity(assembly), code_manifest=identity(manifest),
        unchanged_method_bodies=checks, outputs=[identity(path) for path in outputs],
        generator=identity(Path(__file__)), host_binding=identity(host),
        adaptation='Original stop branch/method bodies; ILSpy async state machine recompiled with the same UniTask delay, scaled time, player-loop timing 8 and cancellation token. Scene-owned disposable OnDie subscription replaces the unavailable MyFightRoleCharacter lookup; source death is emitted at the existing scene death-view boundary.',
        limits=['Native graph extraction does not establish production grid/status lifecycle binding or full battle death animation timing.']),
        ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def main():
    prepare_runtime()
    mapped = dict(SCRIPTS)
    mapped.update({1640720687: ('StorySounds', 'MyPlayableSound'),
                   -638476292: ('BattleGridItemPlayables', 'AutoStopOnSourceDie')})
    build(DEST=ROOT/'unity/Assets/Official/Combat/StatusBurns/Resources/Combat/StatusBurns',
        DATA=ROOT/'unity/Assets/Game/Resources/Combat/status-burns.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots={
            13052: 'Prefabs/EcEntity/Fight/FightSkill/13050/playable13052.prefab',
            13482: 'Prefabs/EcEntity/Fight/FightSkill/13480/playable13482.prefab'},
        MAPPED=mapped, family='StatusBurns', shader_prefix='StatusBurn_', generator=Path(__file__),
        reference_scripts={-210918009: dict(name='WwiseEventReference', native_asset=EVENT_ASSET,
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        limits=['Original status fire graphs and their full native dependencies are restored; production grid/status lifecycle and audio-bank availability require actual battle validation.'])
    proof = json.loads(RECEIPT.read_bytes())
    proof['source_death_leaf_runtime'] = identity(RUNTIME_RECEIPT)
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


if __name__ == '__main__':
    main()
