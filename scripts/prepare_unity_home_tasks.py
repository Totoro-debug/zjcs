"""Restore the fixed-version task panel and its local mainline row assets."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import shutil
import struct
import sys


ROOT = Path(__file__).resolve().parents[1]
OFFICIAL_UI = ROOT / 'assets/official/ui.json'
CITY_MANIFEST = ROOT / 'assets/reference/official-city/source-manifest.json'
TASK_MANIFEST = ROOT / 'assets/reference/task/source-manifest.json'
PREFAB_BUNDLE = ROOT / '.tools/source-packages/extracted/defaultpackage_assets_prefabs_ui_task.bundle'
Wwise = ROOT / '.tools/source-packages/WwiseBnk'
APK_SHA256 = 'ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9'
MANIFEST_ID = '854_156383'
PREFAB_BUNDLE_NAME = 'defaultpackage_assets_prefabs_ui_task.bundle'
PREFAB_BUNDLE_MD5 = 'fea54f282c89352049981b549cbcf5ee'
OUTPUT_DIR = ROOT / 'unity/Assets/StreamingAssets/Home/Tasks'
OUTPUT = OUTPUT_DIR / 'task-panels.json'
SPRITE_DIR = OUTPUT_DIR / 'sprites'
SPRITE_SOURCE = ROOT / 'assets/official/sprites'
LOCALIZATION_SOURCES = {
    'wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile': {
        'md5': 'a373f2acc39aeb5c4560f1456879b997',
        'sha256': 'a068ee47db184c96c8923172a104ed90b37f72e2b4adb5d73211d894d7ec9001',
        'size': 5532,
    },
    'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile': {
        'md5': '9a0b6ab8c5136050fd005ead8f567863',
        'sha256': '738069eafacd7fe33aae970aa5edacd76f48ed02acdd07ededad1c4a499b93b8',
        'size': 5291479,
    },
}
PANELS = {'quests': 'TaskPanel', 'task_progress': 'TaskPanelListItemProgress'}
TASK_IDS = (1000, 1005, 1010, 1015, 1020, 1025, 1030, 1035, 1040, 1045, 1050)
RULES = ROOT / 'assets/official/rules.json'
RULES_MANIFEST = ROOT / 'assets/reference/official-city/rules-manifest.json'


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_meta(path: Path, importer: str) -> None:
    relative = path.relative_to(ROOT / 'unity').as_posix()
    guid = hashlib.md5(('zjcs-official:' + relative).encode('utf-8')).hexdigest()
    if importer == 'folderAsset':
        content = (f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n'
                   'DefaultImporter:\n  externalObjects: {}\n  userData: \n'
                   '  assetBundleName: \n  assetBundleVariant: \n')
    else:
        content = (f'fileFormatVersion: 2\nguid: {guid}\n'
                   'DefaultImporter:\n  externalObjects: {}\n  userData: \n'
                   '  assetBundleName: \n  assetBundleVariant: \n')
    meta = path.with_name(path.name + '.meta')
    if meta.exists():
        existing = meta.read_text(encoding='utf-8')
        if existing == content:
            return
        if ('TextScriptImporter:' in existing and 'DefaultImporter:' in content
                and f'guid: {guid}\n' in existing):
            meta.write_text(content, encoding='utf-8', newline='\n')
            return
        raise ValueError('Refusing to overwrite modified task asset metadata: ' + str(meta))
    meta.write_text(content, encoding='utf-8', newline='\n')


def verify_sources() -> tuple[dict, dict]:
    source_manifest = json.loads(CITY_MANIFEST.read_text(encoding='utf-8'))
    ui_bytes = OFFICIAL_UI.read_bytes()
    if source_manifest.get('manifest') != MANIFEST_ID:
        raise ValueError('Official city source manifest is not pinned to ' + MANIFEST_ID)
    if sha256(ui_bytes) != source_manifest.get('data_sha256', {}).get('ui.json'):
        raise ValueError('Official UI source differs from its pinned source manifest')
    bundle_bytes = PREFAB_BUNDLE.read_bytes()
    if hashlib.md5(bundle_bytes).hexdigest() != PREFAB_BUNDLE_MD5:
        raise ValueError('Pinned task prefab bundle MD5 mismatch')
    for key, path in [('quests', '/TaskPanel.prefab'),
                      ('task_progress', '/TaskPanelListItemProgress.prefab')]:
        record = source_manifest.get('panel_prefabs', {}).get(key)
        if (not record or record.get('bundle') != PREFAB_BUNDLE_NAME
                or record.get('bundle_hash') != PREFAB_BUNDLE_MD5
                or record.get('prefab') != path):
            raise ValueError('Pinned task prefab source record is missing: ' + key)
    ui = json.loads(ui_bytes)
    for key, root_name in PANELS.items():
        panel = ui.get(key)
        if not panel or panel.get('root', {}).get('name') != root_name:
            raise ValueError('Official task panel root differs from its pinned prefab: ' + key)
    return source_manifest, ui


def localization_map() -> tuple[dict[str, str], dict]:
    sys.path.insert(0, str(ROOT / 'scripts'))
    from prepare_character_creation_group_icons import xxhash64

    receipts = json.loads((Wwise / 'bundle-sources.json').read_text(encoding='utf-8'))
    values: dict[int, str] = {}
    source_records = {}
    for name, expected in LOCALIZATION_SOURCES.items():
        raw = (Wwise / name).read_bytes()
        receipt = receipts.get(name)
        if (len(raw) != expected['size'] or hashlib.md5(raw).hexdigest() != expected['md5']
                or sha256(raw) != expected['sha256'] or not isinstance(receipt, dict)
                or receipt.get('origin') != 'apk_zip' or receipt.get('apk_sha256') != APK_SHA256
                or receipt.get('bundle_hash') != expected['md5']):
            raise ValueError('Pinned Chinese localization source changed or is missing: ' + name)
        count = struct.unpack_from('<i', raw, 0)[0]
        text_start = 4 + count * 12
        if count < 0 or text_start > len(raw):
            raise ValueError('Pinned Chinese localization index is invalid: ' + name)
        for index in range(count):
            key_hash, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
            if offset < 0:
                raise ValueError('Pinned Chinese localization has a negative offset: ' + name)
            start = text_start + offset
            if start + 2 > len(raw):
                raise ValueError('Pinned Chinese localization offset is invalid: ' + name)
            length = struct.unpack_from('<H', raw, start)[0]
            end = start + 2 + length
            if end > len(raw):
                raise ValueError('Pinned Chinese localization value is truncated: ' + name)
            values[key_hash] = raw[start + 2:end].decode('utf-8')
        source_records[name] = {
            'source': receipt,
            'size': len(raw),
            'md5': expected['md5'],
            'sha256': expected['sha256'],
        }
    return values, source_records


def flatten(panel_key: str, panel: dict) -> tuple[dict, list[dict], set[str]]:
    root = copy.deepcopy(panel['root'])
    labels: set[str] = set()
    sprites = set()
    nodes = []
    root_path = root['path']
    queue = [(root, '')]
    while queue:
        node, parent_path = queue.pop(0)
        path = node['path']
        localization_key = node.get('localization_key')
        if localization_key and not localization_key.startswith('{'):
            labels.add(localization_key)
        image = node.get('image') or {}
        sprite = image.get('sprite') or {}
        file = sprite.get('file')
        if file and file.startswith('res://assets/official/sprites/'):
            sprite['file'] = Path(file).name
            sprites.add(file)
        current = {key: value for key, value in node.items() if key != 'children'}
        if path == root_path:
            flat_root = current
        else:
            nodes.append(dict(parent_path=parent_path, **current))
        for child in node.get('children') or []:
            queue.append((child, path))
    return dict(key=panel_key, root=flat_root, nodes=nodes), labels, sprites


def build() -> None:
    source_manifest, ui = verify_sources()
    localization, localization_sources = localization_map()
    sys.path.insert(0, str(ROOT / 'scripts'))
    from prepare_character_creation_group_icons import xxhash64

    panels = []
    keys = set()
    sprite_files = {}
    for panel_key, root_name in PANELS.items():
        panel, labels, sprites = flatten(panel_key, ui[panel_key])
        if panel['root']['name'] != root_name:
            raise ValueError('Flattened task panel root name changed: ' + panel_key)
        panels.append(panel)
        keys.update(labels)
        for file in sprites:
            sprite_files[file] = ui[panel_key]
    for task_id in TASK_IDS:
        keys.update(f'task_{task_id}_{suffix}' for suffix in ('title', 'desc', 'progress'))
    rules_bytes = RULES.read_bytes()
    rules_manifest = json.loads(RULES_MANIFEST.read_text(encoding='utf-8'))
    city_manifest = json.loads(CITY_MANIFEST.read_text(encoding='utf-8'))
    rules_hash = sha256(rules_bytes)
    if (rules_hash != rules_manifest.get('rules_sha256')
            or rules_hash != city_manifest.get('data_sha256', {}).get('rules.json')):
        raise ValueError('Official rules.json differs from its task panel source manifests')
    task_rows = {int(row['ClassId']): row for row in json.loads(rules_bytes).get('task', [])}
    for task_id in TASK_IDS:
        row = task_rows.get(task_id)
        condition = row.get('FinishCondition') if row else None
        if condition and condition.get('_type') == 'WorldMapProgressCondition':
            keys.add(f"region_{condition['MapClassId']}_{condition['Region']}")

    texts = []
    missing = []
    for key in sorted(keys):
        key_hash = xxhash64(key.encode('utf-8'))
        if key_hash not in localization:
            missing.append(key)
        else:
            texts.append({'key': key, 'value': localization[key_hash]})
    if missing:
        raise ValueError('Pinned Chinese localization is missing task UI keys: ' + ', '.join(missing))

    resources = source_manifest.get('resources', {})
    sprite_records = {}
    copied = {}
    for file, panel in sorted(sprite_files.items()):
        record = resources.get(file)
        if (not record or not record.get('bundle') or not record.get('bundle_hash')
                or not record.get('path_id') or not record.get('object')):
            raise ValueError('Task panel sprite source identity is missing: ' + file)
        name = Path(file).name
        source = SPRITE_SOURCE / name
        if not source.is_file():
            raise FileNotFoundError('Official extracted task sprite is missing: ' + str(source))
        source_bytes = source.read_bytes()
        source_hash = sha256(source_bytes)
        if source_hash != record.get('sha256'):
            raise ValueError('Extracted task sprite hash differs from the official source record: ' + name)
        prior_hash = copied.setdefault(name, source_hash)
        if prior_hash != source_hash:
            raise ValueError('Task sprite basename collision has different content: ' + name)
        target = SPRITE_DIR / name
        if target.exists() and sha256(target.read_bytes()) != source_hash:
            raise ValueError('Refusing to overwrite modified task sprite: ' + str(target))
        sprite_records[file] = {
            'bundle': record['bundle'],
            'bundle_md5': record['bundle_hash'],
            'path_id': record['path_id'],
            'object': record['object'],
            'source_sha256': source_hash,
            'output': f'sprites/{name}',
            'output_sha256': source_hash,
        }

    extension = {
        'version': MANIFEST_ID,
        'panels': panels,
        'dynamic_sprites': [],
        'texts': texts,
    }
    extension_bytes = (json.dumps(extension, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    SPRITE_DIR.mkdir(parents=True, exist_ok=True)
    for folder in [OUTPUT_DIR, SPRITE_DIR]:
        write_meta(folder, 'folderAsset')
    for file in sprite_files:
        name = Path(file).name
        source = SPRITE_SOURCE / name
        target = SPRITE_DIR / name
        if not target.exists():
            shutil.copyfile(source, target)
        write_meta(target, 'text')
    if OUTPUT.exists() and sha256(OUTPUT.read_bytes()) != sha256(extension_bytes):
        prior = json.loads(TASK_MANIFEST.read_text(encoding='utf-8')).get('task_ui', {}) if TASK_MANIFEST.exists() else {}
        prior_hash = prior.get('outputs', {}).get('task-panels.json')
        if prior_hash != sha256(OUTPUT.read_bytes()):
            raise ValueError('Refusing to overwrite changed task panel extension: ' + str(OUTPUT))
        OUTPUT.write_bytes(extension_bytes)
    elif not OUTPUT.exists():
        OUTPUT.write_bytes(extension_bytes)
    write_meta(OUTPUT, 'text')

    panel_sources = {}
    for key in PANELS:
        panel_sources[key] = source_manifest['panel_prefabs'][key]
    manifest = {
        'manifest': MANIFEST_ID,
        'panel_bundle': {
            'name': PREFAB_BUNDLE_NAME,
            'md5': PREFAB_BUNDLE_MD5,
            'size': len(PREFAB_BUNDLE.read_bytes()),
        },
        'panels': panel_sources,
        'localization': localization_sources,
        'localization_keys': sorted(keys),
        'sprites': sprite_records,
        'outputs': {
            'task-panels.json': sha256(extension_bytes),
            **{record['output']: record['output_sha256'] for record in sprite_records.values()},
        },
        'source_ui_sha256': sha256(OFFICIAL_UI.read_bytes()),
        'source_manifest_sha256': sha256(CITY_MANIFEST.read_bytes()),
        'source_rules_sha256': rules_hash,
        'scope': 'Official task panel and progress row. Dynamic task state remains local to OfflineMainlineProgressSession.',
    }
    TASK_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')
    if TASK_MANIFEST.exists() and json.loads(TASK_MANIFEST.read_text(encoding='utf-8')).get('resources'):
        current = json.loads(TASK_MANIFEST.read_text(encoding='utf-8'))
        current['task_ui'] = manifest
        manifest_bytes = (json.dumps(current, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')
    elif TASK_MANIFEST.exists():
        current = json.loads(TASK_MANIFEST.read_text(encoding='utf-8'))
        current['task_ui'] = manifest
        manifest_bytes = (json.dumps(current, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')
    if TASK_MANIFEST.exists() and sha256(TASK_MANIFEST.read_bytes()) != sha256(manifest_bytes):
        existing = json.loads(TASK_MANIFEST.read_text(encoding='utf-8'))
        expected = json.loads(manifest_bytes)
        if existing.get('task_ui') != expected.get('task_ui'):
            TASK_MANIFEST.write_bytes(manifest_bytes)
    elif not TASK_MANIFEST.exists():
        TASK_MANIFEST.write_bytes(manifest_bytes)

    print(f'Restored official task panels: {len(panels)} panels, {len(texts)} Chinese strings, '
          f'{len(sprite_records)} sprites, extension sha256={sha256(extension_bytes)}')


if __name__ == '__main__':
    build()
