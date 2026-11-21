"""Verify fourth-rank skill inputs using the shared APK, numeric and native dependency checks."""
import argparse
import json

from official_apk import ROOT
from prepare_third_profession_skill_sources import build, compact_receipt, identity


SKILLS = {
    10047: dict(entities=[10880, 10881, 10024],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo',
                      'FightDamageComponentInfo', 'BattleFormulaHandler']),
    10082: dict(entities=[11980, 11981, 11982, 11983, 11984, 24377, 13580, 13581, 13582, 24659],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo',
                      'FightDamageComponentInfo', 'FightDamageLimitComponentInfo',
                      'DamageStatusCfg', 'FightStatusComponentInfo',
                      'FightStatusShieldPersistentComponentInfo', 'BattleFormulaHandler']),
    10083: dict(entities=[11990, 11991, 11992, 24378, 12000, 12001, 10026],
                native_receipts=['assets/reference/battle-animation/profession-status-assets-source-manifest.json'],
                client_code=['MyEntity', 'MyAction', 'MyFightStatus', 'MyFightStatusLogic',
                             'MyPlayableOrbitMovement', 'ApplyByStatusStackCount', 'MyPlayableFollowBone',
                             'ApplyByGroundType', 'ApplyOnStopFadeOut'],
                framework_code=['MyPlayableSequence', 'ApplyEnableOnPlay'],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'FightStatusComponentInfo', 'FightStatusLogicComponentInfo',
                      'FightOnAfterBeDamageTriggerCfg', 'FightHandleCfgTriggerSkills',
                      'FightHandleCfgChangeStatusStack', 'DamageResultTypeFilter',
                      'DamageSkillCanTriggerStatusFilter', 'ESkillTryAtStartType', 'BattleFormulaHandler',
                      'FightHandleCfgBBEcId2EcEntityAndStore', 'FightHandleCfgFireTriggerStatus',
                      'FightHandleCfgNewIntInBB', 'EntityHasActionTypeByEntityKeyFilter', 'LogicFilter',
                      'LogicNotFilter', 'IteratingAgentStatusClassIdFilter', 'StatusTriggerTarget',
                      'FightStatusTriggerSkillCfg', 'EFightBlackBoardKeyValue', 'FightPort', 'DamageResultType']),
    10084: dict(entities=[11890, 11891, 11892, 24216],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'FightStatusComponentInfo', 'DamageStatusCfg', 'StatusAutoRemoveComponentInfo',
                      'SkillAiCfg', 'AiPriorityType', 'BattleFormulaHandler']),
    10085: dict(entities=[11900, 11901, 11902, 11903, 10026],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo',
                      'FightDamageComponentInfo', 'BattleFormulaHandler']),
}


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--item', type=int, choices=SKILLS, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    result = compact_receipt(build(args.item, SKILLS[args.item], (4, 8, 13, 17)))
    result['entry_generator'] = identity(ROOT / 'scripts/prepare_fourth_profession_skill_sources.py')
    target = ROOT / f'assets/reference/battle-rules/fourth-profession-{args.item}-source-manifest.json'
    if args.write:
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    else:
        assert json.loads(target.read_bytes()) == result, 'Fourth-profession source receipt changed'
    print(f'Current official source verified: {args.item}, {len(result["fragments"])} EC fragments, '
          f'{len(result["native_bundles"])} native dependency bundles')


if __name__ == '__main__':
    main()
