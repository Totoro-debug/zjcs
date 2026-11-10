"""Resolve official skill entities to their serialized battle Playable prefabs."""
import json
import re
from collections import defaultdict
from hashlib import sha256
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXPORT = (ROOT / '.tools/source-packages/current/884_156692/native-conversion'
          / 'native-export-all/ExportedProject/Assets/Prefabs/EcEntity/Fight/FightSkill')
SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source/MyFightSkill.cs'
EXPORT_RECEIPT = EXPORT.parents[6] / 'native-export-all-source-manifest.json'
FIELDS = ('myPlayablePrefab', 'UpPlayablePrefab', 'DownPlayablePrefab',
          'LeftPlayablePrefab', 'RightPlayablePrefab')
SERIALIZED_MY_FIGHT_SKILL_FILE_ID = 1725120999


def script_file_id():
    text = SOURCE.read_text(encoding='utf-8-sig')
    namespace = re.search(r'(?m)^namespace ([\w.]+)', text)
    if namespace is not None:
        raise ValueError('Unexpected official MyFightSkill source namespace.')
    return SERIALIZED_MY_FIGHT_SKILL_FILE_ID


def ordinary_movement_configuration(skills):
    rules_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    receipt_path = ROOT / 'assets/reference/battle-rules/active-entity-configuration-source-manifest.json'
    rules = json.loads(rules_path.read_bytes())['entities']
    receipt = json.loads(receipt_path.read_bytes())
    if receipt['manifest'] != '884_156692':
        raise ValueError('Ordinary movement configuration must use the selected official audit.')
    audited = {row['entity_id']: row for row in receipt['rows']}
    rows = []
    for skill in skills:
        if not skill['entity_prefab'].startswith('Move/'):
            continue
        key = skill['skill_id']
        digest = sha256(json.dumps(rules[str(key)], ensure_ascii=False, sort_keys=True,
            separators=(',', ':')).encode('utf-8')).hexdigest()
        row = audited.get(key)
        if row is None or row['changed'] or row['installed_components_sha256'] != digest \
                or row['selected_components_sha256'] != digest:
            raise ValueError('Review ordinary movement configuration against the selected source: ' + str(key))
        rows.append({'entity_id': key, 'components_sha256': digest})
    if len(rows) != 22:
        raise ValueError('Review the original ordinary movement skill membership.')
    return {'audit': {'path': receipt_path.relative_to(ROOT).as_posix(),
                      'sha256': sha256(receipt_path.read_bytes()).hexdigest()}, 'rows': rows}


def main():
    export_receipt = json.loads(EXPORT_RECEIPT.read_text(encoding='utf-8'))
    verified = {row['path']: row['sha256'] for row in export_receipt['outputs']}
    guid_paths = defaultdict(list)
    for metadata in EXPORT.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', metadata.read_text(encoding='utf-8-sig'))
        if match:
            guid_paths[match[1]].append(metadata.with_suffix('').relative_to(EXPORT).as_posix())

    component_id = script_file_id()
    skills = []
    entity_ids = set()
    # Ordinary movement skills retain the same original MyFightSkill view in
    # the separate Move subtree. Include their complete entities, not just the
    # effect-only projections used by the earlier movement renderer.
    entity_paths = list(EXPORT.glob('[0-9]*/[0-9]*.prefab'))
    entity_paths.extend((EXPORT / 'Move').glob('[0-9]*/[0-9]*.prefab'))
    for path in sorted(entity_paths):
        # Numeric directories group a skill with its children. Each numeric
        # prefab names its own entity (10160/10163 is entity 10163).
        skill_id = int(path.stem)
        raw = path.read_bytes()
        digest = sha256(raw).hexdigest()
        if digest != verified[path.relative_to(EXPORT.parents[5]).as_posix()]:
            raise ValueError(f'Official skill entity differs from its export receipt: {path}')
        text = path.read_text(encoding='utf-8-sig')
        if not re.search(rf'(?m)^  m_Name: {skill_id}$', text):
            raise ValueError(f'Official skill entity name does not match its prefab: {path}')
        matches = []
        for component in re.finditer(
                r'(?ms)^--- !u!114 &(?P<id>\d+)\nMonoBehaviour:\n(?P<body>.*?)(?=^--- !u!|\Z)', text):
            body = component['body']
            if not re.search(rf'(?m)^  m_Script: \{{fileID: {component_id},', body):
                continue
            refs = {}
            for field in FIELDS:
                ref = re.search(rf'(?m)^  {field}: \{{fileID: (\d+), guid: ([a-f0-9]{{32}}), type: (\d+)\}}$', body)
                if ref:
                    refs[field] = {'file_id': int(ref[1]), 'guid': ref[2], 'type': int(ref[3])}
            matches.append(refs)
        if not matches:
            continue
        if len(matches) != 1:
            raise ValueError(f'Expected one official MyFightSkill component in {path}, found {len(matches)}.')
        if skill_id in entity_ids:
            raise ValueError(f'Official skill entity ID is duplicated: {skill_id}')
        entity_ids.add(skill_id)
        resolved = {}
        for field, ref in matches[0].items():
            candidates = guid_paths.get(ref['guid'], [])
            if len(candidates) == 1:
                ref['path'] = candidates[0]
            else:
                ref['candidates'] = candidates
            resolved[field] = ref
        skills.append({'skill_id': skill_id, 'entity_prefab': path.relative_to(EXPORT).as_posix(),
                       'entity_prefab_sha256': digest,
                       'playables': resolved})

    referenced = {ref['path'] for skill in skills for ref in skill['playables'].values() if 'path' in ref}
    result = {
        'manifest': '884_156692',
        'source': SOURCE.relative_to(ROOT).as_posix(),
        'source_sha256': sha256(SOURCE.read_bytes()).hexdigest(),
        'native_export_manifest': EXPORT_RECEIPT.relative_to(ROOT).as_posix(),
        'native_export_manifest_sha256': sha256(EXPORT_RECEIPT.read_bytes()).hexdigest(),
        'native_exported_files': len(export_receipt['outputs']),
        'skills': skills,
        'unique_playable_prefabs': sorted(referenced),
        'binding_audit': {'path': Path(__file__).relative_to(ROOT).as_posix(),
                          'sha256': sha256(Path(__file__).read_bytes()).hexdigest()},
        'ordinary_movement_configuration': ordinary_movement_configuration(skills),
    }
    print(json.dumps(result, ensure_ascii=False, separators=(',', ':')))


if __name__ == '__main__':
    main()
