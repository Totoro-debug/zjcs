"""Port the selected original menu buttons without changing their condition branches."""
import json
import re
from pathlib import Path

from prepare_home_auto_increase_runtime import ROOT,CODE,sha,metadata

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeFunctionMenuUI'
NAMES=('FunctionEntryBtnBase','FunctionEntryTaskBtn','FunctionEntryBigBtnItem','FunctionEntrySmallBtnItem',
    'FunctionEntryBannerDotItem','AchievementGroupBtn','CollectBookBtn','HomeSkinBtn','LocalRecordBtn',
    'SettingsBtn','ScreenShotEntry','AnnounceBtn','FriendBtn','ShopEntryBtn','SocialBtn','TopListBtn','UserBtn',
    'PlanesMergeFunctionEntryBtn')
PORTS='''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFunctionMenuUI
{
    public sealed class HomeMenuFunctionLimitInfo { public IHomeHudCondition Condition; }
    public enum HomeMenuSettingStyle { Login, Normal }
    public interface IHomeMenuPlayer
    {
        bool IsFunctionLimit(FunctionType type);
        event Action<int> OnLevelUpdate;
        event Action<int> OnSubRankUpdate;
    }
    public interface IHomeMenuPanel { void Init(); }
    public interface IHomeMenuAchievementPanel { void Init(List<object> definitions); }
    public interface IHomeMenuCollectBookPanel { void Init(CollectionType type,CollectBookDataBase data); }
    public interface IHomeMenuSettingsDialog { void Init(HomeMenuSettingStyle style); }
    // Popup callbacks run at the same point as the original WindowManager
    // callbacks. Bind real feature hosts before any original Awake/OnEnable.
    public interface IHomeFunctionMenuServices
    {
        IHomeMenuPlayer Player { get; }
        IHomeHudConditionHandler ConditionHandler { get; }
        HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type);
        bool IsMet(IHomeHudCondition condition);
        bool IsHomeScene { get; }
        IEnumerable<object> GetAllAchievementGroups();
        void ReportClick(string panel,string eventId);
        void PopupAchievementGroups(IWindowOperator owner,Action<IHomeMenuAchievementPanel> initialize);
        void PopupCollectBook(IWindowOperator owner,Action<IHomeMenuCollectBookPanel> initialize);
        void PopupHomeSkin(Action<IHomeMenuPanel> initialize);
        void PopupLocalFightRecord(Action<IHomeMenuPanel> initialize);
        void PopupSettings(IWindowOperator owner,Action<IHomeMenuSettingsDialog> initialize);
        void PopupShopActivityEntry(Action<IHomeMenuPanel> initialize);
        void PopupPlanesMerge(Action<IHomeMenuPanel> initialize);
        void PopupFriend(IWindowOperator owner);
        void PopupUser();
        void PopupPersonalTop(string eventId,string source);
        void NavigateTaskPanel(object argument,object completion);
        void OpenScreenShot();
        void PopAnnounce();
        void PopSocial();
        void RemoveNewFunction(FunctionType type);
        bool CheckFunctionDisabled(string type);
        void ShowError(int id,object args,bool translate);
    }
}
'''


def replace(text,before,after,count=1):
    assert text.count(before)==count,(before,text.count(before),count)
    return text.replace(before,after)


def build():
    inputs,outputs={},{}
    def read(path,verify=False):
        if verify:
            proof=path.with_suffix('.decompilation.json');data=json.loads(proof.read_bytes())
            assert sha(path)==data['sha256'],path
            for relative,digest in data['inputs'].items():assert sha(ROOT/relative)==digest,relative
            inputs[proof.relative_to(ROOT).as_posix()]=sha(proof)
        inputs[path.relative_to(ROOT).as_posix()]=sha(path)
        return path.read_text('utf-8-sig')
    def write(name,text):
        path=DEST/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,'utf8',newline='\n')
        for item in (path,metadata(path)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    for name in NAMES:
        text=read(CODE/'client-source-resolved'/(name+'.cs'),True)
        assert 'Unknown result type' not in text and 'IAsyncStateMachine' not in text,name
        for using in ('using Common;\n','using Framework.DependencyInjection;\n','using GameFramework;\n','using Framework;\n'):
            text=text.replace(using,'')
        # Dependency injection declarations are replaced by one explicit local
        # service binding; all call sites retain their original control flow.
        text=re.sub(r'\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','',text)
        needs_services=name not in ('FunctionEntryBigBtnItem','FunctionEntrySmallBtnItem','FunctionEntryBannerDotItem','PlanesMergeFunctionEntryBtn')
        if needs_services:
            getter='protected' if name=='FunctionEntryBtnBase' else 'private'
            at=text.index('\n{',text.index('public '))+2
            text=text[:at]+'\n\tpublic IHomeFunctionMenuServices Services { '+getter+' get; set; }\n'+text[at:]
        text=text.replace('ICondition','IHomeHudCondition').replace('ConditionHandler.Instance','Services.ConditionHandler')
        text=text.replace('StateSwitcher<bool>','WorldStateSwitcher<bool>')
        text,n=re.subn(r'WindowUtils.ReportClickPanelEvent\("([^"]+)", SDKEventPanelId\.([A-Za-z0-9_]+), null, MeManager.MainPlayer.GetShuShuAccountId\(\), MeManager.MainPlayer.Account\);',
            r'Services.ReportClick("\1", "\2");',text)
        if 'ReportClickPanelEvent' in text:raise ValueError(name)
        text=text.replace('FunctionLimitInfo functionLimitInfo','HomeMenuFunctionLimitInfo functionLimitInfo')
        text=re.sub(r'Parsers.Instance.FunctionLimitInfoParser.Get\(([^\n;]+)\)',r'Services.GetFunctionLimit(\1)',text)
        text=text.replace('\t\tFunctionLimitInfoParser functionLimitInfoParser = Parsers.Instance.FunctionLimitInfoParser;\n','')
        text=re.sub(r'functionLimitInfoParser.Get\(([^\n;]+)\)',r'Services.GetFunctionLimit(\1)',text)
        text=re.sub(r'(value|mCondition).IsMet\(MeManager.MainPlayer\)',r'Services.IsMet(\1)',text)
        text=text.replace('MeManager.MainPlayer','Services.Player')
        text=text.replace('MySceneManager.ActiveScene.SceneType == SceneType.Home','Services.IsHomeScene')
        popups={
            'AchievementGroupPanel':'PopupAchievementGroups','CollectBookPanel':'PopupCollectBook',
            'HomeSkinPanel':'PopupHomeSkin','LocalFightRecordPanel':'PopupLocalFightRecord',
            'SettingsDialog':'PopupSettings','ShopActivityEntryPanel':'PopupShopActivityEntry','PlanesMergePanel':'PopupPlanesMerge'}
        interfaces={'AchievementGroupPanel':'IHomeMenuAchievementPanel','CollectBookPanel':'IHomeMenuCollectBookPanel','SettingsDialog':'IHomeMenuSettingsDialog'}
        for original,method in popups.items():
            text=text.replace('WindowManager.Instance.Popup(this, ('+original+' ',
                'Services.'+method+'(this, ('+interfaces.get(original,'IHomeMenuPanel')+' ')
            text=text.replace('WindowManager.Instance.Popup(('+original+' ',
                'Services.'+method+'(('+interfaces.get(original,'IHomeMenuPanel')+' ')
        text=text.replace('Parsers.Instance.AchievementGroupInfoParser.GetAll()','Services.GetAllAchievementGroups()')
        text=text.replace('SettingsDialog.SettingStyle.Normal','HomeMenuSettingStyle.Normal')
        text=text.replace('WindowManager.Instance.Popup<FriendPanel>(this)','Services.PopupFriend(this)')
        text=text.replace('WindowManager.Instance.Popup<UserDialog>()','Services.PopupUser()')
        text=text.replace('WindowUtils.PopupAndReport<PersonalTopPanel>(SDKEventPanelId.TopPanel_CombatRating, SDKEventPanelSource.Manual)',
            'Services.PopupPersonalTop("TopPanel_CombatRating", "Manual")')
        text=text.replace('SubRank obj','int obj')
        text=text.replace('NavigateManager.Navigate(ENavigateType.TaskPanel, null, null)','Services.NavigateTaskPanel(null, null)')
        text=text.replace('ScreenShotPanelLauncher.Open(ServiceProvider, ResourceLoader)','Services.OpenScreenShot()')
        text=text.replace('AnnouncePopHandler.PopAnnounce()','Services.PopAnnounce()')
        text=text.replace('SocialPopHandler.PopSocial()','Services.PopSocial()')
        text=text.replace('NewFunctionManager.Instance.RemoveNewFunction','Services.RemoveNewFunction')
        text=text.replace('ServerInfoManager.Instance.CheckFunctionDisabled(Services.Player, ServerFunctionType.PlanesIconEdit)',
            'Services.CheckFunctionDisabled("PlanesIconEdit")')
        text=text.replace('BroadcastManager.ShowErrText(53883, null, false)','Services.ShowError(53883, null, false)')
        assert not re.search(r'\b(?:MeManager|Parsers|WindowManager|WindowUtils|SDKEventPanelId|Inject|SceneType|ServerInfoManager)\b',text),name
        text='\n'.join(line.rstrip() for line in text.splitlines()).rstrip()+'\n'
        write(name+'.cs','// Original menu component; explicit local service bindings only.\nusing Zjcs.Official.HomeHudUI;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nnamespace Zjcs.Official.HomeFunctionMenuUI\n{\n'+text+'}\n')
    for name in ('CollectionType','CollectBookDataBase'):
        text=read(CODE/'client-source'/(name+'.cs'))
        write(name+'.cs','// Original menu data contract.\nnamespace Zjcs.Official.HomeFunctionMenuUI\n{\n'+text+'}\n')
    settings=read(CODE/'client-source/SettingsDialog.cs')
    assert re.search(r'public enum SettingStyle\s*\{\s*Login,\s*Normal\s*\}',settings)
    write('HomeFunctionMenuServices.cs',PORTS)
    from home_function_menu_support_runtime import prepare_support
    support=ROOT/'scripts/home_function_menu_support_runtime.py';inputs[support.relative_to(ROOT).as_posix()]=sha(support)
    prepare_support(read,write)
    from home_function_menu_recommend_runtime import prepare_recommend
    helper=ROOT/'scripts/home_function_menu_recommend_runtime.py';inputs[helper.relative_to(ROOT).as_posix()]=sha(helper)
    prepare_recommend(read,write)
    write('Zjcs.Official.HomeFunctionMenuUI.asmdef',json.dumps(dict(name='Zjcs.Official.HomeFunctionMenuUI',
        references=['Unity.ugui','UniTask','Zjcs.Official.HomeHudUI','Zjcs.Official.ProfessionUI','Zjcs.Official.WorldUI','Zjcs.Official.HomePlayerPanelUI','Zjcs.Official.SkillsUI']),indent=2)+'\n')
    folder_meta=metadata(DEST,True);outputs[folder_meta.relative_to(ROOT).as_posix()]=sha(folder_meta)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Original full function-menu UI, button conditions/callbacks, time/Wiki/predownload components and recommendation carousel/pool. Local production services and formal HUD integration remain required; actual validation scope is maintained only in the home feature page.')
    (ROOT/'assets/reference/home-function-menu-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original menu controls:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
