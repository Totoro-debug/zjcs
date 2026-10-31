"""Decode the selected partner tables using that version's generated constructors."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors


TABLES = (
    'npc', 'npc_gift', 'npc_unlock_item', 'npc_friendship_level',
    'npc_friendship_tag', 'npc_profile', 'npc_talk', 'npc_talent_item',
    'npc_talent_tree_npc_map', 'npc_talent_tree_npc_map_prop',
    'function_limit', 'conditions', 'item', 'game_settings',
    'achievement', 'achievement_group',
)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode('utf8')


def metadata(path, folder):
    identity = path.relative_to(ROOT).as_posix()
    header = 'folderAsset: yes\nDefaultImporter:\n' if folder else 'TextScriptImporter:\n'
    return ('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-current-partner:' + identity).encode()).hexdigest()
            + '\n' + header + '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()


def validate(tables):
    npcs = {row['Id'] for row in tables['npc']}
    items = {row['ClassId'] for row in tables['item']}
    conditions = {row['ClassId']: row['Condition'] for row in tables['conditions']}
    used_conditions = set()

    def visit(value):
        if isinstance(value, list):
            for child in value:
                visit(child)
        elif isinstance(value, dict):
            if value.get('_type') in ('CompositeAndCondition', 'CompositeOrCondition'):
                for identity in value['Ids'] or []:
                    if identity not in conditions:
                        raise ValueError('Original condition is unavailable: ' + str(identity))
                    if identity not in used_conditions:
                        used_conditions.add(identity)
                        visit(conditions[identity])
            for child in value.values():
                visit(child)

    for name, rows in tables.items():
        visit(rows)
        if name in ('npc_gift', 'npc_unlock_item'):
            if any(row['ClassId'] not in items for row in rows):
                raise ValueError('Partner gift item definition is unavailable: ' + name)
        if name == 'npc_profile':
            if any(row['Id'] not in npcs for row in rows):
                raise ValueError('Partner table refers to an unavailable NPC: ' + name)
    absent_exclusive_items = {str(npc['Id']): [identity for identity in npc['ExclusiveGiftIds'] or [] if identity not in items]
                              for npc in tables['npc']}
    absent_exclusive_items = {npc: values for npc, values in absent_exclusive_items.items() if values}
    levels = set()
    for row in tables['npc_friendship_level']:
        key = (row['Id'], row['Level'])
        if key in levels:
            raise ValueError('Duplicate friendship threshold: ' + str(key))
        levels.add(key)
    # The official threshold table also contains dormant NPC IDs. Preserve them
    # without manufacturing roster entries or treating them as current partners.
    unused = sorted({row['Id'] for row in tables['npc_friendship_level']} - npcs)
    return dict(condition_references=len(used_conditions), friendship_thresholds=len(levels),
                unused_friendship_npc_ids=unused, absent_exclusive_item_definitions=absent_exclusive_items,
                npcs=len(npcs), items=len(items))


def compare_existing(tables):
    # Compare the serialized data model: JSON object keys are strings, whereas
    # generated Luban dictionaries initially carry their original integer keys.
    tables = json.loads(encoded(tables))
    path = ROOT / 'unity/Assets/Official/Character/character-progression.json'
    baseline = json.loads(path.read_bytes())
    old = baseline['rules']
    aliases = {'npc_talent_tree_npc_map': 'npc_talent_tree_map'}
    same = [name for name, rows in tables.items() if old.get(aliases.get(name, name)) == rows]
    old_npcs = {row['Id']: row for row in old['npc']}
    differences = {}
    for row in tables['npc']:
        before = old_npcs.get(row['Id'], {})
        fields = sorted(key for key in before.keys() | row.keys() if before.get(key) != row.get(key))
        if fields:
            differences[str(row['Id'])] = fields
    old_conditions = {row['ClassId']: row for row in old['conditions']}
    changed_conditions = [row['ClassId'] for row in tables['conditions']
                          if row['ClassId'] in old_conditions and old_conditions[row['ClassId']] != row]
    settings = tables['game_settings']
    return dict(comparison_only_input=dict(path=path.relative_to(ROOT).as_posix(),
                    manifest=baseline['manifest'], sha256=sha(path.read_bytes())),
                equal_tables=same, npc_changed_fields=differences, changed_existing_condition_ids=changed_conditions,
                changed_npc_setting_keys=sorted(key for key in settings if 'npc' in key.lower()
                                                and settings[key] != old['game_settings'].get(key)))


def build(work):
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
               re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    objects = {}
    for bundle in ('defaultpackage_assets_config_luban.bundle',
                   'defaultpackage_assets_config_luban_npctalenttree.bundle'):
        for obj in apk.objects(bundle):
            if obj.type.name != 'TextAsset':
                continue
            name = obj.read().m_Name.rsplit('/', 1)[-1]
            if name in objects:
                raise ValueError('Ambiguous original table: ' + name)
            objects[name] = obj
    tables, inputs = {}, {}
    for name in TABLES:
        obj = objects[name + '.g']
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        value = constructors.table(raw, mapping[name])
        if not value:
            raise ValueError('An original partner table is empty: ' + name)
        tables[name] = value
        inputs[name] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                            payload_sha256=sha(raw), bytes=len(raw), constructor=mapping[name],
                            rows=len(value) if isinstance(value, list) else 1)
        print(name, inputs[name]['rows'], flush=True)
    # Include every transitively used generated constructor and metadata input.
    sources = {}
    for name, text in constructors.sources.items():
        if not text:
            continue
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.is_file() or name in ('Pos2d', 'Vector2', 'Vector3'):
            path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.read_bytes() != (ROOT / '.tools/source-packages/official-code' / path.relative_to(code)).read_bytes():
            raise ValueError('Working official source differs: ' + name)
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    metadata_path = code / 'global-metadata.dat'
    sources[metadata_path.relative_to(ROOT).as_posix()] = sha(metadata_path.read_bytes())
    for path in (ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_metadata.py'):
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    validation = validate(tables)
    output = encoded(dict(manifest=apk.version, tables=tables))
    (work / 'partner-rules.json').write_bytes(output)
    receipt = dict(manifest=apk.version, tables=inputs, source_sha256=sources,
                   output=dict(path='unity/Assets/Official/Partners/partner-rules.json', sha256=sha(output), bytes=len(output)),
                   validation=validation, comparison=compare_existing(tables),
                   generator_sha256=sha(Path(__file__).read_bytes()))
    target = ROOT / receipt['output']['path']
    receipt['metadata'] = {str(path.relative_to(ROOT)).replace('\\', '/') + '.meta': sha(metadata(path, folder))
                           for path, folder in ((target.parent, True), (target, False))}
    (work / 'partner-rules-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return receipt


def install(work, receipt):
    output = ROOT / receipt['output']['path']
    proof = ROOT / 'assets/reference/partner-rules-source-manifest.json'
    if output.exists():
        if not proof.is_file() or sha(output.read_bytes()) != json.loads(proof.read_bytes())['output']['sha256']:
            raise ValueError('Refusing to replace partner rules without matching prior provenance')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes((work / 'partner-rules.json').read_bytes())
    for path, folder in ((output.parent, True), (output, False)):
        target = Path(str(path) + '.meta')
        raw = metadata(path, folder)
        if target.exists() and target.read_bytes() != raw:
            raise ValueError('Unrelated partner import metadata exists: ' + str(target))
        target.write_bytes(raw)
    proof.write_bytes((work / 'partner-rules-source-manifest.json').read_bytes())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', required=True, type=Path)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    receipt = build(args.workspace)
    if args.install:
        install(args.workspace, receipt)
