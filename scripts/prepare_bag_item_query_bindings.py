"""Bind original Bag item queries to selected configuration and local profile data."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from official_tables import Constructors


RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/BagItemQueries'
ASSETS = ROOT / 'unity/Assets/Game/Resources/Home/BagItemQueries'
RECEIPT = ROOT / 'assets/reference/bag-item-query-bindings-source-manifest.json'
TABLES = ('item', 'equip_piece_merge', 'item_piece_merge', 'random_treasure_exchange',
    'pet', 'period_card_item', 'periodic_card_pay', 'cooking', 'equip_attribute_replace_item', 'game_settings')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in re.findall(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    assets = read_asset_manifest(source.manifest_bytes('DefaultPackage'))
    selected = {}
    for name in TABLES:
        matches = [row for row in assets if row['path'].lower().endswith('/' + name + '.g.bytes')]
        if len(matches) != 1:
            raise ValueError('Resolve the actual selected Bag table address: ' + name)
        selected[name] = matches[0]
    tables, proofs = {}, {}
    with Apk.current() as apk:
        for bundle in dict.fromkeys(row['bundle'] for row in selected.values()):
            container = {path.lower(): obj for path, obj in apk.container(bundle)}
            for name, row in selected.items():
                if row['bundle'] != bundle:
                    continue
                obj = container[row['path'].lower()].deref()
                raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
                tables[name] = constructors.table(raw, mapping[name])
                proofs[name] = dict(source=apk.provenance(obj), asset=row['path'], constructor=mapping[name],
                    payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data()))
    inputs = {}

    def input_file(path):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())

    for name in constructors.sources:
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.is_file(): path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.is_file(): input_file(path)
        elif name not in ('Vector2', 'Vector3', 'Pos2d'):
            raise ValueError('Verify the actual Bag table decoder source: ' + name)
    for name in ('CookingInfoParser', 'PetInfoParser', 'PlayerPayItemManager',
        'PlayerPayItemKeepDaysBoughtInfoWrap', 'TimeUtils', 'CookingInfo', 'UsedItemCondition',
        'EquipPropBase', 'PlayerItemParamEquipWrap', 'PlayerItemPurifyPropsWrap', 'PlayerItemManagerExt', 'PlayerPetDataManager'):
        input_file(code / 'common-source/Common' / (name + '.cs'))
    input_file(code.parent / 'code-manifest.json')
    for name in ('official_apk.py', 'official_source.py', 'official_tables.py', 'official_manifest.py', 'official_metadata.py', 'official_bundle_reader.py'):
        input_file(ROOT / 'scripts' / name)
    input_file(Path(__file__))
    query_receipt = ROOT / 'assets/reference/bag-item-queries-source-manifest.json'
    input_file(query_receipt)
    for name, expected in json.loads(query_receipt.read_bytes())['outputs'].items():
        path = ROOT / name
        if sha(path.read_bytes()) != expected: raise ValueError('The verified original Bag query differs: ' + name)
        input_file(path)

    def rows(name, keys):
        return [{key: row[key] for key in keys} for row in tables[name]]

    cooking = []
    for row in tables['cooking']:
        condition = row['Condition']
        kind = condition['_type']
        if kind not in ('AlwaysCondition', 'NoneCondition', 'MapUnlockCondition', 'UsedItemCondition'):
            raise ValueError('Bind the actual local cooking condition: ' + kind)
        if kind == 'UsedItemCondition' and condition['UseSourceOperate'] != 89:
            raise ValueError('Bind the actual item-used record owner for the selected cooking formula')
        cooking.append(dict(Id=row['Id'], ConditionType=kind,
            ConditionItemId=condition.get('ClassId', 0), UseSourceOperate=condition.get('UseSourceOperate', 0), MapClassId=condition.get('MapClassId', 0)))
    settings = tables['game_settings']
    payload = dict(Items=rows('item', ('ClassId', 'ItemType', 'RedDotType', 'Quality')),
        EquipPieces=rows('equip_piece_merge', ('Id', 'PieceMergeCount')),
        ItemPieces=rows('item_piece_merge', ('Id', 'PieceMergeCount')),
        RandomPieces=rows('random_treasure_exchange', ('ItemId', 'RequireCount')),
        Pets=rows('pet', ('ClassId', 'PetPieceId', 'PieceMergeCount')),
        CardItems=rows('period_card_item', ('ItemId', 'PeriodCardId')),
        Cards=rows('periodic_card_pay', ('Id',)), Cookings=cooking,
        Replacements=rows('equip_attribute_replace_item', ('ItemId', 'AttributePropId', 'Quality')),
        ShowTypes=settings['ShowCanUseRedDotItemTypes'], EquipTypes=settings['EquipTypes'],
        PeriodicCardMaxDurationDay=settings['Pay']['PeriodicCardMaxDurationDay'])
    if len({row['ClassId'] for row in payload['Items']}) != len(payload['Items']): raise ValueError('Ambiguous original item identity')
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another Bag owner change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        kind = 'DefaultImporter' if folder else 'TextScriptImporter' if path.suffix == '.json' else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:bag-item-query-bindings:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest() + '\n'
        text += 'folderAsset: yes\n' if folder else ''
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    metadata(RUNTIME, True); metadata(ASSETS, True)
    for name in ('OfflineBagItemQueryCatalog.cs', 'OfflineBagItemQueries.cs'):
        path = RUNTIME / name; input_file(path); outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes()); metadata(path)
    definition = RUNTIME / 'Zjcs.Offline.BagItemQueries.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Offline.BagItemQueries', references=[
        'Zjcs.Offline.Progression', 'Zjcs.Offline.Equipment', 'Zjcs.Offline.Commerce', 'Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagTipDots']), indent=2) + '\n').encode()); metadata(definition)
    interface = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/IOfflineInventoryItemIndicators.cs'
    input_file(interface); outputs[interface.relative_to(ROOT).as_posix()] = sha(interface.read_bytes()); metadata(interface)
    path = ASSETS / 'catalog.json'
    write(path, (json.dumps(payload, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode()); metadata(path)
    validations = [ROOT / path for path in ('scripts/fixtures/bag_item_query_bindings_validation.cs', 'scripts/test_bag_item_query_bindings.ps1')]
    for path in (ROOT / 'docs/common/refresh-time.md', ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineRefreshClock.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Equipment/OfflineEquipmentProfile.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Commerce/OfflineCommerceSession.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineInventoryCatalog.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Inventory/InventoryFeatureRuntime.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Inventory/OfficialInventoryPanelView.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.NativeFrame.cs',
        ROOT / 'unity/Assets/Game/Runtime/Official/HomeScreenUI/SafeArea.cs',
        ROOT / 'unity/Assets/Game/Runtime/Official/HomeScreenUI/CanvasWidthAdjuster.cs',
        ROOT / 'unity/Assets/Game/Runtime/Presentation/Inventory/Zjcs.Presentation.Inventory.asmdef'):
        input_file(path)
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, source_selection_sha256=sha((ROOT/'official-source.json').read_bytes()),
        configuration=proofs, inputs=inputs, outputs=outputs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validations if p.is_file()},
        adaptation='Original Bag queries read current local inventory, pets, periodic-card expiry, formula-use records and equipment including persisted pending purify properties. Calendar day counts follow the shared user-confirmed local refresh boundary. Query reads do not mutate or reload a save.',
        scope='Local item query owner and formal inventory presentation. Complete native storage/city and Windows Player gameplay remain separate.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared local Bag query configuration and bindings:', len(payload['Items']), 'original items')


if __name__ == '__main__':
    prepare()
