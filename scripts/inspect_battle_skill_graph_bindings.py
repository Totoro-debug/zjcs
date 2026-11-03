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


def main():
    guid_paths = defaultdict(list)
    for metadata in EXPORT.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', metadata.read_text(encoding='utf-8-sig'))
        if match:
            guid_paths[match[1]].append(metadata.with_suffix('').relative_to(EXPORT).as_posix())

    component_id = script_file_id()
    skills = []
    for path in sorted(EXPORT.glob('[0-9]*/[0-9]*.prefab')):
        skill_id = int(path.parent.name)
        text = path.read_text(encoding='utf-8-sig')
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
        resolved = {}
        for field, ref in matches[0].items():
            candidates = guid_paths.get(ref['guid'], [])
            if len(candidates) == 1:
                ref['path'] = candidates[0]
            else:
                ref['candidates'] = candidates
            resolved[field] = ref
        skills.append({'skill_id': skill_id, 'entity_prefab': path.relative_to(EXPORT).as_posix(),
                       'entity_prefab_sha256': sha256(path.read_bytes()).hexdigest(),
                       'playables': resolved})

    referenced = {ref['path'] for skill in skills for ref in skill['playables'].values() if 'path' in ref}
    export_receipt = json.loads(EXPORT_RECEIPT.read_text(encoding='utf-8'))
    result = {
        'manifest': '884_156692',
        'source': SOURCE.relative_to(ROOT).as_posix(),
        'source_sha256': sha256(SOURCE.read_bytes()).hexdigest(),
        'native_export_manifest': EXPORT_RECEIPT.relative_to(ROOT).as_posix(),
        'native_export_manifest_sha256': sha256(EXPORT_RECEIPT.read_bytes()).hexdigest(),
        'native_exported_files': len(export_receipt['outputs']),
        'skills': skills,
        'unique_playable_prefabs': sorted(referenced),
    }
    print(json.dumps(result, ensure_ascii=False, separators=(',', ':')))


if __name__ == '__main__':
    main()
