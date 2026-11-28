"""Migrate original treasure slot level checks, caps, seasonal state and callbacks."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_tables import Constructors
from prepare_pet_score_chest_preview import original_method
from prepare_treasure_slots import adapted as slot_adapted

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureSlotLevels.cs'
DATA = ROOT / 'unity/Assets/Game/Resources/Treasure/treasure-slot-levels.json'
RECEIPT = ROOT / 'assets/reference/treasure-slot-levels-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/treasure_cultivation/TreasureSlotLevelPorts.cs'
METHODS = {
    'TreasureHelper': ('public static int CanTreasureSlotLevelUp(', 'private static int CheckLevelLimits(',
        'private static int CheckTreasureLevelLimitByPlayerLevel(', 'public static Dictionary<int, long> GetTreasureLevelUpCostMaterials(',
        'private static int CheckLevelMaterials(', 'public static bool CanTresureSlotBlessLevelUp('),
    'Player': ('public bool CanTresureSlotBlessLevelUp(', 'public void TreasureSlotLevelUp(',
        'public bool TreasureSlotBlessLevelUp(', 'public void ClearTreasureBlessLevel(',
        'public void TryUpdatePlayerTreasureSlotData(', 'public void FireOnTreasureSlotShareLevelUpdate(',
        'public void FireOnAllTreasureBlessUpdate(', 'public void FireOnTreasureRefresh('),
    'SystemLevelLimitInfoParser': ('private void SetInfo(', 'private void SetTreasureMaxLevelLimit(',
        'public SystemLevelLimitInfo GetLevelLimitInfo(', 'public int GetTreasureBaseLevelLimit(', 'public int GetTreasureBlessLevelLimit(',
        'public int GetMinSystemLimitInfo('),
    'SystemLevelGapLimitInfoParser': ('public override void PostParse(', 'public bool TryGetSystemLevelGapInfo(',
        'public bool IsBlessLevelGapLimit('),
    'FormulaHandler': ('public int GetMaxTreasureBlessLevel(',),
    'EnumExt': ('public static Rank ToRank(',),
    'PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap': ('public override void Init(', 'public bool AddBlessLevel(',),
    'HasItemCondition': ('public override bool IsMet(',),
    'HasItemTypeCondition': ('public override (long current, long target) GetProgress(', 'public override bool IsMet(', 'protected long GetValue('),
    'LevelCondition': ('public override bool IsMet(', 'private int GetLevel('),
    'CompositeOrCondition': ('public override bool IsMet(',),
    'Formula': ('public static bool Compare<T>(T lhs, string op, T rhs)',),
}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def adapt(body, owner):
    body = body.replace('public override void', 'public void').replace('base.Init(player, rank);', '')
    body = body.replace('((ParserBase)this).PostParse();', '')
    if owner == 'EnumExt':
        body = slot_adapted(body, owner).replace('reportError?.Invoke(', 'TreasureLevelDiagnostics.ReportError?.Invoke(')
    if owner in ('HasItemCondition','HasItemTypeCondition','LevelCondition'):
        body = slot_adapted(body,owner)
    who = 'player.' if owner == 'TreasureHelper' else ''
    body = body.replace('Formula.GetMaxTreasureBaseLevel(', who + 'Tables.Limits.GetTreasureBaseLevelLimit(')
    body = body.replace('Formula.Instance.GetMaxTreasureBlessLevel(', who + 'Tables.Formula.GetMaxTreasureBlessLevel(')
    body = body.replace('PlanesInfoUtils.PlanesRank', who + 'MyPlanesRank')
    for original, replacement in (
        ('Parsers.Instance.SystemLevelLimitInfoParser.', 'Tables.Limits.'),
        ('Parsers.Instance.SystemLevelGapLimitInfoParser.', 'Tables.Gaps.'),
        ('Parsers.Instance.TreasureBlessInfoParser.Get(', 'Tables.GetBlessCost('),
        ('Parsers.Instance.TreasureLevelInfoParser.Get(', 'Tables.GetLevelCost('),
        ('Parsers.Instance.AstrologicalSeasonConfigParser.GetSeasonByRank(', 'Tables.GetSeasonByRank('),
        ('Parsers.Instance.ItemMergeInfoParser.CalcCanMergeAmout(', 'Tables.CalcCanMergeAmount('),
    ):
        body = body.replace(original, who + replacement)
    if owner == 'FormulaHandler':
        body = body.replace('Tables.Limits.', 'limits.')
    if owner == 'SystemLevelGapLimitInfoParser':
        body = body.replace('Tables.Gaps.', '')
    if owner in ('Player', 'PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap'):
        body = slot_adapted(body, owner)
        if owner == 'Player': body = body.replace('reportError?.Invoke(', 'ReportError?.Invoke(')
    if owner == 'TreasureHelper':
        body = body.replace('Parsers.GameSettings.TreasureSlotElementTypes', 'player.Settings.TreasureSlotElementTypes')
        body = body.replace('Parsers.GameSettings.TreasureMaxSlotCount', 'player.Settings.TreasureMaxSlotCount')
        body = body.replace('ItemSourceDataManager.IsItemSourceFromMerge(player, itemId)', 'player.IsItemSourceFromMerge(itemId)')
        # Decompiled KeyValuePair.Deconstruct uses ref instead of out.
        body = body.replace('item.Deconstruct(ref num, ref num2);', 'num = item.Key; num2 = item.Value;')
    if any(token in body for token in ('Parsers.', 'PlanesInfoUtils.', 'Formula.Instance', 'GLogger', 'Log.')):
        raise ValueError('Unbound slot level dependency: ' + owner)
    return '\n'.join(line.rstrip() for line in body.splitlines())


def prepare(check=False, oracle=None):
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    source = OfficialSource(prior['manifest'] if prior else None)
    code = source.prepared_path / 'official-code'
    proof = source.prepared_path / 'code-manifest.json'
    assembly = code / 'managed/decoded/Console.dll'
    if sha(assembly.read_bytes()) != json.loads(proof.read_bytes())['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Original slot level assembly differs')
    inputs = [proof, assembly, TEMPLATE, Path(__file__), ROOT / 'scripts/prepare_treasure_slots.py']
    methods, bodies = {}, {}
    for owner, signatures in METHODS.items():
        path = code / 'common-source' / ('TreasureHelper.cs' if owner == 'TreasureHelper' else 'Common/' + owner + '.cs')
        inputs.append(path)
        bodies[owner] = [original_method(path.read_text('utf-8-sig'), sig) for sig in signatures]
        methods.update({owner + ':' + sig: sha(body.encode()) for sig, body in zip(signatures, bodies[owner])})
    contracts = {}
    for relative, signatures in (
        ('common-source/PlayerServiceProxyImpl.cs', ('protected override void OperPlayerTreasureSlotLevelUpCallback(',)),
        ('client-source/TreasureLevelupPanel.cs', ('private void OnLevelBtnClick(',)),
        ('common-source/Common/PlayerBlessLevelUtils.cs', ('public static int GetCurBlessPlayerLevel(', 'public static int GetBlessPlayerLevel(this Player player, int season)')),
        ('common-source/Common/ItemSourceItemMergeHandler.cs', ('public override bool IsSourceLimited(',)),
        ('common-source/Common/ItemSourceItemMergeAgent.cs', ('public IEnumerable<ItemSourceData> GenerateSourceData(',)),
        ('common-source/Common/FunctionLimitInfoParser.cs', ('public FunctionLimitInfo Get(',)),
        ('common-source/Common/CompositeCondition.cs', ('public List<ICondition> GetConditions(',)),
    ):
        path = code / relative; inputs.append(path)
        for sig in signatures: contracts[relative + ':' + sig] = sha(original_method(path.read_text('utf-8-sig'), sig).encode())
    constructors = Constructors(code); apk = Apk(source.version, reuse_current_cache=True)
    tables, sources = {}, {}
    try:
        for name, kind in (('system_level_limit','TbSystemLevelLimitInfo'), ('system_level_gap_limit','TbSystemLevelGapLimitInfo'),
            ('treasure_levelup_material','TbTreasureLevelInfo'), ('treasure_bless','TbTreasureBlessInfo'),
            ('astrological_season_config','TbAstrologicalSeasonConfig'),
            ('function_limit','TbFunctionLimitInfo'), ('conditions','TbConditionInfo'), ('item','TbItemInfo'), ('item_merge','TbItemMergeInfo')):
            bundle = 'defaultpackage_assets_config_luban_astrologicaldestiny.bundle' if name == 'astrological_season_config' else 'defaultpackage_assets_config_luban.bundle'
            obj = apk.asset(bundle, '/' + name + '.g.bytes'); raw = obj.read().m_Script.encode('utf8','surrogateescape')
            tables[name] = constructors.table(raw, kind); sources[name] = dict(source=apk.provenance(obj), decoded_sha256=sha(raw))
    finally: apk.reader.close()
    selected = {'Limits': ('system_level_limit', ('SubRank','PlayerLevel','PlayerBlessLevel','TreasureLevel','TreasureBlessLevel')),
        'Gaps': ('system_level_gap_limit', ('DevelopType','SeasonId','Level','LimitLevel','IsOpen')),
        'LevelCosts': ('treasure_levelup_material', ('Level','Materials')),
        'BlessCosts': ('treasure_bless', ('Rank','BlessLevel','LevelCostItems')),
        'Seasons': ('astrological_season_config', ('Rank','Season'))}
    payload = {key: [{field: row[field] for field in fields} for row in tables[name]] for key,(name,fields) in selected.items()}
    nodes={row['ClassId']:row['Condition'] for row in tables['conditions']}
    def condition(node, seen=()):
        kind=node['_type']
        if kind=='CompositeOrCondition':
            children=[]
            for key in node['Ids']:
                if key in seen: raise ValueError('Cyclic original merge-source condition')
                children.append(condition(nodes[key],seen+(key,)))
            return dict(Type=kind,Children=children)
        fields={'HasItemCondition':('ClassId','Count','Op'), 'HasItemTypeCondition':('ItemType','Count'), 'LevelCondition':('Level','Op')}
        if kind not in fields: raise ValueError('Review original merge-source condition: '+kind)
        return dict(Type=kind,**{key:node[key] for key in fields[kind]})
    functions={(r['Type'],r['Key0'],r['Key1']):r['Condition'] for r in tables['function_limit']}
    material_ids={int(k) for row in payload['LevelCosts'] for k in (row['Materials'] or {})}
    material_ids|={int(k) for row in payload['BlessCosts'] for k in (row['LevelCostItems'] or {})}
    products={row['ProductId'] for row in tables['item_merge']}
    payload['MergeSourceBagualu']=condition(functions[(7,0,0)])
    payload['MergeSourceProducts']=[dict(ItemId=key,Condition=condition(functions[(0,key,0)]) if (0,key,0) in functions else None)
        for key in sorted(material_ids & products)]
    item_types={row['ClassId']:row['ItemType'] for row in tables['item']}
    slot_items=json.loads((ROOT/'unity/Assets/Game/Resources/Treasure/treasure-slots.json').read_bytes())['Items']
    if {key for key,value in item_types.items() if value==305}!={row['ClassId'] for row in slot_items}:
        raise ValueError('Review complete treasure item-type projection for original merge-source conditions')
    inputs.append(ROOT/'unity/Assets/Game/Resources/Treasure/treasure-slots.json')
    for name, text in constructors.sources.items():
        if text:
            path = constructors.source_root / (name + '.cs')
            if not path.is_file(): path = constructors.source_root.parent / 'Common.LubanGen' / (name + '.cs')
            inputs.append(path)
    text = TEMPLATE.read_text('utf8')
    for owner, values in bodies.items(): text = text.replace('/*' + owner + '*/', '\n\n'.join(values if oracle else [adapt(v,owner) for v in values]))
    enum_text = ''
    for name in ('Rank','SubRank','DevelopType'):
        path = code / 'common-source/Common' / (name + '.cs'); inputs.append(path)
        enum_text += path.read_text('utf-8-sig').replace('namespace Common;', '') + '\n'
    text = text.replace('/*ENUMS*/', enum_text)
    text = '\n'.join(line.rstrip() for line in text.splitlines()) + '\n'
    if oracle:
        # Original helper and Player bodies are compiled unchanged against explicit parser/record ports.
        text = text.replace('Zjcs.Official.TreasureSlots', 'TreasureSlotOracle')
        text = text.replace('item.Deconstruct(ref num, ref num2);', 'num = item.Key; num2 = item.Value;')
        text = text.replace('/*ORACLE_PORTS*/', '''
public static partial class Parsers {
    public static TreasureSlotLevelTables Tables;
    public static OracleParserPorts Instance = new OracleParserPorts();
}
public sealed class OracleParserPorts {
    public SystemLevelLimitInfoParser SystemLevelLimitInfoParser => Parsers.Tables.Limits;
    public SystemLevelGapLimitInfoParser SystemLevelGapLimitInfoParser => Parsers.Tables.Gaps;
    public TreasureSlotLevelTables AstrologicalSeasonConfigParser => Parsers.Tables;
    public OracleLevelCosts TreasureLevelInfoParser = new OracleLevelCosts();
    public OracleBlessCosts TreasureBlessInfoParser = new OracleBlessCosts();
    public OracleMerge ItemMergeInfoParser = new OracleMerge();
}
public sealed class OracleLevelCosts { public TreasureLevelInfo Get(int level) => Parsers.Tables.GetLevelCost(level); }
public sealed class OracleBlessCosts { public TreasureBlessInfo Get(Rank rank,int level) => Parsers.Tables.GetBlessCost(rank,level); }
public sealed class OracleMerge { public long CalcCanMergeAmout(Player player,int id,bool recursion) => Parsers.Tables.CalcCanMergeAmount(player,id,recursion); }
public static class PlanesInfoUtils { public static Rank PlanesRank; }
public static partial class Formula {
    public static FormulaHandler Instance => Parsers.Tables.Formula;
    public static int GetMaxTreasureBaseLevel(SubRank rank) => Parsers.Tables.Limits.GetTreasureBaseLevelLimit(rank);
}
public static class ItemSourceDataManager { public static bool IsItemSourceFromMerge(Player player,int id) => player.IsItemSourceFromMerge(id); }
public static class JsonUtils { public static string ToJson(object value) => ""; }
''')
        oracle.parent.mkdir(parents=True,exist_ok=True); oracle.write_text(text,'utf8',newline='\n')
        oracle.with_suffix('.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        return
    outputs = {DEST: text.encode(), DATA: (json.dumps(payload,ensure_ascii=False,indent=2)+'\n').encode()}
    for path in list(outputs):
        kind = 'MonoImporter' if path.suffix == '.cs' else 'TextScriptImporter'
        guid = hashlib.md5(('zjcs:treasure-slot-levels:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')] = ('fileFormatVersion: 2\nguid: '+guid+'\n'+kind+':\n  externalObjects: {}\n').encode()
    bindings = [ROOT / name for name in (
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.Levels.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.Levels.cs.meta',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterPropertyProgress.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotProgress.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureSlotService.cs',
        'scripts/fixtures/treasure_slot_levels_validation.cs', 'scripts/test_treasure_slot_levels.ps1')]
    receipt = dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},
        methods=methods,contracts=contracts,table_sources=sources,
        runtime_bindings={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in bindings},
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        scope='Original treasure slot upgrade checks, ordered fee/cap/gap parsers, seasonal blessing records and notification transitions; local atomic upgrade uses shared inventory/automatic-merge rules and explicit current source-function predicates. Native UI and automatic setup remain separate owners.')
    outputs[RECEIPT]=(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw: raise ValueError('Treasure slot level output differs: '+str(path))
        else:
            if path!=RECEIPT and path.is_file() and path.read_bytes()!=raw and sha(path.read_bytes())!=(prior or {}).get('outputs',{}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another slot level edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original slot level methods:',len(methods),'; limit rows:',len(payload['Limits']))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--oracle',type=Path)
    args=parser.parse_args();prepare(args.check,args.oracle)
