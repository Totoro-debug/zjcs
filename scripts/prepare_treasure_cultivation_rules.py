"""Migrate original TreasureHelper promotion checks through explicit local ports."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_pet_score_chest_preview import original_method

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/TreasureCultivation'
RECEIPT = ROOT / 'assets/reference/treasure-cultivation-checks-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/treasure_cultivation/TreasureCultivationServices.cs'
METHODS = (
    'public static int CanActivateTreasure(', 'public static int CanMergeTreasure(',
    'public static bool PieceCanMerge(', 'public static Dictionary<int, long> GetTreasureUpgradeMaterial(',
    'public static int GetTreasureNextLevelUpgradeMaterial(', 'public static int CanTreasureUpgrade(',
    'public static bool IsItemEffectTreasurePromote(',
)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def selected_items(source):
    apk = Apk(source.version, reuse_current_cache=True)
    constructors = Constructors(source.prepared_path / 'official-code')
    try:
        obj = apk.asset('defaultpackage_assets_config_luban.bundle', '/item.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = constructors.table(raw, 'TbItemInfo')
        result = [{key: row[key] for key in ('ClassId', 'Quality', 'ItemType')} for row in rows]
        inputs = {}
        for name, text in constructors.sources.items():
            path = constructors.source_root / (name + '.cs')
            if not path.is_file():
                path = constructors.source_root.parent / 'Common.LubanGen' / (name + '.cs')
            if text:
                inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return result, dict(source=apk.provenance(obj), decoded_sha256=sha(raw),
            metadata_sha256=sha((json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode())), inputs
    finally:
        apk.reader.close()


def adapt(text):
    text = text.replace('public static ', 'public ')
    text = text.replace('ItemMergeInfoParser itemMergeInfoParser = Parsers.Instance.ItemMergeInfoParser;', '')
    text = text.replace('itemMergeInfoParser.CalcMergeCost(', 'tables.CalcMergeCost(')
    for before, after in (
        ('Parsers.Instance.TreasureInfoParser.Get(', 'tables.GetTreasure('),
        ('Parsers.Instance.ItemInfoParser.Get(', 'tables.GetItemInfo('),
        ('Parsers.Instance.TreasureUpgradeInfoParser.Get(', 'tables.GetUpgrade('),
        ('Parsers.Instance.TreasureUpgradeInfoParser.GetMergeMaterials(', 'tables.GetMergeMaterials('),
        ('Parsers.Instance.TreasureUpgradeInfoParser.GetUpgradeMaterials(', 'tables.GetUpgradeMaterials('),
        ('Parsers.Instance.TreasureUpgradeInfoParser.GetSpecialUpgradeMaterials(', 'tables.GetSpecialUpgradeMaterials('),
        ('Parsers.Instance.TreasureUpgradeInfoParser.GetStarUpNonPiece(', 'tables.GetStarUpNonPiece('),
        ('Parsers.GameSettings.TreasureSettings.CommonMaterial2Piece.TryGetValue(', 'tables.TryGetCommonMaterial('),
        ('Parsers.Instance.TreasureUpgradeInfoParser.MaterialsClassId.Contains(', 'tables.IsUpgradeMaterial('),
        ('Parsers.Instance.TreasureLevelInfoParser.MaterialsClassId.Contains(', 'tables.IsLevelMaterial('),
        ('Formula.GetTreasureIdByPieceId(', 'tables.GetTreasureIdByPieceId('),
        ('Formula.GetPieceIdByTreasureId(', 'tables.GetPieceIdByTreasureId('),
    ):
        text = text.replace(before, after)
    for before, after in (('PlayerItemDataWrap', 'TreasureItem'), ('PlayerItemParamTreasureWrap', 'TreasureState'),
                          ('Player', 'ITreasureCultivationPlayer')):
        text = re.sub(r'\b' + before + r'\b', after, text)
    if any(word in text for word in ('Parsers.', 'Formula.', 'ItemMergeInfoParser')):
        raise ValueError('Unbound original promotion dependency')
    return '\n'.join(line.rstrip() for line in text.split('\n'))


def prepare(check=False, oracle=None):
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    source = OfficialSource(prior['manifest'] if prior else None)
    code = source.prepared_path / 'official-code'
    proof = source.prepared_path / 'code-manifest.json'
    identity = json.loads(proof.read_bytes())
    assembly = code / 'managed/decoded/Console.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Original treasure check assembly differs')
    paths = [proof, assembly, code / 'common-source/TreasureHelper.cs',
             code / 'common-source/Common/ItemType.cs', TEMPLATE, Path(__file__)]
    text = paths[2].read_text('utf-8-sig').replace('\r\n', '\n')
    bodies = [original_method(text, signature) for signature in METHODS]
    item_rows, item_proof, item_inputs = selected_items(source)
    enums = paths[3].read_text('utf-8-sig')
    for name, value in (('TreasurePiece', 306), ('CommonTreasureMaterial', 427)):
        if not re.search(r'\b' + name + r'\s*=\s*' + str(value) + r'\s*,', enums):
            raise ValueError('Original treasure item type differs: ' + name)
    if oracle:
        oracle.parent.mkdir(parents=True, exist_ok=True)
        parser_text = (code / 'common-source/Common/TreasureUpgradeInfoParser.cs').read_text('utf-8-sig')
        cost_methods = ('public Dictionary<int, long> GetMergeMaterials(',
            'public Dictionary<int, long> GetUpgradeMaterials(int treasureClassId,',
            'public Dictionary<int, long> GetSpecialUpgradeMaterials(',
            'public Dictionary<int, long> GetUpgradeMaterials(PlayerItemDataWrap',
            'public IEnumerable<KeyValuePair<int, long>> GetStarUpNonPiece(')
        oracle.write_text('using System;\nusing System.Collections.Generic;\nusing System.Linq;\n'
            'namespace TreasureOracle { public static class OriginalTreasureHelper {\n'
            + '\n\n'.join(bodies) + '\n}\npublic sealed partial class OriginalTreasureCostParser {\n'
            + '\n\n'.join(original_method(parser_text, sig) for sig in cost_methods) + '\n}}\n', 'utf8', newline='\n')
        (oracle.parent / 'TreasureCultivationItems.json').write_text(
            json.dumps(item_rows, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        return
    header = ('// Original TreasureHelper promotion methods; explicit player/parser ports.\n'
        'using System;\nusing System.Collections.Generic;\nusing System.Linq;\n'
        'namespace Zjcs.Official.TreasureCultivation {\npublic sealed class OfficialTreasureCultivationRules {\n'
        'private readonly ITreasureCultivationTables tables;\n'
        'public OfficialTreasureCultivationRules(ITreasureCultivationTables tables) { this.tables = tables ?? throw new ArgumentNullException(nameof(tables)); }\n')
    outputs = {
        DEST / 'OfficialTreasureCultivationRules.cs': (header + '\n\n'.join(adapt(body) for body in bodies) + '\n}}\n').encode(),
        DEST / TEMPLATE.name: TEMPLATE.read_bytes(),
        DEST / 'Zjcs.Official.TreasureCultivation.asmdef': (json.dumps(dict(name='Zjcs.Official.TreasureCultivation',
            references=[], noEngineReferences=True), indent=2) + '\n').encode(),
    }
    for path in list(outputs) + [DEST]:
        kind = 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter' if path.suffix == '.cs' else 'DefaultImporter'
        guid = hashlib.md5(('zjcs:treasure-cultivation:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path) + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid + '\n'
            + ('folderAsset: yes\n' if path == DEST else '') + kind + ':\n  externalObjects: {}\n').encode()
    for path, raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes() != raw:
                raise ValueError('Generated treasure check differs: ' + str(path))
        else:
            if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != (prior or {}).get('outputs', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another treasure check edit: ' + str(path))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
    existing = ROOT / 'assets/reference/treasure-rules-source-manifest.json'
    catalog_receipt = json.loads(existing.read_bytes())
    catalog_code = ROOT / '.tools/source-packages/current' / catalog_receipt['manifest'] / 'official-code/common-source'
    old_helper = catalog_code / 'TreasureHelper.cs'
    if [original_method(old_helper.read_text('utf-8-sig'), sig) for sig in METHODS] != bodies:
        raise ValueError('Previously migrated catalog needs a separate promotion behavior review')
    latest_parser = code / 'common-source/Common/TreasureUpgradeInfoParser.cs'
    old_parser = catalog_code / 'Common/TreasureUpgradeInfoParser.cs'
    if old_parser.read_bytes() != latest_parser.read_bytes():
        raise ValueError('Previously migrated material parser differs from selected original')
    paths += [existing, old_helper, old_parser, latest_parser,
        code / 'common-source/Common/PlayerItemManagerExt.cs', code / 'common-source/Common/Formula.cs',
        code / 'common-source/Common/TreasureInfoParser.cs', code / 'global-metadata.dat',
        ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_apk.py',
        ROOT / 'scripts/prepare_pet_score_chest_preview.py']
    bindings = [ROOT / p for p in (
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureService.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureService.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/Zjcs.Offline.Progression.asmdef',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCostRules.cs',
        'scripts/fixtures/treasure_cultivation_validation.cs',
        'scripts/fixtures/treasure_cultivation_oracle.cs', 'scripts/test_treasure_cultivation.ps1')]
    result = dict(manifest=source.version, catalog_manifest=catalog_receipt['manifest'],
        item_metadata=item_proof,
        inputs=dict({p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in paths}, **item_inputs),
        runtime_bindings={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in bindings},
        methods={signature: sha(body.encode()) for signature, body in zip(METHODS, bodies)},
        outputs={p.relative_to(ROOT).as_posix(): sha(raw) for p, raw in outputs.items()},
        scope='Original activation, merge, piece hint, star-up material selection and eligibility checks. Slot-level rules, transactions and native UI are separate owners.')
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode()
    if check:
        if RECEIPT.read_bytes() != raw:
            raise ValueError('Treasure check receipt differs')
    else:
        RECEIPT.write_bytes(raw)
    print('Verified original treasure promotion methods:', len(bodies))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--oracle', type=Path)
    args = parser.parse_args()
    prepare(args.check, args.oracle)
