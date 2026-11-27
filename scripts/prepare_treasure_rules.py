"""Extract selected official Treasure rules without changing table order."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors


DEST = ROOT / 'unity/Assets/Game/Resources/Treasure/treasure-rules.json'
RECEIPT = ROOT / 'assets/reference/treasure-rules-source-manifest.json'


def write_oracle(destination):
    from prepare_pet_score_chest_preview import original_method
    prior = json.loads(RECEIPT.read_bytes())
    code = ROOT / '.tools/source-packages/current' / prior['manifest'] / 'official-code/common-source/Common'
    texts = {}
    for name in ('TreasureInfo', 'TreasureSuitInfoParser', 'TreasureUpgradeInfoParser', 'Formula'):
        path = code / (name + '.cs')
        if sha(path.read_bytes()) != prior['decoder_inputs'][path.relative_to(ROOT).as_posix()]:
            raise ValueError('Original Treasure oracle source differs: ' + name)
        texts[name] = path.read_text('utf8')
    result = 'using System;\nusing System.Collections.Generic;\nusing System.Linq;\n'
    result += 'public sealed class OriginalTreasureRule { public int ClassId, StarMax; public object Effect; public Dictionary<int,int> LevelPropId;\n'
    result += 'public int UpgradeMaxStar => StarMax - 1;\n'
    for signature in ('public int GetInitialHitCount()', 'public int GetLevelPropId('):
        result += original_method(texts['TreasureInfo'], signature) + '\n'
    result += '}\npublic sealed class OriginalTreasureSuitParser { public void Process(TreasureSuitInfo value) { processInfo(value); }\n'
    result += original_method(texts['TreasureSuitInfoParser'], 'private void processInfo(') + '\n}\n'
    result += 'public sealed partial class OriginalTreasureCostParser {\n'
    for signature in ('public Dictionary<int, long> GetMergeMaterials(',
                      'public Dictionary<int, long> GetUpgradeMaterials(int treasureClassId,',
                      'public Dictionary<int, long> GetSpecialUpgradeMaterials('):
        result += original_method(texts['TreasureUpgradeInfoParser'], signature) + '\n'
    result += '}\npublic static class Formula {\n'
    result += original_method(texts['Formula'], 'public static int GetPieceIdByTreasureId(') + '\n}\n'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(result, 'utf8', newline='\n')
    apk = Apk(prior['manifest'], reuse_current_cache=True)
    constructors = Constructors(code.parent.parent)
    rows = {}
    try:
        for name, kind in (
            ('treasure', 'TbTreasureInfo'), ('treasure_suit', 'TbTreasureSuitInfo'),
            ('treasure_levelup_material', 'TbTreasureLevelInfo'), ('treasure_upgrade_material', 'TbTreasureUpgradeInfo'),
            ('treasure_bless', 'TbTreasureBlessInfo'), ('random_treasure_exchange', 'TbRandomTreasureExchangeInfo')):
            raw = apk.asset('defaultpackage_assets_config_luban.bundle', '/' + name + '.g.bytes').read().m_Script.encode('utf8', 'surrogateescape')
            if sha(raw) != prior['inputs'][name + '.g.bytes']:
                raise ValueError('Original oracle table differs: ' + name)
            rows[name] = constructors.table(raw, kind)
        raw = apk.asset('defaultpackage_assets_config_luban.bundle', '/game_settings.g.bytes').read().m_Script.encode('utf8', 'surrogateescape')
        if sha(raw) != prior['inputs']['game_settings.g.bytes']:
            raise ValueError('Original oracle settings differ')
        settings = constructors.table(raw, 'TbGameSettings')
        rows['settings'] = {key: settings[key] for key in ('TreasureMaxSlotCount', 'TreasureSlotElementTypes',
            'TreasureSlotPropAddRate', 'TreasureMaxLevel', 'TreasurePlanMaxCount', 'TreasureSettings')}
        raw = apk.asset('defaultpackage_assets_config_luban.bundle', '/item.g.bytes').read().m_Script.encode('utf8', 'surrogateescape')
        if sha(raw) != prior['inputs']['item.g.bytes']:
            raise ValueError('Original oracle item table differs')
        treasure_ids = {row['ClassId'] for row in rows['treasure']}
        rows['items'] = [{'ClassId': row['ClassId'], 'Quality': row['Quality']}
                         for row in constructors.table(raw, 'TbItemInfo') if row['ClassId'] in treasure_ids]
    finally:
        apk.reader.close()
    destination.with_suffix('.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def _materials_list(mapping: dict | None) -> list[dict]:
    if not mapping:
        return []
    return [{'ItemId': int(k), 'Count': int(v)} for k, v in mapping.items()]


def prepare(*, check=False):
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    source = OfficialSource(prior['manifest'] if prior else None)
    code_root = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    apk = Apk(source.version, reuse_current_cache=True)
    constructors = Constructors(code_root)
    bundle = 'defaultpackage_assets_config_luban.bundle'
    table_sources = {}

    def load_table(filename: str, table_name: str):
        obj = apk.asset(bundle, '/' + filename)
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        table_sources[filename] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        return constructors.table(raw, table_name), sha(raw)

    try:
        levelup_rows, levelup_sha = load_table('treasure_levelup_material.g.bytes', 'TbTreasureLevelInfo')
        upgrade_rows, upgrade_sha = load_table('treasure_upgrade_material.g.bytes', 'TbTreasureUpgradeInfo')
        bless_rows, bless_sha = load_table('treasure_bless.g.bytes', 'TbTreasureBlessInfo')
        exchange_rows, exchange_sha = load_table('random_treasure_exchange.g.bytes', 'TbRandomTreasureExchangeInfo')
        treasure_rows, treasure_sha = load_table('treasure.g.bytes', 'TbTreasureInfo')
        suit_rows, suit_sha = load_table('treasure_suit.g.bytes', 'TbTreasureSuitInfo')
        gs, gs_sha = load_table('game_settings.g.bytes', 'TbGameSettings')
        _, item_sha = load_table('item.g.bytes', 'TbItemInfo')
    finally:
        apk.reader.close()

    ts = gs['TreasureSettings']
    payload = {
        'Manifest': source.version,
        'Settings': {
            'TreasureMaxSlotCount': int(gs['TreasureMaxSlotCount']),
            'TreasureSlotElementTypes': [int(x) for x in gs['TreasureSlotElementTypes']],
            'TreasureSlotPropAddRate': float(gs['TreasureSlotPropAddRate']),
            'TreasureMaxLevel': int(gs['TreasureMaxLevel']),
            'TreasurePlanMaxCount': int(gs['TreasurePlanMaxCount']),
            'CommonMaterial2Piece': [
                {
                    'Quality': int(q),
                    'ItemClassId': int(v['ItemClassId']),
                    'Item2PieceCount': int(v['Item2PieceCount']),
                }
                for q, v in ts['CommonMaterial2Piece'].items()
            ],
            'PieceSplitSetting': [
                {
                    'Quality': int(q),
                    'GroupCount': int(v['GroupCount']),
                    'Award': _materials_list(v.get('Award')),
                }
                for q, v in ts['PieceSplitSetting'].items()
            ],
        },
        'LevelUpMaterials': [
            {
                'Level': int(r['Level']),
                'Materials': _materials_list(r.get('Materials')),
            }
            for r in levelup_rows
        ],
        'UpgradeMaterials': [
            {
                'Quality': int(r['Quality']),
                'Star': int(r['Star']),
                'PieceCount': int(r['PieceCount']),
                'SpecialPieceCount': int(r['SpecialPieceCount']),
                'Materials': _materials_list(r.get('Materials')),
            }
            for r in upgrade_rows
        ],
        'BlessInfos': [
            {
                'Rank': int(r['Rank']),
                'BlessLevel': int(r['BlessLevel']),
                'LevelCostItems': _materials_list(r.get('LevelCostItems')),
            }
            for r in bless_rows
        ],
        'RandomExchanges': [
            {
                'ItemId': int(r['ItemId']),
                'RequireCount': int(r['RequireCount']),
                'RewardRuleIds': [int(x) for x in (r.get('RewardRuleIds') or [])],
            }
            for r in exchange_rows
        ],
        'Treasures': [
            {
                'ClassId': int(r['ClassId']),
                'TreasurePieceId': int(r['TreasurePieceId']),
                'StarMax': int(r['StarMax']),
                'StarPropTypes': [int(x) for x in (r.get('StarPropTypes') or [])],
                'StarScalePropId': int(r['StarScalePropId']),
                'LevelPropTypes': [int(x) for x in (r.get('LevelPropTypes') or [])],
                'EffectPropTypes': [int(x) for x in (r.get('EffectPropTypes') or [])],
                'LevelPropId': [
                    {'SeasonId': int(k), 'PropId': int(v)}
                    for k, v in (r.get('LevelPropId') or {}).items()
                ],
                'ObtainedShow': bool(r['ObtainedShow']),
                'LimitUseCommonMaterial': bool(r['LimitUseCommonMaterial']),
                'IsLimited': bool(r['IsLimited']),
                'EffectType': str((r.get('RawEffect') or {}).get('_type', '')),
                'EffectPerCount': int((r.get('RawEffect') or {}).get('PerCount', 0)),
                'EffectMaxHitCount': int((r.get('RawEffect') or {}).get('MaxHitCount', 0)),
            }
            for r in treasure_rows
        ],
        'Suits': [
            {
                'ClassId': int(r['ClassId']),
                'Treasures': [int(x) for x in (r.get('Treasures') or [])],
                'Props': [
                    {
                        'Condition': int(cond),
                        'PropId': int(pid),
                        'PropTypes': [
                            int(pt)
                            for pt in r['PropTypes'][index]
                        ],
                    }
                    for index, (cond, pid) in enumerate((r.get('Props') or {}).items())
                ],
            }
            for r in suit_rows
        ],
    }

    encoded = (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    meta = (
        'fileFormatVersion: 2\n'
        f"guid: {hashlib.md5(b'zjcs:treasure-rules:json').hexdigest()}\n"
        'TextScriptImporter:\n'
        '  externalObjects: {}\n'
        '  userData:\n'
        '  assetBundleName:\n'
        '  assetBundleVariant:\n'
    )

    receipt = {
        'manifest': source.version,
        'inputs': {
            'treasure_levelup_material.g.bytes': levelup_sha,
            'treasure_upgrade_material.g.bytes': upgrade_sha,
            'treasure_bless.g.bytes': bless_sha,
            'random_treasure_exchange.g.bytes': exchange_sha,
            'treasure.g.bytes': treasure_sha,
            'treasure_suit.g.bytes': suit_sha,
            'game_settings.g.bytes': gs_sha,
            'item.g.bytes': item_sha,
        },
        'outputs': {
            DEST.relative_to(ROOT).as_posix(): sha(encoded),
        },
        'generator_sha256': sha(Path(__file__).read_bytes()),
        'table_sources': table_sources,
    }
    receipt['source'] = apk.source.profile
    receipt['decoder_inputs'] = {
        (code_root / 'global-metadata.dat').relative_to(ROOT).as_posix(): sha((code_root / 'global-metadata.dat').read_bytes()),
    }
    for name, text in constructors.sources.items():
        path = constructors.source_root / (name + '.cs')
        if not path.is_file():
            path = constructors.source_root.parent / 'Common.LubanGen' / (name + '.cs')
        if text:
            receipt['decoder_inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('TreasureInfo', 'TreasureSuitInfoParser', 'TreasureUpgradeInfoParser', 'Formula'):
        path = constructors.source_root / (name + '.cs')
        receipt['decoder_inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt['outputs'][Path(str(DEST) + '.meta').relative_to(ROOT).as_posix()] = sha(meta.encode())
    receipt['runtime_bindings'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCatalog.cs.meta',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCostRules.cs',
        ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureCostRules.cs.meta',
        ROOT / 'scripts/fixtures/treasure_catalog_validation.cs', ROOT / 'scripts/test_treasure_catalog.ps1',
        ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_apk.py',
        ROOT / 'scripts/prepare_pet_score_chest_preview.py')}
    receipt['scope'] = 'Selected original treasure cultivation/suit/settings tables, lookup helpers and original merge/cumulative promotion material queries. Cultivation transactions, native windows, HUD and production entry are not validated by these queries.'
    receipt_raw = (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf8')
    if check:
        if not prior or receipt_raw != RECEIPT.read_bytes() or encoded != DEST.read_bytes() or meta.encode() != Path(str(DEST) + '.meta').read_bytes():
            raise ValueError('Treasure rules or verified source receipt differs; rebuild from the selected original inputs.')
        print('Verified original Treasure tables, order, decoder sources and runtime payload.')
        return
    if prior:
        if any(receipt['inputs'].get(name) != digest for name, digest in prior['inputs'].items()):
            raise ValueError('Selected original Treasure table hashes changed.')
        for path, expected in prior['outputs'].items():
            if sha((ROOT / path).read_bytes()) != expected:
                raise ValueError('Refusing to overwrite changed Treasure output: ' + path)
    DEST.parent.mkdir(parents=True, exist_ok=True)
    DEST.write_bytes(encoded)
    Path(str(DEST) + '.meta').write_text(meta, encoding='utf-8', newline='\n')
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes(receipt_raw)
    print(f'Wrote {DEST.relative_to(ROOT)} ({len(payload["Treasures"])} treasures, {len(payload["Suits"])} suits)')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--oracle', type=Path)
    args = parser.parse_args()
    if args.oracle:
        write_oracle(args.oracle)
    else:
        prepare(check=args.check)
