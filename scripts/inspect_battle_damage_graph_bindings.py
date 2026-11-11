"""Resolve the selected original damage entities and their five authored graph fields."""
import json
import re
import struct
import subprocess
from collections import defaultdict
from hashlib import sha256
from pathlib import Path

from inspect_battle_hit_graph_bindings import CLIENT, MD4
from inspect_battle_skill_graph_bindings import EXPORT, EXPORT_RECEIPT, ROOT
from prepare_battle_ground_effect_assets import EXPORTED

FIELDS = ('myCommonPlayable', 'myDamagePlayable', 'myBlockPlayable',
          'myCritPlayable', 'myTriggerStatusPlayable')
FLAGS = dict(myShowZeroDamage=True, myHideDamage=False)


def inspect():
    proof = json.loads(EXPORT_RECEIPT.read_bytes())
    if proof['manifest'] != '884_156692':
        raise ValueError('Review original damage entity source version.')
    verified = {row['path']: row['sha256'] for row in proof['outputs']}
    source = CLIENT / 'MyFightDamage.cs'
    original = source.read_text(encoding='utf-8-sig')
    for name, default in FLAGS.items():
        declaration = re.search(rf'public bool {name}(?: = (true|false))?;', original)
        if declaration is None or (declaration[1] == 'true') != default:
            raise ValueError('Review original damage flag default: ' + name)
    file_id = struct.unpack('<i', MD4.new(b's\0\0\0MyFightDamage').digest()[:4])[0]
    paths = defaultdict(list)
    for path in EXPORT.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.read_text(encoding='utf-8-sig'))
        if match:
            paths[match[1]].append(path.with_suffix('').relative_to(EXPORTED).as_posix())

    def read(path):
        raw = path.read_bytes()
        if sha256(raw).hexdigest() != verified[path.relative_to(EXPORT.parents[5]).as_posix()]:
            raise ValueError('Changed original damage prefab input: ' + str(path))
        return raw

    damages = []
    complete_index = False
    # Global contains shared damage entities such as 10004; it must be scanned
    # alongside numeric skill folders.
    for path in sorted(EXPORT.glob('*/[0-9]*.prefab')):
        raw = read(path)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        components = []
        for component in re.finditer(
                r'(?ms)^--- !u!114 &(?P<id>\d+)\nMonoBehaviour:\n(?P<body>.*?)(?=^--- !u!|\Z)', text):
            body = component['body']
            if not re.search(rf'(?m)^  m_Script: \{{fileID: {file_id},', body):
                continue
            graphs = {}
            for field in FIELDS:
                reference = re.search(rf'(?m)^  {field}: \{{fileID: (\d+), guid: ([a-f0-9]{{32}}), type: (\d+)\}}$', body)
                if reference:
                    matches = paths.get(reference[2], [])
                    if not matches and not complete_index:
                        # Some original damage graphs live outside FightSkill.
                        # Resolve their original GUID across the complete native
                        # export, retaining strict uniqueness and export digests.
                        found = subprocess.run(['rg', '--hidden', '--no-ignore',
                            '--with-filename', '--no-heading', '--color', 'never',
                            '--max-count', '1', '--glob', '*.meta',
                            r'^guid: [a-f0-9]{32}\r?$', str(EXPORTED)],
                            capture_output=True, text=True, encoding='utf-8')
                        if found.returncode not in (0, 1):
                            raise RuntimeError(found.stderr)
                        paths.clear()
                        for value in found.stdout.splitlines():
                            filename, guid = value.rsplit(':guid: ', 1)
                            paths[guid.strip()].append(Path(filename).with_suffix('').relative_to(EXPORTED).as_posix())
                        complete_index = True
                        matches = paths.get(reference[2], [])
                    if len(matches) != 1:
                        raise ValueError(f'Unresolved original damage graph {path.stem}:{field}: {matches}')
                    graph = EXPORTED / matches[0]
                    read(graph)
                    read(graph.with_name(graph.name + '.meta'))
                    graphs[field] = dict(file_id=int(reference[1]), guid=reference[2], path=matches[0])
                elif not re.search(rf'(?m)^  {field}: \{{fileID: 0\}}$', body):
                    raise ValueError('Review serialized original damage graph field: ' + field)
            flags = {}
            for field, default in FLAGS.items():
                value = re.search(rf'(?m)^  {field}: ([01])$', body)
                flags[field] = bool(int(value[1])) if value else default
            components.append(dict(damage_entity_id=int(path.stem),
                entity_prefab=path.relative_to(EXPORT).as_posix(),
                entity_prefab_sha256=sha256(raw).hexdigest(),
                component_file_id=int(component['id']),
                game_object_file_id=int(re.search(r'(?m)^  m_GameObject: \{fileID: (\d+)\}$', body)[1]),
                component_enabled=bool(int(re.search(r'(?m)^  m_Enabled: ([01])$', body)[1])),
                flags=flags, playables=graphs))
        if len(components) > 1:
            raise ValueError('Multiple original damage views require explicit component ordering.')
        damages.extend(components)
    # Non-addressed shared graphs have no assetBundleName in their native meta.
    # Follow the actual serialized pointer on a referencing original damage
    # component, rather than inferring a bundle from the exported graph name.
    from current_component_schemas import CurrentSchemas
    from official_apk import Apk
    from prepare_battle_pet_followers import identity_object
    from UnityPy.classes import PPtr
    schemas, apk = CurrentSchemas(), Apk.current()
    graph_originals, component_originals = {}, []
    try:
        for row in damages:
            unresolved = {field: reference for field, reference in row['playables'].items()
                if not reference['path'].startswith('Prefabs/EcEntity/Fight/FightSkill/')
                and reference['path'] not in graph_originals}
            if not unresolved:
                continue
            entity_meta = EXPORT / (row['entity_prefab'] + '.meta')
            bundle_name = re.search(r'(?m)^  assetBundleName: ([^\s\r\n]+)', read(entity_meta).decode())
            if not bundle_name:
                raise ValueError('Resolve original referencing damage entity bundle: ' + str(entity_meta))
            candidates = []
            for component in apk.objects(bundle_name[1] + '.bundle'):
                if component.type.name != 'MonoBehaviour':
                    continue
                value = component.read(check_read=False)
                if (value.m_Script.read().m_ClassName == 'MyFightDamage'
                        and value.m_GameObject.read().m_Name == str(row['damage_entity_id'])):
                    candidates.append(component)
            if len(candidates) != 1:
                raise ValueError('Original damage component pointer is ambiguous: ' + str(row['damage_entity_id']))
            component = candidates[0]
            fields = schemas.read(component)
            component_originals.append(dict(damage_entity_id=row['damage_entity_id'],
                component=identity_object(apk, component)))
            for field, reference in unresolved.items():
                graph = PPtr(**fields[field], assetsfile=component.assets_file).deref()
                if graph.read().m_Name != Path(reference['path']).stem:
                    raise ValueError('Original damage graph pointer differs from its native export: ' + reference['path'])
                graph_originals[reference['path']] = identity_object(apk, graph)
    finally:
        apk.archive.close()
    return dict(manifest=proof['manifest'], sources=[dict(path=source.relative_to(ROOT).as_posix(),
        sha256=sha256(source.read_bytes()).hexdigest(), file_id=file_id)],
        graph_originals=graph_originals, original_damage_components=component_originals,
        component_schemas=schemas.records,
        native_export_receipt=dict(path=EXPORT_RECEIPT.relative_to(ROOT).as_posix(),
            sha256=sha256(EXPORT_RECEIPT.read_bytes()).hexdigest()), damages=damages)


if __name__ == '__main__':
    print(json.dumps(inspect(), ensure_ascii=False, separators=(',', ':')))
