using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Editor;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.TreasureUI;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;
using S=Zjcs.Official.TreasureSlots;

[InitializeOnLoad]
public static class BattlePlanWidgetValidation
{
    const string Key="Zjcs.BattlePlanWidgetValidation";
    static string Work=>Environment.GetEnvironmentVariable("ZJCS_BATTLE_PLAN_WIDGET_OUTPUT");
    static int checks,errors;
    static Camera camera;
    static RenderTexture target;
    static EventSystem events;
    static FontAssetFontService fonts;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static string State(object value)=>JsonConvert.SerializeObject(value);
    sealed class Runner:MonoBehaviour
    {
        IEnumerator Start(){var steps=Steps();while(true){bool next;try{next=steps.MoveNext();}catch(Exception error){Finish(error);yield break;}if(!next)break;yield return steps.Current;}Finish(errors==0?null:new Exception("Unexpected Unity errors: "+errors));}
    }
    static BattlePlanWidgetValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){
            AudioListener.volume=0;Application.runInBackground=true;Application.logMessageReceived+=Log;new GameObject("NativePlanWidgetValidation").AddComponent<Runner>();}};
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",float.MaxValue))Finish(new TimeoutException("Native shared plan widget timed out."));};
    }
    public static void Run()
    {
        string project=Environment.GetEnvironmentVariable("ZJCS_BATTLE_PLAN_WIDGET_PROJECT");
        if(string.IsNullOrEmpty(project)||Path.GetFullPath(Application.dataPath)!=Path.Combine(Path.GetFullPath(project),"Assets")||string.IsNullOrEmpty(Work))throw new InvalidOperationException("Use the selected independent source project.");
        ValidationGameView.Portrait();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+300);EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    static T Field<T>(object owner,string key)=>(T)owner.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    sealed class Moderation:IWorldContentModerationServices
    {
        Action<HashSet<long>> handlers;public int Subscriptions;public bool Disabled;
        public event Action<HashSet<long>> DisabledUpdated{add{handlers+=value;Subscriptions++;}remove{handlers-=value;Subscriptions--;}}
        public bool CheckDisabled(int type)=>Disabled;
        public void ShowError(int code)=>throw new Exception("Unexpected unowned mask click: "+code);
        public void Publish(int type)=>handlers?.Invoke(new HashSet<long>{type});
    }
    sealed class Deferred:IBattlePlanWidgetServices
    {
        public IBattlePlanWidgetServices Inner;public int Subscriptions,Requests;public bool Delay;public Queue<Action> Pending=new Queue<Action>();
        public event Action<BattlePlanType,int,string,string> OnBattlePlanNameChanged{add{Subscriptions++;Inner.OnBattlePlanNameChanged+=value;}remove{Subscriptions--;Inner.OnBattlePlanNameChanged-=value;}}
        public int GetActivePlanIndex(BattlePlanType type)=>Inner.GetActivePlanIndex(type);
        public string GetPlanName(BattlePlanType type,int index)=>Inner.GetPlanName(type,index);
        public string GetBattlePlanName(BattlePlanType type,int index,string name)=>Inner.GetBattlePlanName(type,index,name);
        public bool IsBattlePlanContentUsable(BattlePlanType type,int index)=>Inner.IsBattlePlanContentUsable(type,index);
        public int GetPlanMaxCount(BattlePlanType type)=>Inner.GetPlanMaxCount(type);
        public bool IsPlanLocked()=>Inner.IsPlanLocked();public bool IsPlanFunctionDisabled(BattlePlanType type)=>Inner.IsPlanFunctionDisabled(type);
        public int CheckSkillPlanNameLegal(string name)=>Inner.CheckSkillPlanNameLegal(name);public int GetCharacterMaxCount()=>Inner.GetCharacterMaxCount();
        public void ChangePlanName(PlayerChangeBattlePlanNameRequest request,Action<bool> complete){Requests++;if(Delay)Pending.Enqueue(()=>Inner.ChangePlanName(request,complete));else Inner.ChangePlanName(request,complete);}
        public void Confirm(IWindowOperator owner,string key,Action accepted)=>Inner.Confirm(owner,key,accepted);
        public void ShowError(int code,params object[] args)=>Inner.ShowError(code,args);
        public void SystemMessage(string key,params object[] args)=>Inner.SystemMessage(key,args);
    }
    static void Click(Button button)
    {
        Check(button.gameObject.activeInHierarchy&&button.interactable,"Native button must be active and interactable.");
        Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();
        Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Check(hits.Any(hit=>hit.gameObject.transform==button.transform||hit.gameObject.transform.IsChildOf(button.transform)),"Native button participates in the actual rendered raycast hierarchy: "+button.name);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Steps()
    {
        checks=errors=0;BattlePlanNameDiagnostics.ReportObserverError=error=>errors++;
        var data=OfflineBattlePlanUiCatalog.LoadDefault();var slotsConfig=OfflineTreasureSlotCatalog.LoadDefault();
        Check(data.Maximum(BattlePlanType.Treasure)==slotsConfig.MaxPlanCount,"Current original widget count agrees with retained slot configuration.");
        var oracle=new PlanNameOracle.Validator();
        var cases=new List<string>{null,"","a","火","a\u0301","😀","𠀀","Ａ","#","<",">","a b","a　b","\t","\n"};
        string longGrapheme="a"+new string('\u0301',600);cases.Add(longGrapheme);
        for(int n=1;n<=20;n++){cases.Add(new string('a',n));cases.Add(new string('火',n));cases.Add(string.Concat(Enumerable.Repeat("😀",n)));}
        foreach(int max in new[]{1,2,12,16})foreach(string name in cases){oracle.Maximum=max;
            Check(BattlePlanNames.CheckName(name,true,max)==oracle.CheckBattleConfigPlanNameLegal(name),"Original battle-config name legality including Unicode grapheme weights.");
            Check(BattlePlanNames.CheckName(name,false,max)==(string.IsNullOrEmpty(name)?0:62503),"Original reset-name envelope.");}
        var creation=CharacterCreationOfflineService.OpenDefault(Path.Combine(Work,"isolated-saves"));string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="方案验证";
        Check(creation.TryCreateCharacter(slot,draft,out var error)&&creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create real isolated character: "+error);
        var saves=new ProfileSlotCatalog(Path.Combine(Work,"isolated-saves"));var session=OfflineMainlineProgressSession.OpenCharacter(saves,slot);
        var slots=new OfflineTreasureSlotService(session,slotsConfig);var names=new OfflineTreasurePlanNameService(session,slotsConfig.MaxPlanCount,data.NameMaximum(0));
        string file=Path.Combine(saves.GetSlotDirectory(slot),"city-save.json");var oldSlots=State(session.Save.Player.CharacterProperties.TreasureSlots);var oldInventory=State(session.Save.Player.Inventory);
        int nameEvents=0;names.OnNameChanged.Subscribe((type,index,previous,next)=>{nameEvents++;Check(File.ReadAllText(file).Contains("name-backend"),"Name observers run after durable commit.");});
        Check(names.ChangeName(1,"火焰方案",true,"name-backend-first").IsApplied,"Persist custom plan name.");
        Check(names.ChangeName(1,"火焰方案",true,"name-backend-first").Status==ProgressionResultStatus.Unchanged&&nameEvents==1,"Replay has no duplicate name event.");
        Check(names.GetName(1)=="火焰方案"&&State(session.Save.Player.CharacterProperties.TreasureSlots)==oldSlots&&State(session.Save.Player.Inventory)==oldInventory,"Name transaction does not alter equipment, training or inventory.");
        byte[] before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try{Check(names.ChangeName(1,"失败",true,"name-backend-retry").Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before)&&nameEvents==1,"Real name save failure rolls back metadata, receipts and notifications.");}
        finally{Directory.Delete(file+".tmp");}
        Check(names.ChangeName(1,"重试",true,"name-backend-retry").IsApplied&&nameEvents==2,"Name save retries after storage recovery.");
        Check(names.ChangeName(3,longGrapheme,true,"name-backend-grapheme").IsApplied&&names.GetName(3)==longGrapheme,"Persist valid original grapheme-count names without imposing an invented UTF-16 length cap.");
        var cold=OfflineMainlineProgressSession.OpenCharacter(saves,slot);Check(new OfflineTreasurePlanNameService(cold,slotsConfig.MaxPlanCount,data.NameMaximum(0)).GetName(1)=="重试","Cold-open restores custom plan metadata.");
        var view=slots.Snapshot();int treasure=OfflineTreasureCatalog.Load().Treasures.First().ClassId;Check(slotsConfig.TryGetElement(treasure,out int element),"Original treasure element exists.");
        var trained=view.GetTreasureElementSlotData((S.ElementType)element);trained.InsertTreasureToSlot(0,0);
        trained.TreasureSlots[0].BaseLevel=8;trained.TreasureSlots[0].BlessLevel=2;trained.UpdateTreasureSlotsShareLevel();
        ((S.PlayerBattlePlanTreasurePlanContentWrap)view.GetOrCreatePlanContent(S.BattlePlanType.Treasure,2)).Data.TreasureSlots.Add((S.ElementType)element,new Dictionary<int,int>{{0,treasure}});
        Check(session.ApplyCharacterMutation(p=>{p.Inventory.Add(new OfflineItemStack{ItemId=treasure,Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=treasure,Active=true,Star=1});p.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(view);}).IsApplied,"Seed actual named/empty/usable plans.");
        bool locked=false,disabled=false;int failed=0,sounds=0;var messages=new List<string>();var errorsShown=new List<int>();var confirms=new Queue<Tuple<string,Action>>();
        var live=new OfflineTreasurePlanWidgetServices(session,slots,names,data,0,()=>locked,()=>disabled,(owner,key,accepted)=>confirms.Enqueue(Tuple.Create(key,accepted)),(code,args)=>errorsShown.Add(code),messages.Add,result=>failed++);
        var services=new Deferred{Inner=live};var moderation=new Moderation();
        camera=new GameObject("Original plan UI camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f);
        target=new RenderTexture(675,1463,24);target.Create();camera.targetTexture=target;
        var canvas=new GameObject("Plan widget original host",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,2340);
        var host=new GameObject("Original zero-size slots-content parent",typeof(RectTransform)).GetComponent<RectTransform>();host.SetParent(canvas.transform,false);host.anchorMin=host.anchorMax=new Vector2(.5f,.5f);host.sizeDelta=Vector2.zero;
        events=new GameObject("Owned synthetic event context",typeof(EventSystem)).GetComponent<EventSystem>();
        var hud=OfflineHomeHudAssets.LoadDefault();fonts=hud.CreateFontService();fonts.Initialize();Check(hud.defaultFont!=null&&hud.defaultFont.sourceFontFile!=null,"Original regional source font is supplied by shared font service.");
        var ownerWidget=new OfflineTreasurePlanWidget(host,services,moderation,hud.defaultFont,data.Text,(clip,go)=>{Check(clip!=null&&clip.samples>0,"Original click PCM is bound.");sounds++;},slots,result=>failed++);
        var widget=ownerWidget.Widget;ownerWidget.Init();yield return null;
        foreach(var node in ownerWidget.Root.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"All original widget components import: "+node.name);
        var prev=Field<Button>(widget,"mPrevPlanBtn");var nextButton=Field<Button>(widget,"mNextPlanBtn");var clear=Field<Button>(widget,"mClearBtn");var input=Field<TMP_InputField>(widget,"mPlanNameInput");var bar=Field<Scrollbar>(widget,"mPlanSelectedBar");
        Check(widget.PlanIndex==0&&!clear.gameObject.activeSelf&&services.Subscriptions==1,"Original active-plan initialization and default clear-button hiding.");
        Check(bar.numberOfSteps==slotsConfig.MaxPlanCount&&Mathf.Approximately(bar.size,1f/slotsConfig.MaxPlanCount),"Selection bar includes original configured empty plans.");
        var buttonAnimator=nextButton.GetComponent<Animator>();var buttonGraphic=nextButton.transform.Find("BgImg");
        Check(buttonAnimator!=null&&buttonGraphic!=null&&nextButton.transform.Find("Content")==null,"Navigation keeps the original button hierarchy and animator without inventing a curve target.");
        var pressedClip=buttonAnimator.runtimeAnimatorController.animationClips.First(clip=>clip.name=="Pressed");
        var scaleBindings=AnimationUtility.GetCurveBindings(pressedClip);
        Check(scaleBindings.Length==3&&scaleBindings.All(binding=>binding.path=="Content"&&binding.propertyName.StartsWith("m_LocalScale.")),"Original common press clip targets Content scale, as encoded in the source binding.");
        for(float until=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<until&&buttonAnimator.IsInTransition(0);)yield return null;
        Vector3 normalScale=buttonGraphic.localScale;Capture("normal-pose");
        var press=new PointerEventData(events){button=PointerEventData.InputButton.Left};
        ExecuteEvents.Execute(nextButton.gameObject,press,ExecuteEvents.pointerDownHandler);
        for(float until=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<until&&(!buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Pressed")||buttonAnimator.IsInTransition(0));)yield return null;
        Check(buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Pressed"),"Synthetic native pointer-down enters the original Pressed animator state; initialized="+buttonAnimator.isInitialized+", current="+buttonAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash+", next="+buttonAnimator.GetNextAnimatorStateInfo(0).shortNameHash+", transition="+buttonAnimator.IsInTransition(0)+", speed="+buttonAnimator.speed+", enabled="+buttonAnimator.isActiveAndEnabled+", timeScale="+Time.timeScale);
        Check(buttonGraphic.localScale==normalScale,"An unmatched original Content curve does not scale the differently named native graphic.");Capture("pressed-pose");
        ExecuteEvents.Execute(nextButton.gameObject,press,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(nextButton.gameObject,press,ExecuteEvents.pointerExitHandler);events.SetSelectedGameObject(null);
        for(float until=Time.realtimeSinceStartup+3;Time.realtimeSinceStartup<until&&(!buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal")||buttonAnimator.IsInTransition(0));)yield return null;
        Check(buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal")&&!buttonAnimator.IsInTransition(0)&&buttonGraphic.localScale==normalScale,"Release completes the original Normal transition and preserves the source graphic pose; current="+buttonAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash+", next="+buttonAnimator.GetNextAnimatorStateInfo(0).shortNameHash+", transition="+buttonAnimator.IsInTransition(0)+", selected="+events.currentSelectedGameObject);
        ownerWidget.Init(PlanSwitchWidget.PlanIndexSource.UiState,0,true);yield return null;
        Check(bar.numberOfSteps==2&&Mathf.Approximately(bar.size,.5f),"Skip-empty selection bar counts default plus actual usable content, without counting named empty plans.");
        Click(nextButton);yield return null;Check(widget.PlanIndex==2&&slots.Snapshot().IsTreasureInSlot(treasure)&&Mathf.Approximately(bar.value,1f),"Original next navigation skips named empty content and applies the usable plan.");
        ownerWidget.Init(PlanSwitchWidget.PlanIndexSource.UiState,0,false);yield return null;
        Click(nextButton);yield return null;Check(widget.PlanIndex==1&&session.Save.Player.CharacterProperties.TreasureSlots.ActivePlanIndex==1&&input.text=="重试","Native next click persists selected empty plan and displays its custom name.");Capture("named-plan");
        int unchangedRequests=services.Requests;input.onEndEdit.Invoke(input.text);Check(services.Requests==unchangedRequests,"Finishing an unchanged native name makes no request.");
        input.text="火焰方案";input.onEndEdit.Invoke(input.text);Check(names.GetName(1)=="火焰方案"&&input.text=="火焰方案","Native TMP end-edit commits and refreshes original name text.");
        int requests=services.Requests;input.text="#";input.onEndEdit.Invoke(input.text);Check(services.Requests==requests&&input.text=="火焰方案"&&errorsShown.Last()==1150,"Illegal characters restore input without a request.");
        input.text=new string('a',data.NameMaximum(0)+1);input.onEndEdit.Invoke(input.text);Check(services.Requests==requests&&errorsShown.Last()==702&&input.text=="火焰方案","Original overlength display uses error 702 and restores input.");
        before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try{input.text="写入失败";input.onEndEdit.Invoke(input.text);Check(input.text=="火焰方案"&&File.ReadAllBytes(file).SequenceEqual(before),"Failed UI rename restores prior input and durable file.");}
        finally{Directory.Delete(file+".tmp");}
        services.Delay=true;input.text="延迟名称";input.onEndEdit.Invoke(input.text);Check(services.Pending.Count==1,"Original rename request may remain in flight.");Click(nextButton);yield return null;
        string otherName=input.text;int messageCount=messages.Count;services.Pending.Dequeue()();services.Delay=false;
        Check(widget.PlanIndex==2&&input.text==otherName&&names.GetName(1)=="延迟名称"&&messages.Count==messageCount,"Captured plan identity prevents a late rename response from overwriting another plan's view.");
        Check(names.ChangeName(2,"外部方案",true,"name-backend-external").IsApplied&&input.text=="外部方案","Original name event refreshes a visible widget after another owner commits.");
        Check(slots.Snapshot().IsTreasureInSlot(treasure),"Original plan callback applies real equipment.");
        Click(clear);Check(confirms.Count==1&&confirms.Peek().Item1=="ui_battle_plan_clear_confirm_tip"&&slots.Snapshot().IsTreasureInSlot(treasure),"Native clear requires the original host confirmation before changing equipment.");confirms.Dequeue().Item2();
        Check(!slots.Snapshot().IsTreasureInSlot(treasure)&&names.GetName(1)=="延迟名称"&&names.GetName(2)=="外部方案","Confirmed clear removes worn content and preserves active and inactive plan names.");
        Check(slots.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BaseLevel==8&&slots.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BlessLevel==2,"Names, native plan application and clearing preserve physical slot cultivation.");
        ownerWidget.Init(PlanSwitchWidget.PlanIndexSource.UiState,0,true);yield return null;Click(nextButton);yield return null;
        Check(widget.PlanIndex==0&&bar.numberOfSteps==1&&Mathf.Approximately(bar.size,1f),"Original skip-empty navigation retains default plan when every other plan is empty, including custom-named plans.");
        ownerWidget.Init(PlanSwitchWidget.PlanIndexSource.UiState,0,false);Click(prev);yield return null;Check(widget.PlanIndex==slotsConfig.MaxPlanCount-1,"Native previous wraps through configured empty plans.");
        before=File.ReadAllBytes(file);int confirmed=widget.PlanIndex;Directory.CreateDirectory(file+".tmp");
        try{Click(nextButton);Check(widget.PlanIndex==confirmed&&File.ReadAllBytes(file).SequenceEqual(before)&&nextButton.interactable,"Failed plan application rolls UI back and restores interactability.");}
        finally{Directory.Delete(file+".tmp");}
        ownerWidget.Init(PlanSwitchWidget.PlanIndexSource.UiState,1,false);yield return null;
        moderation.Disabled=true;var mask=ownerWidget.Root.GetComponentInChildren<WorldContentModerationClickMask>(true);moderation.Publish(mask.Type);yield return null;
        Click(mask.MaskBtn);Check(confirms.Count==1&&confirms.Peek().Item1=="ui_battle_plan_contentmoderation_confirm"&&names.GetName(1)=="延迟名称","Original moderation mask asks before resetting custom name.");confirms.Dequeue().Item2();
        Check(names.GetName(1)==string.Empty&&input.text==data.Text("treasure_plan_default_name_2"),"Confirmed reset restores original localized default name.");
        moderation.Disabled=false;moderation.Publish(mask.Type);locked=true;widget.RefreshView();Check(!ownerWidget.Root.activeSelf&&services.Subscriptions==0,"Locked feature hides and releases name subscriptions.");
        locked=false;ownerWidget.Init();yield return null;Check(ownerWidget.Root.activeSelf&&services.Subscriptions==1,"Re-enable restores exactly one name subscription.");
        disabled=true;widget.RefreshView();Check(!ownerWidget.Root.activeSelf,"Original feature-disabled state hides widget.");disabled=false;ownerWidget.Init(rename:false,clear:false);yield return null;
        Check(!input.gameObject.activeSelf&&!clear.gameObject.activeSelf&&Field<WorldMultiLanguageText>(widget,"mPlanNameText").gameObject.activeSelf,"Read-only configuration retains original alternate name text.");Capture("readonly-plan");
        ownerWidget.Dispose();yield return null;Check(services.Subscriptions==0&&moderation.Subscriptions==0&&sounds>0&&failed>=2,"Closing releases original listeners; native sound and storage failure routes ran.");
        cold=OfflineMainlineProgressSession.OpenCharacter(saves,slot);Check(new OfflineTreasurePlanNameService(cold,slotsConfig.MaxPlanCount,data.NameMaximum(0)).GetName(1)==string.Empty,"Cold-open after UI changes restores reset name.");
        Debug.Log("BATTLE_PLAN_WIDGET_STAGE: original UI, real names, actual slots and persistence complete");
    }
    static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=old;
        Check(image.GetPixels32().Select(c=>(int)c.r+(c.g<<8)+(c.b<<16)).Distinct().Count()>32,"Actual native widget renders its sprites and localized text.");File.WriteAllBytes(Path.Combine(Work,name+".png"),image.EncodeToPNG());Object.Destroy(image);
    }
    static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;fonts?.Dispose();BattlePlanNameDiagnostics.ReportObserverError=null;
        if(error==null){File.WriteAllText(Path.Combine(Work,"result.json"),State(new{success=true,checks}));Debug.Log("BATTLE_PLAN_WIDGET_PASSED: "+checks);}else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
