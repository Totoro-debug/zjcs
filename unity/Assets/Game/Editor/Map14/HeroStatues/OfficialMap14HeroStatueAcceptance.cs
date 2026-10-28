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
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.MapDisplays;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.World.NativeMap14EntityAssets;
using Zjcs.Offline.World.NativeMap14HeroStatues;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Pos2d = Zjcs.Official.World.NativeTerrain.Common.Pos2d;
using Direction = Zjcs.Official.World.NativeTerrain.Common.Direction;
using DisplayGround = Zjcs.Official.World.Map13Entities.GroundType;
using DisplayInfoParser = Zjcs.Official.World.Map13Entities.EntityMapDisplayInfoParser;
using Visibility = Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14HeroStatueAcceptance
    {
        private const string Key="Map14.HeroStatue";
        private static bool started; private static int errors;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        [InitializeOnLoadMethod] private static void Hook(){if(!SessionState.GetBool(Key,false))return;started=false;errors=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;}
        public static void Run()
        {
            if(Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS")=="1")Debug.Log("Explicit entity assembly isolation; full ProjectTools.Validate is not executed.");
            else{var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Zjcs.Editor.ProjectTools")).First(x=>x!=null);t.GetMethod("Validate").Invoke(null,null);}
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");QualitySettings.renderPipeline=null;PlayerSettings.colorSpace=ColorSpace.Linear;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
        }
        private static void Mode(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Observe;GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));}
        private static void Tick(){if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Original statue validation timed out.");EditorApplication.Exit(1);return;}if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;new GameObject("Original statue validation").AddComponent<Runner>().StartCoroutine(Verify());}
        private static void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static IEnumerator Verify()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Steps());while(stack.Count>0){object value;try{var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}value=top.Current;if(value is IEnumerator nested&&!(value is CustomYieldInstruction)){stack.Push(nested);continue;}}catch(Exception e){var messages=new List<string>();for(var error=e;error!=null;error=error.InnerException)messages.Add(error.GetType().FullName+": "+error.Message+"\n"+string.Join("\n",(new System.Diagnostics.StackTrace(error,false).GetFrames()??Array.Empty<System.Diagnostics.StackFrame>()).Select(frame=>frame.GetMethod()?.DeclaringType?.FullName+"."+frame.GetMethod()?.Name)));Debug.LogError(string.Join("\n",messages));EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original four hero-statue display prefabs Play Mode passed: native hierarchy/managed references/renderers/animators, original bind-pre selection and dynamic acquisition, FIFO reuse, low-memory loan retention/release/reload, real driver lifecycle, tip-dot ownership and three window requests/state effects. Original shared DLL grid/stage queries and original emitted events drive the display; fixture host/visibility/tip/window services remain isolated. Formal map input, UI workflow, save and Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Original statues: "+message);}
        private static MyPlayable StatePlayable(ECPlayableWorldStageHandler value)=>(MyPlayable)typeof(ECPlayableWorldStageHandler).GetField("mStatePlayable",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
        private static IEnumerator Frames(MyPlayableCenter center,float duration){float t=0;while(t<duration){yield return null;t+=Time.deltaTime;center.Update(Time.deltaTime);center.LateUpdate();}}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            var camera=new GameObject("Original statue camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=8;camera.aspect=450f/800;camera.transform.position=new Vector3(0,4,-100);camera.backgroundColor=new Color(.08f,.08f,.08f,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var light=new GameObject("Original Sprite light").AddComponent<Light2D>();light.lightType=Light2D.LightType.Global;
            var scope=new GameObject("Original statue playback scope");var staging=new GameObject("Inactive dependency injection");staging.SetActive(false);var center=new MyPlayableCenter();
            var factory=new Factory(center,staging.transform);var manager=new MyPlayableManager{ScopeRoot=scope,InstanceFactory=factory,MyPlayableCenter=center,LowMemory=new Zjcs.Official.StoryPlayables.GAction(),ActivePublisher=_=>{},InactivePublisher=_=>{}};
            var oldResolve=StoryPlayableInstantiation.Resolve;StoryPlayableInstantiation.Resolve=t=>t.IsChildOf(scope.transform)?factory:null;manager.Initialize();manager.OnActivate();
            var oldQueries=EntityTipDotServices.Queries;var oldWindow=WorldStageOperationServices.WindowHost;
            var services=new Services{Manager=manager};EntityTipDotServices.Queries=services;WorldStageOperationServices.WindowHost=services;
            var catalog=Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog");
            var sharedCatalog=Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using(var shared=new NativeMap14SharedAssemblies(sharedCatalog))
            using(var entityUtilities=new NativeMap14SharedEntityUtilities(shared))
            using(var sprites=new NativeMap14EntitySpriteLibrary(catalog))
            {
                var displayCatalog=Resources.Load<NativeMap14DisplayCatalog>("Map14/Native/map-display-catalog");Require(displayCatalog&&displayCatalog.selectors.Length==1253&&displayCatalog.displays.Length==4,"original display catalog row/address coverage differs");services.Catalog=displayCatalog;services.Root=scope.transform;
                services.DisplayFactory=new NativeMap14DisplayInstantiation(staging.transform,services,new MapClientVisibilitySupporterFactory{MapRootManagerProxy=services},sprites,services.InjectMono);
                var memory=new Zjcs.Official.StoryPlayables.GAction();var displayManager=new MapDisplayManager{MapRootManagerProxy=services,ResourceLoader=displayCatalog,ServiceProvider=services,Queries=services,LowMemory=memory};displayManager.Initialize();
                var sharedPrefabs=new NativeMap14SharedPrefabs(shared,sharedCatalog);var sharedHost=SharedFixtureHost(shared);
                var entities=new List<EntityInput>();var drivers=new List<ECViewDriver>();
                for(int i=0;i<4;i++)
                {
                    int classId=6570+i;var path="Assets/Official/World/Map/14/NativeHeroStatues/Prefabs/Map/Map_14/EntityDisplays/WorldGamePlay/"+classId+"_14_view.prefab";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"native display did not import: "+classId);
                    Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"native display has missing scripts");
                    var source=new GameObject("Isolated original statue entity "+classId);source.SetActive(false);source.transform.position=new Vector3((i%2==0?-1:1)*2.1f,i<2?5:1,0);source.AddComponent<Zjcs.Official.EntityLifecycle.MyEntity>();var entity=source.AddComponent<EntityInput>();entity.Data=new DataInput(SharedFixtureEntity(shared,sharedPrefabs,sharedHost,classId,i),shared);services.Entities.Add(entity.Data);entities.Add(entity);
                    var selector=source.AddComponent<MyMapDisplay>();typeof(MyMapDisplay).GetField("mMyEntity",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(selector,entity);selector.MapRootManagerProxy=services;selector.MapDisplayManager=displayManager;selector.MyMapPositionHandler=services;selector.Queries=services;
                    source.AddComponent<Zjcs.Official.EntityLifecycle.MyGridTransform>();var lifecycle=source.AddComponent<LifecycleProbe>();var visibility=source.AddComponent<Zjcs.Official.EntityRootVisibility.MyVisibilityGrid>();visibility.EntityClientVisibilityHandlerFactory=services;Require(!visibility.IsVisible,"original root visible before start");
                    source.SetActive(true);entity.BindPre();var driver=source.GetComponentInChildren<ECViewDriver>(true);Require(driver,"original bind-pre did not acquire a display");drivers.Add(driver);var display=driver.gameObject;
                    var binding=display.GetComponent<NativeMap14HeroStatueSprites>();Require(binding&&binding.bindings.Length==4,"original Sprite reference count differs");Require(display.GetComponentsInChildren<SpriteRenderer>(true).All(r=>r.sprite&&r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported)),"original renderer has missing Sprite/material/shader");
                    Require(driver.GetView<ECWorldStageView>()!=null&&driver.GetView<ECAnimatorView>()!=null&&driver.GetView<ECAttachAgentView>().Configs.Values.Count==9&&driver.GetView<ECTipdotLinkerView>()!=null&&driver.GetView<ECPlayableSupporterView>().GetPlayableHandler<ECPlayableWorldStageHandler>()!=null,"original managed-reference types/fields failed to deserialize");
                    entity.StartEntity();VerifySharedStage(entity.Data);
                    lifecycle.Expect("pre,bind,bind-event,component-start,start,post");Require(entity.Owner.myStarted&&!entity.Owner.myNewEntity&&entity.Owner.Components.Count==3&&entity.Data.Grid.ReceiverCount==2,"original component cache/start/grid subscription differs");
                    entity.Owner.ImmediateToEnd();entity.Owner.PrepareReplace();entity.Owner.Replace(entity.Data.Id);lifecycle.Expect("immediate,prepare,replace");
                    var expectedPosition=entity.Data.Grid.GridToPosition(entity.Data.Grid.Pos);expectedPosition.z=ZSetUtils.XYToZ(expectedPosition.x,expectedPosition.y);Require(source.transform.localPosition==expectedPosition,"original root grid transform/Z did not initialize");
                    Require(driver.GetView<ECAnimatorView>().AnimationController.myAnimator.runtimeAnimatorController&&driver.GetView<ECWorldStageView>().State==0,"original Animator/state not loaded");
                    var gridView=driver.GetView<ECGridTransformView>();var rootGrid=source.GetComponent<Zjcs.Official.EntityLifecycle.MyGridTransform>();var beforeGrid=entity.Data.Grid.Pos;var beforeView=source.transform.localPosition;int rootMoves=0,viewMoves=0;
                    Action<Zjcs.Official.EntityLifecycle.MyGridTransform,Pos2d> rootMoved=(value,previous)=>{Require(previous.Equals(beforeGrid)&&value.Pos.Equals(new Pos2d(beforeGrid.X+1,beforeGrid.Y)),"original root grid event arguments differ");rootMoves++;};
                    Action<ECGridTransformView,Pos2d> viewMoved=(value,previous)=>{Require(previous.Equals(beforeGrid),"original display grid previous position differs");viewMoves++;};rootGrid.OnPosUpdated+=rootMoved;gridView.OnPosUpdated+=viewMoved;
                    NativeMap14SharedPrefabs.Set(entity.Data.Grid.Original,"Pos",Activator.CreateInstance(shared.CommonType("Pos2d"),beforeGrid.X+1,beforeGrid.Y));Require(rootMoves==1&&viewMoves==1&&source.transform.localPosition==beforeView,"original shared event did not reach both views or changed the source no-immediate-move behavior");
                    rootGrid.OnPosUpdated-=rootMoved;gridView.OnPosUpdated-=viewMoved;NativeMap14SharedPrefabs.Set(entity.Data.Grid.Original,"Pos",Activator.CreateInstance(shared.CommonType("Pos2d"),beforeGrid.X,beforeGrid.Y));
                    Require(entity.Data.Grid.PositionToGrid(entity.Data.Grid.GridToPosition(beforeGrid)).Equals(beforeGrid)&&entity.Data.Grid.Info.GetBodyRange().Count==2,"original coordinate conversion/body differs");
                    yield return Frames(center,.15f);
                    var state=driver.GetView<ECWorldStageView>();var cfg=state.GetCurStateCfg();Require(cfg.myLoopPlayable&&cfg.myLoopPlayable.name=="herostatue_active_playable"&&cfg.InteractionOperation.InteractionOperation is ECWorldStageInteractionOperationWindow,"state zero native effect/window differs");
                    Require(cfg.InteractionOperation.InteractionOperation.DoOper(services,entity.Data.Stage)&&services.LastWindow=="WorldMap/WorldTouchDialogWithPreview"&&services.InputHooks==0,"state zero window/hook request differs");
                }
                Require(services.VisibilityHandlers.Count==4,"original root handlers not created");
                foreach(var handler in services.VisibilityHandlers.ToArray())handler.Set(false);
                Require(entities.All(e=>!e.Owner.Visibility.IsVisible&&!e.Owner.ClientVisibilitySupporter.IsVisible),"original root hide did not reach native display");foreach(var driver in drivers)VerifyModelVisibility(driver,false);yield return Frames(center,.15f);Capture(camera,"hero-statues-native-hidden.png",false);
                foreach(var handler in services.VisibilityHandlers.ToArray())handler.Set(true);
                Require(entities.All(e=>e.Owner.Visibility.IsVisible&&e.Owner.ClientVisibilitySupporter.IsVisible),"original root show did not reach native display");foreach(var driver in drivers)VerifyModelVisibility(driver,true);
                foreach(var entity in entities)entity.Data.VisibilityData.RemoveClientVisibilityType(Visibility.ClientVisibilityType.NPCVisibility);
                Require(services.VisibilityHandlers.Count==0&&services.VisibilityReturns==4,"original root request rebuild did not return handlers");
                foreach(var entity in entities)entity.Data.VisibilityData.AddClientVisibilityRequest(Visibility.ClientVisibilityType.NPCVisibility);
                Require(services.VisibilityHandlers.Count==4&&entities.All(e=>e.Owner.Visibility.IsVisible),"original root request rebuild did not restore handlers");
                memory.SafeInvoke();Require(drivers.All(d=>d&&d.gameObject.activeInHierarchy)&&services.Created==4,"low-memory cleanup removed loaned original displays");
                Require(services.Linked.Count==4,"original tip-dot nodes were not registered");
                foreach(var callback in services.Linked.Values.ToArray())callback(new TipDotStateInfo{TipLevel=ETipDotLevelType.Important,Data="101"});
                foreach(var d in drivers){var linker=d.GetComponentInChildren<MapEntityTipDotNodeLinker>(true);Require(linker.TipLevelState.State==ETipDotLevelType.Important,"original tip state did not switch");}
                yield return Frames(center,.5f);Capture(camera,"hero-statues-native-loop.png");
                foreach(var entity in entities)SharedUpdateState(shared,entity.Data,1);
                foreach(var d in drivers){var cfg=d.GetView<ECWorldStageView>().GetCurStateCfg();Require(cfg.myStartPlayable&&cfg.myStartPlayable.name=="herostatue_disappear_playable"&&!cfg.myLoopPlayable,"state one native effect differs");Require(cfg.InteractionOperation.InteractionOperation.DoOper(services,entities[drivers.IndexOf(d)].Data.Stage)&&services.LastWindow=="WorldMap/WorldStageDialog","state one window differs");}
                yield return Frames(center,.35f);Capture(camera,"hero-statues-native-disappear.png");
                foreach(var entity in entities)SharedUpdateState(shared,entity.Data,2);
                foreach(var d in drivers){var cfg=d.GetView<ECWorldStageView>().GetCurStateCfg();Require(cfg.myLoopPlayable&&cfg.InteractionOperation.InteractionOperation.DoOper(services,entities[drivers.IndexOf(d)].Data.Stage)&&services.LastWindow=="WorldMap/WorldTouchDialogPeriodicityInteraction","state two window/loop differs");}
                yield return Frames(center,.3f);
                services.Show=false;services.OnTipDotShowChanged?.Invoke();foreach(var d in drivers)Require(!d.transform.Find("UI/DotSlot/MapSceneCommonTipdotLinker/Content").gameObject.activeSelf,"original tip visibility did not hide");
                VerifyOwnership(services,entities[0].Data);
                foreach(var entity in entities){entity.RemoveEntity();entity.GetComponent<LifecycleProbe>().Expect("end,component-remove,remove-pre,remove");Require(!entity.Owner.myStarted&&entity.Owner.Entity==null&&entity.Data.Grid.ReceiverCount==0&&!entity.Owner.Visibility.IsVisible,"original pool reset/end retained entity or grid receiver");}Require(services.Linked.Count==0&&services.Buffered.Count==0&&services.VisibilityHandlers.Count==0,"original unload leaked tip/buffer subscriptions");
                yield return Frames(center,.1f);
                for(int i=0;i<entities.Count;i++){entities[i].BindPre();Require(entities[i].GetComponentInChildren<ECViewDriver>()==drivers[i],"original FIFO display was not reused");entities[i].StartEntity();entities[i].GetComponent<LifecycleProbe>().Expect("pre,bind,bind-event,component-start,start,post");}
                Require(services.Created==4,"original pool instantiated during reuse");foreach(var entity in entities)entity.RemoveEntity();yield return Frames(center,.1f);
                memory.SafeInvoke();yield return null;Require(drivers.All(d=>!d),"idle original display pools were not destroyed");
                entities[0].BindPre();entities[0].StartEntity();Require(services.Created==5&&entities[0].GetComponentInChildren<ECViewDriver>(),"original display did not reload after low-memory release");entities[0].RemoveEntity();yield return Frames(center,.1f);displayManager.Dispose();yield return null;
                manager.RemoveAllPlayable(true);
                foreach(var entity in entities){entity.Data.Shared.Dispose();SharedCall(entity.Data.Shared.Original,"End",new[]{shared.CommonType("MySerializable")},new object[]{null});NativeMap14SharedPrefabs.Call(entity.Data.Shared.Original,"Destroy");UnityEngine.Object.Destroy(entity.gameObject);}
                yield return null;
            }
            manager.RemoveAllPlayable(true);manager.OnDeactivate();manager.Dispose();center.RemoveAll();StoryPlayableInstantiation.Resolve=oldResolve;EntityTipDotServices.Queries=oldQueries;WorldStageOperationServices.WindowHost=oldWindow;
            UnityEngine.Object.Destroy(scope);UnityEngine.Object.Destroy(staging);UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(light.gameObject);yield return null;Require(services.OnTipDotShowChanged==null&&errors==0,"unexpected errors or tip-dot enable subscription leak");
        }
        private static void VerifyOwnership(Services services,DataInput data)
        {
            var source=data.Tip;source.Value=null;Require(EntityTipDotOwnership.EntityShowTipForMe(source),"null ownership");source.Value=new EntityOwnershipData{OwnerId=0,Type=EntityOwnershipType.Player};Require(EntityTipDotOwnership.EntityShowTipForMe(source),"nonpositive owner");source.Value.OwnerId=services.MainPlayerId;Require(EntityTipDotOwnership.EntityShowTipForMe(source),"self owner");source.Value.OwnerId=services.MainPlayerId+1;services.Unique=true;Require(!EntityTipDotOwnership.EntityShowTipForMe(source),"foreign unique owner");services.Unique=false;Require(EntityTipDotOwnership.EntityShowTipForMe(source),"foreign shared owner");source.Value.Type=EntityOwnershipType.Entity;services.Unique=true;Require(EntityTipDotOwnership.EntityShowTipForMe(source),"entity ownership");
        }
        private static void VerifyModelVisibility(ECViewDriver driver,bool visible){var view=driver.GetView<ECVisibleControlerNormalView>();if(view==null){Require(driver.transform.childCount>0&&driver.transform.Cast<Transform>().All(t=>t.gameObject.activeSelf==visible),"original default child visibility targets did not switch");return;}var nodes=(GameObject[])typeof(ECVisibleControlerNormalView).GetField("mActivedChildren",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);var components=(Behaviour[])typeof(ECVisibleControlerNormalView).GetField("mEnabledChildren",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);Require(nodes.Length>0&&nodes.Where(n=>n).All(n=>n.activeSelf==visible)&&(components==null||components.Where(c=>c).All(c=>c.enabled==visible)),"original configured visibility targets did not switch");}
        private static void Capture(Camera camera,string name,bool requireContent=true){var rt=new RenderTexture(450,800,24);rt.Create();var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(450,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();var pixels=image.GetPixels32();var bg=pixels[0];Require(!requireContent||pixels.Count(c=>Math.Abs(c.r-bg.r)>2||Math.Abs(c.g-bg.g)>2||Math.Abs(c.b-bg.b)>2)>500,"native display frame is empty");File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.Destroy(image);rt.Release();UnityEngine.Object.Destroy(rt);}
        private sealed class DataInput:ECViewEntityData,IMapDisplayEntityState
        {
            public readonly NativeMap14SharedViewEntity Shared;
            public readonly Visibility.Map13VisibilityData VisibilityData=new Visibility.Map13VisibilityData{ClientVisibilityType=Visibility.ClientVisibilityType.NPCVisibility};
            public NativeMap14SharedViewEntity.StageData Stage=>Shared.Stage;public NativeMap14SharedViewEntity.GridData Grid=>Shared.Grid;
            public readonly TipSource Tip;
            public DataInput(object original,NativeMap14SharedAssemblies assemblies){Shared=new NativeMap14SharedViewEntity(assemblies,original);Tip=new TipSource{Owner=this};}
            public bool Removing=>(bool)NativeMap14SharedPrefabs.Get(Shared.Original,"Removing");public Pos2d GridPosition=>Grid.Pos;public string EntityTag=>"";
            public override int ClassId=>Shared.ClassId;public override EcEntityIdentifier Id=>Shared.Id;
            public override T GetComponent<T>(){object value=typeof(T)==typeof(ECEntityTipDotSourceData)?Tip:typeof(T)==typeof(Visibility.Map13VisibilityData)?VisibilityData:null;return value!=null?(T)value:Shared.GetComponent<T>();}
        }
        private sealed class TipSource:ECEntityTipDotSourceData{public DataInput Owner;public EntityOwnershipData Value;public override ECViewEntityData Entity=>Owner;public override EntityOwnershipData Ownership=>Value;}
        // Only the fixture supplies root/caches because global Parsers are not
        // initialized here. Original entity/component methods remain unchanged.
        private static object SharedFixtureHost(NativeMap14SharedAssemblies shared)
        {
            Func<string,object> create=name=>Activator.CreateInstance(shared.CommonType(name));
            var container=create("EcContainer");var root=create("EcEntity");var ground=create("GridGroundComponent");var info=create("GridGroundComponentInfo");
            NativeMap14SharedPrefabs.Set(info,"Size",Activator.CreateInstance(shared.CommonType("Pos2d"),16,16));NativeMap14SharedPrefabs.Set(info,"OriginPos",new Vector3(-5,-2,0));NativeMap14SharedPrefabs.Call(info,"Init");NativeMap14SharedPrefabs.Set(ground,"_Info",info);
            SharedCall(root,"AddComponent",new[]{shared.CommonType("EcComponent")},ground);container.GetType().GetField("mRootEntity",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(container,root);NativeMap14SharedPrefabs.Set(root,"Container",container);NativeMap14SharedPrefabs.Call(root,"Awake");return container;
        }
        private static object SharedFixtureEntity(NativeMap14SharedAssemblies shared,NativeMap14SharedPrefabs prefabs,object container,int classId,int index)
        {
            var entity=prefabs.CreateClientPrefab(classId);NativeMap14SharedPrefabs.Set(entity,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14L,classId));NativeMap14SharedPrefabs.Set(entity,"Container",container);
            var grid=SharedCall(entity,"GetComponent",new[]{typeof(Type)},shared.CommonType("GridTransformComponent"));var root=container.GetType().GetField("mRootEntity",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(container);var ground=SharedCall(root,"GetComponent",new[]{typeof(Type)},shared.CommonType("GridGroundComponent"));grid.GetType().GetField("mGridGround",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(grid,ground);
            NativeMap14SharedPrefabs.Set(grid,"Pos",Activator.CreateInstance(shared.CommonType("Pos2d"),index%2==0?3:7,index<2?7:3));NativeMap14SharedPrefabs.Call(entity,"Awake");NativeMap14SharedPrefabs.Call(entity,"Start");return entity;
        }
        private static void SharedUpdateState(NativeMap14SharedAssemblies shared,DataInput data,int state){SharedCall(data.Stage.Original,"UpdateState",new[]{typeof(int),shared.CommonType("MySerializable")},state,null);VerifySharedStage(data);}
        private static void VerifySharedStage(DataInput data)
        {
            var stage=data.Stage;var info=stage.GetCurStateInfo();var sub=info.SubStateList.Single();Require(stage.IsActive()&&sub.Behaviour.CompType==(stage.State==1?9:2)&&sub.Behaviour.CompIndex==0,"original stage config/activity did not reach display DTO");
            Require(stage.GetStateComponentIndex((StageComponentType)(stage.State==1?9:2))==0&&stage.GetStateComponentIndex(StageComponentType.None)==-1,"original stage index/None query differs");
            Require(stage.GetStateComponentIndex((StageComponentType)110)==(stage.State==2?0:-1)&&sub.Conditions.Count==(stage.State==2?1:0),"original periodicity condition query differs");
        }
        private static object SharedCall(object owner,string name,Type[] types,params object[] values)=>owner.GetType().GetMethod(name,types).Invoke(owner,values);
        private sealed class LifecycleProbe:Zjcs.Official.EntityLifecycle.MyComponent,
            Zjcs.Official.EntityLifecycle.IMyEntityBindPreEventReceiver,Zjcs.Official.EntityLifecycle.IMyEntityBindEventReceiver,
            Zjcs.Official.EntityLifecycle.IMyEntityStartEventReceiver,Zjcs.Official.EntityLifecycle.IMyEntityStartPostEventReceiver,
            Zjcs.Official.EntityLifecycle.IMyEntityRemovePreEventReceiver,Zjcs.Official.EntityLifecycle.IMyEntityRemoveEventReceiver,
            Zjcs.Official.EntityLifecycle.IMyEntityPrepareReplaceEventReceiver,Zjcs.Official.EntityLifecycle.IMyEntityReplaceEventReceiver
        {
            private readonly List<string> trace=new List<string>();
            private void Awake(){myEntity.BindPreEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityBindPreEventReceiver)this;myEntity.BindEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityBindEventReceiver)this;myEntity.StartEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityStartEventReceiver)this;myEntity.StartPostEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityStartPostEventReceiver)this;myEntity.RemovePreEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityRemovePreEventReceiver)this;myEntity.RemoveEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityRemoveEventReceiver)this;myEntity.PrepareReplaceEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityPrepareReplaceEventReceiver)this;myEntity.ReplaceEvent+=(Zjcs.Official.EntityLifecycle.IMyEntityReplaceEventReceiver)this;}
            public void Expect(string expected){Require(string.Join(",",trace)==expected,"native lifecycle sequence: "+string.Join(",",trace));trace.Clear();}
            public override void Bind(){Require(myEntity.Entity!=null,"component bind preceded entity assignment");trace.Add("bind");}
            public override void OnStart(bool isNew){Require(myEntity.myStarted,"component start preceded started flag");trace.Add("component-start");}
            public override void OnEnd(object value)=>trace.Add("end");public override void OnRemove(object value)=>trace.Add("component-remove");public override void OnImmediateToEnd()=>trace.Add("immediate");
            public void MyEntity_OnBindPre(ECViewEntityData value){Require(myEntity.Entity==null&&value!=null,"bind-pre followed entity assignment/reset failed");trace.Add("pre");}
            public void MyEntity_OnBind()=>trace.Add("bind-event");
            public void MyEntity_OnStart(Zjcs.Official.EntityLifecycle.MyEntity value,bool isNew)=>trace.Add("start");public void MyEntity_OnStartPost(Zjcs.Official.EntityLifecycle.MyEntity value)=>trace.Add("post");
            public void MyEntity_RemovePre(Zjcs.Official.EntityLifecycle.MyEntity value)=>trace.Add("remove-pre");public void MyEntity_OnRemove(Zjcs.Official.EntityLifecycle.MyEntity value)=>trace.Add("remove");
            public void MyEntity_OnPrepareReplace(Zjcs.Official.EntityLifecycle.MyEntity value)=>trace.Add("prepare");public void MyEntity_OnReplace(Zjcs.Official.EntityLifecycle.MyEntity value,EcEntityIdentifier previous)=>trace.Add("replace");
        }
        private sealed class EntityInput:Zjcs.Offline.World.NativeMap14EntityLifecycle.NativeMyEntityBinding
        {
            public DataInput Data;
            public void BindPre()=>Owner.Bind(null,Data,false);
            public void StartEntity()=>Owner.MyStart();
            public void RemoveEntity(){Owner.End(null);Owner.Remove(null);Owner.OnGameObjectPoolReset();}
        }
        private sealed class Services:IServiceProvider,IECViewInjection,IECViewMapRootLifetime,IEcEntityBufferEventHandleModule,IEntityTipDotQueries,IProfessionTipDotSystem,IProfessionTipDotSystemController,IWorldStageWindowMapRoot,IWorldStageWindowHost,IMapDisplayRoot,IMapDisplayQueries,IMapDisplayPosition,IMapDisplayInstantiation,Visibility.IEntityClientVisibilityHandlerFactory
        {
            public readonly List<VisibilityHandler> VisibilityHandlers=new List<VisibilityHandler>();public int VisibilityReturns;
            public void GetHandlers(Visibility.ClientVisibilityType flags,ref List<Visibility.IClientVisibilityHandler> handlers){Require(flags==Visibility.ClientVisibilityType.NPCVisibility,"unexpected isolated visibility flags");var handler=new VisibilityHandler();VisibilityHandlers.Add(handler);handlers.Add(handler);}
            public Visibility.IClientVisibilityHandler GetHandler(Visibility.ClientVisibilityType flags)=>throw new InvalidOperationException();
            public void ReturnHanlder(Visibility.IClientVisibilityHandler handler){Require(VisibilityHandlers.Remove((VisibilityHandler)handler),"original handler returned twice");VisibilityReturns++;}
            public MyPlayableManager Manager;public readonly List<DataInput> Entities=new List<DataInput>();public readonly Dictionary<TipDotNodeKey,Action<TipDotStateInfo>> Linked=new Dictionary<TipDotNodeKey,Action<TipDotStateInfo>>();public readonly Dictionary<ECViewEntityData,Action<EntityWorldStageUpdateEventMessage>> Buffered=new Dictionary<ECViewEntityData,Action<EntityWorldStageUpdateEventMessage>>();
            public bool Show=true,Unique;public int InputHooks;public string LastWindow;public bool Destroying=>false;public bool Initialized=>true;public bool IsLeader=>true;public long MainPlayerId=>123;public bool IsShowTipDot=>Show;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}public SafeAction OnTipDotSystemInited{get;set;}public SafeAction OnTipDotSystemPreClear{get;set;}public TipDotGraphType? GetGraphType(string name)=>null;
            public NativeMap14DisplayCatalog Catalog;public NativeMap14DisplayInstantiation DisplayFactory;public Transform Root;public int Created;public Transform MapRoot=>Root;public int CurrentMapClassId=>14;public DisplayGround GetGridGroundType(Pos2d p)=>DisplayGround.Wall;public DisplayInfoParser Selector=>Catalog.Selector;public int GetMapGroupId(int id)=>1;public string GetAreaTag(int id,int area)=>"isolated source area";public bool IsMapForbidLowMemoryDeal()=>false;public int QueryArea(Pos2d p)=>1;public GameObject Instantiate(GameObject prefab){Created++;return DisplayFactory.Instantiate(prefab);}public void InjectMono(MonoBehaviour value){Inject(value);if(value is MapEntityTipDotNodeLinker linker){linker.Controller=this;linker.TipDotSystem=this;}}
            public object GetService(Type t)=>t==typeof(IECViewMapRootLifetime)||t==typeof(IWorldStageWindowMapRoot)?this:throw new InvalidOperationException("Unexpected original service query: "+t);
            public void Inject(object value){if(value is ECWorldStageView stage)stage.EcEntityBufferEventHandleModule=this;if(value is ECPlayableSupporterView p){p.MyPlayableManager=Manager;p.ServiceProvider=this;}}
            public IDisposable Subscribe<T>(ECViewEntityData e,Action<T> callback) where T:IEcEntityBufferEventContext{var typed=(Action<EntityWorldStageUpdateEventMessage>)(object)callback;Buffered.Add(e,typed);var subscription=((DataInput)e).Stage.Subscribe(typed);return new Lease(()=>{subscription.Dispose();Buffered.Remove(e);});}
            public bool LinkNodeStateChange(TipDotNodeKey key,GameObject obj,Action<TipDotStateInfo> callback){Linked.Add(key,callback);return true;}public bool UnlinkNodeStateChange(TipDotNodeKey key,GameObject obj,Action<TipDotStateInfo> callback)=>Linked.Remove(key);
            public ECViewEntityData GetEntity(EcEntityIdentifier id)=>Entities.FirstOrDefault(e=>e.Id==id);public int GetContainerClassId(ECViewEntityData e)=>14;public Pos2d GetPosition(ECViewEntityData e)=>new Pos2d(e.ClassId,1);public string GetEntityTag(ECViewEntityData e)=>"";public EntityTipDotExtendInfo GetExtendInfo(int id)=>new EntityTipDotExtendInfo{InteractionUnique=Unique};public EntityTipDotMapInfo GetMapInfo(int id)=>new EntityTipDotMapInfo{MapGroupId=1};public string GetReddotGraphName(ECViewEntityData e,int g,int m,string t)=>"isolated original graph consumption";
            public IDisposable SubscribeHookMapInput(EcEntityIdentifier id){InputHooks++;return new Lease(()=>InputHooks--);}public UniTask<StagePopupWindowOperation> Popup(string name){LastWindow=name;return UniTask.FromResult<StagePopupWindowOperation>(null);}public UniTask<StagePopupWindowOperation> PopupAndReport(string n,SDKEventPanelId id,SDKEventPanelSource source)=>throw new InvalidOperationException("Original statue rows do not report panels.");public WorldStageUiContext GetUiContext(ECWorldStageStateData s)=>throw new InvalidOperationException("Formal UI is outside this source-fixture validation.");
        }
        private sealed class VisibilityHandler:Visibility.IClientVisibilityHandler
        {
            public Visibility.ClientVisibilityType VisibilityType=>Visibility.ClientVisibilityType.NPCVisibility;public bool Visible{get;private set;}=true;public ECViewEntityData Entity{get;private set;}public event Action<bool> OnVisibleChanged;
            public void Init(ECViewEntityData entity,bool isNew){Entity=entity;}public void Release(){Require(OnVisibleChanged==null,"original handler released with subscriber");Entity=null;}public void Set(bool visible){Visible=visible;OnVisibleChanged?.Invoke(visible);}
        }
        private sealed class Lease:IDisposable{private Action action;public Lease(Action a){action=a;}public void Dispose(){var a=action;action=null;a?.Invoke();}}
        private sealed class Factory:IStoryPlayableInstanceFactory{private readonly MyPlayableCenter center;private readonly Transform staging;public Factory(MyPlayableCenter c,Transform t){center=c;staging=t;}public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays){var obj=UnityEngine.Object.Instantiate(prefab,staging,false);foreach(var p in obj.GetComponentsInChildren<MyPlayable>(true))p.MyPlayableCenter=center;obj.transform.SetParent(parent,worldPositionStays);return obj;}}
        private sealed class Runner:MonoBehaviour{}
    }
}
