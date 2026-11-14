"""Record original custom-hit inputs and the confirmed offline cell expansion."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def identity(relative):
    raw = (subprocess.check_output(["git", "show", ":" + relative], cwd=ROOT)
           if "--index" in sys.argv and not relative.startswith(".tools/") else (ROOT / relative).read_bytes())
    return dict(path=relative, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())

def main():
    original = '.tools/source-packages/current/884_156692/official-code/'
    runtime = 'unity/Assets/Game/Runtime/'
    proof = dict(code_manifest='884_156692', configuration_manifest='854_156383',
        original=[identity(original + name) for name in (
            'common-source/Common/FightHitCustomTargetComponent.cs',
            'common-source/Common/FightHitCustomTargetComponentInfo.cs',
            'common-source/Common/SelfTargetPosResolver.cs',
            'client-source/MyFightHitCustomTarget.cs')],
        inputs=[identity('unity/Assets/Official/Combat/' + name) for name in (
            'skill-rules.json', 'skill-rules-source-manifest.json')],
        migration=[identity(runtime + name) for name in (
            'Official/Combat/FightBattleHitCollector.cs', 'Official/Combat/FightBattleState.cs',
            'Official/Combat/FightOfficialCombatCatalog.cs', 'Official/Combat/FightBattleExecutor.cs',
            'Offline/Combat/FightHitChainExecution.cs', 'Offline/Combat/FightHitChainExecution.CustomTargets.cs')],
        validation=[identity(name) for name in (
            'scripts/test_battle_custom_targets.ps1', 'scripts/fixtures/battle_custom_targets_harness.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.CustomTargets.cs')],
        generator=identity('scripts/record_battle_custom_target_sources.py'),
        bindings=dict(execution='FightBattleHitCollector -> FightHitChainExecution.SelectCustom -> existing settlement',
            presentation='FightBattleHitSelectionSnapshot -> OfficialBattleHitReceiptPlayback -> original custom/summon views'),
        operation='User-confirmed offline SelfTargetPosResolver: scan the original Scope around the actual parent hit cell in original order; '
            'for each eligible occupied body cell create each original HitList child with its edge and scope delays. '
            'Preserve original orientation and flip. Parent performs no damage or random draw. '
            'No new artwork or prefab changes. Only the three installed original custom parents are validated.')
    # These pre-existing proofs also name the shared traversal and fixture. Refresh
    # only those local code credentials; official inputs and asset outputs stay fixed.
    for receipt, key, changed in (
        ('assets/reference/battle-rules/chained-hits-source-manifest.json', 'migration',
         runtime + 'Offline/Combat/FightHitChainExecution.cs'),
        ('assets/reference/battle-animation/hit-view-runtime-source-manifest.json',
         'local_resource_and_identity_bindings',
         'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.cs')):
        path=ROOT/receipt
        existing=json.loads(path.read_bytes())
        row=next(row for row in existing[key] if row['path']==changed)
        current=identity(changed)
        for field in tuple(row):
            if field in current: row[field]=current[field]
        path.write_text(json.dumps(existing,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    destination=ROOT/'assets/reference/battle-rules/custom-targets-source-manifest.json'
    destination.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

if __name__ == '__main__':
    main()
