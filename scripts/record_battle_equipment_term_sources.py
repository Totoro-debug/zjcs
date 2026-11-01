"""Record the original equipment replacement formula and its typed battle binding."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CURRENT = ROOT / '.tools/source-packages/current/884_156692'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())


def main():
    receipt = CURRENT / 'code-manifest.json'
    code = json.loads(receipt.read_bytes())
    assembly = CURRENT / 'official-code/managed/decoded/Console.dll'
    assert code['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == code['assemblies']['Console.dll']['decoded_sha256']
    source = CURRENT / 'official-code/common-source/Common'
    proof = dict(
        code_manifest=code['manifest'], configuration_manifest='854_156383',
        code_receipt=identity(receipt), assembly=identity(assembly),
        original=[identity(source / (name + '.cs')) for name in (
            'BattleFormulaHandler', 'EquipAttributesPassiveSkillInfo', 'EquipAttributeConfig',
            'EquipAttributeBlessScaleInfoParser', 'ValueType')],
        inputs=[identity(ROOT / name) for name in (
            'unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json',
            'unity/Assets/Official/Equipment/Runtime/source-manifest.json',
            'unity/Assets/Official/Equipment/UI/equipment-ui.json',
            'unity/Assets/Official/Equipment/UI/source-manifest.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json')],
        adapter=[identity(ROOT / name) for name in (
            'unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentCatalog.cs',
            'unity/Assets/Game/Runtime/Offline/Equipment/OfficialEquipmentCatalog.TermReplacement.cs',
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs')],
        validation=identity(ROOT / 'scripts/test_battle_equipment_replay.ps1'),
        recorder=identity(Path(__file__).resolve()),
        operation='Port original CalcEquipTermsReplacePropDictByPropGroupIndices and '
            'GetEquipAttributePassiveSkillProp: retain property indices, Long/Float fixed-point types, '
            'rank-gated blessing, missing-factor fallback and truncation. Pass replacements through '
            'passive skill bindings to original replay status configuration. No quality multiplier is '
            'introduced into this direct replacement formula. Equipped-term selection and formal '
            'gameplay-host integration are not yet connected.')
    target = ROOT / 'assets/reference/battle-rules/equipment-term-source-manifest.json'
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recorded original equipment formula, historical payload and typed battle binding identities.')


if __name__ == '__main__':
    main()
