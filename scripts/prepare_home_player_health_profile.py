"""Recover the selected original cure-item query and stage the live HUD player."""
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
from validation_workspace import require_temporary_unity_project

CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
RULES = ROOT/'unity/Assets/Game/Runtime/Official/HomePlayerHealthRules'
PLAYER = ROOT/'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI'


def build():
    inputs, outputs = {}, {}
    def source(relative):
        p=CODE/relative; inputs[p.relative_to(ROOT).as_posix()]=sha(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')
    def write(path,text):
        path.parent.mkdir(parents=True,exist_ok=True); path.write_text(text,'utf8',newline='\n')
        for p in (path,metadata(path)): outputs[p.relative_to(ROOT).as_posix()]=sha(p)
    body=method(source('common-source/Common/Player.cs'),'public bool HasCureItem()')
    body=body.replace('Parsers.Instance.CookingInfoParser.CookingTypeDict','CookingTypeDict')
    cooking_type=source('common-source/Common/CookingType.cs').split('namespace Common;')[1].strip()
    original=source('common-source/Common/CookingInfo.cs')
    assert 'public readonly int Id;' in original and 'public readonly CookingType Type;' in original
    prefix='// Selected original 884 cure-item query; table and inventory boundaries only.\nusing System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomePlayerHealthRules\n{\n'
    write(RULES/'HomeCureItemQuery.cs',prefix+cooking_type+'''
[Serializable] public sealed class CookingInfo { public int Id; public CookingType Type; }
public sealed class HomeCureItemQuery
{
    private readonly Dictionary<CookingType,List<CookingInfo>> CookingTypeDict = new Dictionary<CookingType,List<CookingInfo>>();
    private readonly Func<int,long> inventory;
    public HomeCureItemQuery(IEnumerable<CookingInfo> rows, Func<int,long> inventory)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        foreach (var row in rows) {
            if (!CookingTypeDict.TryGetValue(row.Type, out var list)) CookingTypeDict.Add(row.Type, list = new List<CookingInfo>());
            list.Add(row);
        }
    }
    private long GetItemAmount(int itemId) => inventory(itemId);
'''+body+'}\n}\n')
    write(RULES/'Zjcs.Official.HomePlayerHealthRules.asmdef',json.dumps(dict(name='Zjcs.Official.HomePlayerHealthRules',
        references=[],noEngineReferences=True),indent=2)+'\n')
    source('common-source/Common/CookingInfoParser.cs')
    source('client-source/PlayerHpRedPoint.cs')
    source('client-source/MainPlayerHpImage.cs')
    apk=Apk(reuse_current_cache=True)
    try:
        constructors=Constructors(CODE)
        mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
        tables={};sources={}
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name!='TextAsset':continue
            name=obj.read().m_Name.rsplit('/',1)[-1].removesuffix('.g')
            if name not in ('game_settings','cooking','item'):continue
            raw=obj.read().m_Script.encode('utf8','surrogateescape')
            tables[name]=constructors.table(raw,mapping[name])
            sources[name]=dict(source=apk.provenance(obj),sha256=hashlib.sha256(raw).hexdigest(),constructor=mapping[name])
        settings=tables['game_settings']
        # Keep the selected IDs/types. No recipe/unlock/quantity rule is added.
        cooking=[dict(Id=row['Id'],Type=row['Type']) for row in tables['cooking']]
        item_types={row['ClassId']:row['ItemType'] for row in tables['item']}
        cooking_item_types={item_types[row['Id']] for row in cooking}
        assert len(cooking_item_types)==1
        config=dict(manifest=apk.version,playerHpRedDotThreshold=settings['PlayerHpRedDotThreshold'],
                    cookingItemType=cooking_item_types.pop(),cooking=cooking,
                    itemTypes=[dict(id=key,type=value) for key,value in item_types.items()])
        write(ROOT/'unity/Assets/Game/Resources/Home/player-health-ui.json',json.dumps(config,ensure_ascii=False,separators=(',',':'))+'\n')
        for name in constructors.sources:
            p=CODE/'common-source/Common'/f'{name}.cs'
            if not p.is_file():p=CODE/'common-source/Common.LubanGen'/f'{name}.cs'
            inputs[p.relative_to(ROOT).as_posix()]=sha(p)
    finally:apk.reader.close()
    for folder in (RULES,PLAYER):
        for p in (*folder.glob('*.cs'),*folder.glob('*.asmdef')):
            for output in (p,metadata(p)):outputs[output.relative_to(ROOT).as_posix()]=sha(output)
        p=metadata(folder,True);outputs[p.relative_to(ROOT).as_posix()]=sha(p)
    for relative in ('scripts/official_apk.py','scripts/official_tables.py','scripts/official_source.py','scripts/official_bundle_reader.py',
                     'scripts/prepare_home_auto_increase_runtime.py','scripts/prepare_home_experience_rules.py',
                     'assets/reference/home-player-panel-native-source-manifest.json',
                     'assets/reference/home-experience-profile-source-manifest.json',
                     'scripts/fixtures/home_player_panel_native_validation.cs',
                     'scripts/fixtures/home_player_health_profile_checks.cs',
                     'unity/Assets/Game/Runtime/Offline/Progression/OfflinePlayerHealth.cs'):
        inputs[relative]=sha(ROOT/relative)
    proof=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,configuration=sources,
        scope='Original HasCureItem over selected cooking/item types, real local health and inventory/name projection. Experience uses the separately migrated selected rules. Combat ratings, active maps, status records and blessing-reset events remain mandatory explicit owner services, without inferred server rules.')
    (ROOT/'assets/reference/home-player-health-profile-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Recovered selected cure-item query:',len(cooking),'cooking items, threshold',config['playerHpRedDotThreshold'],flush=True)
    return proof


def stage(project, proof):
    project=require_temporary_unity_project(project)
    for relative,expected in proof['outputs'].items():
        source=ROOT/relative
        if sha(source)!=expected:raise ValueError('Changed health HUD output: '+relative)
        target=project/Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
    source=(ROOT/'scripts/fixtures/home_player_panel_native_validation.cs').read_text('utf-8-sig')
    source=source.replace('HomePlayerPanelNative','HomePlayerHealthProfile').replace(
        'HOME_PLAYER_PANEL_NATIVE_PASSED: full original panel, real selected-player portrait, HP/blessing/status events, source countdown, internal pointer and hide/reopen lifecycle.',
        'HOME_PLAYER_HEALTH_PROFILE_PASSED: real saved health/inventory/name/XP, selected 110 cooking rows, original full panel and portrait, internal pointer, disk reopen, hide/reopen and disposal. Rating/map/battle/status owner inputs are controlled; formal HomeFeatureRuntime and Windows Player are outside this test.')
    source='using Zjcs.Offline.HomePlayerHealthUI;\nusing Zjcs.Offline.HomeExperienceUI;\n'+source
    source=source.replace('avatar-saves-','health-profile-saves-').replace('home-avatar-head.png','home-player-health-head.png').replace('avatar-portrait-cache','health-profile-portrait-cache')
    marker='            LanguageManager.Initialize(experience);'
    assert source.count(marker)==1
    setup='''            var healthConfig=OfflineHomePlayerHealthConfiguration.LoadDefault();
            // Explicit controlled valid initial-rank scenario; production choice is pending.
            Require(profile.ApplyCharacterMutation(saved=>{saved.SubRank=1;saved.Rank=1;saved.Level=1;}).IsApplied,"controlled valid rank");
            var owner=new OwnerState();var games=new List<bool>();
            var health=new OfflinePlayerHealth(profile,growth,()=>owner.Maps.Count,()=>games,()=>experience.Now);
            using var xp=new OfflineHomeExperiencePlayer(profile,OfflineHomeExperienceConfiguration.LoadDefault().CreateTables(),
                ()=>new OfflineHomeExperienceEnvironment{PlanesRank=Zjcs.Official.HomeExperienceRules.Rank.Norank});
            using var expServices=new OfflineHomeExperienceServices(xp,new OfflineHomeExperienceStore(xp),null,_=>{},
                value=>CultureNumberArg.Compact(value),rank=>rank.ToString(),_=>{});
            using var actualPlayer=new OfflineHomePanelPlayer(profile,xp,health,owner,healthConfig);
            experience.Player=actualPlayer;experience.ExpServices=expServices;experience.HealthConfig=healthConfig;
'''
    source=source.replace(marker,setup+marker)
    start=source.index('            var hp=avatar.Root.GetComponentInChildren<MainPlayerHpImage>(true);')
    end=source.index('\n        }\n        finally',start)
    source=source[:start]+(ROOT/'scripts/fixtures/home_player_health_profile_checks.cs').read_text('utf-8-sig')+source[end:]
    source=source.replace('public readonly PlayerData Player=new PlayerData();public Func<long,long,IEnumerator> Display;',
        'public OfflineHomePanelPlayer Player;public OfflineHomeExperienceServices ExpServices;public OfflineHomePlayerHealthConfiguration HealthConfig;')
    source=source.replace('this.assets=assets;Player.PlayerId=id;','this.assets=assets;')
    start=source.index('        public bool ManagerAvailable=>true;')
    end=source.index('        public bool TMPEffectFontSizeIssueFixed',start)
    source=source[:start]+'''        public bool ManagerAvailable=>ExpServices.ManagerAvailable;
        public SafeAction OnPlayerUpdate=>ExpServices.OnPlayerUpdate;
        public IHomeLevelExpParser LevelExpParser=>ExpServices.LevelExpParser;
        public IHomeLevelBlessParser LevelBlessInfoParser=>ExpServices.LevelBlessInfoParser;
        public IHomeExperienceRewards Rewards=>ExpServices.Rewards;
        public int PlayerExpItemId=>ExpServices.PlayerExpItemId;public int PlanesRank=>ExpServices.PlanesRank;
        public void ResolveSubRank(int value)=>ExpServices.ResolveSubRank(value);
        public object Compact(long value)=>ExpServices.Compact(value);
        public string GetSeasonNameByRank(int rank)=>ExpServices.GetSeasonNameByRank(rank);
'''+source[end:]
    source=source.replace('IHomePlayerPanelBindings,IHomeLevelExpParser,IHomeLevelBlessParser,IHomeExperienceRewards,','IHomePlayerPanelBindings,')
    source=source.replace('public float PlayerHpRedDotThreshold=>.5f;','public float PlayerHpRedDotThreshold=>HealthConfig.playerHpRedDotThreshold;')
    source=source.replace('public HomePanelItemInfo GetItemInfo(int id)=>new HomePanelItemInfo{ItemType=HomePanelItemType.Cooking,PartInfo=new HomePanelCookingInfo{Type=CookingType.Recovery}};',
                          'public HomePanelItemInfo GetItemInfo(int id)=>HealthConfig.GetItemInfo(id);')
    start=source.index('    sealed class PlayerData:IHomePanelPlayer')
    source=source[:start]+'''    sealed class OwnerState:IOfflineHomePanelState
    {
        // These scenarios are explicit fixture owner inputs, never a production
        // empty-status or guessed no-blessing-rating implementation.
        public long CombatRating=>12345;public long NoBlessCombatRating=>12345;
        public readonly List<int> ActiveMaps=new List<int>();public IReadOnlyCollection<int> Maps=>ActiveMaps;
        public IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffects{get;}=new Dictionary<int,IHomePanelStatusEffect>();
        Action<int,int> reset;Action<IHomePanelStatusEffect> status;
        public event Action<int,int> BlessLevelDataReset{add{reset+=value;}remove{reset-=value;}}
        public event Action<IHomePanelStatusEffect> StatusEffectDataUpdate{add{status+=value;}remove{status-=value;}}
        public int Subscribers=>(reset?.GetInvocationList().Length??0)+(status?.GetInvocationList().Length??0);
        public void Publish(){reset?.Invoke(1,1);status?.Invoke(null);}
    }
}
'''
    folder=project/'Assets/HomePlayerHealthProfileValidation';folder.mkdir(exist_ok=True)
    (folder/'HomePlayerHealthProfileValidation.cs').write_text(source,'utf8',newline='\n')
    refs=['Zjcs.Official.HomePlayerPanelUI','Zjcs.Official.HomePlayerInfoUI',
        'Zjcs.Official.HomeHudUI','Zjcs.Offline.HomeUI','Zjcs.Offline.PartnersUI','Zjcs.Offline.Progression',
        'Zjcs.Offline.CharacterCreation','Zjcs.Offline.State','Zjcs.Official.Partners','Zjcs.Official.ProfessionUI',
        'Zjcs.Offline.ProfessionUI','Zjcs.Official.TeamUI','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.World.NativeText','Unity.TextMeshPro','Unity.ugui',
        'Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Core.Runtime']
    refs+=['Zjcs.Offline.HomeExperienceUI','Zjcs.Offline.HomePlayerHealthUI','Zjcs.Official.HomePlayerHealthRules',
           'Zjcs.Official.HomeExperienceRules','Zjcs.Official.HomeExperienceRewards']
    (folder/'HomePlayerHealthProfileValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomePlayerHealthProfile.Validation',
        references=refs,includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')
    print('Staged actual health profile in',project,flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args()
    proof=build()
    if args.project:stage(args.project,proof)
