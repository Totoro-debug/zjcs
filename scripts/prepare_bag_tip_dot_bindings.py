"""Bind the complete original Bag graph and Home's selected local data owners."""
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from official_tables import Constructors


RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/BagTipDots'
ASSETS = ROOT / 'unity/Assets/Game/Resources/Home/BagTipDots'
RECEIPT = ROOT / 'assets/reference/bag-tip-dot-bindings-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    inputs, outputs = {}, {}

    def read(path):
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another Bag binding change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        kind = 'DefaultImporter' if folder else 'NativeFormatImporter' if path.suffix == '.asset' else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:bag-tip-dot-bindings:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest() + '\n'
        text += 'folderAsset: yes\n' if folder else ''
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    identity = json.loads(read(code.parent / 'code-manifest.json'))
    assembly = code / 'managed/decoded/Console.dll'
    if identity['manifest'] != source.version or sha(read(assembly)) != identity['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Verify the selected original Bag configuration assembly')
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    manifest = read_asset_manifest(source.manifest_bytes('DefaultPackage'))
    names = ('tip_dot', 'game_settings')
    selected = {}
    for name in names:
        matches = [row for row in manifest if row['path'].lower().endswith('/' + name + '.g.bytes')]
        if len(matches) != 1: raise ValueError('Resolve original Bag graph configuration: ' + name)
        selected[name] = matches[0]
    tables, configuration = {}, {}
    with Apk.current() as apk:
        for bundle in dict.fromkeys(row['bundle'] for row in selected.values()):
            container = {path.lower(): pointer for path, pointer in apk.container(bundle)}
            for name, row in selected.items():
                if row['bundle'] != bundle: continue
                obj = container[row['path'].lower()].deref(); raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
                tables[name] = constructors.table(raw, mapping[name])
                configuration[name] = dict(source=apk.provenance(obj), asset=row['path'], constructor=mapping[name],
                    payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data()))
    for name in constructors.sources:
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.exists(): path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.exists(): read(path)
        elif name not in ('Vector2', 'Vector3', 'Pos2d'): raise ValueError('Verify the original graph configuration decoder: ' + name)
    row, = [row for row in tables['tip_dot'] if row['GraphName'] == 'Bag']

    # The existing native restoration already preserves every original node,
    # edge, managed type and referenced visual. Reuse that actual graph asset.
    native_receipt = ROOT / 'assets/reference/home-storage-native-source-manifest.json'
    native = json.loads(read(native_receipt))
    graph, = [value for value in native['resources'].values() if value.get('type') == 'TipDotNodeGraph' and value.get('name') == row['GraphName']]
    graph_path = ROOT / 'unity/Assets/Official/Home/StorageBox/Native' / graph['output']
    read(graph_path)
    for name, expected in native['outputs'].items():
        path = ROOT / name
        if sha(read(path)) != expected: raise ValueError('Original native storage/Bag asset differs: ' + name)
    prefab_routes = json.loads(read(ROOT / 'assets/reference/home-native-prefab-references-source-manifest.json'))
    for location in ('Home/Dynamic/Theme/House/House', 'Home/Dynamic/Furniture/StorageBox/StorageBox'):
        route, = [value for value in prefab_routes['routes'] if value['location'] == location]
        for path, group in ((route['prefab'], 'inputs'), (route['reference'], 'outputs')):
            if sha(read(ROOT / path)) != prefab_routes[group][path]:
                raise ValueError('Original indoor prefab route differs: ' + path)
    weapons = json.loads(read(ROOT / 'assets/reference/home-weapon-parts-source-manifest.json'))
    weapon_catalog = 'unity/Assets/Official/Home/WeaponParts/HomeWeaponAppearanceCatalog.asset'
    for path in (weapon_catalog, weapon_catalog + '.meta'):
        if sha(read(ROOT / path)) != weapons['outputs'][path]:
            raise ValueError('Original rack appearance catalog differs: ' + path)
    graph_guid = re.search(r'^guid: ([a-f0-9]{32})$', Path(str(graph_path) + '.meta').read_text('utf8'), re.M).group(1)
    if graph_guid != graph['guid']: raise ValueError('The original Bag graph identity differs')
    for folder in (RUNTIME, ASSETS): metadata(folder, True)
    for name in ('OfflineBagTipDotAssets.cs', 'OfflineBagTipDotServices.cs'):
        path = RUNTIME / name; raw = read(path); outputs[path.relative_to(ROOT).as_posix()] = sha(raw); metadata(path)
    definition = RUNTIME / 'Zjcs.Offline.BagTipDots.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Offline.BagTipDots', references=[
        'Zjcs.Offline.Progression', 'Zjcs.Offline.Equipment', 'Zjcs.Offline.Home', 'Zjcs.Offline.BagItemQueries', 'Zjcs.Offline.BagRecycleQueries',
        'Zjcs.Official.BagTipDots', 'Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagRecycleQueries', 'Zjcs.Official.HomeUI', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.PartnersUI', 'Zjcs.Official.PartnerRosterUI']), indent=2) + '\n').encode()); metadata(definition)
    script = re.search(r'^guid: ([a-f0-9]{32})$', Path(str(RUNTIME / 'OfflineBagTipDotAssets.cs') + '.meta').read_text('utf8'), re.M).group(1)
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    text += f'  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n  m_Name: Assets\n  m_EditorClassIdentifier:\n'
    text += f'  Graph: {{fileID: 11400000, guid: {graph_guid}, type: 2}}\n'
    text += ''.join('  ' + key + ': ' + json.dumps(row[key], ensure_ascii=False) + '\n' for key in ('GraphName', 'GraphPath', 'GraphType', 'GraphScope'))
    settings = tables['game_settings']
    for key in ('EquipTypes', 'ShowCanUseRedDotItemTypes', 'NewDayRefreshRedDotItemType'):
        text += '  ' + key + ': ' + b''.join(struct.pack('<i', value) for value in settings[key]).hex() + '\n'
    path = ASSETS / 'Assets.asset'; write(path, text.encode()); metadata(path)
    partial = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.BagTips.cs'
    read(partial); outputs[partial.relative_to(ROOT).as_posix()] = sha(partial.read_bytes()); metadata(partial)
    for name in ('assets/reference/bag-tip-dots-source-manifest.json', 'assets/reference/bag-item-query-bindings-source-manifest.json',
        'assets/reference/bag-recycle-queries-source-manifest.json'):
        receipt = json.loads(read(ROOT / name))
        for path, expected in receipt['outputs'].items():
            if sha(read(ROOT / path)) != expected: raise ValueError('Verified Bag dependency differs: ' + path)
    for path in (
        'unity/Assets/Game/Runtime/Offline/PartnersUI/OfflinePartnerTipDotRuntime.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/TipDotNodeLinker.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/TipDotGraphNodeSelector.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeEnvironmentView.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.NativeStorage.cs.meta',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeNativeStorageAssets.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeNativeStorageAssets.cs.meta',
        'unity/Assets/Game/Resources/Home/NativeStorageAssets.asset',
        'unity/Assets/Game/Resources/Home/NativeStorageAssets.asset.meta',
        'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
        'unity/Assets/Game/Runtime/Offline/Equipment/OfflineEquipmentProfile.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineRefreshClock.cs', 'docs/common/refresh-time.md'):
        read(ROOT / path)
    read(ROOT / 'scripts/prepare_unity_profession_tipdot_sources.py')
    for name in ('PlayerEquipDataExt',): read(code / 'common-source/Common' / (name + '.cs'))
    read(code / 'common-source/PlayerEquipPurifyUtils.cs')
    for name in ('official_apk.py', 'official_source.py', 'official_manifest.py', 'official_tables.py', 'official_metadata.py', 'official_bundle_reader.py'):
        read(ROOT / 'scripts' / name)
    read(Path(__file__))
    validations = [ROOT / name for name in ('scripts/test_bag_tip_dot_bindings.ps1',
        'scripts/fixtures/bag_tip_dot_bindings_validation.cs', 'scripts/fixtures/home_storage_pointer_validation.cs')]
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, source_selection_sha256=sha((ROOT/'official-source.json').read_bytes()),
        inputs=inputs, outputs=outputs, configuration=configuration, graph=dict(configuration=row, resource=graph, installed=graph_path.relative_to(ROOT).as_posix()),
        validation_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in validations if path.exists()},
        adaptation='The complete original Bag graph uses the existing Home tip factory/controller, original query modules and current local profile/furniture state. Committed snapshot deltas replace server messages; unknown award context stays null, no invented split/recycle events. Local daily invalidation follows the confirmed monotonic local refresh boundary.',
        scope='Full Bag graph and default native storage in the current Home. Native room replacement, decorating variants, character interaction graph and Windows Player remain separate.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared complete original Bag graph and Home data bindings')


if __name__ == '__main__':
    prepare()
