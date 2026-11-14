"""Verify official equipped-passive selection sources and the existing runtime inputs."""
from __future__ import annotations

import argparse
import json

import prepare_battle_damage_reference as source


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--record', action='store_true')
    args = parser.parse_args()
    names = ('PlayerSkillDataExt', 'PlayerSnapshotData', 'ProfessionBaseInfoParser',
             'ProfessionUtils', 'FightUtils', 'FightUtilsHandler')
    for name in names:
        source.original(name)
    receipt = dict(
        manifest='884_156692',
        assembly=source.identity(source.CURRENT / 'official-code/managed/decoded/Console.dll'),
        official_code_receipt=source.identity(source.CURRENT / 'code-manifest.json'),
        official_inputs=[source.identity(path) for path in sorted(source.USED)],
        methods=['PlayerSkillDataExt.GetValidSkillItems', 'PlayerSnapshotData.TryGetValidSkillItems',
                 'PlayerSnapshotData.CheckSkillProfession', 'ProfessionBaseInfoParser.IsSkillAvailableForProfession',
                 'ProfessionBaseInfoParser.IsSkillAvailableForSingleProfession', 'ProfessionUtils.GetSafeProfession',
                 'FightUtilsHandler.IsSkillDisabled'],
        existing_inputs=[source.identity(source.ROOT / path) for path in (
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/skill-runtime-rules.json')],
        input_versions='Official selection code 884_156692; existing payloads retain their recorded provenance.',
        scope='46 passive items directly listed by professions 1,10,2,6,11,15; original PrePro inheritance and weapon fallback.',
        boundary='Player ownership replaces original equipped item-reference lookup. Live UseSkillItem maintenance disabling is a server operational switch, not a new local skill condition. No server list or network request is introduced.',
    )
    target = source.ROOT / 'assets/reference/battle-rules/skill-passive-loadout-source-manifest.json'
    if args.record:
        target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    elif json.loads(target.read_text(encoding='utf-8-sig')) != receipt:
        raise ValueError('Passive loadout source identity differs; inspect before recording.')
    print('Official passive loadout sources verified: 6 classes, 2 existing runtime inputs.')


if __name__ == '__main__':
    main()
