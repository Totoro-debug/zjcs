"""Install the verified original NPC metadata/combat-rating runtime and local inputs."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_ui_helpers import meta
from validation_workspace import validation_directory

TABLES = ('game_settings', 'npc_dialogue', 'npc', 'npc_profession_detail', 'npc_level_detail',
          'prop_cfg', 'level_prop_default_value', 'role_prop_group', 'level_number', 'item',
          'skill', 'profession_base', 'entity_prop_group_level', 'score_level_group')
ASSETS = ROOT / 'unity/Assets/Official/Partners/NpcRuntime'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersNpc'
RECEIPT = ROOT / 'assets/reference/partner-npc-runtime-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(path):
    return re.search(rb'(?m)^guid: ([0-9a-f]{32})', Path(str(path) + '.meta').read_bytes())[1].decode()


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    inputs = json.loads((work / 'npc-shared-inputs.json').read_bytes())
    payload = project / 'Assets/Editor/NpcSharedPayload'
    identities = json.loads((payload / 'identities.json').read_text(encoding='utf-8-sig'))
    if inputs['manifest'] != '884_156692' or len(identities['modules']) != 3:
        raise ValueError('Review selected original NPC runtime identity')
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    outputs = {}

    def canonical_meta(path):
        target = Path(str(path) + '.meta')
        text = target.read_text(encoding='utf-8-sig')
        raw = ('\n'.join(line.rstrip() for line in text.splitlines()) + '\n').encode()
        if target.read_bytes() != raw:
            target.write_bytes(raw)

    def asset_meta(path):
        target = Path(str(path) + '.meta')
        identity = hashlib.md5(('zjcs-partner-ui:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'NativeFormatImporter' if path.suffix == '.asset' else 'TextScriptImporter'
        text = 'fileFormatVersion: 2\nguid: ' + identity + '\n' + importer + ':\n  externalObjects: {}\n'
        if path.suffix == '.asset':
            text += '  mainObjectFileID: 11400000\n'
        raw = (text + '  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
        if target.exists() and target.read_bytes() != raw:
            expected = None if prior is None else prior['installed_sha256'].get(target.relative_to(ROOT).as_posix())
            if sha(target.read_bytes()) != expected:
                raise ValueError('Refusing to replace unrelated NPC asset importer: ' + str(target))
        if not target.exists() or target.read_bytes() != raw:
            target.write_bytes(raw)

    def install(path, raw):
        if path.exists() and path.read_bytes() != raw:
            expected = None if prior is None else prior['installed_sha256'].get(path.relative_to(ROOT).as_posix())
            if sha(path.read_bytes()) != expected:
                raise ValueError('Refusing to replace unrelated NPC runtime input: ' + str(path))
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        asset_meta(path)
        for item in (path, Path(str(path) + '.meta')):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())

    def pointer(path, file_id=4900000):
        asset_type = 2 if path.suffix == '.asset' else 3
        return '{fileID: ' + str(file_id) + ', guid: ' + guid(path) + ', type: ' + str(asset_type) + '}'

    for row in identities['modules']:
        source = payload / (row['name'] + '.bytes')
        if sha(source.read_bytes()) != row['sha256']:
            raise ValueError('NPC adapted payload changed')
        install(ASSETS / source.name, source.read_bytes())
    install(ASSETS / 'LiteDB-LICENSE.txt', (payload / 'LiteDB-LICENSE.txt').read_bytes())
    tables = []
    for name in TABLES:
        filename = name + '.g.bytes'
        source = project / 'Assets/NpcSharedConfiguration/Resources/PartnerNpcShared' / filename
        proof = inputs['tables'][name + '.g']
        raw = source.read_bytes()
        if sha(raw) != proof['payload_sha256']:
            raise ValueError('NPC raw Luban table changed: ' + name)
        destination = ASSETS / 'Tables' / filename
        install(destination, raw)
        tables.append(dict(location='Config/Luban/' + filename, source=proof, path=destination.relative_to(ROOT).as_posix(), sha256=sha(raw)))
    binary_path = ROOT / 'unity/Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes'
    binary = project / 'Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes'
    if sha(binary.read_bytes()) != inputs['tables']['level_prop.g.bytes']['payload_sha256']:
        raise ValueError('NPC raw property binary changed')
    install(binary_path, binary.read_bytes())
    for folder in (RUNTIME, ASSETS, ASSETS / 'Tables', binary_path.parent, binary_path.parent.parent, binary_path.parent.parent.parent):
        meta(folder, folder=True)
        canonical_meta(folder)
        path = Path(str(folder) + '.meta')
        outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for path in RUNTIME.iterdir():
        if path.suffix in ('.cs', '.asmdef'):
            meta(path)
            canonical_meta(path)
            for item in (path, Path(str(path) + '.meta')):
                outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    catalog_script = RUNTIME / 'OfficialPartnerNpcRuntimeCatalog.cs'
    bindings = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerNpcRuntimeCatalog.asset'
    text = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        '  m_Script: ' + pointer(catalog_script, 11500000) + '\n'
        '  m_Name: OfficialPartnerNpcRuntimeCatalog\n  m_EditorClassIdentifier:\n  manifest: 884_156692\n'
        '  dependencies: ' + pointer(ROOT / 'unity/Assets/Game/Resources/Map14/Native/shared-assembly-catalog.asset', 11400000) + '\n'
        '  assemblies:\n')
    for row in identities['modules']:
        text += ('  - Name: ' + row['name'] + '\n    Sha256: ' + row['sha256'] + '\n    ModuleId: ' + row['moduleId'] +
                 '\n    Data: ' + pointer(ASSETS / (row['name'] + '.bytes')) + '\n')
    text += '  tables:\n'
    for row in tables:
        text += ('  - Location: ' + row['location'] + '\n    Sha256: ' + row['sha256'] + '\n    Data: ' + pointer(ROOT / row['path']) + '\n')
    text += '  propertyBinarySha256: ' + sha(binary.read_bytes()) + '\n'
    install(bindings, text.encode())
    source_root = ROOT / '.tools/source-packages/current/884_156692'
    official_files = [source_root / 'code-manifest.json', source_root / 'official-code/global-metadata.dat',
        source_root / 'official-code/aot-metadata/source-manifest.json']
    for name in ('NpcTargetData', 'PlayerLiteInfo', 'NpcFightData', 'NpcInfo', 'NpcInfoParser', 'NpcDialogueInfoParser',
                 'NpcLevelDetailInfo', 'NpcLevelDetailInfoParser', 'NpcLevelDetailInfoWrap', 'NpcProfessionDetailInfoParser',
                 'BattleFormulaHandler', 'FormulaHandler', 'CommonCombatRatingRefData', 'RolePropGroupInfo', 'RolePropGroupInfoParser',
                 'PropCfgInfoParser', 'LevelNumberParser', 'LevelPropParser', 'LevelPropBinaryReader', 'LevelPropDefaultValueParser',
                 'ItemInfoParser', 'SkillInfoParser', 'ProfessionBaseInfoParser', 'EntityPropGroupLevelInfoParser', 'ScoreLevelGroupInfoParser', 'RawFileUtility'):
        official_files.append(source_root / 'official-code/common-source/Common' / (name + '.cs'))
    generators = [Path(__file__), ROOT / 'scripts/prepare_partner_npc_shared_validation.py',
        ROOT / 'scripts/prepare_partner_shared_identity.ps1', ROOT / 'scripts/fixtures/partner_npc_shared_validation.cs']
    dependency_proofs = [ROOT / 'assets/reference/official-map' / name for name in
        ('map14-shared-ec-runtime-source-manifest.json', 'map14-shared-serialization-source-manifest.json')]
    expected_dependencies = {}
    for path in dependency_proofs:
        expected_dependencies.update({row['path']: row['sha256'] for row in json.loads(path.read_bytes())['outputs']})
    dependency_catalog = ROOT / 'unity/Assets/Game/Resources/Map14/Native/shared-assembly-catalog.asset'
    dependency_inputs = []
    for block in dependency_catalog.read_text(encoding='utf-8-sig').split('  - Name: ')[1:]:
        name = block.splitlines()[0]
        expected = re.search(r'(?m)^    Sha256: ([0-9a-f]{64})', block)[1]
        module_id = re.search(r'(?m)^    ModuleId: ([0-9a-f-]{36})', block)[1]
        path = ROOT / 'unity/Assets/Official/World/Map/14/SharedRuntime' / (name + '.bytes')
        relative = path.relative_to(ROOT).as_posix()
        if sha(path.read_bytes()) != expected or expected_dependencies.get(relative) != expected:
            raise ValueError('Historical shared dependency no longer matches its own source record: ' + name)
        dependency_inputs.append(dict(name=name, path=relative, sha256=expected, moduleId=module_id))
    relative = dependency_catalog.relative_to(ROOT).as_posix()
    catalog_raw = dependency_catalog.read_bytes()
    catalog_canonical = catalog_raw.replace(b'\r\n', b'\n')
    if sha(catalog_canonical) != expected_dependencies.get(relative):
        raise ValueError('Historical shared dependency catalog no longer matches its source record')
    receipt = dict(manifest=inputs['manifest'], operation=identities['operation'], assemblies=inputs['assemblies'],
        adapted_modules=identities['modules'], litedb=inputs['litedb'], tables=tables,
        binary=inputs['tables']['level_prop.g.bytes'],
        dependencies=dict(catalog=dict(path=relative, sha256=sha(catalog_raw), canonical_lf_sha256=sha(catalog_canonical),
            text_conversion='Compare canonical LF text to the existing receipt; retain the actual Windows working-tree hash separately.'),
            assemblies=dependency_inputs, proofs=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes())) for path in dependency_proofs]),
        official_source_sha256={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in official_files},
        generator_sha256={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in generators},
        installed_sha256=outputs)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original NPC runtime:', len(identities['modules']), 'modules,', len(tables), 'raw tables and property binary', flush=True)


if __name__ == '__main__':
    main()
