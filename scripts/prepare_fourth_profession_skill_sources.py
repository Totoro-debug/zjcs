"""Verify fourth-rank skill inputs using the shared APK, numeric and native dependency checks."""
import argparse
import json

from official_apk import ROOT
from prepare_third_profession_skill_sources import build, compact_receipt, identity


SPEC_DIRECTORY = ROOT / 'scripts/profession_skill_sources/fourth'
SPEC_FILES = {int(path.stem): path for path in sorted(SPEC_DIRECTORY.glob('*.json'))}
SKILLS = {item: json.loads(path.read_bytes()) for item, path in SPEC_FILES.items()}


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--item', type=int, choices=SKILLS, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    result = compact_receipt(build(args.item, SKILLS[args.item], (4, 8, 13, 17)))
    result['entry_generator'] = identity(ROOT / 'scripts/prepare_fourth_profession_skill_sources.py')
    result['source_spec'] = identity(SPEC_FILES[args.item])
    target = ROOT / f'assets/reference/battle-rules/fourth-profession-{args.item}-source-manifest.json'
    if args.write:
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    else:
        assert json.loads(target.read_bytes()) == result, 'Fourth-profession source receipt changed'
    print(f'Current official source verified: {args.item}, {len(result["fragments"])} EC fragments, '
          f'{len(result["native_bundles"])} native dependency bundles')


if __name__ == '__main__':
    main()
