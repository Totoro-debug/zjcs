"""Compile selected unchanged official property methods against a binary-backed test boundary."""
import argparse,hashlib,json,re
from pathlib import Path
from official_source import ROOT,OfficialSource
from prepare_pet_score_chest_preview import original_method
from prepare_treasure_properties import logs

def prepare(work):
    work=work.resolve()
    if not work.is_relative_to(ROOT/'output/validation') or not (work/'.zjcs-validation.json').is_file():raise ValueError('Use an owned validation directory')
    source=OfficialSource();code=source.prepared_path/'official-code';inputs={}
    def read(name,client=False):
        path=(code/('client-source' if client else 'common-source/Common')/(name+'.cs')).resolve()
        inputs[path.relative_to(ROOT).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest();return path.read_text('utf-8-sig')
    def methods(name,signatures):return '\n'.join(original_method(read(name),s) for s in signatures)
    info=methods('TreasureInfo',['public int GetLevelPropId('])
    effect=methods('ITreasureEffect',['public virtual int GetLevelMaxHitCount(', 'public virtual int GetMaxHitCount('])
    task=methods('TreasureEffectTask',['public override int GetLevelMaxHitCount(', 'public override int GetMaxHitCount('])
    suit=methods('TreasureSuitInfo',[
        'public IEnumerable<TreasureSuitPropCondition> GetActivedCondition(IEnumerable<PlayerItemDataWrap>',
        'public IEnumerable<TreasureSuitPropCondition> GetActivedCondition(Dictionary<int, int>',
        'public IEnumerable<TreasureSuitPropCondition> GetActivedConditionBySuitActivedStars(',
        'public IEnumerable<(int PropId, List<PropType> PropTypes)> GetActivatedProps(IEnumerable<PlayerItemDataWrap>'])
    parser=methods('LevelPropParser',['public IReadOnlyDictionary<PropType, long> GetProps(int classId, int level)',
        'private IReadOnlyDictionary<PropType, long> GetConfigProps(', 'public long GetProp('])
    formula=methods('BattleFormulaHandler',[
        'public void CalcTreasureProps(', 'public Dictionary<PropType, long> CalcTreasureLevelPartProps(',
        'private void CalcTreasureLevelPartProps(', 'public Dictionary<PropType, long> CalcTreasureStarPartProps(',
        'private void CalcTreasureStarPartProps(',
        'public void CalcTreasureSuitProps(PlayerItemDataWrap suitItem, IEnumerable<PlayerItemDataWrap>',
        'public void MergeProps(IReadOnlyDictionary<PropType, long>',
        'public void MergePropsInPlace(Dictionary<PropType, long> dest, (PropType, long) add)'])
    formula+='''
        private void MergePropsInPlace(Dictionary<PropType,long> dest,IReadOnlyDictionary<PropType,long> source)
        {if(source!=null)foreach(var pair in source)MergePropsInPlace(dest,(pair.Key,pair.Value));}
'''
    numeric=methods('Formula',['public static double Long2Double(', 'public static int GetMaxTreasureBaseLevel('])
    element=read('TreasureInfoParser').split('public static readonly List<PropType> sElementProps',1)[1].split('};',1)[0]
    model=(ROOT/'scripts/fixtures/treasure_property_reference_ports.cs').read_text('utf-8-sig')
    body=model.replace('/*INFO*/',info).replace('/*EFFECT*/',effect).replace('/*TASK*/',task).replace('/*SUIT*/',suit).replace('/*PARSER*/',parser).replace('/*FORMULA*/',formula).replace('/*NUMERIC*/',numeric).replace('/*ELEMENT*/','public static readonly List<PropType> sElementProps'+element+'};')
    for name in ('CalcPropDataBase','TreasureCalcPropData','TreasureSuitPropCondition'):
        body+=read(name).split('namespace Common;\n',1)[1]+'\n'
    query=read('../TreasurePropInfoConfigUtils');body+=query[query.index('public static class '):]+'\n'
    for name in ('ITreasurePropProvider','TreasureDummyPropProvider','TreasureSnapshotPropProvider','TreasureInSlotPropProvider','TreasureNoSlotPropProvider'):
        text=read(name,True);body+=re.sub(r'^using [^\n]+\n','',text,flags=re.M)+'\n'
    # Only logger formatting is replaced. The reference retains the original globals,
    # enum types, method signatures, pool calls and arithmetic rather than the local ports.
    body=logs(body,'Diagnostics')
    output=('using System;\nusing System.Collections.Generic;\nusing System.Linq;\n'
        'using PropType=Zjcs.Official.HomeIncomeRules.PropType;\nusing SubRank=Zjcs.Official.HomeExperienceRules.SubRank;\n'
        'using PlayerItemParamTreasureWrap=TreasurePropertyOriginal.TreasureState;\n'
        'namespace TreasurePropertyOriginal {\n'+body+'\n}\n')
    (work/'OriginalTreasureProperties.cs').write_text(output,'utf8',newline='\n')
    inputs.update(source.verify_code_files(ROOT/p for p in list(inputs)))
    # The existing original binary parser is reused only when its consumed original
    # methods agree with the current source, including defaults and range branches.
    baseline=ROOT/'unity/Assets/Game/Runtime/Official/HomeLevelUpRules'
    for name,signatures in {
        'LevelPropBinaryReader':['public bool TryGetProps(', 'private void ReadFileHeader(', 'private void ReadIndex(', 'private void ReadFileSections(', 'private IPropDictionary ReadRowData(', 'private long ReadVarInt('],
        'LevelPropParser':['private IReadOnlyDictionary<PropType, long> GetConfigProps(', 'private bool TryGetPropDict(', 'public bool TryGetLevelRange(']
    }.items():
        before=baseline/(name+'.cs');after=code/'common-source/Common'/(name+'.cs')
        for signature in signatures:
            left=original_method(before.read_text('utf-8-sig'),signature);right=logs(original_method(after.read_text('utf-8-sig'),signature),'Diagnostics')
            left=re.sub(r'^\s*HomeLevelUpDiagnostics\.Error\([^\n]+\);','',left,flags=re.M)
            right=re.sub(r'^\s*Diagnostics\.ReportError\([^\n]+\);','',right,flags=re.M)
            if name=='LevelPropParser':
                right=re.sub(r'\s*if \(mDefaultValueParser == null\)\s*\{\s*mDefaultValueParser = \(\(ParserBase\)this\)\.ConfigManager.GetParser<LevelPropDefaultValueParser>\(\);\s*\}','',right)
            if re.sub(r'\s+','',left)!=re.sub(r'\s+','',right):raise ValueError('Review original binary parser compatibility: '+name+' '+signature)
        inputs[before.relative_to(ROOT).as_posix()]=hashlib.sha256(before.read_bytes()).hexdigest()
    reused=ROOT/'unity/Assets/Game/Runtime/Official/TreasureCultivation/OfficialTreasureSlotLevels.cs'
    for signature in ('private void SetTreasureMaxLevelLimit(', 'public int GetTreasureBaseLevelLimit('):
        current=original_method(read('SystemLevelLimitInfoParser'),signature)
        if re.sub(r'\s+','',current) not in re.sub(r'\s+','',reused.read_text('utf-8-sig')):raise ValueError('Current original treasure capacity helper differs: '+signature)
    inputs[reused.relative_to(ROOT).as_posix()]=hashlib.sha256(reused.read_bytes()).hexdigest()
    for path in (Path(__file__),ROOT/'scripts/fixtures/treasure_property_reference_ports.cs',ROOT/'scripts/prepare_treasure_properties.py'):
        inputs[path.relative_to(ROOT).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest()
    (work/'reference-source.json').write_text(json.dumps(dict(inputs=inputs,output_sha256=hashlib.sha256(output.encode()).hexdigest()),indent=2)+'\n','utf8')
    print('Prepared original property reference methods',flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--work',type=Path,required=True);prepare(p.parse_args().work)
