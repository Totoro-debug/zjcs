"""Compile unchanged official profession predicates in a marked validation project."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import prepare_battle_damage_reference as source


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--record', action='store_true')
    args = parser.parse_args()
    project = args.project.resolve()
    workspace = next((p for p in project.parents if (p / '.zjcs-validation.json').is_file()), None)
    if (source.ROOT / 'output/validation').resolve() not in project.parents or workspace is None:
        raise ValueError('Use a marked isolated validation project.')
    marker = json.loads((workspace / '.zjcs-validation.json').read_text(encoding='utf-8-sig'))
    if marker['format'] != 1 or Path(marker['repository']).resolve() != source.ROOT:
        raise ValueError('Wrong validation workspace.')
    signatures = {
        'ProfessionBaseInfoParser': ['public bool IsSkillAvailableForProfession(', 'public bool IsSkillAvailableForSingleProfession('],
        'ProfessionUtils': ['public static Profession GetSafeProfession(', 'private static Profession GetProfessionByWeaponType('],
    }
    fixture = source.ROOT / 'scripts/fixtures/skill_profession_reference.cs'
    template = fixture.read_text(encoding='utf-8-sig').replace('\r\n', '\n')
    generated = template.replace('/* ORIGINAL_PROFESSION */', source.original('Profession').replace('namespace Common;', ''))
    for name, methods in signatures.items():
        generated = generated.replace('/* ORIGINAL_' + name + ' */', '\n'.join(source.method(name, sig) for sig in methods))
    source.original('PlayerProfessionDataEx')
    detail_source = source.CURRENT / 'official-code/client-source/SkillPanelSkillDetailDialog.cs'
    source.USED.add(detail_source)
    receipt = dict(manifest='884_156692',
                   assembly=source.identity(source.CURRENT / 'official-code/managed/decoded/Console.dll'),
                   official_code_receipt=source.identity(source.CURRENT / 'code-manifest.json'),
                   official_inputs=[source.identity(p) for p in sorted(source.USED)],
                   original_method_signatures=signatures,
                   fixture=dict(path=fixture.relative_to(source.ROOT).as_posix(),
                                sha256=hashlib.sha256(template.encode('utf-8')).hexdigest(), normalization='UTF-8 without BOM; LF'),
                   existing_inputs=[source.identity(source.ROOT / p) for p in (
                       'unity/Assets/Official/Character/character-progression.json',
                       'unity/Assets/Official/Skills/skill-runtime-rules.json')],
                   input_versions='Official profession code 884_156692; existing character and skill payloads retain their recorded provenance and are not replaced.',
                   boundary='Original four methods unchanged; adapters supply existing profession/item tables. PlayerProfessionDataEx confirms safe-weapon selection precedes the parser. No server maintenance switch is invented.')
    target = source.ROOT / 'assets/reference/battle-rules/skill-profession-source-manifest.json'
    if args.record:
        target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    elif json.loads(target.read_text(encoding='utf-8-sig')) != receipt:
        raise ValueError('Profession source receipt differs; inspect before recording.')
    output = project / 'Assets/Game/Editor/Skills/SkillProfessionReference.cs'
    output.write_text(generated, encoding='utf-8')
    print(json.dumps(dict(output=str(output), original_methods=4)))


if __name__ == '__main__':
    main()
