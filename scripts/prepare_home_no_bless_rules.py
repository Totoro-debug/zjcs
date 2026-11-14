"""Recover selected NoBlessPlayerInfoProvider and its original limit/resonance parsers."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_home_auto_increase_runtime import method
from prepare_home_experience_rules import metadata, sha
from prepare_unity_character_catalog import decode_level_props
from validation_workspace import require_temporary_unity_project

CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeNoBlessRules'


def build():
    inputs, outputs = {}, {}
    def read(name):
        p=CODE/'common-source/Common'/f'{name}.cs'
        inputs[p.relative_to(ROOT).as_posix()]=sha(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')
    def write(path,text):
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text(text,'utf8',newline='\n')
        for p in (path,metadata(path)): outputs[p.relative_to(ROOT).as_posix()]=sha(p)
    def emit(name,body):
        write(DEST/(name+'.cs'),'// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.\n'
              +'using System;\nusing System.Collections.Generic;\nusing System.Collections.ObjectModel;\nusing System.Linq;\n'
              +'using Zjcs.Official.HomeExperienceRules;\nnamespace Zjcs.Official.HomeNoBlessRules\n{\n'+body+'\n}\n')
    fields=['PlayerLevel','PlayerBlessLevel','SkillLevel','SkillBlessLevel','EquipLevel','EquipBlessLevel','PetLevel','PetBlessLevel']
    original=read('SystemLevelLimitInfo')
    for name in fields: assert re.search(r'public (?:readonly )?int '+name+';',original),name
    emit('SystemLevelLimitInfo','[Serializable] public sealed class SystemLevelLimitInfo\n{\n    public SubRank SubRank;\n'
         +''.join('    public int '+name+';\n' for name in fields)+'}\n')
    original=read('SystemLevelLimitInfoParser')
    members=['mSkillBaseLevelLimitByRank','mSkillBlessLevelLimitByRank','mEquipBaseLevelLimitByRank',
             'mEquipBlessLevelLimitByRank','mPlayerBaseLevelLimitByRank','mPlayerBlessLevelLimitByRank',
             'mAllRanks','mFirstPlayerBlessLevelRank','mPetLevelLimitByRank','mPetBaseLevelLimitByRank','mPetBlessLevelLimitByRank']
    declarations=[]
    for name in members:
        declarations.append(re.search(r'^\tprivate [^\n]+\b'+name+r'[^\n]*;',original,re.M).group())
    signatures=['private void SetSkillMaxLevelLimit(SystemLevelLimitInfo info)',
                'private void SetEquipMaxLevelLimit(SystemLevelLimitInfo info)',
                'private void SetPlayerMaxLevelLimit(SystemLevelLimitInfo info)',
                'private void SetPetMaxLevelLimit(SystemLevelLimitInfo info)',
                'public int GetPlayerBaseLevelLimit(SubRank subRank)',
                'public int GetSkillBaseLevelLimit(SubRank subRank)',
                'public int GetEquipBaseLevelLimit(SubRank subRank)',
                'public int GetPetLevelLimit(SubRank subRank, int playerLevel)',
                'public int GetPetBaseLevelLimit(SubRank subRank)']
    emit('SystemLevelLimitInfoParser','public sealed class SystemLevelLimitInfoParser\n{\n'+'\n'.join(declarations)+'''
    public SystemLevelLimitInfoParser(IEnumerable<SystemLevelLimitInfo> configs)
    {
        foreach(var info in configs) {
            SetSkillMaxLevelLimit(info);SetEquipMaxLevelLimit(info);
            SetPlayerMaxLevelLimit(info);SetPetMaxLevelLimit(info);
        }
    }
'''+ '\n'.join(method(original,sig) for sig in signatures)+'\n}\n')
    original=read('EquipResonanceInfo')
    for kind,name in [('int','Level'),('int','EquipCount'),('int','EquipLevel'),('Dictionary<PropType, long>','Effect')]:
        assert 'public readonly '+kind+' '+name+';' in original,name
    emit('EquipResonanceInfo','''[Serializable] public sealed class EquipResonanceInfo
{
    public int Level, EquipCount, EquipLevel;
    public Dictionary<int,long> Effect;
    public ReadOnlyDictionary<int,long> TotalEffect;
    public EquipLevelCountCondition Condition;
}
public sealed class EquipLevelCountCondition { public int Level, Count; }
''')
    original=read('EquipResonanceInfoParser')
    post=method(original,'protected override void PostParse(Dictionary<int, EquipResonanceInfo> configs)')
    post=post.replace('protected override void','private void').replace('\t\tbase.PostParse(configs);\n','')
    body='\n'.join(method(original,sig) for sig in (
        'private int Index(uint level)','public EquipResonanceInfo Get(uint level)',
        'public void CalcEquipResonanceProps(uint activedEquipResonanceLevel, Dictionary<PropType, long> props)',
        'public uint SearchCurrentLevel(Func<EquipResonanceInfo, bool> checkReachLevelFunc)'))
    emit('EquipResonanceInfoParser',('''public sealed class EquipResonanceInfoParser
{
    private List<EquipResonanceInfo> mResonances;
    public uint MinLevel { get; private set; }
    public uint MaxLevel { get; private set; }
    public EquipResonanceInfoParser(IEnumerable<EquipResonanceInfo> rows) { PostParse(rows.ToDictionary(row=>row.Level)); }
'''+post+'\n'+body+'\n}\n').replace('PropType','int'))
    # Keep every value-changing member of the original decorator. Other
    # original properties are forwarded unchanged by the offline view owner.
    original=read('NoBlessPlayerInfoProvider')
    read('IPlayerInfoProvider');read('PlayerPetDataWrap');read('FormulaHandler')
    fields=original[original.index('\tpublic Dictionary<long, PlayerPetDataWrap>'):original.index('\tpublic IPlayerInfoProvider PlayerInfoProvider')]
    bodies=[]
    for name in ('MyLevel','MyEquipUpgradeLevel','MyEquipResonance','MyPetDatas','MySkillSlotLevelData','MyActivedEquipResonanceLevel'):
        match=re.search(r'\tpublic [^\n]+ '+name+r'\n',original)
        assert match,name
        bodies.append(method(original,match.group().strip()))
    expressions=[]
    for name in ('MyPlanesRank','MySeasonId','MySubRank'):
        expressions.append(re.search(r'\tpublic [^\n]+ '+name+r' => [^\n]+;',original).group())
    body='\n'.join(expressions+bodies+[method(original,'private void CalcEquipResonanceProps(uint activedEquipResonanceLevel)')])
    body=body.replace('Formula.Instance.GetMaxPlayerLevel(MySubRank, out var baseLevel, out var _);',
                      'int baseLevel = Rules.LevelLimits.GetPlayerBaseLevelLimit(MySubRank);')
    body=body.replace('Formula.Instance.GetMaxEquipBaseLevel(MySubRank)','Rules.LevelLimits.GetEquipBaseLevelLimit(MySubRank)')
    body=body.replace('Formula.Instance.GetMaxSkillSlotBaseLevel(MySubRank)','Rules.LevelLimits.GetSkillBaseLevelLimit(MySubRank)')
    body=body.replace('Parsers.Instance.SystemLevelLimitInfoParser','Rules.LevelLimits')
    body=body.replace('Parsers.Instance.EquipResonanceInfoParser','Rules.Resonances')
    body=body.replace('Parsers.Instance.AstrologicalSeasonConfigParser.GetSeasonByRank','Rules.GetSeasonByRank')
    assert 'Formula.Instance' not in body and 'Parsers.Instance' not in body
    body=fields+'''
    public INoBlessPlayerInput<TPet> PlayerInfoProvider { get; }
    private INoBlessConfiguration Rules { get; }
    public NoBlessPlayerInfoProvider(INoBlessPlayerInput<TPet> player, INoBlessConfiguration rules)
    {
        PlayerInfoProvider=player ?? throw new ArgumentNullException(nameof(player));
        Rules=rules ?? throw new ArgumentNullException(nameof(rules));
        mEquipResonanceReadOnly=new ReadOnlyDictionary<int,long>(mEquipResonanceProps);
    }
'''+body
    body=body.replace('PlayerPetDataWrap','TPet').replace('EquipPos','int').replace('PropType','int').replace('ItemType','int')
    emit('NoBlessPlayerInfoProvider','public sealed class NoBlessPlayerInfoProvider<TPet> where TPet : INoBlessPet<TPet>\n{\n'+body+'\n}\n')
    emit('INoBlessPlayerInput','''public interface INoBlessPet<TPet>
{
    int Level { get; set; }
    TPet CloneWrapForAnother();
}
public interface INoBlessPlayerInput<TPet> where TPet : INoBlessPet<TPet>
{
    Rank MyPlanesRank { get; }
    SubRank MySubRank { get; }
    int MyLevel { get; }
    Dictionary<int,int> MyEquipUpgradeLevel { get; }
    Dictionary<long,TPet> MyPetDatas { get; }
    Dictionary<int,Dictionary<int,int>> MySkillSlotLevelData { get; }
    uint MyActivedEquipResonanceLevel { get; }
}
public interface INoBlessConfiguration
{
    SystemLevelLimitInfoParser LevelLimits { get; }
    EquipResonanceInfoParser Resonances { get; }
    int GetSeasonByRank(Rank rank);
}
''')
    write(DEST/'Zjcs.Official.HomeNoBlessRules.asmdef',json.dumps(dict(name='Zjcs.Official.HomeNoBlessRules',
        references=['Zjcs.Official.HomeExperienceRules'],noEngineReferences=True),indent=2)+'\n')
    apk=Apk(reuse_current_cache=True)
    try:
        constructors=Constructors(CODE)
        mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
        tables,provenance={},{}
        wanted=('system_level_limit','equip_resonance','astrological_season_config')
        for bundle in ('defaultpackage_assets_config_luban.bundle','defaultpackage_assets_config_luban_astrologicaldestiny.bundle'):
            for obj in apk.objects(bundle):
                if obj.type.name!='TextAsset':continue
                name=obj.read().m_Name.rsplit('/',1)[-1].removesuffix('.g')
                if name not in wanted:continue
                raw=obj.read().m_Script.encode('utf8','surrogateescape')
                assert name not in tables,name
                tables[name]=constructors.table(raw,mapping[name])
                provenance[name]=dict(source=apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),constructor=mapping[name])
        assert set(tables)==set(wanted),set(tables)
        write(ROOT/'unity/Assets/Game/Resources/Home/no-bless-properties.json',json.dumps(dict(manifest='884_156692',
            limits=tables['system_level_limit'],resonances=tables['equip_resonance'],seasons=tables['astrological_season_config']),
            ensure_ascii=False,separators=(',',':'))+'\n')
        chunk_names=('treasure','treasure_suit','pet','pet_evolution','pet_potential',
                     'pet_ability_prop','pet_ability_prop_scale_factor','item','player_subrank',
                     'system_level_limit','level_number','game_settings','skill_rank')
        chunks={}
        for name in chunk_names:
            obj=apk.asset('config_luban','/'+name+'.g.bytes')
            raw=obj.read().m_Script.encode('utf8','surrogateescape')
            chunks[name]=constructors.table(raw,mapping[name])
            provenance[name]=dict(source=apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),constructor=mapping[name])
        chunks['treasure_task_effects']=[]
        for row in chunks['treasure']:
            effect=row['RawEffect']
            row['EffectTypeName']='Task' if effect and effect['_type']=='TreasureEffectTask' else 'None'
            if row['EffectTypeName']=='Task':
                chunks['treasure_task_effects'].append(dict(ClassId=row['ClassId'],**effect))
            # TreasureInfoParser's derived friendship additions are independent of
            # the property projection and remain with the existing feature owner.
        prop_ids=set()
        for name in ('treasure','pet'):
            for row in chunks[name]:
                prop_ids.update(row['LevelPropId'].values())
        prop_ids.update(row['StarScalePropId'] for row in chunks['treasure'])
        for row in chunks['treasure_suit']:
            prop_ids.update((row['Props'] or {}).values())
        prop_ids.update(row['PropId'] for row in chunks['pet_ability_prop'])
        for row in chunks['player_subrank']:
            prop_ids.update(row[key] for key in ('TreasurePropBless','PetPropBless'))
        prop_ids.discard(0)
        verified=apk.reader.read('WwiseBnk','wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        properties=decode_level_props(verified.data,prop_ids)
        provenance['level_prop']=dict(source=dict(verified.provenance),sha256=hashlib.sha256(verified.data).hexdigest(),groups=sorted(prop_ids))
        # Item qualities and element types are read by the original chunks. Preserve
        # the complete selected rows; the detached catalog merges them by ClassId.
        chunk_path=ROOT/'unity/Assets/Game/Resources/Home/no-bless-property-chunks.json'
        write(chunk_path,json.dumps(dict(manifest='884_156692',tables=chunks,level_prop=properties),ensure_ascii=False,separators=(',',':'))+'\n')
        write(chunk_path.with_name('no-bless-property-chunks-hash.json'),json.dumps(dict(manifest='884_156692',sha256=sha(chunk_path)))+'\n')
        for name in constructors.sources:
            p=CODE/'common-source/Common'/f'{name}.cs'
            if not p.is_file():p=CODE/'common-source/Common.LubanGen'/f'{name}.cs'
            inputs[p.relative_to(ROOT).as_posix()]=sha(p)
    finally:apk.reader.close()
    out=metadata(DEST,True);outputs[out.relative_to(ROOT).as_posix()]=sha(out)
    for relative in ('scripts/official_apk.py','scripts/official_tables.py','scripts/official_source.py',
                     'scripts/prepare_home_auto_increase_runtime.py','scripts/prepare_home_experience_rules.py',
                     'scripts/fixtures/home_no_bless_profile_checks.cs',
                     'scripts/prepare_home_player_status.py',
                     'scripts/prepare_unity_character_catalog.py',
                     '.tools/source-packages/current/884_156692/official-code/common-source/Common/Player.cs',
                     '.tools/source-packages/current/884_156692/official-code/common-source/Common/PlayerEquipDataExt.cs',
                     '.tools/source-packages/current/884_156692/official-code/common-source/Common/PlayerSkllSlotDataExt.cs',
                     '.tools/source-packages/current/884_156692/official-code/common-source/Common/PlayerPetDataManager.cs'):
        inputs[relative]=sha(ROOT/relative)
    for name in ('TreasureInfo','TreasureEffectTask','TreasurePropChunck','PetPropChunck',
                 'PlayerTreasureSlotDataWrap','BattleFormulaHandler'):
        p=CODE/'common-source/Common'/f'{name}.cs'
        inputs[p.relative_to(ROOT).as_posix()]=sha(p)
    for relative in ('unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/OfflineHomeNoBlessConfiguration.cs',
                     'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/OfflineHomeNoBlessPlayerView.cs',
                     'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/Zjcs.Offline.HomePlayerHealthUI.asmdef',
                     'unity/Assets/Game/Runtime/Offline/Progression/CharacterPropCalculator.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/OfflinePetPropSource.cs'):
        for path in (ROOT/relative,metadata(ROOT/relative)):outputs[path.relative_to(ROOT).as_posix()]=sha(path)
    p=ROOT/'unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterCatalog.HomeProperties.cs'
    for path in (p,metadata(p)):outputs[path.relative_to(ROOT).as_posix()]=sha(path)
    proof=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,configuration=provenance,
               scope='Original NoBlessPlayerInfoProvider value-changing getters with typed player/configuration ports, exact selected cap and cumulative resonance parsers. Unchanged player fields remain with the offline owner. World rank and activated resonance are explicit inputs, never inferred from personal rank or currently equipped item levels.')
    (ROOT/'assets/reference/home-no-bless-rules-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Recovered no-blessing provider and selected tables:',{key:len(value) for key,value in tables.items()},flush=True)
    return proof


def stage(project,proof):
    project=require_temporary_unity_project(project)
    for relative,digest in proof['outputs'].items():
        source=ROOT/relative
        assert sha(source)==digest,relative
        target=project/Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)


def stage_validation(project):
    from prepare_home_player_status import stage_validation as status_validation, stage as status_stage, build as status_build
    project=require_temporary_unity_project(project)
    status_stage(project,status_build())
    status_validation(project)
    folder=project/'Assets/HomePlayerHealthProfileValidation'
    path=folder/'HomePlayerHealthProfileValidation.cs'
    source=path.read_text('utf-8-sig').replace('HomePlayerStatusProfile','HomeNoBlessProfile')
    source=source.replace('HOME_PLAYER_STATUS_PROFILE_PASSED:','HOME_NO_BLESS_PROFILE_PASSED:')
    marker='        var growth=new OfflineCharacterProgressionService(profile,OfficialCharacterAssetCatalog.LoadDefault().Load());'
    assert source.count(marker)==1
    source=source.replace(marker,marker.replace('OfficialCharacterAssetCatalog.LoadDefault().Load()',
        'OfflineHomeNoBlessConfiguration.LoadDefault().CreatePropertyCatalog(OfficialCharacterAssetCatalog.LoadDefault().Load())')+'\n        HomeNoBlessProfileChecks.Run(profile,growth);')
    path.write_text(source,'utf8',newline='\n')
    shutil.copyfile(ROOT/'scripts/fixtures/home_no_bless_profile_checks.cs',folder/'HomeNoBlessProfileChecks.cs')
    asm=folder/'HomePlayerHealthProfileValidation.asmdef'
    data=json.loads(asm.read_bytes())
    data['references']+=['Zjcs.Official.HomeNoBlessRules','Zjcs.Offline.Skills','Zjcs.Offline.Equipment','Unity.Newtonsoft.Json']
    asm.write_text(json.dumps(data,indent=2)+'\n','utf8',newline='\n')
    print('Staged no-blessing/shared-calculator and native-panel regression.',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);parser.add_argument('--validation',action='store_true');args=parser.parse_args()
    proof=build()
    if args.project:
        stage(args.project,proof)
        if args.validation:stage_validation(args.project)
