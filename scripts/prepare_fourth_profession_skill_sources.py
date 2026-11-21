"""Verify fourth-rank skill inputs using the shared APK, numeric and native dependency checks."""
import argparse
import json

from official_apk import ROOT
from prepare_third_profession_skill_sources import build, compact_receipt, identity


SKILLS = {
    10047: dict(entities=[10880, 10881, 10024],
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
