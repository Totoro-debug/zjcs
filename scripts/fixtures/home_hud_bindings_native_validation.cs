using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeResultUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeSocialUiControls;
using Zjcs.Official.HomeTaskUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeCamera;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object=UnityEngine.Object;
using PointerResult=Zjcs.Official.HomeMainUiControls.OnGlobalPointerDownResult;
using PointerModule=Zjcs.Official.HomeMainUiControls.InputOperationModule;
using PointerOperations=Zjcs.Official.HomeMainUiControls.IInputOperationModule;
using PointerOperation=Zjcs.Official.HomeMainUiControls.IInputOperation;

[InitializeOnLoad]
public static class HomeHudBindingsNativeValidation
{
    const string Key="Zjcs.HomeHudBindingsNativeValidation";
    static HomeHudBindingsNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))new GameObject("CompleteHomeUiBindingFixture").AddComponent<HomeHudBindingsNativeRunner>();
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Full HomeUI binding fixture timed out."));};
    }
    public static void Run()
    {
        var work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_HUD_BINDINGS_NATIVE_PASSED: complete retained HomeUI active under its original ancestors, original Show/Hide, original input dispatcher with internal pointer samples and owned frame polling, native task/function pointer dispatch and subscription disposal. Selected conditions and level gating use an actual new-character save and original notifications; task/window/activity/scene data and top-bar pool are controlled fixtures; public avatar/bottom bar and production business integration remain separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}
public sealed class HomeHudBindingsNativeRunner:MonoBehaviour
{
    Camera camera;RenderTexture target;EventSystem events;int errors;OfflineHomeHud view;OfflineHomeHudInput input;PointerSample pointer;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeHudBindingsNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeHudBindingsNativeValidation.Finish(null);
    }
    void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Exercise()
    {
        var assets=OfflineHomeHudAssets.LoadDefault();Require(assets&&assets.framePrefab&&assets.homePrefab&&assets.defaultFont,"explicit resource catalog resolves");
        var dictionary=JsonUtility.FromJson<LanguageData>(assets.texts.text).texts.ToDictionary(x=>x.key,x=>x.text);
        dictionary["Fixture.Title.1"]="完成家园绑定验证";dictionary["Fixture.Condition"]="验证进度";
        camera=new GameObject("MutedUiCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();camera.targetTexture=target;
        var world=new GameObject("ControlledWorldCamera",typeof(Camera)).GetComponent<Camera>();world.tag="MainCamera";world.transform.position=new Vector3(0,0,-10);world.orthographic=true;world.orthographicSize=5;world.targetTexture=target;world.cullingMask=0;world.depth=-1;
        var metrics=ScreenMetrics.Bind(new Metrics());var fonts=assets.CreateFontService();fonts.Initialize();
        var clip=assets.Sound(3772241779u);Require(clip,"original PCM bound");
        var taskType=FindType("HomeTaskUiNativeRunner");taskType.GetField("Clip",BindingFlags.Public|BindingFlags.Static).SetValue(null,clip);
        var language=NewNested("HomeTaskUiNativeRunner","Language",dictionary);LanguageManager.Initialize((ILocalizationTextService)language);
        var tasks=NewNested("HomeTaskUiNativeRunner","Tasks",dictionary,fonts,clip);
        var taskSound=NewNested("HomeTaskUiNativeRunner","Sound");tasks.GetType().GetField("Sound").SetValue(tasks,taskSound);
        var row=NewNested("HomeTaskUiNativeRunner","TaskRow",tasks,1L);Set(row,"Jump",true);((IList)Field<object>(tasks,"Rows")).Add(row);
        var boundTasks=new BoundTasks((IHomeTaskServices)tasks);boundTasks.Instantiate=(template,parent)=>view.InstantiateTaskItem(template,parent);
        var download=NewNested("HomeHudInfrastructureNativeRunner","DownloadServices");Set(download,"Enabled",false);Set(download,"State",PaydState.Downloaded);
        var match=NewNested("HomeSocialUiNativeRunner","MatchServices",dictionary);
        var result=NewNested("HomeResultUiNativeRunner","Results",dictionary);Set(result,"ActiveFight",false);
        var entries=NewNested("HomeHudInfrastructureNativeRunner","EntryServices",(object)Array.Empty<HomeRightEntryUIActivityInfo>());
        string saveRoot=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"hud-conditions-profile-"+Guid.NewGuid().ToString("N"));var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="条件验证";Require(creation.TryCreateCharacter(slot,draft,out string creationError),creationError);
        var profile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot),slot);int initialLevel=profile.Save.Player.Level;var configuration=OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        var currencyRules=Newtonsoft.Json.Linq.JObject.Parse(Zjcs.Offline.HomeCurrencyUI.OfflineHomeCurrencyAssets.LoadDefault().configuration.text);int firstTraceId=(int)currencyRules["tables"]["game_settings"]["TaskSettings"]["TaskFirstTraceConditionId"];
        var conditions=new OfflineHomeHudConditions(profile,configuration,OfficialPartnerAssetCatalog.LoadDefault().Conditions,firstTraceId,_=>false,()=>false,()=>false);
        CheckConditionNotifications(new OfflineHomeConditionPlayer(configuration,()=>profile.Save,_=>false));Require(firstTraceId==5&&!conditions.IsMet(conditions.GetCondition(firstTraceId)),"actual new character has not met selected first trace condition");
        CheckPointerDispatcher();pointer=new PointerSample();input=new OfflineHomeHudInput(pointer);
        var main=new MainServices();var tips=new Tips();var scene=new SceneServices(world);var top=new TopBarPool();
        var dragObject=new GameObject("ControlledDrag");dragObject.SetActive(false);var drag=dragObject.AddComponent<DragProbe>();drag.DragControllerManager=scene.DragControllerManager;dragObject.SetActive(true);
        var bindings=new HomeHudBindings {Camera=camera,TopBar=top,Conditions=conditions,Input=input,Windows=main,
            RightEntries=(IHomeHudRightEntryServices)entries,Download=(IHomeHudDownloadServices)download,Scene=scene,Match=(IHomeMatchServices)match,
            Results=(IHomeResultServices)result,FightRoot=(IHomeResultFightRoot)result,Tasks=boundTasks,TipDots=tips,TipController=tips,
            Localize=key=>dictionary.TryGetValue(key,out var value)?value:key,PostSound=(audio,emitter)=>{Require(audio==clip,"original button PCM");var source=emitter.GetComponent<AudioSource>();if(!source)source=emitter.AddComponent<AudioSource>();source.volume=0;source.PlayOneShot(audio);}};
        var holder=new GameObject("OriginalFrameOwner");view=new OfflineHomeHud(holder.transform,bindings,assets);
        Require(!view.Root.activeSelf&&!view.Home.gameObject.activeSelf,"entire hierarchy remains inactive until all dependencies are supplied");
        var show=view.Show(CancellationToken.None).AsTask();float deadline=Time.realtimeSinceStartup+5;
        while(!show.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"original Show completes");yield return null;}show.GetAwaiter().GetResult();yield return null;yield return null;
        Require(view.Home.gameObject.activeInHierarchy&&view.Home.GetComponent<CanvasGroup>().alpha==1,"original HomeUI visible after animator completion");
        Require(view.Root.GetComponent<OfflineHomeHudInputLoop>().isActiveAndEnabled,"native HUD owner activates desktop input loop");int loopClicks=0;Action<HomeUiGlobalPointerDownResult> loopListener=_=>loopClicks++;input.HookGlobalPointerDown(loopListener);pointer.Position=new Vector2(100,100);pointer.LeftDown=true;yield return null;pointer.LeftDown=false;Require(loopClicks==1,"owned Update polls the original dispatcher once per active HUD frame");input.UnhookGlobalPointerDown(loopListener);
        Require(top.Borrows==1&&top.Requested.SequenceEqual(new[]{PlayerItemType.Money,PlayerItemType.Diamon,PlayerItemType.TiliId}),"original serialized top-bar types and borrow lifecycle");
        var registry=Field<HomeConditionNotifications>(conditions,"notifications");var map=Field<Dictionary<HomeConditionRules.ConditionType,Dictionary<Action,int>>>(registry,"mConditionActionMap");Require(map.Values.Sum(value=>value.Count)==3&&Field<IDictionary>(input,"callbacks").Count==1,"original composite task types and chat condition subscribe to actual selected conditions and input dispatcher");
        var taskGate=view.Home.GetComponentInChildren<MainTaskFunctionLimit>(true);var taskSlot=Field<GameObject>(taskGate,"myTaskSlot");Require(taskSlot.activeSelf&&!conditions.IsMet(conditions.GetCondition(firstTraceId)),"original PinnedTaskPanel prefab keeps its active default even before first-trace condition");Capture("home-hud-conditions-new-character.png");
        // Arrange the original already-hidden branch; this is a fixture action,
        // not the native prefab default or a production initial-visibility policy.
        taskSlot.SetActive(false);conditions.NotifyLevelUpdate();Require(!taskSlot.activeSelf,"original condition callback leaves an already-hidden slot hidden without qualifying progress");
        Require(profile.SetStoryProgress("FirstBattle",1,2,"hud-first-battle:"+Guid.NewGuid().ToString("N")).IsApplied,"commit actual completed story count");conditions.NotifyStoryCountUpdate();yield return null;yield return null;Require(taskSlot.activeInHierarchy&&profile.Save.Player.Level==initialLevel&&conditions.IsMet(conditions.GetCondition(firstTraceId)),"actual story count event activates the hidden original slot through the alternative branch");Capture("home-hud-conditions-story.png");
        taskSlot.SetActive(false);Require(profile.SetStoryProgress("FirstBattle",0,0,"hud-clear-story:"+Guid.NewGuid().ToString("N")).IsApplied,"reset isolated story count to exercise independent level branch");conditions.NotifyStoryCountUpdate();Require(!taskSlot.activeSelf,"reset fixture story count alone does not reactivate hidden slot");
        Require(profile.ApplyInventoryTransaction("hud-level-two:"+Guid.NewGuid().ToString("N"),save=>save.Player.Level=2).IsApplied,"commit actual level satisfying original first trace condition");conditions.NotifyLevelUpdate();yield return null;yield return null;Require(taskSlot.activeInHierarchy&&conditions.IsMet(conditions.GetCondition(firstTraceId)),"actual committed level event activates original task slot");
        var task=view.Home.GetComponentInChildren<TaskListItem>(true);Require(task&&task.MyData?.TaskId==1,"native pinned task instantiated using shared dependency binding");
        var function=view.Home.GetComponentInChildren<FunctionEntrysPanel>(true);events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        Capture("home-hud-bound-visible.png");Click(function.EntryBtns[0]);Require(main.Opened==1&&main.Initialized==1,"original function pointer dispatch initializes its window port");
        var expandedField=typeof(FunctionEntrysPanelBase).GetField("mExpanded",BindingFlags.Instance|BindingFlags.NonPublic);int folds=0;var foldSubscription=function.OnFoldEndPanel.Subscribe(()=>folds++);function.RequestExpandPanel();deadline=Time.realtimeSinceStartup+3;while(!(bool)expandedField.GetValue(function)){Require(Time.realtimeSinceStartup<deadline,"original function Expand animation delivers its completion event");yield return null;}
        var entryRect=(RectTransform)function.EntryBtns[0].transform;Dispatch(RectTransformUtility.WorldToScreenPoint(camera,entryRect.TransformPoint(entryRect.rect.center)));Require((bool)expandedField.GetValue(function),"original input dispatcher preserves expanded native panel on inside click");main.Panel=true;Dispatch(Vector2.zero);Require((bool)expandedField.GetValue(function),"existing panel suppresses native outside folding");main.Panel=false;main.Dialog=true;Dispatch(Vector2.zero);Require((bool)expandedField.GetValue(function),"existing dialog suppresses native outside folding");main.Dialog=false;Capture("home-hud-input-expanded.png");Dispatch(Vector2.zero);Require(!(bool)expandedField.GetValue(function),"unblocked in-screen outside sample folds original native panel through dispatcher");deadline=Time.realtimeSinceStartup+3;while(folds==0){Require(Time.realtimeSinceStartup<deadline,"original Fold animation delivers completion event");yield return null;}Require(folds==1,"original fold event fires exactly once");foldSubscription.Dispose();
        var jump=task.GetComponentsInChildren<Button>(true).First(x=>x.gameObject.activeInHierarchy&&x.name.Contains("Jump"));Click(jump);
        Require(Field<int>(tasks,"Navigations")==1,"original task pointer dispatch reaches navigation port");
        Require(profile.ApplyInventoryTransaction("hud-level-one:"+Guid.NewGuid().ToString("N"),save=>save.Player.Level=1).IsApplied,"commit lower actual level to check original one-way task visibility");conditions.NotifyLevelUpdate();Require(!conditions.IsMet(conditions.GetCondition(firstTraceId))&&taskSlot.activeSelf,"original MainTaskFunctionLimit does not hide a previously activated slot");
        var hide=view.Hide(CancellationToken.None).AsTask();deadline=Time.realtimeSinceStartup+5;
        while(!hide.IsCompleted){Require(Time.realtimeSinceStartup<deadline,"original Hide completes");yield return null;}hide.GetAwaiter().GetResult();
        Require(!view.Home.gameObject.activeSelf&&top.Returns==1&&map.Values.All(value=>value.Count==0)&&Field<IDictionary>(input,"callbacks").Count==0,"Hide returns original bar and subscriptions");
        // Home.Hide disables the panel; the enclosing frame stays active until
        // its owner hides it. Verify root lifetime independently of panel hooks.
        view.Root.SetActive(false);input.HookGlobalPointerDown(loopListener);pointer.LeftDown=true;yield return null;pointer.LeftDown=false;Require(loopClicks==1,"hidden original HUD root stops polling desktop input");input.UnhookGlobalPointerDown(loopListener);
        Require(Field<int>(tasks,"PinObservers")==0&&Field<int>(download,"AllCounts")==0&&Field<int>(match,"Observers")==0&&Field<int>(result,"Observers")==0&&tips.LinkCount==0,"all injected source observers released");
        view.Dispose();view=null;input.Dispose();input.Dispose();conditions.Dispose();conditions.Dispose();bool disposed=false;try{conditions.GetCondition(firstTraceId);}catch(ObjectDisposedException){disposed=true;}Require(disposed,"disposed actual HUD conditions reject reuse");yield return null;((IDisposable)entries).Dispose();((IDisposable)taskSound).Dispose();scene.Dispose();top.Dispose();fonts.Dispose();metrics.Dispose();Object.Destroy(holder);Object.Destroy(world.gameObject);Object.Destroy(drag.gameObject);
        Require(errors==0,"no Unity errors throughout full HomeUI binding lifecycle");
    }
    sealed class Notification:IHomeConditionNotification
    {
        public HomeConditionRules.ConditionType Type{get;set;}public bool IsComposite{get;set;}public List<IHomeConditionNotification> Children=new List<IHomeConditionNotification>();public List<IHomeConditionNotification> GetConditions()=>Children;
    }
    static Notification Leaf(HomeConditionRules.ConditionType type)=>new Notification{Type=type};
    sealed class PointerSample:IHomePointerSnapshot
    {
        public bool LeftDown{get;set;}
        public Vector2 Position{get;set;}
        public int Width=>608;
        public int Height=>1080;
    }
    static void CheckPointerDispatcher()
    {
        var sample=new PointerSample{LeftDown=true,Position=new Vector2(100,100)};
        var failures=new List<Exception>();var diagnostics=new List<string>();var order=new List<string>();
        var module=new PointerModule(new PointerOperation[]{new GlobalPointerDownOperation(sample,failures.Add)},diagnostics.Add);
        PointerOperations operations=module;
        Action<PointerResult> first=_=>order.Add("first"),second=_=>order.Add("second");
        operations.HookOnInput(first);operations.HookOnInput(second);module.Update();Require(order.Count==0,"original dispatcher starts disabled");
        module.Enable=true;module.Update();Require(order.SequenceEqual(new[]{"second","first"}),"original global callbacks dispatch in reverse registration order");
        order.Clear();operations.HookOnInput(second);module.Update();Require(diagnostics.Count==1&&order.SequenceEqual(new[]{"second","first"}),"duplicate hook reports without adding a second callback");
        order.Clear();sample.LeftDown=false;module.Update();Require(order.Count==0,"a held or released pointer without down edge emits no event");sample.LeftDown=true;
        foreach(var position in new[]{new Vector2(-1,0),new Vector2(0,-1),new Vector2(609,0),new Vector2(0,1081)}){sample.Position=position;module.Update();}Require(order.Count==0,"original screen test rejects each exterior edge");
        foreach(var position in new[]{Vector2.zero,new Vector2(608,1080)}){sample.Position=position;module.Update();}Require(order.Count==4,"original screen test includes both corner boundaries");sample.Position=new Vector2(100,100);order.Clear();
        long typeA=operations.Block<PointerResult>(),typeB=operations.Block<PointerResult>(),layer=operations.Block(InputOperationLayer.GlobalInput);Require(typeA!=typeB,"original block signatures are unique");module.Update();operations.Unblock(typeA);operations.Unblock(layer);module.Update();Require(order.Count==0,"one outstanding type block still suppresses dispatch after layer release");operations.Unblock(typeB);module.Update();Require(order.SequenceEqual(new[]{"second","first"}),"dispatch resumes after every overlapping block is released");
        order.Clear();operations.UnhookOnInput(second);operations.UnhookOnInput(second);module.Update();Require(order.SequenceEqual(new[]{"first"}),"original lazy removal ignores repeated unhook and removes stale signature on dispatch");
        operations.UnhookOnInput(first);order.Clear();Action<PointerResult> late=_=>order.Add("late");Action<PointerResult> mutating=null;mutating=_=>{order.Add("mutating");operations.UnhookOnInput(first);operations.UnhookOnInput(mutating);operations.HookOnInput(late);};operations.HookOnInput(first);operations.HookOnInput(mutating);module.Update();Require(order.SequenceEqual(new[]{"mutating"}),"original dispatch sees removal immediately and defers newly appended callback until next frame");order.Clear();module.Update();Require(order.SequenceEqual(new[]{"late"}),"only appended live callback runs on following frame");operations.UnhookOnInput(late);
        order.Clear();operations.HookOnInput(first);operations.HookOnInput<PointerResult>(_=>throw new InvalidOperationException("expected input callback failure"));module.Update();Require(failures.Count==1&&order.SequenceEqual(new[]{"first"}),"original operation catches each callback exception and continues reverse dispatch");module.Enable=false;
        using(var adapter=new OfflineHomeHudInput(sample,failures.Add,diagnostics.Add))
        {
            int calls=0;Action<HomeUiGlobalPointerDownResult> callback=value=>{Require(value.ScreenPos==sample.Position,"adapter preserves original screen coordinate");calls++;};adapter.HookGlobalPointerDown(callback);adapter.HookGlobalPointerDown(callback);adapter.Update();Require(calls==1&&diagnostics.Count==2,"HUD callback identity maps to one original input signature");long blocked=adapter.BlockGlobalPointerDown();adapter.Update();Require(calls==1,"HUD original type block suppresses callback");adapter.Unblock(blocked);adapter.Update();Require(calls==2,"HUD input resumes after original block release");adapter.UnhookGlobalPointerDown(callback);adapter.Update();Require(calls==2,"HUD callback unsubscribe releases mapped original signature");adapter.HookGlobalPointerDown(callback);adapter.Dispose();adapter.UnhookGlobalPointerDown(callback);bool disposed=false;try{adapter.Update();}catch(ObjectDisposedException){disposed=true;}Require(disposed,"disposed HUD input rejects updates and accepts late unsubscription");
        }
        Debug.Log("HOME_POINTER_DISPATCHER_PASSED: original reverse dispatch, duplicate identity, inclusive bounds, overlapping layer/type blocks, lazy removal, live callback mutation, exception isolation and owned HUD input lifetime.");
    }
    static void CheckConditionNotifications(IHomeConditionPlayer player)
    {
        var exceptions=new List<Exception>();var registry=new HomeConditionNotifications(player,exceptions.Add);var level=Leaf(HomeConditionRules.ConditionType.Level);var story=Leaf(HomeConditionRules.ConditionType.StoryCount);
        var nested=new Notification{Type=HomeConditionRules.ConditionType.CompositeAnd,IsComposite=true,Children=new List<IHomeConditionNotification>{level,story}};
        var composite=new Notification{Type=HomeConditionRules.ConditionType.CompositeOr,IsComposite=true,Children=new List<IHomeConditionNotification>{level,nested,Leaf(HomeConditionRules.ConditionType.Level)}};
        int calls=0;Action callback=()=>calls++;registry.Register(composite,callback);registry.Register(composite,callback);registry.NotifyLevelUpdate();registry.NotifyStoryCountUpdate();Require(calls==2,"original nested expansion deduplicates types and duplicate registrations preserve one callback per event");
        registry.Deregister(composite,callback);registry.NotifyLevelUpdate();Require(calls==3,"original registration reference count survives one deregistration");registry.Deregister(composite,callback);registry.NotifyLevelUpdate();registry.NotifyStoryCountUpdate();Require(calls==3,"last deregistration releases all expanded condition types");
        registry.Register(Leaf(HomeConditionRules.ConditionType.None),callback);registry.NotifyItemCountUpdate();Require(calls==3,"original excluded None condition never adds a callback");
        var order=new List<string>();Action late=()=>order.Add("late"),second=()=>order.Add("second");Action first=null;first=()=>{order.Add("first");registry.Register(level,late);registry.Deregister(level,first);registry.Deregister(level,second);};registry.Register(level,first);registry.Register(level,second);registry.NotifyLevelUpdate();Require(order.SequenceEqual(new[]{"first","second"}),"original callback snapshot completes while add/remove operations wait");order.Clear();registry.NotifyLevelUpdate();Require(order.SequenceEqual(new[]{"late"}),"original pending additions then removals govern the next notification");registry.Deregister(level,late);
        order.Clear();Action failing=()=>throw new InvalidOperationException("expected condition callback failure");registry.Register(level,failing);registry.Register(level,()=>order.Add("level"));registry.Register(Leaf(HomeConditionRules.ConditionType.LevelBetween),()=>order.Add("between"));registry.Register(Leaf(HomeConditionRules.ConditionType.MapRegionIsNotLimit),()=>order.Add("region"));registry.NotifyLevelUpdate();Require(exceptions.Count==1&&order.SequenceEqual(new[]{"level","between","region"}),"original exception isolation continues through ordered level condition fan-out");registry.Destroy();order.Clear();registry.NotifyLevelUpdate();registry.Deregister(null,null);Require(order.Count==0,"destroyed original registry releases callbacks and accepts empty late deregistration");
        Debug.Log("HOME_CONDITION_NOTIFICATIONS_PASSED: selected nested expansion/type deduplication, reference counts, callback snapshot, deferred add/remove order, exception isolation, original level fan-out and destruction.");
    }
    void Click(Button button)
    {
        Require(button&&button.isActiveAndEnabled&&button.interactable,"native button receives pointer");Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));var data=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(button.transform),"native button is first pointer hit: "+button.name+" first="+(hits.Count>0?hits[0].gameObject.name:"none"));
        Dispatch(point);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Dispatch(Vector2 point){pointer.Position=point;pointer.LeftDown=true;input.Update();pointer.LeftDown=false;}
    void Capture(string name){Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static Type FindType(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType(name)).First(x=>x!=null);
    // Reuse the already compiled module fixtures as controlled backends. Their
    // business values are deliberately separate from production profile state.
    static object NewNested(string owner,string name,params object[] args)=>Activator.CreateInstance(FindType(owner).GetNestedType(name,BindingFlags.NonPublic),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
    static T Field<T>(object value,string name){var type=value.GetType();var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);return (T)(field!=null?field.GetValue(value):type.GetProperty(name).GetValue(value));}
    static void Set(object value,string name,object data)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(value,data);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    [Serializable] sealed class LanguageData{public LanguageRow[] texts;}[Serializable] sealed class LanguageRow{public string key,text;}
    sealed class TopBarPool:ITopBarManager,IDisposable
    {
        readonly GameObject owner=new GameObject("ControlledTopBarPool");public int Borrows,Returns;public List<PlayerItemType> Requested;
        public TopBarPool(){owner.SetActive(false);}
        public TopBarItem BorrowTopBar(List<CurrencyItemData> data){Borrows++;Requested=data.Select(x=>x.Type).ToList();var result=Object.Instantiate(OfficialHomeTopBarAssetCatalog.LoadDefault().topBarPrefab,owner.transform,false).GetComponent<TopBarItem>();result.gameObject.SetActive(true);return result;}
        public void ReturnTopBar(TopBarItem value){Returns++;value.gameObject.SetActive(false);Object.Destroy(value.gameObject);}
        public void UpdateTopBar(TopBarItem value,List<CurrencyItemData> data)=>throw new NotSupportedException();public void Dispose()=>Object.Destroy(owner);
    }
    sealed class MainServices:IHomeMainUiWindows,IHomeMainFunctionEntryDialog
    {
        public bool Panel,Dialog;public bool HasPanel=>Panel;public bool HasDialog=>Dialog;public int Opened,Initialized;public void ReportFunctionEntryClick(){}public void PopupFunctionEntryDialog(Action<IHomeMainFunctionEntryDialog> initialize){Opened++;initialize(this);}public void Init(){Initialized++;}
    }
    sealed class Tips:IProfessionTipDotSystem,IProfessionTipDotSystemController
    {
        readonly Dictionary<TipDotNodeKey,HashSet<Action<TipDotStateInfo>>> links=new Dictionary<TipDotNodeKey,HashSet<Action<TipDotStateInfo>>>();
        public bool IsShowTipDot=>true;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();public TipDotGraphType? GetGraphType(string name)=>TipDotGraphType.Static;
        public bool LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> changed){if(!links.TryGetValue(key,out var values))links[key]=values=new HashSet<Action<TipDotStateInfo>>();Require(values.Add(changed),"one tip link");changed(default);return true;}
        public bool UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> changed){Require(links.TryGetValue(key,out var values)&&values.Remove(changed),"exact tip link released");return true;}public int LinkCount=>links.Values.Sum(x=>x.Count);
    }
    public sealed class DragProbe:Zjcs.Official.World.NativeCamera.DragControllerBase{protected override void LateUpdate(){}}
    sealed class Furniture:IHomeDirectionFurniture{public Transform transform{get;}public Object UnityIdentity=>transform;public int TipLevel=>0;public Furniture(float x){transform=new GameObject("ControlledFurniturePosition").transform;transform.position=new Vector3(x,0,0);}}
    sealed class SceneServices:IHomeSceneUiServices,IHomeDirectionCameraServices,IHomeDirectionFocusController,IDisposable
    {
        readonly Camera world;readonly Furniture bed=new Furniture(-12),cart=new Furniture(12);Action<HangUpTipLevelChangeData> tips;Action<SetHomeSceneUIActiveData> active;
        public SceneServices(Camera camera){world=camera;}public IHomeDirectionCameraServices Camera=>this;public IDragControllerManager DragControllerManager{get;}=new DragControllerManager();public float DirectionGuideFocusDuration=>.2f;public IHomeDirectionFurniture GetFurniture(Zjcs.Official.HomeUI.EFurniture type)=>type==Zjcs.Official.HomeUI.EFurniture.Bed?bed:cart;
        public IDisposable SubscribeHangUpTips(Action<HangUpTipLevelChangeData> callback){tips+=callback;return new Subscription(()=>tips-=callback);}public IDisposable SubscribeUiActive(Action<SetHomeSceneUIActiveData> callback){active+=callback;return new Subscription(()=>active-=callback);}
        public UnityEvent OnHomeNpcRefresh{get;}=new UnityEvent();public List<IHomeDirectionNpcData> HomeNpcDatas{get;}=new List<IHomeDirectionNpcData>();public int AwardRetrieveHomeNpcId=>1;public Transform GetSpawnPoint(int index)=>throw new NotSupportedException();public HomeDirectionNpcInfo GetNpcInfo(int id)=>throw new NotSupportedException();public Zjcs.Official.HomeFunctionMenuUI.IHomeMenuPrefabHandle LoadPrefab(string path)=>throw new NotSupportedException();public GameObject InstantiatePrefab(GameObject prefab,Transform slot)=>throw new NotSupportedException();public void FreezeSkeletonGraphic(GameObject instance)=>throw new NotSupportedException();public Zjcs.Official.HomeSceneUiControls.HomeViewMode HomeViewMode=>default;
        public UnityEvent<Vector3> ViewPosUpdated{get;}=new UnityEvent<Vector3>();public Object ControlBrainIdentity=>world;public Vector3 CameraFollowPosition=>new Vector3(world.transform.position.x,0,0);public Vector3 GetViewPos()=>CameraFollowPosition;public IHomeDirectionFocusController GetHomeController()=>this;public void SetFocusPositionXForce(float x){world.transform.position=new Vector3(x,0,-10);ViewPosUpdated.Invoke(GetViewPos());}
        public void Dispose(){Object.Destroy(bed.transform.gameObject);Object.Destroy(cart.transform.gameObject);}
    }
    sealed class Subscription:IDisposable{Action dispose;public Subscription(Action action){dispose=action;}public void Dispose(){var callback=dispose;dispose=null;callback?.Invoke();}}
    sealed class BoundTasks:IHomeTaskServices
    {
        readonly IHomeTaskServices inner;public Func<GameObject,Transform,TaskListItem> Instantiate;public BoundTasks(IHomeTaskServices inner){this.inner=inner;}
        public IHomeTaskPlayer Player=>inner.Player;public UnityEvent OnTeamUpdate=>inner.OnTeamUpdate;public IHomeTaskConditions Conditions=>inner.Conditions;public IMapInputSupportHandlerProxy mMapInput=>inner.mMapInput;public INavigateManager NavigateManager=>inner.NavigateManager;public ITaskHandler TaskHandler=>inner.TaskHandler;public IItemIconManager ItemIconManager=>inner.ItemIconManager;public IMyMapPositionHandlerProxy MyMapPositionHandler=>inner.MyMapPositionHandler;public IHomeTaskGlobalColors GlobalColors=>inner.GlobalColors;public IHomeTaskCombatColors CombatRatingConfig=>inner.CombatRatingConfig;public object PlanesRank=>inner.PlanesRank;
        public DateTime GetDateTime()=>inner.GetDateTime();public double CalcRemainSecond(DateTime endTime)=>inner.CalcRemainSecond(endTime);public DateTime GetDailyResetTimeFromDate(DateTime time)=>inner.GetDailyResetTimeFromDate(time);public MapRegionInfo GetMapRegion(int map,int region)=>inner.GetMapRegion(map,region);public int CalcRankSuppression(SubRank a,SubRank b)=>inner.CalcRankSuppression(a,b);public int GetCombatRatingLevel(long rating,IReadOnlyList<long> values)=>inner.GetCombatRatingLevel(rating,values);public bool TryCheckTaskLimit(int id,IHomeTaskPlayer player,out TaskLimitResult result)=>inner.TryCheckTaskLimit(id,player,out result);public string GetTaskTitleKey(int id)=>inner.GetTaskTitleKey(id);public string GetFullName(GameObject go)=>inner.GetFullName(go);
        public TaskListItem InstantiateTaskItem(GameObject template,Transform parent)=>Instantiate(template,parent);public void OpenTaskDetail(PlayerTask task)=>inner.OpenTaskDetail(task);public void DebugCompleteTask(List<long> ids)=>inner.DebugCompleteTask(ids);public void RefreshAllTasks(bool invoke)=>inner.RefreshAllTasks(invoke);public void RefreshLimitTimeTask(long id,bool invoke)=>inner.RefreshLimitTimeTask(id,invoke);public MapRegionLimitStatusRefreshForRankUnlockTime CreateTimedRegionLimitProvider()=>inner.CreateTimedRegionLimitProvider();
    }
}
