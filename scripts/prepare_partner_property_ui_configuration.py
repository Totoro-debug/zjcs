"""Recover the selected entry definitions consumed by original partner property UI."""
import argparse
import json
import re
from pathlib import Path
import hashlib

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_current_partner_rules import encoded, metadata, sha
from validation_workspace import validation_directory
from npc_travel_language import language


def build(work):
    apk = Apk.current()
    if apk.version != '884_156692':
        raise ValueError('Review partner property UI configuration after changing version')
    properties_path = ROOT / 'unity/Assets/Official/Partners/partner-properties.json'
    properties = json.loads(properties_path.read_bytes())
    prior = json.loads((ROOT / 'assets/reference/partner-properties-source-manifest.json').read_bytes())
    if properties['manifest'] != apk.version or prior['output']['sha256'] != sha(properties_path.read_bytes()):
        raise ValueError('Selected partner property configuration provenance changed')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
        re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    objects = [obj for obj in apk.objects('defaultpackage_assets_config_luban.bundle')
        if obj.type.name == 'TextAsset' and obj.read().m_Name.rsplit('/', 1)[-1] == 'entry.g']
    obj, = objects
    payload = obj.read().m_Script.encode('utf8', 'surrogateescape')
    entries = constructors.table(payload, mapping['entry'])
    ids = {row['Id'] for row in entries}
    required = {row['EntryId'] for row in properties['tables']['prop_cfg'] if row['EntryId'] > 0}
    if len(ids) != len(entries) or required - ids:
        raise ValueError('Selected property entries are incomplete or duplicated: ' + str(sorted(required - ids)))
    sources = {properties_path.relative_to(ROOT).as_posix(): sha(properties_path.read_bytes())}
    for name, text in constructors.sources.items():
        if not text:
            continue
        source = code / 'common-source/Common' / (name + '.cs')
        if not source.is_file():
            source = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if source.read_bytes() != (ROOT / '.tools/source-packages/official-code' / source.relative_to(code)).read_bytes():
            raise ValueError('Selected entry constructor differs: ' + name)
        sources[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    for source in (code / 'global-metadata.dat', ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_metadata.py'):
        sources[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    prop_type = code / 'common-source/Common/PropType.cs'
    if prop_type.read_bytes() != (ROOT / '.tools/source-packages/official-code/common-source/Common/PropType.cs').read_bytes():
        raise ValueError('Selected property enum differs')
    sources[prop_type.relative_to(ROOT).as_posix()] = sha(prop_type.read_bytes())
    names = {int(value): name for name, value in re.findall(r'^\s*(\w+) = (\d+),?$', prop_type.read_text('utf-8-sig'), re.M)}
    used = {int(key) for group in properties['level_prop'].values() for row in group['rows'].values() for key in row}
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())['tables']
    sources[rules_path.relative_to(ROOT).as_posix()] = sha(rules_path.read_bytes())
    used.update(rules['game_settings']['NpcAdventureSettings']['TravelNpcPropTypeList'])
    keys = {'PropType.' + names[value] for value in used}
    friendship_npcs = {row['Id'] for row in rules['npc_friendship_level']}
    keys.update(row['NameKey'] for row in rules['npc'] if row['Id'] in friendship_npcs)
    keys.update('entry_' + str(value) + suffix for value in required for suffix in ('_name', '_desc'))
    keys.update(row['Unit'] for row in properties['tables']['prop_cfg'] if row['PropType'] in used and row['Unit'])
    texts, text_sources = language(keys)
    if set(texts) != keys:
        raise ValueError('Selected partner property language is incomplete: ' + repr(sorted(keys - texts.keys())))
    records = {row['name']: row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles']}
    for relative, expected in text_sources.items():
        payload_bytes = (ROOT / relative).read_bytes()
        record = records[Path(relative).name]
        if len(payload_bytes) != record['size'] or hashlib.md5(payload_bytes).hexdigest() != record['hash'] or sha(payload_bytes) != expected:
            raise ValueError('Selected property language bytes differ: ' + relative)
        sources[relative] = expected
    sources['scripts/npc_travel_language.py'] = sha((ROOT / 'scripts/npc_travel_language.py').read_bytes())
    raw = encoded(dict(manifest=apk.version, prop_cfg=properties['tables']['prop_cfg'], entry=entries))
    target = ROOT / 'unity/Assets/Official/Partners/partner-property-ui.json'
    (work / target.name).write_bytes(raw)
    text_target = target.with_name('partner-property-texts.json')
    text_raw = encoded(dict(manifest=apk.version, texts=[dict(key=key, text=texts[key]) for key in sorted(texts)]))
    (work / text_target.name).write_bytes(text_raw)
    proof = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), source_sha256=sources,
        entry_table=dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
            payload_sha256=sha(payload), constructor=mapping['entry'], rows=len(entries)),
        validation=dict(property_entries=len(required), entry_ids=sorted(ids)),
        output=dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(raw)),
        metadata_sha256=sha(metadata(target, False)),
        additional_outputs={text_target.relative_to(ROOT).as_posix(): sha(text_raw),
            (text_target.relative_to(ROOT).as_posix() + '.meta'): sha(metadata(text_target, False))},
        language_keys=sorted(keys))
    (work / 'partner-property-ui-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n',
        'utf8', newline='\n')
    print('Recovered selected entry table:', len(entries), 'rows;', len(required), 'property entries resolved')
    return proof


def install(work, proof):
    target = ROOT / proof['output']['path']
    receipt = ROOT / 'assets/reference/partner-property-ui-source-manifest.json'
    if target.exists() and (not receipt.is_file() or sha(target.read_bytes()) != json.loads(receipt.read_bytes())['output']['sha256']):
        raise ValueError('Preserve existing edited property UI configuration')
    meta_path = Path(str(target) + '.meta')
    raw_meta = metadata(target, False)
    if meta_path.exists() and meta_path.read_bytes() != raw_meta:
        raise ValueError('Preserve existing property UI metadata')
    target.write_bytes((work / target.name).read_bytes())
    meta_path.write_bytes(raw_meta)
    previous = json.loads(receipt.read_bytes()) if receipt.exists() else {}
    for relative in proof['additional_outputs']:
        path = ROOT / relative
        raw = metadata(Path(str(path)[:-5]), False) if path.suffix == '.meta' else (work / path.name).read_bytes()
        if sha(raw) != proof['additional_outputs'][relative]:
            raise ValueError('Staged property language changed: ' + relative)
        if path.exists() and sha(path.read_bytes()) != previous.get('additional_outputs', {}).get(relative):
            raise ValueError('Preserve edited property language: ' + relative)
        path.write_bytes(raw)
    receipt.write_bytes((work / receipt.name).read_bytes())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    proof = build(work)
    if args.install:
        install(work, proof)
