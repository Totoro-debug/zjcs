"""Record original stele dialog rules and the local configuration/service binding."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/official-map/map12-stele-context-source-manifest.json'


def build(write):
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '871_156598':
        raise ValueError('Map12 stele context source selection changed')
    code = ROOT / '.tools/source-packages/current' / version / 'official-code'

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    original_names = (
        'client-source/BattleSteleWorldStageDialog.cs', 'client-source/StageDialog.cs',
        'client-source/StageUI.cs', 'framework-firstpass-source/UIBase.cs',
        'client-source/CombatRatingConfig.cs', 'common-source/Common/StagePartInfoBattleStele.cs',
        'common-source/Common/TeamTargetBattleStele.cs', 'common-source/Common/TeamTargetCatalog.cs',
        'common-source/Common/FormulaHandler.cs', 'common-source/Common/BattleFormulaHandler.cs',
        'common-source/Common/PlayerPlayFightAction.cs')
    sources = {name: (code / name).read_text(encoding='utf-8-sig') for name in original_names}
    for name, tokens in {
        original_names[0]: ('OnTeamPlayerAdd +=', 'OnTeamPlayerRemove +=', 'OnTeamUpdate +=',
                            'OnTeamPlayerAdd -=', 'OnTeamPlayerRemove -=', 'OnTeamUpdate -=',
                            'CalcRankSuppression(MeManager.MainPlayer.SubRank, mapRegionInfo.SubRank) > 0',
                            'new TeamTargetBattleStele(stageId, periodictyInteractionId)',
                            'CombatRateingColorType.BattleStele', '(int)component3.Level + mInfo.Info.MonsterLevelAdd'),
        'common-source/Common/TeamTargetCatalog.cs': ('BattleStele = 9',),
        'common-source/Common/StagePartInfoBattleStele.cs': ('CombatRatingLevelValues[Parsers.GameSettings.RecommendCombatRatingIndex]',),
        'common-source/Common/FormulaHandler.cs': ('combatRating >= combatRatingLevelValues[i]', 'return count - i;'),
    }.items():
        if any(token not in sources[name] for token in tokens):
            raise ValueError('Original stele rule changed: ' + name)
    payload_root = ROOT / 'unity/Assets/Official/World/Map/12'
    payloads = [payload_root / name for name in (
        'Resources/Map12/steles.json', 'Resources/Map12/Native/region-label-data.json', 'map12-battles.json')]
    steles, labels, battles = [json.loads(path.read_bytes()) for path in payloads]
    if len(steles['Steles']) != 7 or steles['manifest'] != version:
        raise ValueError('Unexpected stele configuration closure')
    stages = {row['Id']: row for row in battles['stages']}
    flags = ('SupportTeam', 'JoinInMiddle', 'Background', 'LeaveInPreparing', 'LeaveInPlaying')
    for row in steles['Steles']:
        stage = stages[row['StageId']]
        if any(flag not in stage for flag in flags) or stage['SupportTeam'] != row['SupportTeam']:
            raise ValueError('Original fight-action stage flags are missing or inconsistent')
        if row['RegionSubRank'] >= len(labels['subRankToRank']):
            raise ValueError('Stele entity rank is outside the original rank projection')
    dependencies = [ROOT / 'assets/reference/official-map' / name for name in (
        'map12-steles-source-manifest.json', 'map12-label-components-code-source-manifest.json',
        'map12-native-region-labels-source-manifest.json', 'map12-player-play-source-manifest.json',
        'map12-stele-ui-source-manifest.json')]
    outputs = [ROOT / name for name in (
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldBattleSteleContextFactory.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldBattleSteleContextFactory.cs.meta',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldBattleSteleStageUI.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/Zjcs.Official.WorldUI.asmdef',
        'scripts/fixtures/map12_stele_context_harness.cs', 'scripts/test_map12_stele_context.ps1',
        'scripts/test_map12_stele_context.py', 'scripts/prepare_unity_map12_stele_context.py')]
    result = dict(format=1, manifest=version, mapClassId=12,
        originalCode=[record(code / name) for name in original_names],
        assemblies=[record(code / 'managed/decoded' / name)
                    for name in ('Assembly-CSharp.dll', 'Console.dll', 'Framework.dll', 'Framework.FirstPass.Runtime.dll')],
        inputs=[record(path) for path in payloads + dependencies], outputs=[record(path) for path in outputs],
        bindings=dict(stageIds=[row['StageId'] for row in steles['Steles']], teamTargetCatalog=9,
                      labelResource='Map12/Native/region-label-data', originalFightStageFlags=list(flags)),
        adaptation='Replace injected online/ECS services with explicit local interfaces; retain original UI '
                   'initialization order, rank and combat-rating formulas, reward preview inputs, target identity, '
                   'team event lifecycle and actual PlayerPlayStateModule gate. Use existing original UIBase lifecycle.',
        validation='Standalone source compilation and data-rule execution through test_map12_stele_context.ps1; '
                   'this check neither starts Unity nor exercises MonoBehaviour lifecycle, input or rendering.',
        limitations=['Formal map host and original team panel/roster are not wired by this binding.',
                     'Native window import, Play Mode input/visual/save checks and Windows Player release remain pending.'])
    if write:
        RECEIPT.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    elif json.loads(RECEIPT.read_bytes()) != result:
        raise ValueError('Map12 stele context receipt drift')
    print('Original Map12 stele dialog context verified: seven stages, three team events, original formulas and action gate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
