"""Verify the selected original passive skill-end configuration and shared input code."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import verify_selected_components, identity

ROOT = Path(__file__).resolve().parents[1]

def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill = next(row for row in rules['skill'] if row['ClassId'] == 10564)
    verified = verify_selected_components({str(key): entities[str(key)] for key in (25029, 24221)}, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    inputs = [identity(code / name) for name in (
        'common-source/Common/FightStatusSkillEndComponent.cs',
        'common-source/Common/FightStatusSkillEndComponentInfo.cs',
        'common-source/Common/ActionType.cs', 'client-source/MyFightStatusSkillEnd.cs')]
    record = dict(manifest='884_156692', configuration=verified, official_code=inputs,
        generator=identity(Path(__file__)),
        operation='APK-first exact field verification. The legacy StatusEntClassId recipient and '
        'counter behavior are user-confirmed offline decisions maintained in docs/features/skills.md.')
    (ROOT / 'assets/reference/battle-rules/skill-end-source-manifest.json').write_text(
        json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10564 -> 25029 -> 24221 and shared skill-end input code.')

if __name__ == '__main__':
    main()
