"""Extract the official item-merge parser and its complete local rule closure."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_pet_score_chest_preview import original_method

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/ItemMerge'
PAYLOAD = ROOT / 'unity/Assets/Game/Resources/ItemMerge/item-merge-rules.json'
RECEIPT = ROOT / 'assets/reference/item-merge-rules-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/item_merge/ItemMergeServices.cs'
METHODS = (
    'public override void PostParse(', 'public Dictionary<int, long> GetMaterials(',
    'public long CalcCanMergeAmout(', 'public Dictionary<int, long> CalcMergeCost(',
    'public void CalcMergeItemCostBaseMaterials(', 'public void CalcPlayerMergeItemCost(',
    'private void CalcPlayerMergeItemCostImpl(', 'public bool CanMerge(', 'public bool IsBaseMaterial(',
    'public float GetMergeCountFactor(', 'public List<ItemMergeInfo> GetItemMergeInfosByMaterialId(',
    'public List<ItemMergeInfo> GetItemMergeInfosByType(', 'public List<ItemMergeInfo> GetItemMergeInfosByTypeList(',
    'public void GetMaterialItemIds(',
)
LEVEL_METHODS = ('public float GetNumber(', 'public double GetNumberDouble(', 'public List<double> GetNumbers(', 'public float GetNumberLerp(')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def adapt(body):
    body = body.replace('public override void PostParse()', 'private void PostParse()')
    body = body.replace('ItemInfoParser parser = ((ParserBase)this).ConfigManager.GetParser<ItemInfoParser>();', '')
    body = body.replace('ItemInfo itemInfo = parser.Get(value.ProductId);', 'int itemType = readItemType(value.ProductId);')
    body = body.replace('itemInfo.ItemType', 'itemType')
    body = body.replace('Parsers.GameSettings.MaxMergeAmountAtATime', 'maxMergeAmountAtATime')
    body = body.replace('((ParserBase)this).ConfigManager.GetParser<LevelNumberParser>().GetNumber(', 'levelNumber(')
    body = re.sub(r'\bPlayer\b', 'IItemMergeInventory', body)
    body = body.replace('ItemType itemType', 'int itemType').replace('List<ItemType>', 'List<int>').replace('foreach (ItemType ', 'foreach (int ')
    body = body.replace('CollectionExtensions.GetValueOrDefault<int, long>((IReadOnlyDictionary<int, long>)costs, ', 'Existing(costs, ')
    if any(token in body for token in ('ParserBase', 'Parsers.', 'ItemInfoParser', 'CollectionExtensions.')):
        raise ValueError('Unbound item merge dependency')
    return '\n'.join(line.rstrip() for line in body.split('\n'))


def level_adapt(body):
    body = body.replace('LevelNumber levelNumber', 'ItemMergeLevelNumber levelNumber')
    # Only the client logging boundary changes; the original error return stays.
    begin = body.find('\t\t\tGLoggerErrorInterpolatedStringHandler ')
    if begin >= 0:
        end = body.index('\n\t\t\treturn ', begin)
        body = body[:begin] + '\t\t\tmissing(id);' + body[end:]
    return body


def prepare(check=False, oracle=None):
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    source = OfficialSource(prior['manifest'] if prior else None)
    code = source.prepared_path / 'official-code'
    proof_path = source.prepared_path / 'code-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    assembly = code / 'managed/decoded/Console.dll'
    if proof['manifest'] != source.version or sha(assembly.read_bytes()) != proof['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Selected item merge assembly differs')
    parser = code / 'common-source/Common/ItemMergeInfoParser.cs'
    level_parser = code / 'common-source/Common/LevelNumberParser.cs'
    bodies = [original_method(parser.read_text('utf-8-sig'), signature) for signature in METHODS]
    levels = [original_method(level_parser.read_text('utf-8-sig'), signature) for signature in LEVEL_METHODS]
    apk = Apk(source.version, reuse_current_cache=True)
    constructors = Constructors(code)
    table_sources = {}

    def table(name, kind):
        obj = apk.asset('defaultpackage_assets_config_luban.bundle', '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        table_sources[name] = dict(source=apk.provenance(obj), decoded_sha256=sha(raw), object_sha256=sha(obj.get_raw_data()))
        return constructors.table(raw, kind)

    try:
        recipes = table('item_merge', 'TbItemMergeInfo')
        items = table('item', 'TbItemInfo')
        numbers = table('level_number', 'TbLevelNumber')
        settings = table('game_settings', 'TbGameSettings')
    finally:
        apk.reader.close()
    products = {row['ProductId'] for row in recipes}
    level_ids = {row['LevelFactorId'] for row in recipes if row['LevelFactorId'] > 0}
    data = dict(MaxMergeAmountAtATime=settings['MaxMergeAmountAtATime'], Recipes=recipes,
        ItemTypes=[{key: row[key] for key in ('ClassId', 'ItemType')} for row in items if row['ClassId'] in products],
        Levels=[row for row in numbers if row['Id'] in level_ids])
    if {row['ClassId'] for row in data['ItemTypes']} != products or {row['Id'] for row in data['Levels']} != level_ids:
        raise ValueError('Incomplete original item merge product/level closure')
    # The original recursive methods assume a finite recipe graph. Validate the
    # official table closure as input integrity, without changing those methods.
    by_id = {row['ProductId']: row for row in recipes}
    visited, visiting = set(), set()

    def visit(product):
        if product in visited or product not in by_id:
            return
        if product in visiting:
            raise ValueError('Cyclic official item merge input')
        visiting.add(product)
        for slot in range(1, 6):
            row = by_id[product]
            if row['Material' + str(slot)] > 0 and row['Count' + str(slot)] > 0:
                visit(row['Material' + str(slot)])
        visiting.remove(product)
        visited.add(product)
    for product in by_id:
        visit(product)
    encoded = (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode()
    if oracle:
        oracle.parent.mkdir(parents=True, exist_ok=True)
        oracle.write_text('using System;\nusing System.Collections.Generic;\nnamespace ItemMergeOracle {\n'
            'public sealed partial class OriginalItemMergeParser : ParserBase {\n' + '\n\n'.join(bodies)
            + '\n}\npublic sealed partial class LevelNumberParser {\n' + '\n\n'.join(levels) + '\n}}\n', 'utf8', newline='\n')
        oracle.with_suffix('.json').write_bytes(encoded)
        return
    outputs = {
        DEST / 'OfficialItemMergeRules.cs': ('// Original ItemMergeInfoParser methods; explicit inventory/configuration owners.\n'
            'using System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.ItemMerge {\n'
            'public sealed partial class OfficialItemMergeRules {\n' + '\n\n'.join(adapt(body) for body in bodies) + '\n}\n'
            'public sealed partial class ItemMergeLevelNumbers {\n' + '\n\n'.join(level_adapt(body) for body in levels) + '\n}}\n').encode(),
        DEST / TEMPLATE.name: TEMPLATE.read_bytes(),
        DEST / 'Zjcs.Official.ItemMerge.asmdef': (json.dumps(dict(name='Zjcs.Official.ItemMerge', references=[], noEngineReferences=True), indent=2) + '\n').encode(),
        PAYLOAD: encoded,
    }
    for path in list(outputs) + [DEST, PAYLOAD.parent]:
        guid = hashlib.md5(('zjcs:item-merge:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter' if path.suffix == '.cs' else 'TextScriptImporter' if path.suffix == '.json' else 'DefaultImporter'
        metadata = Path(str(path) + '.meta')
        outputs[metadata] = ('fileFormatVersion: 2\nguid: ' + guid + '\n' + ('folderAsset: yes\n' if path in (DEST, PAYLOAD.parent) else '')
            + kind + ':\n  externalObjects: {}\n').encode()
    inputs = [proof_path, assembly, parser, level_parser, code / 'common-source/Common/LubanKeyCsvParser.cs',
        code / 'global-metadata.dat', TEMPLATE, Path(__file__), ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_tables.py',
        ROOT / 'scripts/prepare_pet_score_chest_preview.py']
    for name, text in constructors.sources.items():
        if text:
            path = constructors.source_root / (name + '.cs')
            if not path.is_file():
                path = constructors.source_root.parent / 'Common.LubanGen' / (name + '.cs')
            inputs.append(path)
    for path, raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes() != raw:
                raise ValueError('Item merge output differs: ' + str(path))
        else:
            if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != (prior or {}).get('outputs', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another item merge edit: ' + str(path))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
    bindings = [ROOT / p for p in (
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineItemMergeCatalog.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineItemMergeCatalog.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureService.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/Zjcs.Offline.Progression.asmdef',
        'scripts/fixtures/item_merge_validation.cs', 'scripts/fixtures/item_merge_oracle.cs',
        'scripts/test_treasure_cultivation.ps1')]
    receipt = dict(manifest=source.version, inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in inputs},
        tables=table_sources, outputs={p.relative_to(ROOT).as_posix(): sha(raw) for p, raw in outputs.items()},
        runtime_bindings={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in bindings},
        methods={signature: sha(body.encode()) for signature, body in zip(METHODS + LEVEL_METHODS, bodies + levels)},
        scope='Original item merge indexing, recursive capacity/cost queries, level factors and treasure automatic-material cost binding. Item grant/consume transactions and native windows are separate owners.')
    raw = (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode()
    if check:
        if RECEIPT.read_bytes() != raw:
            raise ValueError('Item merge receipt differs')
    else:
        RECEIPT.write_bytes(raw)
    print('Verified original item merge recipes:', len(recipes), '; parser methods:', len(bodies) + len(levels))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--oracle', type=Path)
    args = parser.parse_args()
    prepare(args.check, args.oracle)
