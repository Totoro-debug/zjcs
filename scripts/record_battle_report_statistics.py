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
            'common-source/Common/FightReportComponent.cs',
            'common-source/Common/BattleReportCollector.cs',
            'client-source/FightEndReportSkillTabViewItem.cs',
            'client-source/FightEndReportSkillTabView.cs',
            'client-source/CharacterDamageUIManager.cs']
    runtime = ['unity/Assets/Game/Runtime/Official/Combat/' + name + '.cs' for name in
               ['FightBattleExecutor', 'FightBattleStatistics', 'FightBattleReportBuilder',
                'FightBattleState', 'FightStatusLifecycle', 'FightStatusPropertyRuntime']]
    runtime += ['unity/Assets/Game/Editor/Combat/OfficialBattleReportStatisticsAcceptance.cs',
                'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs',
                'scripts/test_battle_report_averages.ps1', 'scripts/test_battle_report_shields.ps1']
    record = dict(manifest='854_156383', generator_sha256=sha(Path(__file__)),
                  official_code={p: sha(official / p) for p in code},
                  runtime={p: sha(ROOT / p) for p in runtime},
                  evidence='The client consumes AvgDamageCount and AvgDamageCountPerTarget; shared PlayerFightSkillReport merges supplied values by cast-weighted averages. BattleReportCollector and FightReportComponent contain no single-battle counter implementation.',
                  user_confirmed=dict(numerator='H: damage hits, including immunity with zero damage and full shield absorption; excluding dodge.',
                                      denominators='C: casts; T: sum of distinct damage-hit targets within each cast. Average hit count H/C; average segments H/T.',
                                      example='10 casts, 2 targets and 3 segments each: H=60, T=20, averages 6 and 3.',
                                      rounding='User example: 18 hits / 10 casts = 1.8, displayed as 2. Presentation formatting is still pending.',
                                      shield='Effective shield is the numeric shield successfully added to targets, not later damage absorption.',
                                      distributions='Each skill effective damage/healing/shield divided by the sum of the corresponding effective values across all skills of that caster.',
                                      hp_ratios='Each skill effective healing/shield divided by the caster maximum HP.'),
                  implementation='Each ExecuteSkill invocation owns its target set, so same-skill recursive replay starts another denominator. Healing, HP-cost records, original blinding/miss and no-damage actions do not enter damage-hit counters. Extra mark attribution creates no additional hit.',
                  shield_implementation='A new numeric-shield lifecycle instance is pending until the original property rebuild initializes its configured/custom shield value. Attribute that successful grant to CreatorId/RootSkillId/ApplyingSkillItemId exactly once. Existing discard/stack updates and restored shields do not manufacture grants. Shield consumption never reduces granted statistics.',
                  persistence='Serialize raw counts, HasDamageHitCounts, HasShieldGrantCounts and pending shield-grant bits. Missing historical counters remain explicitly unavailable; never estimate them from legacy Hits or remaining shields. An incomplete caster shield history also marks its distribution denominator unavailable.',
                  pending='Original report row binding, display rounding, production entry, full passive/status source mapping, status counts and complete report fields remain incomplete.')
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
