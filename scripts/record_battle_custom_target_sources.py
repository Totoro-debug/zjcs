"""Record original custom-hit inputs and the confirmed offline summon placement."""
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
            'skill-rules.json', 'skill-rules-source-manifest.json')]
            + [identity('unity/Assets/Official/Skills/fidelity-texts.json')],
        migration=[identity(runtime + name) for name in (
            'Official/Combat/FightBattleHitCollector.cs', 'Official/Combat/FightBattleState.cs',
            'Official/Combat/FightOfficialCombatCatalog.cs', 'Official/Combat/FightBattleExecutor.cs',
            'Offline/Combat/FightHitChainExecution.cs', 'Offline/Combat/FightHitChainExecution.CustomTargets.cs',
            'Offline/Combat/FightHitTargetSelectors.cs')],
        validation=[identity(name) for name in (
            'scripts/test_battle_custom_targets.ps1', 'scripts/fixtures/battle_custom_targets_harness.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.CustomTargets.cs')],
        generator=identity('scripts/record_battle_custom_target_sources.py'),
        bindings=dict(execution='FightBattleHitCollector -> FightHitChainExecution.SelectCustom -> existing settlement',
            presentation='FightBattleHitSelectionSnapshot -> OfficialBattleHitReceiptPlayback -> original custom/summon views'),
        operation='User-confirmed offline SelfTargetPosResolver for 12063/13303/13673: use the existing AI-selected target; '
            'choose a legal unoccupied cell in the original caster-relative scope by nearest target body, then caster body, '
            'then original Scope order (Manhattan distance). Only if none is legal, try the caster cell ignoring only caster occupancy. '
            'Map validity, other occupants, original pool limits and probability remain enforced. Create one original HitList occurrence '
            'with original edge/scope delays, orientation and flip; parent performs no damage or random draw. '
            'Official descriptions item_10080/item_10167/item_10189 establish caster range four and one summon near ally/enemy; '
            'server ordering and the last-resort overlap are confirmed offline decisions, not recovered official logic.')
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
