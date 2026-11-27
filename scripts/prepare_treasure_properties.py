"""Port original per-treasure property, blessing, provider and suit queries with selected local table payloads."""
import argparse,hashlib,json,re
from pathlib import Path
from official_source import ROOT,OfficialSource
from official_apk import Apk
from official_tables import Constructors
from prepare_pet_score_chest_preview import original_method
from prepare_unity_character_catalog import decode_level_props

DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureProperties.cs'
PAYLOAD=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-properties.json'
RECEIPT=ROOT/'assets/reference/treasure-properties-source-manifest.json'

def sha(raw):return hashlib.sha256(raw).hexdigest()

def logs(text,receiver='Tables'):
    for match in reversed(list(re.finditer(r'(?m)^(\t+)if \(enabled\)\n\1\{',text))):
        at=text.index('{',match.start());end=at+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        if any(line.strip() and not line.strip().startswith('message.Append') for line in text[at+1:end-1].splitlines()):raise ValueError('Non-log branch')
        text=text[:match.start()]+text[end:]
    lines=[]
    for line in text.splitlines():
        if re.search(r'GLogger\w+InterpolatedStringHandler|^\s*bool enabled;',line):continue
        if re.match(r'\s*Log\.(Error|Warn)\(',line):
            method=re.search(r', "([^"]+)", "[^"\n]+\.cs", \d+\);',line)
            if not method:raise ValueError('Unknown source log: '+line)
            line=re.match(r'\s*',line)[0]+receiver+'.Report'+('Error' if 'Log.Error' in line else 'Warning')+'("'+method[1]+'");'
        lines.append(line)
    return '\n'.join(lines)+'\n'

def build_code(read):
    def method(name,sig):return original_method(read(name),sig)
    calc=read('CalcPropDataBase').split('namespace Common;\n',1)[1]
    calc=logs(calc).replace('Parsers.Instance.PlayerSubRankInfoParser.Get(subRank)','Tables.GetSubRank(subRank)').replace('Parsers.Instance.LevelPropParser.GetProps','Tables.GetProps').replace('Formula.Long2Double','TreasurePropertyNumber.Long2Double')
    calc=calc.replace('public abstract class CalcPropDataBase\n{','public abstract class CalcPropDataBase\n{\n    protected readonly ITreasurePropertyTables Tables;\n    protected CalcPropDataBase(ITreasurePropertyTables tables){Tables=tables??throw new ArgumentNullException(nameof(tables));}')
    derived=read('TreasureCalcPropData').split('namespace Common;\n',1)[1]
    derived=derived.replace('TreasureCalcPropData()', 'TreasureCalcPropData(ITreasurePropertyTables tables) : base(tables)')
    derived=derived.replace('TreasureCalcPropData(int finalLevel, int baseLevel)','TreasureCalcPropData(ITreasurePropertyTables tables, int finalLevel, int baseLevel) : base(tables)')
    derived=derived.replace('CreateCalcPropData()', 'CreateCalcPropData(ITreasurePropertyTables tables)').replace('CreateCalcPropDataByBaseLevel(int baseLevel)','CreateCalcPropDataByBaseLevel(ITreasurePropertyTables tables, int baseLevel)').replace('CreateCalcPropDataByFinalLevelAndBaseLevel(int finalLevel, int baseLevel)','CreateCalcPropDataByFinalLevelAndBaseLevel(ITreasurePropertyTables tables, int finalLevel, int baseLevel)')
    # Constructor calls are bound explicitly; the original numeric arguments remain unchanged.
    derived=derived.replace('new TreasureCalcPropData(ITreasurePropertyTables tables) : base(tables)','new TreasureCalcPropData(tables)').replace('new TreasureCalcPropData(baseLevel, baseLevel)','new TreasureCalcPropData(tables, baseLevel, baseLevel)').replace('new TreasureCalcPropData(finalLevel, baseLevel)','new TreasureCalcPropData(tables, finalLevel, baseLevel)')
    formula='\n'.join(method('BattleFormulaHandler',sig) for sig in (
        'public void CalcTreasureProps(',
        'public Dictionary<PropType, long> CalcTreasureLevelPartProps(',
        'private void CalcTreasureLevelPartProps(',
        'public Dictionary<PropType, long> CalcTreasureStarPartProps(',
        'private void CalcTreasureStarPartProps(',
        'public void MergeProps(IReadOnlyDictionary<PropType, long>',
        'public void MergePropsInPlace(Dictionary<PropType, long> dest, (PropType, long) add)'))
    formula=logs(formula).replace('DictionaryPool<PropType, long>.Rent()','new Dictionary<PropType, long>()')
    formula=re.sub(r'^\s*DictionaryPool<PropType, long>\.Return\([^\n]+\);','',formula,flags=re.M)
    formula=formula.replace('Parsers.Instance.LevelPropParser.GetProp','Tables.GetProp').replace('Parsers.GameSettings.TreasureSlotPropAddRate','Tables.TreasureSlotPropAddRate').replace('Formula.Long2Double','TreasurePropertyNumber.Long2Double')
    formula+='''
    private void MergePropsInPlace(Dictionary<PropType,long> dest,IReadOnlyDictionary<PropType,long> add)
    {
        if(add!=null)foreach(var pair in add)MergePropsInPlace(dest,(pair.Key,pair.Value));
    }
'''
    formula='public sealed class TreasurePropertyCalculator {\nprivate readonly ITreasurePropertyTables Tables;\npublic TreasurePropertyCalculator(ITreasurePropertyTables tables){Tables=tables??throw new ArgumentNullException(nameof(tables));}\n'+formula+'}\n'
    info_level=logs(method('TreasureInfo','public int GetLevelPropId('))
    task_methods='\n'.join(method('TreasureEffectTask',sig) for sig in ('public override int GetLevelMaxHitCount(', 'public override int GetMaxHitCount('))
    suit='\n'.join(method('TreasureSuitInfo',sig) for sig in ('public IEnumerable<TreasureSuitPropCondition> GetActivedCondition(Dictionary<int, int>', 'public IEnumerable<TreasureSuitPropCondition> GetActivedConditionBySuitActivedStars('))
    suit=suit.replace('Parsers.Instance.ItemInfoParser.Get(activedStar.Key)?.PartInfo is TreasureInfo treasureInfo','Tables.GetTreasure(activedStar.Key) is TreasureInfo treasureInfo')
    activated=method('TreasureSuitInfo','public IEnumerable<(int PropId, List<PropType> PropTypes)> GetActivatedProps(IEnumerable<PlayerItemDataWrap>')
    activated=activated.replace('IEnumerable<PlayerItemDataWrap> allTreasures','Dictionary<int,int> activedStars').replace('GetActivedCondition(allTreasures)','GetActivedCondition(activedStars)')
    suit_calc=method('BattleFormulaHandler','public void CalcTreasureSuitProps(PlayerItemDataWrap suitItem, IEnumerable<PlayerItemDataWrap>')
    suit_calc=logs(suit_calc).replace('PlayerItemDataWrap suitItem, IEnumerable<PlayerItemDataWrap> treasureItems','TreasureSuitInfo suit, Dictionary<int,int> activedStars').replace('(suitItem.Info.PartInfo as TreasureSuitInfo).GetActivatedProps(treasureItems)','suit.GetActivatedProps(activedStars)').replace('Parsers.Instance.LevelPropParser.GetProp','Tables.GetProp')
    formula=formula[:-2]+suit_calc+'}\n'
    queries=read('../TreasurePropInfoConfigUtils').split('public static class TreasurePropInfoConfigUtils',1)[1]
    queries=logs('public sealed class TreasurePropertyQueries'+queries).replace('private static Dictionary','private readonly Dictionary').replace('public static ','public ').replace('this TreasureInfo self','TreasureInfo self').replace('BattleFormula.Instance','Calculator').replace('PlanesInfoUtils.SeasonId','SeasonId()').replace('TreasureInfoParser.sElementProps','Tables.ElementPropTypes').replace('PropType.None','0')
    queries=queries.replace('public sealed class TreasurePropertyQueries\n{','''public sealed class TreasurePropertyQueries
{
    private readonly ITreasurePropertyTables Tables;
    private readonly TreasurePropertyCalculator Calculator;
    private readonly Func<int> SeasonId;
    public TreasurePropertyQueries(ITreasurePropertyTables tables,Func<int> seasonId)
    {Tables=tables??throw new ArgumentNullException(nameof(tables));Calculator=new TreasurePropertyCalculator(tables);SeasonId=seasonId??throw new ArgumentNullException(nameof(seasonId));}
''')
    providers=[]
    for name in ('ITreasurePropProvider','TreasureDummyPropProvider','TreasureSnapshotPropProvider','TreasureInSlotPropProvider','TreasureNoSlotPropProvider'):
        text=read('@'+name);text=re.sub(r'^using (?:Common|System);\s*$','',text,flags=re.M)
        if name=='TreasureDummyPropProvider':
            text=text.replace('public TreasureDummyPropProvider(int classId)','public TreasureDummyPropProvider(ITreasurePropertyTables tables, Player player, int classId)')
            text=text.replace('private readonly int mClassId;','private readonly int mClassId;\n    private readonly ITreasurePropertyTables Tables;\n    private readonly Player Player;')
            text=text.replace('mClassId = classId;','Tables=tables??throw new ArgumentNullException(nameof(tables));Player=player??throw new ArgumentNullException(nameof(player));\n        mClassId = classId;')
            text=text.replace('MeManager.MainPlayer.GetItemById(mClassId)?.GetItemParamTreasure()','Player.GetTreasureState(mClassId)').replace('mTreasureInfo.TreasureData?.GetCurrentCount(MeManager.MainPlayer)','Player.GetCurrentCount(mTreasureInfo)').replace('Formula.GetMaxTreasureBaseLevel(MeManager.MainPlayer.SubRank)','Tables.GetMaxTreasureBaseLevel(Player.SubRank)').replace('MeManager.MainPlayer','Player').replace('Parsers.Instance.TreasureInfoParser.Get','Tables.GetTreasure')
        if name=='TreasureSnapshotPropProvider':
            text=text.replace('public TreasureSnapshotPropProvider(SnapshotTreasure snapshot)','public TreasureSnapshotPropProvider(ITreasurePropertyTables tables, SnapshotTreasure snapshot)').replace('Parsers.Instance.TreasureInfoParser.Get','tables.GetTreasure').replace('(mSnapshot.TreasureParam.Wrap() as PlayerItemParamTreasureWrap).HitCount','mSnapshot.TreasureParam.HitCount')
        providers.append(text)
    effect=read('ITreasureEffect')
    defaults='\n'.join(method('ITreasureEffect',sig) for sig in ('public virtual int GetLevelMaxHitCount(', 'public virtual int GetMaxHitCount('))
    condition=read('TreasureSuitPropCondition').split('namespace Common;\n',1)[1]
    model=read('$TreasurePropertyPorts').replace('/*INFO_LEVEL*/',info_level).replace('/*TASK_METHODS*/',task_methods).replace('/*SUIT_METHODS*/',suit+'\n'+activated).replace('/*EFFECT_DEFAULTS*/',defaults).replace('/*CONDITIONS*/',condition).replace('/*NUMBER*/',method('Formula','public static double Long2Double('))
    return ('// Original 911 per-item properties; explicit table, player, season and diagnostic boundaries.\n'
        'using System;\nusing System.Linq;\nusing System.Collections.Generic;\nusing PropType=System.Int32;\nusing SubRank=System.Int32;\n'
        'using Player=Zjcs.Official.TreasureProperties.ITreasurePropertyPlayer;\nusing PlayerItemParamTreasureWrap=Zjcs.Official.TreasureCultivation.TreasureState;\n'
        'namespace Zjcs.Official.TreasureProperties {\n'+model+calc+derived+formula+queries+'\n'.join(providers)+'\n}\n').replace('JsonUtils.ToJson(self.StarPropTypes)','string.Join(",",self.StarPropTypes)')

def prepare(check=False,oracle=None):
    source=OfficialSource();code=source.prepared_path/'official-code';inputs={};outputs={}
    if oracle:
        oracle=oracle.resolve()
        if not oracle.is_relative_to(ROOT/'output/validation') or oracle==ROOT/'output/validation':raise ValueError('Use an owned temporary validation directory')
    def read(name):
        path=(ROOT/'scripts/templates/treasure_properties'/(name[1:]+'.cs') if name.startswith('$') else code/'client-source'/(name[1:]+'.cs') if name.startswith('@') else code/'common-source/Common'/(name+'.cs'))
        path=path.resolve();inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes());return path.read_text('utf-8-sig')
    generated=build_code(read);outputs[DEST]=generated.encode()
    # Keep the complete consumed parser branch instead of approximating sparse or over-limit rows.
    parser=read('LevelPropParser')
    parser_methods='\n'.join(original_method(parser,sig) for sig in ('private IReadOnlyDictionary<PropType, long> GetConfigProps(', 'public long GetProp('))
    parser_methods=logs(parser_methods,'this').replace('private IReadOnlyDictionary<PropType, long> GetConfigProps','public IReadOnlyDictionary<PropType, long> GetProps').replace('GetConfigProps(classId, level)','GetProps(classId, level)')
    ports_path=ROOT/'scripts/templates/treasure_properties/OfflineTreasurePropertyCatalog.cs';inputs[ports_path.relative_to(ROOT).as_posix()]=sha(ports_path.read_bytes())
    local=ROOT/'unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasurePropertyCatalog.cs'
    outputs[local]=ports_path.read_text('utf-8-sig').replace('/*PARSER_METHODS*/',parser_methods).encode()
    player_path=ROOT/'scripts/templates/treasure_properties/OfflineTreasurePropertyPlayer.cs';inputs[player_path.relative_to(ROOT).as_posix()]=sha(player_path.read_bytes())
    outputs[local.with_name(player_path.name)]=player_path.read_bytes()
    for name in ('PropType','LevelPropBinaryReader','TreasureInfoParser','SystemLevelLimitInfoParser','Formula'):
        read(name)
    reused=ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureSlotLevels.cs'
    for signature in ('private void SetTreasureMaxLevelLimit(', 'public int GetTreasureBaseLevelLimit('):
        current=original_method(read('SystemLevelLimitInfoParser'),signature)
        if re.sub(r'\s+','',current) not in re.sub(r'\s+','',reused.read_text('utf-8-sig')):raise ValueError('Current original treasure capacity helper differs: '+signature)
    inputs[reused.relative_to(ROOT).as_posix()]=sha(reused.read_bytes())
    source_paths=[ROOT/p for p in inputs if '/official-code/' in p]
    inputs.update(source.verify_code_files(source_paths))
    enum_text=read('PropType');enum_values={};last=-1
    for name,value in re.findall(r'^\s*(\w+)(?:\s*=\s*(-?\d+))?,?\s*$',enum_text,re.M):
        if name in ('namespace','public'):continue
        last=int(value) if value else last+1;enum_values[name]=last
    element_source=read('TreasureInfoParser').split('sElementProps =',1)[1].split('};',1)[0]
    elements=[enum_values[name] for name in re.findall(r'PropType\.(\w+)',element_source)]
    apk=Apk(source.version,reuse_current_cache=True);constructors=Constructors(code);table_inputs={}
    try:
        mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
        def table(name):
            obj=apk.asset('defaultpackage_assets_config_luban.bundle','/'+name+'.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
            table_inputs[name]=dict(source=apk.provenance(obj),sha256=sha(raw),constructor=mapping[name]);return constructors.table(raw,mapping[name])
        treasures=table('treasure');suits=table('treasure_suit');subranks=table('player_subrank');defaults=table('level_prop_default_value');settings=table('game_settings');limits=table('system_level_limit')
        current_items={r['ClassId']:r for r in table('item')}
        retained_slots=ROOT/'unity/Assets/Game/Resources/Treasure/treasure-slots.json';retained=json.loads(retained_slots.read_bytes());retained_items={r['ClassId']:r['ElementType'] for r in retained['Items']}
        for row in treasures:
            if retained_items.get(row['ClassId'])!=current_items[row['ClassId']]['ElementType']:raise ValueError('Current treasure element differs from the retained shared slot catalog: '+str(row['ClassId']))
        for key in ('TreasureMaxSlotCount','TreasurePlanMaxCount','TreasureSlotElementTypes'):
            if retained[key]!=settings[key]:raise ValueError('Current treasure slot setting differs: '+key)
        inputs[retained_slots.relative_to(ROOT).as_posix()]=sha(retained_slots.read_bytes())
        compatibility=dict(retained_slots=retained_slots.relative_to(ROOT).as_posix(),fields=['ClassId','ElementType'],treasures=len(treasures),settings={key:settings[key] for key in ('TreasureMaxSlotCount','TreasurePlanMaxCount','TreasureSlotElementTypes')})
        wanted={r['StarScalePropId'] for r in treasures}
        wanted.update(v for r in treasures for v in r['LevelPropId'].values());wanted.update(v for r in suits for v in r['Props'].values());wanted.update(r['TreasurePropBless'] for r in subranks);wanted.discard(0)
        raw=apk.reader.read('WwiseBnk','wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        classes=decode_level_props(raw.data,wanted)
        selected=dict(Manifest=source.version,TreasureSlotPropAddRate=settings['TreasureSlotPropAddRate'],ElementPropTypes=elements,
            Treasures=[dict(ClassId=r['ClassId'],TreasurePieceId=r['TreasurePieceId'],StarMax=r['StarMax'],StarPropTypes=r['StarPropTypes'],StarScalePropId=r['StarScalePropId'],LevelPropTypes=r['LevelPropTypes'],EffectPropTypes=r['EffectPropTypes'],
                LevelPropId=[dict(SeasonId=k,PropId=v) for k,v in r['LevelPropId'].items()],Effect=dict(Kind=(r['RawEffect'] or {}).get('_type','TreasureEffectNone'),PerCount=(r['RawEffect'] or {}).get('PerCount',0),MaxHitCount=(r['RawEffect'] or {}).get('MaxHitCount',0),LevelHitCount=(r['RawEffect'] or {}).get('LevelHitCount'))) for r in treasures],
            Suits=[dict(ClassId=r['ClassId'],Treasures=r['Treasures'],Props=[dict(Condition=k,PropId=v,PropTypes=r['PropTypes'][i]) for i,(k,v) in enumerate(r['Props'].items())]) for r in suits],
            SubRanks=[dict(SubRank=r['SubRank'],TreasurePropBless=r['TreasurePropBless']) for r in subranks],
            Limits=[{k:r[k] for k in ('SubRank','PlayerLevel','PlayerBlessLevel','TreasureLevel','TreasureBlessLevel')} for r in limits],
            Defaults=[r for r in defaults if r['ClassId'] in wanted],
            Classes=[dict(ClassId=int(k),Min=v['min'],Max=v['max'],Rows=[dict(Level=int(l),Props=[dict(Type=int(p),Value=value) for p,value in values.items()]) for l,values in v['rows'].items()]) for k,v in classes.items()])
        outputs[PAYLOAD]=(json.dumps(selected,ensure_ascii=False,separators=(',',':'))+'\n').encode()
        if oracle:
            oracle.mkdir(parents=True,exist_ok=True);(oracle/'reference-level-prop.g.bytes').write_bytes(raw.data)
            (oracle/'raw-tables.json').write_text(json.dumps(dict(Treasures=treasures,Suits=suits,SubRanks=subranks,Defaults=defaults,Settings=settings,Limits=limits),ensure_ascii=False,separators=(',',':'))+'\n','utf8')
            (oracle/'reference-inputs.json').write_text(json.dumps(dict(manifest=source.version,binary_sha256=sha(raw.data),raw_tables_sha256=sha((oracle/'raw-tables.json').read_bytes()),runtime_payload_sha256=sha(outputs[PAYLOAD])),indent=2)+'\n','utf8')
        binary=dict(source=dict(raw.provenance),sha256=sha(raw.data))
    finally:apk.reader.close()
    for name,text in constructors.sources.items():
        if not text:continue
        path=code/'common-source/Common'/(name+'.cs')
        if not path.exists():path=code/'common-source/Common.LubanGen'/(name+'.cs')
        inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
    inputs.update(source.verify_code_files(ROOT/p for p in list(inputs) if '/official-code/' in p and p.endswith('.cs')))
    inputs[(code/'global-metadata.dat').relative_to(ROOT).as_posix()]=sha((code/'global-metadata.dat').read_bytes())
    for path in list(outputs):
        token=hashlib.md5(('zjcs:treasure-properties:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+('MonoImporter' if path.suffix=='.cs' else 'TextScriptImporter')+':\n  externalObjects: {}\n').encode()
    inputs[Path(__file__).relative_to(ROOT).as_posix()]=sha(Path(__file__).read_bytes())
    for name in ('prepare_unity_character_catalog.py','official_tables.py','official_metadata.py','prepare_pet_score_chest_preview.py'):
        path=ROOT/'scripts'/name;inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
    record=dict(manifest=source.version,inputs=inputs,tables=table_inputs,binary=binary,retained_slot_compatibility=compatibility,outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        selection=dict(treasures=len(treasures),suits=len(suits),property_classes=len(classes),rows=sum(len(c['rows']) for c in classes.values())),
        adaptations=['Keep original property partitions, multiplication/truncation, float slot bonus, hit counts, blessing lookup, season fallback, suit condition order and provider behavior.',
            'Inject local tables, player state and season; retain sparse lookup branches with compact decoded binary rows and original default values. Dictionary scratch ownership is local.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {};outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Treasure property output differs: '+str(path))
        else:
            if path!=RECEIPT and path.exists() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):raise ValueError('Preserve another property edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original treasure properties:',record['selection'],flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');p.add_argument('--oracle',type=Path);a=p.parse_args();prepare(a.check,a.oracle)
