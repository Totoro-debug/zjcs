"""Generate an isolated Unity comparison using unmodified official skill-rating methods."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import prepare_battle_damage_reference as source

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / 'scripts/fixtures/skill_combat_rating_reference.cs'
RECEIPT = ROOT / 'assets/reference/battle-rules/skill-combat-rating-source-manifest.json'
SIGNATURES = (
    'public long CalcCombatRatingSkill(', 'public float GetSkillBaseScore(',
    'public long CalcCombatRatingSkillQuality(', 'public float CalcSkillQualityAttack(',
    'public long CalcCombatRatingSkillLevel(', 'public float CalcSkillLevelCombatRating(',
    'public long CalcCombatRatingByProps(', 'public void CalcPlayerRefBaseProp(',
    'public void HandlePropRate(', 'public double GetProp(', 'public long GetRawProp(',
    'public double Long2Double(long val)',
)


def normalized_identity(path: Path) -> dict:
    data = path.read_text(encoding='utf-8-sig').replace('\r\n', '\n').encode('utf-8')
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(data).hexdigest(),
                normalization='UTF-8 without BOM; LF (versioned comparison fixture only)')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True, help='Existing isolated validation Unity project')
    parser.add_argument('--record', action='store_true')
    args = parser.parse_args()
    project = args.project.resolve()
    validation = (ROOT / 'output/validation').resolve()
    workspace = next((parent for parent in project.parents if (parent / '.zjcs-validation.json').is_file()), None)
    if validation not in project.parents or workspace is None or not (project / 'ProjectSettings/ProjectVersion.txt').is_file():
        raise ValueError('The reference may only be generated in a marked isolated validation project.')
    marker = json.loads((workspace / '.zjcs-validation.json').read_text(encoding='utf-8-sig'))
    if Path(marker['repository']).resolve() != ROOT or marker['format'] != 1:
        raise ValueError('Validation workspace marker differs from this repository.')
    enums = '\n'.join(source.original(name).replace('namespace Common;', '').replace('[NotCheckFightResult]', '')
                      for name in ('PropType', 'SubRank', 'Profession'))
    methods = '\n'.join(source.method('BattleFormulaHandler', signature) for signature in SIGNATURES)
    template = FIXTURE.read_text(encoding='utf-8-sig')
    generated = template.replace('/* ORIGINAL_ENUMS */', enums).replace('/* ORIGINAL_METHODS */', methods)
    generated = generated.replace('/* ORIGINAL_LONG_TO_DOUBLE */', source.method('Formula', 'public static double Long2Double(long val)'))
    availability_signatures = ('public bool IsSkillAvailableForProfession(', 'public bool IsSkillAvailableForSingleProfession(')
    generated = generated.replace('/* ORIGINAL_AVAILABILITY */', '\n'.join(
        source.method('ProfessionBaseInfoParser', signature) for signature in availability_signatures))
    source.original('SkillPropChunck')
    inputs = [source.identity(path) for path in sorted(source.USED)]
    receipt = dict(manifest='884_156692', operation='Compare the offline character skill rating against unchanged official methods.',
                   assembly=source.identity(source.CURRENT / 'official-code/managed/decoded/Console.dll'),
                   official_code_receipt=source.identity(source.CURRENT / 'code-manifest.json'),
                   official_inputs=inputs, original_method_signatures=list(SIGNATURES),
                   original_availability_method_signatures=list(availability_signatures),
                   original_formula_method_signatures=['public static double Long2Double(long val)'],
                   existing_character_inputs=[source.identity(ROOT / name) for name in (
                       'unity/Assets/Official/Character/character-progression.json',
                       'unity/Assets/Official/Character/character-overview.json',
                       'unity/Assets/Official/Skills/skill-runtime-rules.json')],
                   input_versions='884_156692 official formula; existing character/skill catalog payloads keep their recorded 854_156383 provenance and are not replaced.',
                   comparison_fixture=normalized_identity(FIXTURE),
                   adapter_scope='Character table lookups and item wrappers only; original scoring, sorting, float truncation and rate passes remain unchanged.')
    if args.record:
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    elif json.loads(RECEIPT.read_text(encoding='utf-8-sig')) != receipt:
        raise ValueError('Skill rating source receipt differs; inspect sources before recording.')
    target = project / 'Assets/Game/Editor/Skills/SkillCombatRatingReference.cs'
    target.write_text(generated, encoding='utf-8')
    print(json.dumps(dict(output=str(target), methods=len(SIGNATURES) + len(availability_signatures) + 1, inputs=len(inputs))))


if __name__ == '__main__':
    main()
