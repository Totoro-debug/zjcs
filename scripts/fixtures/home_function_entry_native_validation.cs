using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

public sealed partial class HomeFunctionEntryNativeRunner
{
    IEnumerator VerifyNativeEntry()
    {
        Require(Prefab,"native entry prefab loaded by its installed binding");
        var services=new EntryServices();var tips=new EntryTips();
        // This centered mount isolates the unchanged subtree. Its final HomeUI
        // parent, sibling layout and the FunctionEntryDialog are separate work.
        var mount=new GameObject("EntryFixtureMount",typeof(RectTransform));mount.SetActive(false);
        mount.transform.SetParent(view.Root.transform.parent,false);
        var rect=mount.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.zero;
        var root=Object.Instantiate(Prefab,mount.transform,false);
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.font=Items()[0].GetComponentInChildren<TMP_Text>(true).font;
        foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
        {
            var originalKey=text.myKey;var originalText=text.Text;
            text.Localizer=key=>key==originalKey && key!="{0}"?originalText:key;
        }
        var panel=root.GetComponent<FunctionEntrysPanel>();panel.InputOperationModule=services;panel.Windows=services;panel.UiCamera=uiCamera;
        var linker=root.GetComponentInChildren<TipDotNodeLinker>(true);linker.Controller=tips;linker.TipDotSystem=tips;
        var animator=root.GetComponent<Animator>();int expanded=0,folded=0;
        var expandSubscription=panel.OnExpandPanel.Subscribe(()=>expanded++);
        var foldSubscription=panel.OnFoldEndPanel.Subscribe(()=>folded++);
        mount.SetActive(true);yield return null;yield return null;Canvas.ForceUpdateCanvases();
        Require(services.PointerCount==1 && tips.LinkCount==1,"native lifecycle subscribes input and original tip address");
        Require(tips.LinkedKey.IsValid() && tips.LinkedKey.GraphName=="FunctionEntry" && tips.LinkedKey.NodeName=="1","exact original selected graph name and serialized node ID");
        Require(animator.GetInteger("Fold")==1 && !Expanded(panel),"original default folded state");
        Require(panel.EntryBtns.Count==2 && panel.SelfRect.Count==2,"complete original entry references");
        Click(panel.EntryBtns[0]);Require(services.Opened==1 && services.Reported==1 && services.Initialized==1 && expanded==0,
            "native folded entry click opens and initializes the function dialog without toggling base panel");
        Capture("home-function-entry-folded.png");
        panel.RequestExpandPanel();Require(expanded==1 && animator.GetInteger("Fold")==0 && !Expanded(panel),
            "request changes original animator parameter; expansion completes through its animation event");
        float deadline=Time.realtimeSinceStartup+3;
        while(!Expanded(panel) && Time.realtimeSinceStartup<deadline)yield return null;
        Require(Expanded(panel),"native Expand clip delivers OnExpandEnd");
        Require(animator.runtimeAnimatorController.animationClips.Any(x=>x.events.Any(e=>e.functionName=="OnExpandEnd")),"original event retained in the clip");
        Click(panel.EntryBtns[1]);Require(services.Opened==2 && services.Initialized==2 && expanded==1,"expanded entry uses the same official dialog branch");
        Capture("home-function-entry-expanded.png");
        Vector2 inside=RectTransformUtility.WorldToScreenPoint(uiCamera,panel.SelfRect[0].TransformPoint(panel.SelfRect[0].rect.center));
        services.Down(inside);Require(Expanded(panel),"inside pointer does not fold");
        services.Panel=true;services.Down(new Vector2(-500,-500));Require(Expanded(panel),"open panel suppresses outside folding");
        services.Panel=false;services.Dialog=true;services.Down(new Vector2(-500,-500));Require(Expanded(panel),"open dialog suppresses outside folding");
        services.Dialog=false;services.Down(new Vector2(-500,-500));
        Require(!Expanded(panel) && animator.GetInteger("Fold")==1,"unobstructed outside pointer folds using original state config");
        deadline=Time.realtimeSinceStartup+3;while(folded==0 && Time.realtimeSinceStartup<deadline)yield return null;
        Require(folded==1,"native Fold clip delivers OnFoldEnd exactly once");
        tips.Emit(new TipDotStateInfo{TipLevel=ETipDotLevelType.Normal,Data="123"});yield return null;
        Require(linker.StateInfo.Data=="123" && !linker.IsShowData && !EntryField<WorldMultiLanguageText>(linker,"mNodeDataText").gameObject.activeSelf,
            "the original function entry disables count display even when its node carries count data");
        Capture("home-function-entry-tip-dot.png");
        tips.Show=false;tips.OnTipDotShowChanged?.Invoke();
        Require(!EntryField<GameObject>(linker,"mTipDotContent").activeSelf,"global tip visibility hides the native content");
        tips.Show=true;tips.OnTipDotShowChanged?.Invoke();
        Require(EntryField<GameObject>(linker,"mTipDotContent").activeSelf,"global tip visibility restores content");
        root.SetActive(false);Require(services.PointerCount==0 && tips.LinkCount==0,"disable removes input and tip subscriptions");
        root.SetActive(true);yield return null;Require(services.PointerCount==1 && tips.LinkCount==1,"reenable binds exactly once");
        var conditions=VerifyConditionsAndTipVisibility(tips);while(conditions.MoveNext())yield return conditions.Current;
        root.SetActive(false);expandSubscription.Dispose();foldSubscription.Dispose();Object.Destroy(mount);yield return null;
        Require(services.PointerCount==0 && tips.LinkCount==0 && tips.OnTipDotShowChanged==null,"destroy releases all entry and standalone tip listeners");
    }

    IEnumerator VerifyConditionsAndTipVisibility(EntryTips tips)
    {
        var services=new EntryConditions();var owner=new GameObject("ConditionFixture");owner.SetActive(false);
        var task=new GameObject("OriginalTaskSlot");task.transform.SetParent(owner.transform,false);task.SetActive(false);
        var taskControl=owner.AddComponent<MainTaskFunctionLimit>();taskControl.Services=services;SetEntryField(taskControl,"myTaskSlot",task);
        var quick=new GameObject("QuickEntry");quick.transform.SetParent(owner.transform,false);
        var chat=owner.AddComponent<ChatQuickMsgEntryController>();chat.Services=services;chat.ChatQuickMsgEntry=quick;
        var imageObject=new GameObject("GlobalTip",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));imageObject.transform.SetParent(owner.transform,false);
        var image=imageObject.GetComponent<Image>();var sprite=owner.AddComponent<SpriteRenderer>();
        var dot=owner.AddComponent<TipDotObject>();dot.TipDotSystem=tips;SetEntryField(dot,"mTipDotImage",image);SetEntryField(dot,"mTipDotSprite",sprite);
        owner.SetActive(true);yield return null;
        Require(services.Listeners.Count==2 && !task.activeSelf && !quick.activeSelf,"unmet task and chat conditions register before their first refresh");
        services.Task.Met=true;services.Notify();Require(task.activeSelf,"task condition enables its serialized slot");
        services.Task.Met=false;services.Notify();Require(task.activeSelf,"official task observer does not deactivate a slot when the condition becomes unmet");
        services.Chat.Met=true;services.HasMap=true;services.MapChannel=false;services.Notify();Require(!quick.activeSelf,"map chat-channel gate is retained");
        services.MapChannel=true;services.Notify();Require(quick.activeSelf,"chat becomes available when both checks pass");
        services.HasMap=false;services.MapChannel=false;services.Notify();Require(quick.activeSelf,"outside a map only the function condition is required");
        tips.Show=false;tips.OnTipDotShowChanged?.Invoke();Require(!image.enabled && !sprite.enabled,"global tip visibility updates both original supported renderer types");
        tips.Show=true;tips.OnTipDotShowChanged?.Invoke();Require(image.enabled && sprite.enabled,"visibility restores both renderers");
        owner.SetActive(false);Require(services.Listeners.Count==0,"disable deregisters task and registered chat conditions");
        owner.SetActive(true);yield return null;Require(services.Listeners.Count==1,"already available chat does not register a redundant condition listener");
        chat.ChatQuickMsgEntry=null;services.Notify();Require(chat.IsAvailable(),"actual MainCanvas null quick-entry reference is supported");
        owner.SetActive(false);Object.Destroy(owner);yield return null;Require(services.Listeners.Count==0,"condition disposal leaves no listeners");
    }

    static bool Expanded(FunctionEntrysPanelBase panel)=>(bool)typeof(FunctionEntrysPanelBase).GetField("mExpanded",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
    static T EntryField<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    static void SetEntryField(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
    sealed class EntryServices:IHomeMainUiInput,IHomeMainUiWindows,IHomeMainFunctionEntryDialog
    {
        Action<HomeUiGlobalPointerDownResult> pointerDown;
        public int PointerCount=>pointerDown?.GetInvocationList().Length??0;
        public int Opened,Reported,Initialized;public bool Panel,Dialog;
        public bool HasPanel=>Panel;public bool HasDialog=>Dialog;
        public void HookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> value){pointerDown+=value;}
        public void UnhookGlobalPointerDown(Action<HomeUiGlobalPointerDownResult> value){pointerDown-=value;}
        public void Down(Vector2 position){pointerDown?.Invoke(new HomeUiGlobalPointerDownResult{ScreenPos=position});}
        public void ReportFunctionEntryClick(){Reported++;}
        public void PopupFunctionEntryDialog(Action<IHomeMainFunctionEntryDialog> initialize){Opened++;initialize(this);}
        public void Init(){Initialized++;}
    }
    sealed class EntryCondition:IHomeHudCondition { public bool Met; }
    sealed class EntryConditions:IHomeMainUiConditions,IHomeHudConditionHandler
    {
        public EntryCondition Task=new EntryCondition(),Chat=new EntryCondition();
        public readonly Dictionary<Action,IHomeHudCondition> Listeners=new Dictionary<Action,IHomeHudCondition>();
        public bool HasMap,MapChannel;
        public int TaskFirstTraceConditionId=>20501;
        public IHomeHudCondition GetCondition(int id){Require(id==TaskFirstTraceConditionId,"configured task condition lookup");return Task;}
        public IHomeHudCondition GetFunctionCondition(FunctionType type){Require(type==FunctionType.Chat,"original chat function lookup");return Chat;}
        public IHomeHudConditionHandler ConditionHandler=>this;
        public bool IsMet(IHomeHudCondition condition)=>((EntryCondition)condition).Met;
        public bool HasMainMap=>HasMap;public bool OpenMapChannel=>MapChannel;
        public void Register(IHomeHudCondition condition,Action refresh){Listeners.Add(refresh,condition);}
        public void Deregister(IHomeHudCondition condition,Action refresh){Require(Listeners.Remove(refresh),"registered condition removed exactly once");}
        public void Notify(){foreach(var listener in Listeners.Keys.ToArray())listener();}
    }
    sealed class EntryTips:IProfessionTipDotSystem,IProfessionTipDotSystemController
    {
        public bool Show=true;public bool IsShowTipDot=>Show;public bool TempLimitShowTip=>false;
        public Action OnTipDotShowChanged{get;set;}
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();
        public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>TipDotGraphType.Static;
        public TipDotNodeKey LinkedKey;Action<TipDotStateInfo> changed;
        public int LinkCount=>changed?.GetInvocationList().Length??0;
        public bool LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> value)
        {LinkedKey=key;changed+=value;value(default);return true;}
        public bool UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> value)
        {Require(key==LinkedKey,"original tip selector identity retained until unlink");changed-=value;return true;}
        public void Emit(TipDotStateInfo value){changed?.Invoke(value);}
    }
}
