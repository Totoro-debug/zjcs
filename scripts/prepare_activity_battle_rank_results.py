"""Record the original domain-boss damage consumer and the approved local result persistence."""
import hashlib
import json

from official_apk import ROOT


def digest(relative):
    return hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()


def main():
    original = '.tools/source-packages/current/884_156692/official-code/'
    sources = (
        'client-source/FightActivityDomainBossMainPanel.cs',
        'client-source/TeamTopContentDataActivityDomainBoss.cs',
        'client-source/GameEndPanelActivityDomainBoss.cs',
        'common-source/Common/FightUtilsHandler.cs',
        'common-source/Common/PlayerActivityDomainBossData.cs',
        'common-source/Common/GameResultActivityDomainBossInfo.cs',
        'common-source/Common/TopScoreDataActivityDomainBoss.cs',
    )
    runtime = (
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleBossDamage.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineDailyChallengeService.DomainBossRanking.cs',
        'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.ActivityRanking.cs',
    )
    document = dict(
        manifest='884_156692',
        sources={original + name: digest(original + name) for name in sources},
        runtime={name: digest(name) for name in runtime},
        helper=dict(path='scripts/prepare_activity_battle_rank_results.py',
            sha256=digest('scripts/prepare_activity_battle_rank_results.py')),
        original_rule='FightUtilsHandler.CanAccDamageToBoss: Monster target and damage result; '
            'FightActivityDomainBossMainPanel adds abs(RealDamageValue) + ShieldValue.',
        offline_decision='User-approved current-character actual completed results in the local period. '
            'Highest damage follows the original MaxDamageRecord field. A completed zero-damage battle '
            'has a rank entry; an uncompleted encounter has none. No damage carried between encounters.',
        integrations=[
            dict(path='unity/Assets/Game/Runtime/Official/Combat/FightBattleReportBuilder.cs',
                member='Build: DamageToMonsters from common checkpoint receipts'),
            dict(path='unity/Assets/Game/Runtime/Offline/Progression/OfflineDailyChallengeService.cs',
                member='DailyChallengeActivitySave / ResetPeriod: independent period rank fields; daily reset keeps them'),
            dict(path='unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs',
                member='FinalizeChallenge: valid finished result only, including completed-checkpoint recovery'),
        ],
        stage='Production settlement and period persistence connected. Original ranking page/controller pending.')
    path = ROOT / 'assets/reference/daily-challenges/activity-rank-results-source-manifest.json'
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print(path.relative_to(ROOT))


if __name__ == '__main__':
    main()
