using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeUI;
using Object = UnityEngine.Object;
using Sex = Zjcs.Official.HomeHudUI.Sex;

[InitializeOnLoad]
public static class HomeBottomBarNativeValidation
{
    private const string Active="HomeBottomBarNative";
    static HomeBottomBarNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active,false))
                new GameObject("HomeBottomBarFixture").AddComponent<HomeBottomBarNativeRunner>(); };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+"Deadline",float.MaxValue))
                Finish(new TimeoutException("Native bottom bar fixture timeout.")); };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true);
        SessionState.SetFloat(Active+"Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_BOTTOM_BAR_NATIVE_PASSED: original hierarchy, pointer raycasts, five tabs, lock branches, sex sprites, hide reference set, upgrade state, native animations and selector identities; fixture services, not full home/condition evaluation.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeBottomBarNativeRunner : MonoBehaviour, IHomeBottomBarServices, IHomeBottomBarEntryServices,
    IHomePlayerUiServices, IHomePetEvolutionServices, IHomeBottomBarPlayer, IHomeHudConditionHandler,
    IHomeLegionMapDataManager, IProfessionTipDotSystem, IProfessionTipDotSystemController
{
    private OfflineHomeBottomBar view;
    private Camera cameraUi;
    private RenderTexture target;
    private EventSystem events;
    private int errors, soundPosts, changes;
    private bool unlock, upgrade;
    private readonly UnityEvent playerUpdate=new UnityEvent();
    private readonly List<Action> conditionUpdates=new List<Action>();
    private readonly Dictionary<TipDotNodeKey, Action<TipDotStateInfo>> tips=new Dictionary<TipDotNodeKey, Action<TipDotStateInfo>>();
    public event Action OnSubRankUpdate, OnPlanesRankUnlock, OnPlayerUpdate, OnGameUpdate;
    public bool HasPlayer=>true;
    public Sex PlayerSex{get;private set;}=Sex.Male;
    public bool CheckSubRankUpgradeShow()=>upgrade;
    public bool ManagerAvailable=>true;
    UnityEvent IHomePlayerUiServices.OnPlayerUpdate=>playerUpdate;
    public IProfessionTipDotSystem TipDotSystem=>this;
    public IHomeBottomBarPlayer MainPlayer=>this;
    public IHomeHudGameData MainGame=>null;
    public IHomeHudConditionHandler ConditionHandler=>this;
    private sealed class Condition:IHomeHudCondition{}
    public IHomeHudCondition GetFunctionCondition(FunctionType type)=>new Condition();
    public IHomeLegionMapDataManager LegionMapDataManager=>this;
    public StageType GetStageType(int id)=>throw new InvalidOperationException("No fixture games");
    private string lastMessage;
    public void SystemMessage(string key){lastMessage=key;}
    public void SwitchInGame(long id)=>throw new InvalidOperationException("No fixture games");
    public bool IsLegionBossPanelPopup()=>false;
    public void CloseLegionBossPanel()=>throw new InvalidOperationException("No boss popup");
    public void Register(IHomeHudCondition condition,Action refresh){conditionUpdates.Add(refresh);}
    public void Deregister(IHomeHudCondition condition,Action refresh){conditionUpdates.Remove(refresh);}
    public IEnumerable<long> GetAllPetIndices()=>Array.Empty<long>();
    public bool PetCanEvolution(long index)=>false;
    public long LegionId=>0;
    public event Action<int,int,int> OnStageCountUpdate;
    public event Action<IHomeMapActiveableEntity> OnActiveableEntUpdate;
    public event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
    public event Action<int> OnLevelUpdate;
    public event Action<IHomeHudGameData> OnGameDataAdd;
    public event Action<long,bool> OnGameDataRemove;
    public event Action<long,long> OnLegionUpdate;
    public IReadOnlyDictionary<long,IHomeHudGameData> CurrentGames{get;}=new Dictionary<long,IHomeHudGameData>();
    public bool IsUnlockDemonBattle()=>unlock;
    public bool IsFunctionLimit(FunctionType type)=>!unlock;
    public bool IsMopUpUnlocked()=>false;
    public bool IsUnlockDailyDungeon()=>false;
    public bool IsMapGamePlayFruitUnlock()=>false;
    public bool IsMapGamePlayEnergyCollectUnlock()=>false;
    public void QueryAndSubscribeHangUpAffairMsg(){}
    public void QueryAndSubscribeBuildingData(){}
    public void QueryAndSubscribeMemberHangUpMsg(){}
    public void QueryAndSubscribeLifeHangUpData(){}
    public void QueryAndSubscribeItemBagData(){}
    public void UnsubscribeHangUpAffairMsg(){}
    public void UnsubscribeBuildingData(){}
    public void UnsubscribeMemberHangUpMsg(){}
    public void UnsubscribeLifeHangUpData(){}
    public void UnsubscribeItemBagData(){}
    public bool IsShowTipDot=>true;
    public bool TempLimitShowTip=>false;
    public Action OnTipDotShowChanged{get;set;}
    public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();
    public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
    public TipDotGraphType? GetGraphType(string graphName)=>TipDotGraphType.Static;
    public bool LinkNodeStateChange(TipDotNodeKey key,GameObject linker,Action<TipDotStateInfo> callback)
    {tips[key]=callback;callback(default);return true;}
    public bool UnlinkNodeStateChange(TipDotNodeKey key,GameObject linker,Action<TipDotStateInfo> callback)
    {tips.Remove(key);return true;}
    private IEnumerator Start()
    {
        var run=Check();
        while(true){object current;try{if(!run.MoveNext())break;current=run.Current;}
            catch(Exception error){view?.Dispose();HomeBottomBarNativeValidation.Finish(error);yield break;}
            yield return current;}
        view.Dispose();yield return null;
        HomeBottomBarNativeValidation.Finish(errors==0?null:new Exception("Unexpected errors: "+errors));
    }
    private IEnumerator Check()
    {
        AudioListener.volume=0;
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;};
        var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        Require(pipeline,"actual URP2D pipeline");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("HomeValidationFont"));
        cameraUi=new GameObject("BottomBarCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        cameraUi.transform.position=new Vector3(0,0,-10);cameraUi.clearFlags=CameraClearFlags.SolidColor;
        cameraUi.backgroundColor=new Color(.13f,.16f,.18f);target=new RenderTexture(608,1080,24);cameraUi.targetTexture=target;
        var canvas=new GameObject("BottomBarCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var assets=OfficialHomeBottomBarAssetCatalog.LoadDefault();Require(assets.sprites.Length==2,"both original sex sprite bindings");
        var overridePath="Assets/Official/Home/BottomBar/Animation/UI/Public/PublicBottomBarCellController.overrideController";
        var importedOverride=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        Debug.Log("BOTTOM_OVERRIDE_IMPORT: "+(importedOverride?importedOverride.name:"NULL")+" controller="
            +(importedOverride&&importedOverride.runtimeAnimatorController?importedOverride.runtimeAnimatorController.name:"NULL"));
        foreach(var imported in AssetDatabase.LoadAllAssetsAtPath(overridePath))
        {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(imported,out string importedGuid,out long importedId);
            Debug.Log("BOTTOM_OVERRIDE_OBJECT: "+imported.GetType().Name+" "+importedGuid+" "+importedId);}
        view=new OfflineHomeBottomBar(canvas.transform,new HomeBottomBarBindings{Bar=this,Entry=this,PlayerUi=this,Pets=this,TipDots=this,TipController=this,
            Font=font,PostSound=(clip,owner)=>{Require(clip&&owner,"native audio binding");soundPosts++;}},assets);
        view.Bar.OnTabTypeChanged+=(oldTab,newTab)=>changes++;
        yield return new WaitForSeconds(.6f);
        foreach(var transition in view.Root.GetComponentsInChildren<WorldTabAnimatorTransition>(true))
        {
            var anim=transition.Anim;
            Debug.Log("BOTTOM_ANIM: "+anim.name+" active="+anim.gameObject.activeInHierarchy+" enabled="+anim.enabled
                +" controller="+(anim.runtimeAnimatorController?anim.runtimeAnimatorController.name:"NULL")+" layers="+anim.layerCount);
            Require(anim.runtimeAnimatorController&&(!anim.gameObject.activeInHierarchy||anim.layerCount>0),
                "native tab controller imported and initialized "+anim.name);
        }
        Require(view.Bar.TabDatas.Count==5,"five original tabs");
        var travel=view.Root.GetComponentInChildren<TravelNoteBtn>(true);
        var legion=view.Root.GetComponentInChildren<LegionBtn>(true);
        Require(travel.myLockBtn.gameObject.activeInHierarchy&&!travel.myUnlockObj.activeSelf,"travel original lock branch");
        Require(legion.myLockBtn.gameObject.activeInHierarchy&&!legion.myUnlockObj.activeSelf,"legion original lock branch");
        Click(travel.myLockBtn.transform);Require(lastMessage=="TravelNote.FunctionLimit","travel native lock click");
        Click(legion.myLockBtn.transform);Require(lastMessage=="Legion.FunctionLimit","legion native lock click");
        Capture("bottom-bar-locked.png");
        unlock=true;foreach(var update in conditionUpdates.ToArray())update();OnLevelUpdate?.Invoke(50);
        yield return null;
        Require(!travel.myLockBtn.gameObject.activeSelf&&travel.myUnlockObj.activeInHierarchy,"travel condition refresh");
        Require(!legion.myLockBtn.gameObject.activeSelf&&legion.myUnlockObj.activeInHierarchy,"legion condition refresh");
        foreach(var tab in view.Bar.TabDatas){Click(tab.Toggle.transform);yield return new WaitForSeconds(.5f);
            Require(view.Bar.CurrentTabType==tab.TabType&&tab.Toggle.isOn,"native selected tab "+tab.TabType);
            var transition=tab.Toggle.GetComponentInChildren<WorldTabAnimatorTransition>(true);
            Require(transition&&transition.Anim.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("ToggleOn"),
                "original selected animator state "+tab.TabType);
            Require(transition.Anim.GetCurrentAnimatorClipInfo(0).Any(row=>row.clip.name=="PublicBottomBarCell_On"),
                "original override selection clip "+tab.TabType);
            Require(view.Bar.TabDatas.Count(row=>row.Toggle.isOn)==1,"exclusive original selection");}
        Capture("bottom-bar-selected.png");
        Require(changes>=4,"actual native tab callbacks");
        var character=view.Root.GetComponentInChildren<PublicBottomBarCharacterBtn>(true);
        var before=character.GetComponentsInChildren<Image>(true).Select(row=>row.sprite).ToArray();
        PlayerSex=Sex.Female;OnPlayerUpdate?.Invoke();yield return null;
        var after=character.GetComponentsInChildren<Image>(true).Select(row=>row.sprite).ToArray();
        Require(before.Zip(after,(a,b)=>a!=b).Any(v=>v),"original sex sprite proxy updates");
        Capture("bottom-bar-female.png");
        upgrade=true;OnSubRankUpdate?.Invoke();yield return null;Capture("bottom-bar-upgrade.png");
        object first=new object(),second=new object();int hides=0,shows=0;
        view.Bar.OnHideBar+=()=>hides++;view.Bar.OnShowBar+=()=>shows++;
        view.Bar.HideBottomBar(first);view.Bar.HideBottomBar(first);view.Bar.HideBottomBar(second);
        // The original motionless Default state has an exit-time gate of .75 and
        // a .25-second blend. Wait for that transition, not just the .1s clip.
        yield return new WaitForSeconds(1.5f);Require(!view.Bar.IsShow&&hides==2,"original distinct hide references");
        var mainAnimator=view.Root.GetComponentsInChildren<Animator>(true).Single(row=>row.name=="Content");
        Debug.Log("BOTTOM_HIDE: state="+mainAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash+" time="
            +mainAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime+" y="+((RectTransform)mainAnimator.transform).anchoredPosition.y
            +" Group="+(mainAnimator.transform.Find("Group")?mainAnimator.transform.Find("Group").gameObject.activeSelf.ToString():"missing"));
        Require(mainAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("Hide")&&!mainAnimator.IsInTransition(0),
            "original exit-time gate and blend reach the Hide state");
        Require(Mathf.Approximately(((RectTransform)mainAnimator.transform).anchoredPosition.y,-100),"original hide curve endpoint");
        Require(!mainAnimator.transform.Find("Group").gameObject.activeSelf,"original hide disables the tab group");
        Require(Mathf.Approximately(mainAnimator.GetComponent<CanvasGroup>().alpha,.7f),"original hide opacity endpoint");
        yield return null;
        yield return null;
        Capture("bottom-bar-hidden.png");
        view.Bar.TryShowBottomBar(first);Require(!view.Bar.IsShow,"second reference keeps bar hidden");
        view.Bar.TryShowBottomBar(second);yield return new WaitForSeconds(.6f);Require(view.Bar.IsShow&&shows==1,"last reference restores native bar");
        view.Bar.HideBottomBar(first,true);Require(!view.Root.activeSelf,"original instant hide");
        view.Bar.TryShowBottomBar(first,true);yield return null;Require(view.Root.activeSelf,"original instant show");
        var addresses=view.Root.GetComponentsInChildren<TipDotNodeLinker>(true).Length;
        Require(tips.Count>0&&addresses>=tips.Count,"native tip selectors bind original graph/node identities");
        Require(assets.tipSourceObjects.text.Contains("TipDotSkillCanRankUpConditionNode"),"unabridged source condition data preserved");
        Require(AudioListener.volume==0&&soundPosts>=2,"muted native sound events");
        Require(view.Root.GetComponentsInChildren<UIClickSound>(true).Count(row=>row.AkEvent.PlayingId>0)==5,
            "five original tab audio events posted with real playing IDs");
        view.Dispose();yield return null;
        Require(conditionUpdates.Count==0&&tips.Count==0,"native condition/tip unsubscribe on destroy");
        Require(PublicBottomBar.Instance==null,"native singleton released");
    }
    private void Click(Transform button)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button;
        var point=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(pointer,hits);
        Require(hits.Count>0&&(hits[0].gameObject.transform==button||hits[0].gameObject.transform.IsChildOf(button)),
            "native top raycast "+button.name+" actual="+(hits.Count==0?"none":hits[0].gameObject.name));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),texture.EncodeToPNG());Object.Destroy(texture);
    }
    private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
}
