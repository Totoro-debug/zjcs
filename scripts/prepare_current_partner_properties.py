"""Recover selected NPC friendship and talent properties from the original binary."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_current_partner_rules import encoded, metadata
from prepare_unity_character_catalog import decode_level_props
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(work):
    apk = Apk.current()
    rules_path = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != apk.version:
        raise ValueError('Partner rules and property binary versions must match')
    prior = json.loads((ROOT / 'assets/reference/partner-rules-source-manifest.json').read_bytes())
    if prior['output']['sha256'] != sha(rules_path.read_bytes()):
        raise ValueError('Partner table provenance changed')
    name = 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    record, = [row for row in apk.source.manifest('WwiseBnk', apk.archive)['bundles'] if row['name'] == name]
    entry = 'assets/yoo/WwiseBnk/' + record['file']
    packaged = entry in apk.available
    verified = apk.reader.read('WwiseBnk', name)
    raw, origin = verified.data, dict(verified.provenance)
    (work / 'reference-level-prop.g.bytes').write_bytes(raw)
    wanted = {row['LevelPropId'] for row in rules['tables']['npc'] if row['LevelPropId'] > 0}
    wanted.update(row['PropId'] for row in rules['tables']['npc_talent_tree_npc_map_prop'] if row['PropId'] > 0)
    classes = decode_level_props(raw, wanted)
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
               re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    tables, table_inputs = {}, {}
    objects = {obj.read().m_Name.rsplit('/', 1)[-1]: obj for obj in apk.objects('defaultpackage_assets_config_luban.bundle')
               if obj.type.name == 'TextAsset'}
    for table in ('level_prop_default_value', 'prop_cfg'):
        obj = objects[table + '.g']
        payload = obj.read().m_Script.encode('utf8', 'surrogateescape')
        tables[table] = constructors.table(payload, mapping[table])
        table_inputs[table] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                                  payload_sha256=sha(payload), constructor=mapping[table], rows=len(tables[table]))
    sources = {rules_path.relative_to(ROOT).as_posix(): sha(rules_path.read_bytes())}
    for name, text in constructors.sources.items():
        if not text:
            continue
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.is_file():
            path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.read_bytes() != (ROOT / '.tools/source-packages/official-code' / path.relative_to(code)).read_bytes():
            raise ValueError('Working official constructor differs: ' + name)
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('LevelPropBinaryReader', 'LevelPropParser', 'NpcInfoParser', 'NpcFriendshipLevelInfoParser', 'PropType'):
        path = code / 'common-source/Common' / (name + '.cs')
        if path.read_bytes() != (ROOT / '.tools/source-packages/official-code' / path.relative_to(code)).read_bytes():
            raise ValueError('Working official property source differs: ' + name)
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for path in (code / 'global-metadata.dat', ROOT / 'scripts/official_tables.py',
                 ROOT / 'scripts/official_metadata.py', ROOT / 'scripts/prepare_unity_character_catalog.py',
                 ROOT / 'scripts/prepare_current_partner_rules.py', ROOT / 'scripts/prepare_partner_ui_helpers.py'):
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    target = ROOT / 'unity/Assets/Official/Partners/partner-properties.json'
    known_properties = {row['PropType'] for row in tables['prop_cfg']}
    used_properties = {int(key) for group in classes.values() for row in group['rows'].values() for key in row}
    if used_properties - known_properties:
        raise ValueError('Original NPC rows use unbound property definitions')
    baseline_path = ROOT / 'unity/Assets/Official/Character/character-progression.json'
    baseline = json.loads(baseline_path.read_bytes())
    old_ids = {row['Id']: row['LevelPropId'] for row in baseline['rules']['npc']}
    new_ids = {row['Id']: row['LevelPropId'] for row in rules['tables']['npc']}
    if old_ids != new_ids:
        raise ValueError('Review NPC property identity before extending the character catalog')
    output = encoded(dict(manifest=apk.version, level_prop=classes, tables=tables))
    (work / target.name).write_bytes(output)
    receipt = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), source_sha256=sources,
        binary=dict(package='WwiseBnk', bundle=record, source=origin, sha256=sha(raw),
                    apk_checked_first=dict(entry=entry, packaged=packaged)), tables=table_inputs,
        classes=sorted(wanted), rows=sum(len(value['rows']) for value in classes.values()),
        validation=dict(property_ids=sorted(used_properties), npc_property_identity_count=len(new_ids),
                        talent_property_classes=sorted({row['PropId'] for row in rules['tables']['npc_talent_tree_npc_map_prop']})),
        comparison_only=dict(path=baseline_path.relative_to(ROOT).as_posix(), manifest=baseline['manifest'], sha256=sha(baseline_path.read_bytes())),
        output=dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(output), bytes=len(output)),
        metadata_sha256=sha(metadata(target, False)))
    (work / 'partner-properties-source-manifest.json').write_bytes(
        (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf8'))
    print('Recovered partner properties:', len(classes), 'classes,', receipt['rows'], 'rows')
    return receipt


def install(work, receipt):
    from prepare_partner_ui_helpers import meta as make_meta
    target = ROOT / receipt['output']['path']
    proof = ROOT / 'assets/reference/partner-properties-source-manifest.json'
    if target.exists() and (not proof.is_file() or sha(target.read_bytes()) != json.loads(proof.read_bytes())['output']['sha256']):
        raise ValueError('Existing partner properties lack matching provenance')
    meta = Path(str(target) + '.meta')
    if meta.exists() and meta.read_bytes() != metadata(target, False):
        raise ValueError('Existing partner property metadata differs')
    target.write_bytes((work / target.name).read_bytes())
    meta.write_bytes(metadata(target, False))
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfficialPartnerAssetCatalog.cs'
    make_meta(script)
    folder = ROOT / 'unity/Assets/Game/Resources/Partners'
    folder.mkdir(parents=True, exist_ok=True)
    make_meta(folder, folder=True)
    binding = folder / 'OfficialPartnerAssetCatalog.asset'
    binding_meta = Path(str(binding) + '.meta')
    binding_guid = hashlib.md5(('zjcs-partner-ui:' + binding.relative_to(ROOT).as_posix()).encode()).hexdigest()
    if not binding_meta.exists():
        binding_meta.write_text('fileFormatVersion: 2\nguid: ' + binding_guid +
            '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
    for own_meta in (Path(str(script) + '.meta'), Path(str(folder) + '.meta'), binding_meta):
        own_meta.write_bytes(own_meta.read_bytes().replace(b'\r\n', b'\n'))
    def guid(path):
        return re.search(rb'(?m)^guid: ([0-9a-f]{32})', Path(str(path) + '.meta').read_bytes())[1].decode()
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        '  m_Script: {fileID: 11500000, guid: ' + guid(script) + ', type: 3}\n'
        '  m_Name: OfficialPartnerAssetCatalog\n  m_EditorClassIdentifier:\n'
        '  rules: {fileID: 4900000, guid: ' + guid(ROOT / 'unity/Assets/Official/Partners/partner-rules.json') + ', type: 3}\n'
        '  properties: {fileID: 4900000, guid: ' + guid(target) + ', type: 3}\n').encode()
    if binding.exists() and binding.read_bytes() != raw:
        raise ValueError('Existing partner binding asset differs')
    binding.write_bytes(raw)
    files = [target, meta, binding, Path(str(binding) + '.meta'), Path(str(folder) + '.meta'),
             script, Path(str(script) + '.meta')]
    receipt['installed_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in files}
    receipt['runtime_source_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
        (script, script.with_name('OfficialCharacterAssetCatalog.cs'))}
    receipt['migrated_methods'] = ['LevelPropParser.GetConfigProps', 'LevelPropParser.TryGetPropDict',
        'NpcInfoParser.GetFriendshipLevelPropDiff']
    receipt['validation_source_sha256'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
        (ROOT / 'scripts/prepare_partner_property_validation.py', ROOT / 'scripts/fixtures/partner_property_catalog_validation.cs')}
    proof.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    receipt = build(work)
    if args.install:
        install(work, receipt)
