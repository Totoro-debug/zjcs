"""Migrate original home condition methods and reuse the selected partner leaves."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_gift_configuration import method, normalize, without_logging
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(work):
    version=json.loads((ROOT/'official-source.json').read_bytes())['active']
    code=ROOT/'.tools/source-packages/current'/version/'official-code/common-source/Common'
    inputs, methods={}, {}

    def source(name):
        path=code/(name+'.cs');raw=path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()]=sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n','\n')

    def selected(name,signature,diagnostics=0):
        value=method(source(name),signature)
        methods.setdefault(name,{})[signature]=sha(value.encode())
        return without_logging(value,diagnostics) if diagnostics else value

    template=ROOT/'scripts/templates/home_conditions/HomeConditionRules.cs'
    inputs[template.relative_to(ROOT).as_posix()]=sha(template.read_bytes())
    text=template.read_text('utf8')
    base=source('ICondition');start=base.index('\tpublic class DataHub\n');end=base.index('\n\t}\n',start)+len('\n\t}')
    hub=base[start:end]
    methods.setdefault('ICondition',{})['DataHub']=sha(hub.encode())
    hub=without_logging(hub.replace('GLoggerWarnInterpolatedStringHandler','GLoggerErrorInterpolatedStringHandler')
                        .replace('Log.Warn(','Log.Error('),1).replace('public class DataHub','private class DataHub')
    text=text.replace('/*DATA_HUB*/',hub)
    for name,marker in [('ConditionDataType','CONDITION_DATA_TYPE'),('ConditionType','CONDITION_TYPE'),
                        ('SubRank','SUB_RANK'),('Rank','RANK'),('ItemType','ITEM_TYPE'),('Profession','PROFESSION')]:
        value=source(name).split('namespace Common;\n',1)[1].strip()
        text=text.replace('/*'+marker+'*/',value)
    members={
        'HasItemTypeCondition':['ItemType','Count'], 'RankCondition':['Rank','Op'],
        'SubRankCondition':['SubRank','Op'], 'MaxProfessionRankCondition':['Rank','Op'],
        'StoryCountCondition':['StoryKey','Count','Op'], 'AnyMapGoddessActivedCondition':[],
        'StagePassCondition':['StageId','Op'], 'CompositeCondition':['Ids'],
        'CompositeAndCondition':[], 'CompositeOrCondition':[],
        'PlayerProfessionCondition':['Profession'], 'PlayerProfessionFactionCondition':['Profession'],
    }
    extra={
        'HasItemTypeCondition':[('public override (long current, long target) GetProgress(DataHub dataHub)',0),('protected long GetValue(DataHub dataHub)',1)],
        'RankCondition':[('private Rank GetData(DataHub hub)',0)],
        'SubRankCondition':[('protected SubRank GetSubRank(DataHub hub)',1)],
        'MaxProfessionRankCondition':[('private int GetValue(DataHub dataHub)',0)],
        'AnyMapGoddessActivedCondition':[('private bool IsAnyActived(DataHub dataHub)',1)],
        'StagePassCondition':[('private int GetCount(DataHub dataHub)',0)],
        'CompositeCondition':[('public List<ICondition> GetConditions()',1)],
        'PlayerProfessionCondition':[('private Profession GetValue(DataHub dataHub)',0)],
        'PlayerProfessionFactionCondition':[('private Profession GetValue(DataHub dataHub)',0)],
    }
    classes=[]
    for name,expected in members.items():
        raw=source(name)
        fields=re.findall(r'^\tpublic (?:int|string|List<int>|ItemType|Rank|SubRank|Profession) (\w+)(?: = [^;]+)?;',raw,re.M)
        if fields!=expected:raise ValueError('Original condition fields changed: '+name)
        parts=re.findall(r'^\tpublic (?:int|string|List<int>|ItemType|Rank|SubRank|Profession) \w+(?: = [^;]+)?;',raw,re.M)
        parts.append('public '+name+'(HomeConditionRules configuration) : base(configuration) { }')
        parts+=re.findall(r'\tpublic override (?:ConditionType Type|bool IsComposite) => [^;]+;',raw)
        if name!='CompositeCondition':
            parts.append(selected(name,'public override bool IsMet(DataHub dataHub)',int(name=='StoryCountCondition')))
        for signature,diagnostics in extra.get(name,[]):parts.append(selected(name,signature,diagnostics))
        parent='CompositeCondition' if name in ('CompositeAndCondition','CompositeOrCondition') else 'ICondition'
        declaration='private '+('abstract ' if name=='CompositeCondition' else '')+'class '+name+' : '+parent
        body='\n\n'.join(parts).replace('Parsers.Instance.ConditionInfoParser.GetCondition','configuration.GetCondition')
        body=body.replace('Parsers.Instance.ProfessionBaseInfoParser',
            'configuration.professionBaseInfoParser ?? throw new InvalidOperationException("Selected profession configuration is required.")')
        classes.append(declaration+'\n{\n'+body+'\n}')
    text=text.replace('/*CONDITIONS*/','\n\n'.join(classes))
    profession_parser='''private sealed class ProfessionBaseInfo { public Profession PrePro; }
private sealed class ProfessionBaseInfoParser
{
    private readonly Dictionary<Profession,ProfessionBaseInfo> configs;
    public ProfessionBaseInfoParser(IEnumerable<Dictionary<string,object>> rows)
    {
        configs=rows.ToDictionary(row=>(Profession)Number(row,"Profession"),
            row=>new ProfessionBaseInfo { PrePro=(Profession)Number(row,"PrePro") });
    }
    private ProfessionBaseInfo Get(Profession type)=>configs.TryGetValue(type,out var row) ? row : null;
'''+selected('ProfessionBaseInfoParser','public bool ExistProfession(Profession pro)')+'\n'+selected(
        'ProfessionBaseInfoParser','public bool IsPreProfession(Profession pro, Profession pre, bool recursive = true)')+'\n}'
    text=text.replace('/*PROFESSION_PARSER*/',profession_parser)
    text=text.replace('/*COMPARE*/',selected('Formula','public static bool Compare<T>(T lhs, string op, T rhs) where T : IComparable<T>, IEquatable<T>'))
    text=text.replace('/*TO_RANK*/',selected('EnumExt','public static Rank ToRank(this SubRank subRank)',1))
    unbound=[line.strip() for line in text.splitlines()
             if any(token in line for token in ('/*','Parsers.','GLogger','Log.Error','Log.Warn'))]
    if unbound:
        raise ValueError('Unbound original home condition dependency: '+repr(unbound))
    for name in ('PartnerConditionRules.cs','NpcRosterRules.cs','Zjcs.Official.Partners.asmdef'):
        path=ROOT/'unity/Assets/Game/Runtime/Official/Partners'/name
        inputs[path.relative_to(ROOT).as_posix()]=sha(path.read_bytes())
    payload=work/'home-rules-current.json'
    data=json.loads(payload.read_bytes())
    if data['manifest']!=version:raise ValueError('Selected home condition data version differs')
    rows={row['ClassId']:row['Condition'] for row in data['tables']['conditions']}
    needed,types=set(),set()
    def visit(value,visiting=()):
        if isinstance(value,list):
            for child in value:visit(child,visiting)
        elif isinstance(value,dict):
            if '_type' in value:types.add(value['_type'])
            if value.get('_type') in ('CompositeAndCondition','CompositeOrCondition'):
                for key in value['Ids'] or []:
                    if key in visiting:raise ValueError('Cyclic selected home condition: '+str(key))
                    if key not in needed:
                        needed.add(key);visit(rows[key],visiting+(key,))
            for child in value.values():visit(child,visiting)
    for name in ('furniture','hang_up','hang_up_reward','hang_up_upgrade_material','home_level','fast_hang_up'):
        visit(data['tables'][name])
    income_reward_ids={row['RewardRuleId'] for row in data['tables']['hang_up_reward']}
    visit([row for row in data['tables']['reward_rule'] if row['Id'] in income_reward_ids])
    expected={'LevelCondition','NoneCondition','CompositeOrCondition','HasItemTypeCondition','CompositeAndCondition',
              'MapUnlockCondition','MaxProfessionRankCondition','HasManualMeetNpcCondition','StoryCountCondition',
              'AnyMapGoddessActivedCondition','RankCondition','SubRankCondition',
              'PlayerProfessionCondition','PlayerProfessionFactionCondition'}
    if types!=expected:raise ValueError('Selected home condition closure changed: '+repr(types))
    stage=work/'home-conditions';stage.mkdir(exist_ok=True)
    output=normalize('// Original predicates and explicit player/configuration ports; see home-condition-source-manifest.json.\n'+text).encode()
    (stage/'HomeConditionRules.cs').write_bytes(output)
    receipt=dict(manifest=version,source_sha256=inputs,methods=methods,generator_sha256=sha(Path(__file__).read_bytes()),
        configuration_sha256=sha(payload.read_bytes()),closure=dict(types=sorted(types),condition_ids=sorted(needed)),
        outputs={'HomeConditionRules.cs':sha(output)},adaptations=[
            'Original IsMet/helper/compare methods and enum identities, with diagnostic logging omitted.',
            'Reuse selected PartnerConditionRules for None/Always/Level/HasItem/MapUnlock/MapRegionActived/HasManualMeetNpc leaves.',
            'Original composite traversal resolves current home condition IDs without flattening or changing short circuit.',
            'Inventory and player values come through typed read-only ports; offline opening policy is explicitly injected.',
            'Home evaluation only; feature/UI/save integration is not claimed by staged condition code.'])
    (work/'home-condition-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Prepared original home predicate bodies:',sum(len(value) for value in methods.values()),flush=True)
    return receipt


if __name__=='__main__':
    argparse.ArgumentParser(__doc__).parse_args()
    build(validation_directory())
