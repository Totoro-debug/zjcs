"""Convert the pinned client's chest map selector and map entry artwork."""
import hashlib
import json
import os
import tempfile
import time
from prepare_profession_ui import ProfessionExporter
from prepare_official_panels import unique_sibling_paths
from prepare_official_city import ART, REFERENCE, ROOT
from official_resource_manifest import write_resources


def save(path, value):
    handle, name = tempfile.mkstemp(prefix=path.name + '.', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(handle, 'w', encoding='utf8') as stream:
            json.dump(value, stream, ensure_ascii=False, separators=(',', ':'))
            stream.write('\n')
        for attempt in range(30):
            try:
                os.replace(name, path)
                break
            except PermissionError:
                if attempt == 29:
                    raise
                time.sleep(.1)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def build():
    e = ProfessionExporter()
    panels = {}
    for key, name in [('relic_maps', 'ChestMapSwitchPanel'), ('relic_map_row', 'ChestMapSwitchItem')]:
        panels[key] = e.ui('prefabs_ui_chestgacha', '/' + name + '.prefab')
        unique_sibling_paths(panels[key]['root'])
    obj = e.apk.asset('scriptableconfig_modules_treasure', '/ChestGachaConfig.asset')
    config = e.fields(obj)
    entries = config['mChestGachaConfigs']
    maps = {}
    for entry in entries['mSerializeBuffer']:
        ident, value = entry['Key'], entry['Value']
        maps[str(ident)] = {key: e.guid_sprite(value[key]['mAssetGuid']) for key in
                           ['EntryBgSpriteGuidProxy', 'EntryIconSpriteGuidProxy', 'IconSpriteGuidProxy']}
    save(ART / 'relic_navigation.json', {'maps': maps})
    catalog = json.loads((ART / 'ui.json').read_text(encoding='utf8'))
    catalog.update(panels)
    save(ART / 'ui.json', catalog)
    inputs = {}
    for name in e.apk.loaded:
        digest = e.apk.index[name]
        entry = f'assets/yoo/DefaultPackage/{digest}.bundle'
        if entry in e.apk.available:
            raw = e.apk.archive.read(entry)
            source = {'origin': 'apk', 'apk_entry': entry}
        else:
            raw, source = e.apk.cloud.read('DefaultPackage', e.apk.bundle_records[name])
        inputs[name] = dict(source, md5=digest, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())
    for record in e.records.values():
        if 'origin' not in record:
            record.update(origin='apk', manifest='854_156383',
                          apk_entry=f"assets/yoo/DefaultPackage/{record['bundle_hash']}.bundle")
    evidence = {'manifest': '854_156383', 'prefabs': {key: value['source'] for key, value in panels.items()},
                'apk_sha256': 'ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9',
                'inputs': inputs, 'config': dict(e.apk.provenance(obj), object='ChestGachaConfig',
                                               object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()),
                'resources': e.records,
                'behavior': ['PreciousChestPanel.cs', 'ChestMapSwitchPanel.cs', 'ChestMapSwitchPanelItem.cs',
                             'GachaProbabilityContent.cs', 'ItemFilterForTreasureGacha.cs'],
                'outputs': {'relic_navigation.json': hashlib.sha256((ART / 'relic_navigation.json').read_bytes()).hexdigest()},
                'shared_catalog': {'file': 'ui.json',
                                   'snapshot_sha256': hashlib.sha256((ART / 'ui.json').read_bytes()).hexdigest(),
                                   'entry_hash_format': 'UTF-8 JSON, sorted keys, compact separators, ensure_ascii=False',
                                   'entries_sha256': {key: hashlib.sha256(json.dumps(value, sort_keys=True,
                                                       ensure_ascii=False, separators=(',', ':')).encode('utf8')).hexdigest()
                                                      for key, value in panels.items()}}}
    folder = ROOT / 'assets/reference/relic-navigation'
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'source-manifest.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    manifest = json.loads((REFERENCE / 'source-manifest.json').read_text(encoding='utf8'))
    for resource, record in e.records.items():
        existing = manifest['resources'].get(resource)
        # Other source catalogs compare shared records verbatim. Keep their
        # canonical provenance when this conversion produces identical bytes;
        # the dedicated relic receipt records this extraction independently.
        if existing and existing['sha256'] == record['sha256']:
            continue
        manifest['resources'][resource] = record
    manifest['data_sha256'].update(evidence['outputs'])
    manifest['data_sha256']['ui.json'] = evidence['shared_catalog']['snapshot_sha256']
    manifest['panel_prefabs'].update(evidence['prefabs'])
    save(REFERENCE / 'source-manifest.json', manifest)
    write_resources(manifest)
    print('Relic navigation:', len(maps), 'maps,', len(e.records), 'original sprites')


if __name__ == '__main__':
    build()
