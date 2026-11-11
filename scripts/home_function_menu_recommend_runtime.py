"""Original recommendation dialog, carousel and pool with explicit data ports."""
import re
from prepare_home_auto_increase_runtime import CODE

PORTS='''using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Zjcs.Official.HomeFunctionMenuUI
{
    // These projections expose only fields consumed by the original menu UI.
    // The owning activity module supplies records, configuration and events.
    public interface IHomeMenuActivityEntryData { ActivityEntryType Type { get; } int Id { get; } }
    public sealed class HomeMenuActivityRecommendInfo
    { public ActivityEntryType ActivityEntryType; public int Id; public int Order; public bool ShowInMap; }
    public sealed class HomeMenuActivityEntryInfo { public string EntryPrefabPath; }
    public interface IHomeMenuPrefabHandle:IDisposable { GameObject Result { get; } }
    public interface IHomeMenuActivityServices
    {
        IEnumerable<IHomeMenuActivityEntryData> ActivityEntryDatas { get; }
        event Action<object> OnActivityOpen;
        event Action<object> OnActivityClose;
        bool ActivityDataManagerExists { get; }
        bool IsMapScene { get; }
        HomeMenuActivityRecommendInfo GetRecommend(ActivityEntryType type,int id);
        IReadOnlyDictionary<int,HomeMenuActivityEntryInfo> GetEntries(ActivityEntryType type);
        int RandomRange(int minimum,int maximum);
        IHomeMenuPrefabHandle LoadPrefab(string path);
        // Match the original scene-container Instantiate(prefab,parent,false):
        // bind dependencies before original Awake/OnEnable and preserve layout.
        GameObject Instantiate(GameObject prefab,Transform parent);
        IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position);
        void ReportRecommendationClick(string panel,string eventId,Dictionary<string,string> extend);
    }
    public static class HomeMenuUIUtils
    {
        public static GameObject Instantiate(IHomeMenuActivityServices services,GameObject prefab,Transform parent)
        {
            var go=services.Instantiate(prefab,parent);
            ResetUI(go,EUIInstantiateFlag.All);return go;
        }
        public static T Instantiate<T>(IHomeMenuActivityServices services,GameObject prefab,Transform parent)
        { return Instantiate(services,prefab,parent).GetComponent<T>(); }
        // Original UIUtils.ResetUI; no layout or geometry approximation.
        public static void ResetUI(GameObject go,EUIInstantiateFlag flag=EUIInstantiateFlag.All)
        {
            Transform transform=go.transform;
            if((flag&EUIInstantiateFlag.Position)!=0)transform.localPosition=Vector3.zero;
            if((flag&EUIInstantiateFlag.Rotation)!=0)transform.localRotation=Quaternion.identity;
            if((flag&EUIInstantiateFlag.Scale)!=0)transform.localScale=Vector3.one;
            if((flag&EUIInstantiateFlag.AutoActive)!=0)go.SetActive(true);
        }
    }
}
'''


def prepare_recommend(read,write):
    for name in ('ActivityEntryType',):
        text=read(CODE/'common-source/Common'/(name+'.cs'))
        text=text.replace('namespace Common;','namespace Zjcs.Official.HomeFunctionMenuUI\n{')
        write(name+'.cs','// Original shared activity entry identifiers.\n'+text+'}\n')
    for name in ('EUIInstantiateFlag',):
        text=read(CODE/'client-source'/(name+'.cs'))
        write(name+'.cs','namespace Zjcs.Official.HomeFunctionMenuUI\n{\n'+text+'}\n')
    imports='using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.HomePlayerPanelUI;\nusing Zjcs.Official.SkillsUI.FancyScrollView;\n'
    names=('FunctionEntryDialog','ActivityRecommendItemView','ActivityRecommendContext','ActivtyEntryKeyData','ActivityRecommendItemCell','ActivityEntryBaseItem','ActivityRecommendEventUpItem')
    for name in names:
        resolved=name!='ActivityEntryBaseItem'
        text=read(CODE/('client-source-resolved' if resolved else 'client-source')/(name+'.cs'),resolved)
        for using in ('Common','Framework.DependencyInjection','GameFramework','Framework','R3','AK.Wwise','FancyScrollView'):
            text=text.replace('using '+using+';\n','')
        text=re.sub(r'\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','',text)
        text=re.sub(r'\[UIWindow\([^\n]+\)\]\n','',text)
        text=re.sub(r'\bActivityEntryData\b','IHomeMenuActivityEntryData',text)
        text=text.replace('.IHomeMenuActivityEntryData','.ActivityEntryData')
        text=text.replace('IHomeMenuActivityEntryData IHomeMenuActivityEntryData =>','IHomeMenuActivityEntryData ActivityEntryData =>')
        text=text.replace('DisposableBag','HomePanelDisposableBag')
        text=text.replace('using EasingCore;','using Zjcs.Official.SkillsUI.EasingCore;')
        if name in ('FunctionEntryDialog','ActivityRecommendItemView'):
            at=text.index('\n{',text.index('public class'))+2
            text=text[:at]+'\n\tpublic IHomeMenuActivityServices Services { private get; set; }\n'+text[at:]
        if name=='FunctionEntryDialog':
            text=text.replace('ActivityDataManager.Instance != null','Services.ActivityDataManagerExists')
            text=text.replace('ActivityDataManager.Instance.','Services.')
            text=text.replace('Action<ActivityData>','Action<object>').replace('OnActivityChange(ActivityData data)','OnActivityChange(object data)')
            text=text.replace('ActivityEntryManager.ActivityEntryDatas','Services.ActivityEntryDatas')
            text=text.replace('ActivityRecommendInfo activityRecommendInfo','HomeMenuActivityRecommendInfo activityRecommendInfo')
            text=text.replace('Parsers.Instance.ActivityRecommendInfoParser.Get','Services.GetRecommend')
            text=text.replace('MySceneManager.Instance.ActiveScene.SceneType != SceneType.Map','!Services.IsMapScene')
            text=text.replace('RandomUtils.Range','Services.RandomRange')
        elif name=='ActivityRecommendContext':
            at=text.index('\n{')+2;text=text[:at]+'\n\tpublic IHomeMenuActivityServices Services;\n'+text[at:]
        elif name=='ActivityRecommendItemView':
            text=text.replace('AK.Wwise.Event','OfficialWwiseEvent')
            text=text.replace('base.Initialize();','base.Initialize();\n\t\tContext.Services = Services;')
            text=text.replace('UIUtils.Instantiate(mBannerDotPrefab, mBannerDotSlot)','HomeMenuUIUtils.Instantiate(Services, mBannerDotPrefab, mBannerDotSlot)')
        elif name=='ActivityRecommendItemCell':
            text=text.replace('public ActivityEntryBaseItem CurItem => mCurItem;',
                'public ActivityEntryBaseItem CurItem => mCurItem;\n\tinternal IHomeMenuActivityServices ActivityServices => Context.Services;')
            # Clean ILSpy renders the old Queue extension using ref; the current
            # Unity BCL TryDequeue(out) has the same result and mutation ordering.
            text=text.replace('TryDequeue(ref activityEntryBaseItem)','TryDequeue(out activityEntryBaseItem)')
            text=text.replace('Parsers.Instance.ActivityEntryInfoParser.Get','Context.Services.GetEntries')
            text=text.replace('ResourceLoader.LoadPrefab','Context.Services.LoadPrefab')
            text=text.replace('UIUtils.Instantiate<ActivityEntryBaseItem>(result, Context.PoolItemSlot)',
                'HomeMenuUIUtils.Instantiate<ActivityEntryBaseItem>(Context.Services, result, Context.PoolItemSlot)')
            text=text.replace('ActivityEntryManager.ActivityEntryDatas','Context.Services.ActivityEntryDatas')
        elif name=='ActivityRecommendEventUpItem':
            text=text.replace('public ActivityRecommendItemCell Items;',
                'public ActivityRecommendItemCell Items;\n\tprivate IHomeMenuActivityServices Services => Items.ActivityServices;')
            text=text.replace('WindowUtils.ReportClickPanelEvent("FunctionEntrysPanel", SDKEventPanelId.FunctionEntryPanel_Activity_Recommend_Click, extend, MeManager.MainPlayer.GetShuShuAccountId(), MeManager.MainPlayer.Account)',
                'Services.ReportRecommendationClick("FunctionEntrysPanel", "FunctionEntryPanel_Activity_Recommend_Click", extend)')
            text=text.replace('EventSystemManager.ForeachRaycastResults','Services.ForeachRaycastResults')
        assert not re.search(r'\b(?:Parsers|MySceneManager|ActivityDataManager|ActivityEntryManager|ResourceLoader|Inject|UIUtils|R3)\b',text),name
        assert 'Unknown result type' not in text,name
        write(name+'.cs','// Original menu recommendation implementation; explicit data/resource bindings.\n'+imports+
            'namespace Zjcs.Official.HomeFunctionMenuUI\n{\n'+text+'}\n')
    for name in ('ActivityEntryInfo','ActivityRecommendInfo'):
        read(CODE/'common-source/Common'/(name+'.cs'))
    read(CODE/'client-source/ActivityEntryData.cs')
    read(CODE/'client-source/UIUtils.cs')
    write('HomeMenuActivityServices.cs',PORTS)
