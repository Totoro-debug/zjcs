"""Record original report evidence and the user-confirmed local average-counting rules."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(source_root):
    official = source_root / '.tools/source-packages/official-code'
    code = ['common-source/Common/PlayerFightSkillReport.cs',
            'common-source/Common/FightReportDamageStats.cs',
            'common-source/Common/FightRoleNormalReport.cs',
            'common-source/Common/FightReportData.cs',
            'common-source/Common/GameCharacterType.cs',
            'common-source/Common/DamageResultParameter.cs',
            'common-source/Common/BattleFormulaHandler.cs',
            'client-source/FightEndReportPlayerTabViewItem.cs',
            'common-source/Common/FightReportComponent.cs',
            'common-source/Common/BattleReportCollector.cs',
            'common-source/Common/BattleReportDistributor.cs',
            'common-source/Common/BattleReportCollectorApplyStatusByStatus.cs',
            'common-source/Common/BattleReportCollectorCfgApplyStatusByStatus.cs',
            'common-source/Common/FightStatusHpAccDecreaseComponentInfo.cs',
            'common-source/Common/FightStatusSkillEndComponentInfo.cs',
            'common-source/Common/StatusType.cs',
            'common-source/Common/FightReportStatusStats.cs',
            'client-source/FightEndReportSkillTabViewItem.cs',
            'client-source/FightEndReportSkillTabView.cs',
            'client-source/CharacterDamageUIManager.cs']
    runtime = ['unity/Assets/Game/Runtime/Official/Combat/' + name + '.cs' for name in
               ['FightBattleExecutor', 'FightBattleStatistics', 'FightBattleReportBuilder',
                'FightBattleState', 'FightStatusLifecycle', 'FightStatusPropertyRuntime',
                'FightStatusTriggerRuntime', 'FightBattleExecutor.Resilience', 'FightBattleReportGrouping']]
    runtime += ['unity/Assets/Game/Editor/Combat/OfficialBattleReportStatisticsAcceptance.cs',
                'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs',
                'scripts/test_battle_report_averages.ps1', 'scripts/test_battle_report_shields.ps1',
                'scripts/test_battle_report_cures.ps1',
                'scripts/test_battle_report_status_counts.ps1',
                'scripts/test_battle_report_sources.ps1',
                'scripts/test_battle_report_players.ps1', 'scripts/test_battle_resilience.ps1',
                'scripts/test_battle_report_grouping.ps1',
                'scripts/test_unity_combat.ps1',
                'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportSkillRowBinder.cs']
    record = dict(manifest='854_156383', generator_sha256=sha(Path(__file__)),
                  official_code={p: sha(official / p) for p in code},
                  runtime={p: sha(ROOT / p) for p in runtime},
                  evidence='The client consumes AvgDamageCount and AvgDamageCountPerTarget; shared PlayerFightSkillReport merges supplied values by cast-weighted averages. BattleReportCollector and FightReportComponent contain no single-battle counter implementation.',
                  user_confirmed=dict(numerator='H: damage hits, including immunity with zero damage and full shield absorption; excluding dodge.',
                                      denominators='C: casts; T: sum of distinct damage-hit targets within each cast. Average hit count H/C; average segments H/T.',
                                      example='10 casts, 2 targets and 3 segments each: H=60, T=20, averages 6 and 3.',
                                      rounding='User example: 18 hits / 10 casts = 1.8, displayed as 2. The original row adapter uses Mathf.RoundToInt for display and retains raw ratios.',
                                      damage_count='DamageCount equals the confirmed damage-hit count, including immunity/full shielding and excluding dodge.',
                                      cure_count='CureCount counts landed cure-target hits, including full HP with zero effective healing.',
                                      status_counts='Each target accepted application counts once: new, stack and refresh count one, multiple stacks still one, discard and probability failure zero. Buff=1; Debuff=2 and AbnormalDebuff=3 both count as debuffs; None=0 excluded.',
                                      indirect_sources='A status directly triggering another status keeps its established root skill; an independently invoked skill starts its own source; unknown roots remain unattributed.',
                                      shield='Effective shield is the numeric shield successfully added to targets, not later damage absorption.',
                                      distributions='Each skill effective damage/healing/shield divided by the sum of the corresponding effective values across all skills of that caster.',
                                      hp_ratios='Each skill effective healing/shield divided by the caster maximum HP.',
                                      received_damage='BeAbsoluteDamage includes shield absorption, HP overflow and damage negated by defence/reduction.',
                                      break_resilience='BreakResilience counts successful enemy breaks, not resilience points removed.',
                                      player_groups='NPC teammates retain independent rows. Summons of summons remain in the final owner summon report category. Pet-created summons retain the final owner pet category.'),
                  implementation='Each ExecuteSkill invocation owns its target set, so same-skill recursive replay starts another denominator. Healing, HP-cost records, original blinding/miss and no-damage actions do not enter damage-hit counters. Extra mark attribution creates no additional hit.',
                  shield_implementation='A new numeric-shield lifecycle instance is pending until the original property rebuild initializes its configured/custom shield value. Attribute that successful grant to CreatorId/RootSkillId/ApplyingSkillItemId exactly once. Existing discard/stack updates and restored shields do not manufacture grants. Shield consumption never reduces granted statistics.',
                  status_implementation='The common lifecycle journals each accepted application using its incoming creator/root skill/item, independent of the active instance. The executor drains pending records once after property rebuild. This preserves refreshes, multiple applications and statuses removed before aggregation; records without a root skill remain explicit unit-level unattributed counts, never a fabricated skill 0. Unattributed caster applications mark per-skill totals unavailable while retaining recorded lower bounds.',
                  source_implementation='HP-threshold, accumulated-HP-loss and skill-end status reactions copy their parent ReportSource. ReportSource contains the root caster/entity/item independently of gameplay CreatorId/RootSkillId, property snapshots and falloff. Accepted application journals and successful shield grants consume that statistical source. Independently executed skills retain their own casts/hits/direct status roots. IsSet distinguishes intentional unknown ancestry from Unity default inline objects; legacy saves use only their recorded direct source and never infer nearby parents.',
                  player_implementation='Accumulate the resolved official AbsoluteDamage independently of actual HP loss. Never add shield values, overflow, reductions or mark source-credit again. Count enemy resilience positive-to-zero transitions once before their configured response. Preserve separate ordinary/abnormal debuff application counters for the original 5/5/10/1 formula, including known-caster applications without a root skill. Newly begun units and summons start complete; restored legacy histories are never promoted to complete by later events.',
                  grouping_implementation='Snapshot MasterId and CharacterType into report units. Player/NPC records retain roster order; typed summons follow recorded summon chains, including the pet category for pet-created summons and their summon descendants. Dead/ended units remain included. Unresolved ownership, cycles, cross-side chains and unconfirmed proxy categories remain explicit; the view adapter rejects incomplete groups rather than manufacturing complete team ratios.',
                  persistence='Serialize raw counts, HasAbsoluteDamageTaken, HasBreakResilienceCount, HasStatusTypeCounts, HasDamageHitCounts, HasCureHitCounts, HasStatusApplicationCounts, HasShieldGrantCounts, pending status applications and pending shield-grant bits. Missing historical counters remain explicitly unavailable; never estimate them from legacy Hits, active statuses or remaining shields. An incomplete caster shield history also marks its distribution denominator unavailable.',
                  pending='The original numeric row has a reusable binding adapter; production entry, icon/detail providers, full passive/status source mapping and complete report fields remain incomplete.')
    path = ROOT / 'assets/reference/battle-rules/report-statistics-source-manifest.json'
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        temporary.write_bytes((json.dumps(record, ensure_ascii=False, indent=2) + '\n').encode())
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)
    print(path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    build(parser.parse_args().source_root)
