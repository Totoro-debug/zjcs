using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14EntityAssets;

namespace Zjcs.Editor.Map14
{
    // The entity/event/container/frame adapters below are isolated fixture
    // inputs. They do not implement the formal world entity or R3 container.
    public static class OfficialMap14EntityViewAcceptance
    {
        private const string Key="Map14.EntityViewLifecycle";
        private static bool started;
        private static int errors, expectedErrors;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static readonly List<string> Trace=new List<string>();
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;
            started=false;errors=0;expectedErrors=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        public static void Run()
        {
            if(Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS")=="1")
                Debug.Log("Explicit entity-view assembly isolation: full ProjectTools.Validate is not executed in this run.");
            else
            {
                var tools=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Zjcs.Editor.ProjectTools")).First(t=>t!=null);
                tools.GetMethod("Validate",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
            }
            Require(MonoImporter.GetExecutionOrder(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Runtime/Official/EntityViews/ECViewDriver.cs"))==-1,"original driver execution order differs");
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline=null;PlayerSettings.colorSpace=ColorSpace.Linear;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Observe;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));
            QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));
            PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);
            EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("EC view lifecycle validation timed out.");EditorApplication.Exit(1);return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            started=true;Application.logMessageReceived+=Observe;
            new GameObject("Original EC view lifecycle validation").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Observe(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            if(expectedErrors>0&&text.Contains("Official playable diagnostic")){expectedErrors--;return;}
            errors++;
        }
        private static IEnumerator Verify()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0)
            {
                object next;
                try{var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}next=top.Current;if(next is IEnumerator nested&&!(next is CustomYieldInstruction)){stack.Push(nested);continue;}}
                catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}
                yield return next;
            }
            if(errors==0&&expectedErrors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original EC view lifecycle Play Mode passed: append/sort/inject/init, actual visibility and native Sprite, start/load/show/hide/pause/remove, coroutine completion/cancel, frame leases and reload. Entity/event/DI/frame providers are isolated fixture boundaries; formal entity prefab/map/save/Player remain pending.");}
            EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Original EC view: "+message);}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static void Private(ECViewDriver driver,string method,params object[] arguments)=>typeof(ECViewDriver).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(driver,arguments);
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            var camera=new GameObject("Native Sprite visibility probe camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,1,-20);camera.orthographic=true;camera.orthographicSize=2.5f;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.07f,.07f,0);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var illumination=new GameObject("Original Sprite probe illumination").AddComponent<Light2D>();illumination.lightType=Light2D.LightType.Global;illumination.intensity=1;
            var renderTarget=new RenderTexture(450,450,24);renderTarget.Create();camera.targetTexture=renderTarget;
            var readback=new Texture2D(450,450,TextureFormat.RGBA32,false);
            var catalog=Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog");
            using(var library=new NativeMap14EntitySpriteLibrary(catalog))
            {
                var owner=new GameObject("Explicit isolated EC entity input");owner.SetActive(false);
                var entity=owner.AddComponent<EntityInput>();var services=new Services();
                var child=new GameObject("Original EC driver scope");child.transform.SetParent(owner.transform,false);
                var driver=child.AddComponent<ECViewDriver>();
                driver.ServiceProvider=services;driver.ClientVisibilitySupporterFactory=new MapClientVisibilitySupporterFactory{MapRootManagerProxy=services};
                var model=new GameObject("Native original Sprite visibility probe");model.transform.SetParent(child.transform,false);
                var renderer=model.AddComponent<SpriteRenderer>();
                renderer.sprite=library.Require(catalog.sprites.Single(s=>s.originalName=="building_statue_doushi_TaoYuan").key);
                renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Official/World/Map/14/NativeEntityMaterials/08c41716e96c66ddcf71bd56dd72f6e2/1419505037369228759.mat");
                var normal=new ECVisibleControlerNormalView();Set(normal,"mActivedChildren",new[]{model});Set(normal,"mEnabledChildren",Array.Empty<Behaviour>());
                var a=new ViewProbe("a",10);var b=new ViewProbe("b",20);var c=new ViewProbe("c",30);var d=new ViewProbe("d",40);
                b.Appended=a;a.Appended=c;a.Multiple=new IECView[]{d,null};
                Set(driver,"mECViews",new List<IECView>{b,normal});Trace.Clear();owner.SetActive(true);
                Require(Trace.Where(t=>t.StartsWith("inject:")).SequenceEqual(new[]{"inject:a","inject:b","inject:c","inject:d","inject:normal"}),"append traversal/sort/injection order changed");
                Require(driver.GetView<ViewProbe>()==a&&a.AppendReads==2&&b.AppendReads==2,"first matching view or append evaluation count changed");
                Require(entity.Receivers==4&&entity.Visibility==driver.ECViewClientVisibilitySupporter,"registration/event forwarding differs");
                var visibility=driver.ECViewClientVisibilitySupporter;
                Require(!visibility.IsVisible&&visibility.NeedStop(),"registered-before-start visibility differs");
                Trace.Clear();entity.PublishStart(true);
                Require(Trace.SequenceEqual(new[]{"load:a:False","load:b:False","load:c:False","load:d:False","show:a","show:b","show:c","show:d"}),"start/load/static/post-show order differs");
                Require(visibility.IsVisible&&!visibility.NeedStop()&&services.Leases==8,"start visibility or per-view frame subscriptions differ");
                yield return null;yield return null;
                Require(a.Updates>0&&a.LateUpdates>0&&a.ReadClassId==6650&&a.ReadId.EntityId==101&&a.ReadValue=="explicit fixture component","base view entity/loop consumption differs");
                Require(ReadPixels(camera,renderTarget,readback)>100,"original native Sprite is not visible");
                File.WriteAllBytes(Path.Combine(Output,"entity-view-visible.png"),readback.EncodeToPNG());
                Trace.Clear();Require(visibility.SetEntityVisible(1,false)&&!model.activeSelf&&!visibility.IsVisible,"first hide did not deactivate model");
                Require(!visibility.SetEntityVisible(2,false)&&!visibility.SetEntityVisible(1,true)&&!model.activeSelf,"independent hide keys were lost");
                Require(ReadPixels(camera,renderTarget,readback)==0,"hidden model still renders");
                visibility.SetForceVisible(true);Require(model.activeSelf&&visibility.IsVisible,"forced show differs");
                Require(!visibility.SetEntityVisible(3,false)&&model.activeSelf,"forced show failed to retain pending hide");
                visibility.SetForceVisible(false);Require(!model.activeSelf,"pending hide did not resume after force release");
                Require(!visibility.SetEntityVisible(2,true)&&visibility.SetEntityVisible(3,true)&&model.activeSelf,"pending hide-key removal differs");
                Require(Trace.SequenceEqual(new[]{"hide:a","hide:b","hide:c","hide:d","show:a","show:b","show:c","show:d","hide:a","hide:b","hide:c","hide:d","show:a","show:b","show:c","show:d"}),"visibility callback order changed");
                Private(driver,"OnApplicationPause",true);Require(Trace.TakeLast(4).SequenceEqual(new[]{"pause:a:True","pause:b:True","pause:c:True","pause:d:True"}),"pause forwarding differs");
                int completed=0;var bridge=(IECViewBridge)driver;long routine=bridge.StartCoroutine(Routine(()=>completed++));
                Require(routine!=0,"pending coroutine signature is zero");
                for(int i=0;i<6&&completed==0;i++)yield return null;
                Require(completed==1&&CoroutineCount(driver)==0,"natural coroutine completion cleanup differs");
                var cancelled=new RoutineProbe();routine=bridge.StartCoroutine(cancelled);yield return null;
                int cancelledMoves=cancelled.Moves;bridge.StopCoroutine(routine);yield return null;yield return null;
                Require(cancelled.Moves==cancelledMoves&&cancelled.Disposals==0&&CoroutineCount(driver)==0,"explicit coroutine cancellation differs");
                var removedA=new RoutineProbe();var removedB=new RoutineProbe();
                bridge.StartCoroutine(removedA);bridge.StartCoroutine(removedB);yield return null;
                int movesA=removedA.Moves,movesB=removedB.Moves;
                Trace.Clear();entity.Remove();
                Require(Trace.Take(8).SequenceEqual(new[]{"hide:a","hide:b","hide:c","hide:d","unload:a","unload:b","unload:c","unload:d"}),"remove-pre/hide/unload order differs");
                Require(entity.Receivers==0&&services.Leases==0&&CoroutineCount(driver)==0&&!visibility.IsVisible,"unregister left receivers/frames/coroutines");
                yield return null;yield return null;
                Require(removedA.Moves==movesA&&removedB.Moves==movesB&&removedA.Disposals==0&&removedB.Disposals==0,"remove did not cancel all pending executions");
                int updates=a.Updates;yield return null;Require(a.Updates==updates,"unloaded view still receives updates");
                expectedErrors++;Require(bridge.StartCoroutine(Routine(()=>completed++))==0,"unregistered coroutine was accepted");Require(expectedErrors==0,"unregistered diagnostic did not execute");
                child.SetActive(false);child.SetActive(true);entity.PublishStart(false);
                Require(a.IsStatic&&entity.Receivers==4&&visibility.IsVisible&&services.Leases==8,"re-enable/static reload differs");
                services.Destroying=true;Require(!visibility.SetEntityVisible(9,false)&&model.activeSelf&&!visibility.IsVisible&&!visibility.NeedStop(),"destroying map incorrectly mutates view nodes or requests stop");
                services.Destroying=false;visibility.SetEntityVisible(9,true);
                var token=bridge.DestroyCancellationToken;UnityEngine.Object.Destroy(child);yield return null;
                Require(token.IsCancellationRequested&&entity.Receivers==0&&CoroutineCountSafe(driver)==0&&services.Leases==8,"destroy/unregister boundary differs");
                // Original OnDestroy unregisters; unload belongs to RemoveEvent.
                // Stop the isolated frame source explicitly after this branch.
                services.Dispose();UnityEngine.Object.Destroy(owner);
                var constant=new ConstantClientVisibilitySupporterFactory().Create(null,null,new GameObject("Constant visibility scope").transform);
                Require(!constant.SetEntityVisible(1,false)&&!constant.NeedStop(),"constant supporter overrides differ");constant.SetForceVisible(true);
            }
            UnityEngine.Object.Destroy(readback);renderTarget.Release();UnityEngine.Object.Destroy(renderTarget);
            Require(errors==0,"unexpected execution log errors");
        }
        private static int ReadPixels(Camera camera,RenderTexture target,Texture2D image)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=previous;return image.GetPixels32().Count(c=>c.a>0);
        }
        private static int CoroutineCount(ECViewDriver driver)=>((Dictionary<long,System.Threading.CancellationTokenSource>)typeof(ECViewDriver).GetField("mViewCoroutines",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(driver)).Count;
        private static int CoroutineCountSafe(ECViewDriver driver)=>CoroutineCount(driver);
        private static IEnumerator Routine(Action done){yield return null;yield return null;done();}
        private sealed class RoutineProbe:IEnumerator,IDisposable
        {public int Moves,Disposals;public object Current=>null;public bool MoveNext(){Moves++;return true;}public void Reset()=>throw new NotSupportedException();public void Dispose()=>Disposals++;}
        private sealed class Runner:MonoBehaviour { }
        private sealed class DataInput:ECViewEntityData
        {public override int ClassId=>6650;public override EcEntityIdentifier Id=>new EcEntityIdentifier(14,101);public override T GetComponent<T>()=>(T)(object)"explicit fixture component";}
        private sealed class EntityInput:ECViewEntityBinding
        {
            private readonly DataInput data=new DataInput();
            private readonly List<IMyEntityStartEventReceiver> starts=new List<IMyEntityStartEventReceiver>();
            private readonly List<IMyEntityRemoveEventReceiver> removes=new List<IMyEntityRemoveEventReceiver>();
            private readonly List<IMyEntityStartPostEventReceiver> posts=new List<IMyEntityStartPostEventReceiver>();
            private readonly List<IMyEntityRemovePreEventReceiver> pres=new List<IMyEntityRemovePreEventReceiver>();
            public IClientVisibilitySupporter Visibility;
            public int Receivers=>starts.Count+removes.Count+posts.Count+pres.Count;
            public override ECViewEntityData Entity=>data;
            public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value)=>Visibility=value;
            public override void AddStartReceiver(IMyEntityStartEventReceiver value)=>starts.Add(value);
            public override void RemoveStartReceiver(IMyEntityStartEventReceiver value)=>starts.Remove(value);
            public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver value)=>removes.Add(value);
            public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value)=>removes.Remove(value);
            public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver value)=>posts.Add(value);
            public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value)=>posts.Remove(value);
            public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value)=>pres.Add(value);
            public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value)=>pres.Remove(value);
            public void PublishStart(bool fresh){foreach(var v in starts.ToArray())v.MyEntity_OnStart(this,fresh);foreach(var v in posts.ToArray())v.MyEntity_OnStartPost(this);}
            public void Remove(){foreach(var v in pres.ToArray())v.MyEntity_RemovePre(this);foreach(var v in removes.ToArray())v.MyEntity_OnRemove(this);}
        }
        private sealed class ViewProbe:ECViewBase,IUpdatable,ILateUpdatable
        {
            public readonly string Name;private readonly int order;
            public IECView Appended;public IECView[] Multiple;public int AppendReads,Updates,LateUpdates;
            public int ReadClassId=>mClassId;public EcEntityIdentifier ReadId=>mEntityIdentifier;public string ReadValue=>GetComponent<string>();public bool IsStatic=>mIsStatic;
            public ViewProbe(string name,int order){Name=name;this.order=order;}
            protected override int mOrder=>order;
            protected override IECView mAppend{get{AppendReads++;return Appended;}}
            protected override IEnumerable<IECView> mAppendMulti=>Multiple;
            protected override void Load(){Require(mOwner&&mServiceProvider!=null,"view Init did not bind before Load");Trace.Add("load:"+Name+":"+mIsStatic);}
            protected override void OnShow()=>Trace.Add("show:"+Name);
            protected override void OnHide()=>Trace.Add("hide:"+Name);
            protected override void Unload()=>Trace.Add("unload:"+Name);
            protected override void OnApplicationPause(bool pause)=>Trace.Add("pause:"+Name+":"+pause);
            public void Update()=>Updates++;public void LateUpdate()=>LateUpdates++;
        }
        private sealed class Services:IServiceProvider,IECViewInjection,IECViewFrameSource,IECViewMapRootLifetime,IDisposable
        {
            private readonly List<FrameLease> leases=new List<FrameLease>();public bool Destroying{get;set;}public int Leases=>leases.Count(l=>l.Active);
            public object GetService(Type type)=>type==typeof(IECViewFrameSource)?this:null;
            public void Inject(object value)=>Trace.Add("inject:"+(value is ViewProbe probe?probe.Name:"normal"));
            public IDisposable SubscribeUpdate(Action<ECViewFrameUnit> callback)=>Subscribe(callback,PlayerLoopTiming.Update);
            public IDisposable SubscribePreLateUpdate(Action<ECViewFrameUnit> callback)=>Subscribe(callback,PlayerLoopTiming.PreLateUpdate);
            private IDisposable Subscribe(Action<ECViewFrameUnit> callback,PlayerLoopTiming timing){var lease=new FrameLease(callback);leases.Add(lease);PlayerLoopHelper.AddAction(timing,lease);return lease;}
            public void Dispose(){foreach(var lease in leases)lease.Dispose();}
        }
        private sealed class FrameLease:IDisposable,IPlayerLoopItem
        {private readonly Action<ECViewFrameUnit> callback;public bool Active=true;public FrameLease(Action<ECViewFrameUnit> callback){this.callback=callback;}public bool MoveNext(){if(!Active)return false;callback(default);return Active;}public void Dispose()=>Active=false;}
    }
}
