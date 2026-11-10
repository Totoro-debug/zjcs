"""Verify directional skill inputs against the selected APK; retain legacy identities."""
import hashlib
import json
import re
from pathlib import Path
from verify_battle_skill_graph_rules import verify_selected_components
from official_apk import Apk
from official_tables import Constructors

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    apk = Apk.current()
    if apk.version != '884_156692':
        raise ValueError('Review the selected directional skill source version')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    parser = Constructors(code)
    mapping = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
    runtime = ROOT / 'unity/Assets/Official/Combat/rules.json'
    rules = json.loads(runtime.read_bytes())
    professions = {1, 10, 2, 6, 11, 15}
    rows = [row for row in rules['profession_base'] if row['Profession'] in professions]
    wanted = {item for row in rows for item in row['Skills'] + row['FightUsedSkills']}
    originals = {}
    try:
        for name, key, ids in [('profession_base', 'Profession', professions), ('skill', 'ClassId', wanted), ('item', 'ClassId', wanted)]:
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            selected = json.loads(json.dumps([row for row in parser.table(raw, mapping[name]) if row[key] in ids]))
            migrated = [row for row in rules[name] if row[key] in ids]
            if sorted(selected, key=lambda row: row[key]) != sorted(migrated, key=lambda row: row[key]):
                raise ValueError('Selected original table differs from runtime: ' + name)
            if len(selected) != len(ids):
                raise ValueError('Incomplete selected original table: ' + name)
            originals[name] = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest(), rows=selected)
    finally:
        apk.archive.close()
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((runtime.parent / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    skill = next(row for row in rules['skill'] if row['ClassId'] == 10026)
    entity_ids = {skill['EcEntityId'], *skill['DirectionEcEntityId'].values()}
    definitions = {}
    pending = list(entity_ids)
    while pending:
        key = str(pending.pop())
        if key in definitions:
            continue
        definitions[key] = entities[key]
        for component in entities[key].values():
            for link in component.get('HitList', []):
                pending.append(link['ClassId'])
    verification = verify_selected_components(definitions, [skill])
    sources = [identity(code / name) for name in [
        'common-source/Common/FightSkillData.cs', 'common-source/Common/SkillEntityData.cs',
        'common-source/Common/Pos2d.cs', 'common-source/Common/Direction.cs', 'common-source/Common/SkillInfo.cs']]
    record = dict(manifest=apk.version, official_tables=originals, official_code=sources,
        runtime_input=identity(runtime), selected_configuration=verification,
        generator=identity(Path(__file__)),
        operation='APK-first full-field verification of six professions and 104 direct skill/item rows. '
        'Verify default and three directional skill entities and their direct hit dependencies. '
        'Original dictionary construction, per-entity properties and Pos2d.LookDirection are reused; '
        'target-based directional selection is a user-confirmed offline rule, not recovered server code.')
    target = ROOT / 'assets/reference/battle-rules/directional-skill-source-manifest.json'
    target.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified six professions, 104 direct skills/items and directional skill/hit configuration.')


if __name__ == '__main__':
    main()
