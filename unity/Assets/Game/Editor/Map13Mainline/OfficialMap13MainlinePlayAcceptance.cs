using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Editor.Combat;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;
using Zjcs.Presentation.World.NativeMap13;
using Zjcs.Presentation.World.NativeMap12;
using Zjcs.Official.World;
using Zjcs.Offline.World.NativeMap13TreeEntities;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13Mainline
{
    [InitializeOnLoad]
    public static class OfficialMap13MainlinePlayAcceptance
    {
        private const string Key="Map13.FormalNativeEntry";
        private static bool started,failed;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidDataException("Explicit validation directory required.");
        private static int MapId=>SessionState.GetInt(Key+".map",13);
        static OfficialMap13MainlinePlayAcceptance()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);
                string pipeline=SessionState.GetString(Key+".pipeline","");
                string quality=SessionState.GetString(Key+".qualityPipeline","");
                GraphicsSettings.defaultRenderPipeline=string.IsNullOrEmpty(pipeline)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipeline);
                QualitySettings.renderPipeline=string.IsNullOrEmpty(quality)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(quality);
                string queue=SessionState.GetString(Key+".queue","");
                if(SessionState.GetInt(Key+".result",1)==0&&!string.IsNullOrEmpty(queue))
                {
                    string[] maps=queue.Split(',');string remaining=string.Join(",",maps.Skip(1));
                    EditorApplication.delayCall+=()=>
                    {
                        if(maps[0]=="15")RunMap15();else Run();
                        SessionState.SetString(Key+".queue",remaining);
                    };
                    return;
                }
                EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
            };
        }
        public static void Run()
        {
            started=false;failed=false;SessionState.SetString(Key+".queue","");
            SessionState.SetInt(Key+".map",13);
            SessionState.SetBool(Key+".trees",false);
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED","1");
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key+".qualityPipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);PlayerSettings.colorSpace=ColorSpace.Linear;
            ShaderUtil.allowAsyncCompilation=false;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
        }
        public static void RunTrees(){Run();SessionState.SetBool(Key+".trees",true);}
        public static void RunMap11()
        {
            Run();SessionState.SetInt(Key+".map",11);
            // Map11's migrated region-light adapter uses the committed built-in
            // renderer. Keep another session's temporary pipeline out of this fixture.
            GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
        }
        public static void RunMap12(){Run();SessionState.SetInt(Key+".map",12);}
        public static void RunSharedMaps(){RunMap12();SessionState.SetString(Key+".queue","15,13");}
        public static void RunMap15And13(){RunMap15();SessionState.SetString(Key+".queue","13");}
        public static void RunMap15()
        {
            Run();SessionState.SetInt(Key+".map",15);
            GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
        }
        public static void RunRules()
        {
            try
            {
                var data=JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map13/stories").text);
                Require(data.puzzleRules.Length==2690,"Original puzzle placement closure is incomplete.");
                Require(data.puzzleRules.Count(r=>r.TerminalPuzzleType==6)==6,"Original step-grid terminal count differs.");
                Require(data.puzzleRules.Count(r=>r.TerminalPuzzleType==2)==5,"Original road-switch terminal count differs.");
                Require(data.puzzleRules.Single(r=>r.PlacementId==101).InitialState==1,"Original entry state was lost.");
                var lockRule=data.puzzleRules.Single(r=>r.PlacementId==10901);
                Require(lockRule.LockConditions.Single().PlacementId==10903&&lockRule.LockConditions.Single().State==1
                    &&lockRule.ConditionEntityInactiveInUnlock,"Original numeric dictionary lock was lost.");
                var session=new OfflineMapPuzzleSession(data.puzzleRules,Array.Empty<MapPuzzleEntityState>());
                session.RefreshLocks();
                var snapshot=session.Save();
                var restored=new OfflineMapPuzzleSession(data.puzzleRules,snapshot);
                Require(JsonUtility.ToJson(new SavedPuzzles{states=snapshot})==JsonUtility.ToJson(new SavedPuzzles{states=restored.Save()}),
                    "All original puzzle states failed their local cold restore.");
                File.WriteAllText(Path.Combine(Output,"map"+MapId+"-formal-puzzle-rules.json"),JsonUtility.ToJson(new SavedPuzzles{states=snapshot},true));
                Debug.Log("Map13 original puzzle rules passed: 2690 placements, 6 step-grid / 5 road-switch terminals, original numeric locks and full local state restore.");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] private sealed class SavedPuzzles{public MapPuzzleEntityState[] states;}
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Map13 formal entry verification timed out.");EditorApplication.Exit(1);return;}
            if(EditorApplication.isPlaying)EditorApplication.QueuePlayerLoopUpdate();
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            started=true;new GameObject("Map13 formal entry verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived+=Observe;var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0)
            {
                object next;
                try{var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}next=current.Current;if(next is IEnumerator nested&&!(next is CustomYieldInstruction)){stack.Push(nested);continue;}}
                catch(Exception e){Debug.LogException(e);Application.logMessageReceived-=Observe;EditorApplication.isPlaying=false;yield break;}
                yield return next;
            }
            if(!failed)SessionState.SetInt(Key+".result",0);
            Application.logMessageReceived-=Observe;EditorApplication.isPlaying=false;
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true;}
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
        private static object Call(object value,string name,params object[] arguments)=>value.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,arguments);
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;BattleGameViewInput.Focus();yield return new WaitForSecondsRealtime(.5f);
            if(EventSystem.current==null)new GameObject("Map13 simulated pointer",typeof(EventSystem));
            var canvas=new GameObject("Map13 formal HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            string saves=Path.Combine(Output,"formal-map13-profile-"+Guid.NewGuid().ToString("N"));
            var slots=new ProfileSlotCatalog(saves);string slot=slots.CreateSlot();
            var save=new OfflineMainlineSave{CreatedAtUnixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds()};
            save.Player.CharacterCreated=true;save.Player.CharacterName="地图"+MapId+"验证";save.Player.InitialWeaponType=201;
            var characterCatalog=OfficialCharacterAssetCatalog.LoadDefault().Load();
            save.Player.Level=90;save.Player.Experience=characterCatalog.CumulativeLevelExperience[90];
            save.Player.SubRank=characterCatalog.LevelLimits.Where(r=>r.PlayerBlessLevel==0&&r.PlayerLevel>=90).Min(r=>r.SubRank);
            save.Player.Rank=characterCatalog.RankOfSubRank(save.Player.SubRank);save.Player.Wardrobe.Profession=1;
            save.Player.UnlockedMapClassIds.AddRange(new[]{11,12,13,15});
            save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks.Where(t=>t.classId<=1040).Select(t=>t.classId));
            File.WriteAllText(slots.GetProgressPath(slot),JsonUtility.ToJson(save,true));
            var progression=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
            var initializedWardrobe=new OfflineCharacterProgressionService(progression,characterCatalog).InitializeWardrobe("map"+MapId+"-formal-entry-wardrobe");
            Require(initializedWardrobe.Status==ProgressionResultStatus.Applied,"Isolated character fixture must use consistent official rank/subrank/level values.");
            var runtime=Open(slot,saves,canvas.transform);
            float deadline=Time.realtimeSinceStartup+120;
            if(MapId==13)
            {
                var world=Field<OfficialMap13NativeWorldView>(runtime,"nativeMap13World");
                while(!world.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
                Require(world.IsReady,"Formal Map13 original partitions did not become ready.");
            }
            if(MapId==12)
            {
                var world=Field<OfficialMap12NativeWorldView>(runtime,"nativeMap12World");
                while(!world.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
                Require(world.IsReady,"Formal Map12 original partitions did not become ready.");
            }
            var navigation=Field<object>(runtime,"navigation");var initial=Field<MapGridPoint>(navigation,"currentGrid");
            if(MapId==13)Require((int)Call(runtime,"GetDefaultEntryId")==11001&&initial.Equals(new MapGridPoint(113,233)),"Original TbMapWorldInfo.DefEntryId 11001 and its (113,233) placement were not used.");
            if(MapId==11)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(167,133)),"Original Map11 DefEntryId 101 and its (167,133) placement were not used.");
            if(MapId==12)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(248,32)),"Original Map12 entry 101 at (248,32) was not used.");
            if(MapId==15)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(12,6)),"Original Map15 entry 101 at (12,6) was not used.");
            Require(Field<object>(runtime,"mapGridVisibility")!=null,"Formal original grid component is not bound.");
            yield return new WaitForSecondsRealtime(2);Canvas.ForceUpdateCanvases();
            if(MapId==15)
            {
                var originals=runtime.GetComponentsInChildren<MainlineGroundAnimation>(true);
                Require(originals.Length==10&&originals.All(value=>value.VertexCount>0),"Map15 lost original animated ground geometry.");
                var animated=originals.Where(value=>!string.IsNullOrEmpty(value.AnimationName)&&value.GetComponent<MeshRenderer>().isVisible).ToArray();
                Require(animated.Length>0&&animated.All(value=>value.Elapsed>0),"Visible original ground animation clocks did not advance.");
                var poses=animated.Select(value=>value.GetComponent<MeshFilter>().sharedMesh.vertices).ToArray();
                var colors=animated.Select(value=>value.GetComponent<MeshFilter>().sharedMesh.colors32).ToArray();
                yield return new WaitForSecondsRealtime(.35f);
                Require(animated.Where((value,index)=>!poses[index].SequenceEqual(value.GetComponent<MeshFilter>().sharedMesh.vertices)
                    ||!colors[index].SequenceEqual(value.GetComponent<MeshFilter>().sharedMesh.colors32)).Any(),"Visible original ground animation did not change its rendered geometry/colors.");
                var tiles=runtime.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
                int renderedCells=0;
                foreach(var tile in tiles)foreach(var pos in tile.cellBounds.allPositionsWithin)
                    if(tile.HasTile(pos)&&tile.GetSprite(pos))renderedCells++;
                Require(renderedCells>0,"Original ground tiles lost their serialized sprite references.");
                Debug.Log("Map15 original ground: "+originals.Length+" animated instances, "+renderedCells+" cells with bound tile sprites.");
            }
            int initialVisible=CheckFog(runtime);
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-formal-entry.png"));yield return new WaitForSecondsRealtime(.5f);
            var camera=Field<Camera>(runtime,"worldCamera");var session=Field<OfficialWorldMapSession>(runtime,"session");
            MapGridPoint target=initial;Vector2 screen=default;bool found=false;
            foreach(var offset in new[]{new MapGridPoint(1,0),new MapGridPoint(-1,0),new MapGridPoint(0,1),new MapGridPoint(0,-1),new MapGridPoint(2,0),new MapGridPoint(0,2)})
            {
                var candidate=new MapGridPoint(initial.X+offset.X,initial.Y+offset.Y);
                var path=session.FindPath(new OfficialMapPathRequest{Start=initial,End=candidate,PreciseTarget=true,BodyOffsets=new[]{new MapGridPoint(0,0)}});
                if(path==null||path.Count==0)continue;
                var position=(Vector3)Call(navigation,"GridToWorld",candidate);screen=camera.WorldToScreenPoint(position);
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
                if(hits.Count!=0||Physics2D.OverlapPoint(position)!=null)continue;
                target=candidate;found=true;break;
            }
            Require(found,"No unobstructed nearby map pointer target is available.");
            bool sent=false;runtime.PointerPressProvider=()=>{if(sent)return null;sent=true;return screen;};
            deadline=Time.realtimeSinceStartup+15;while(!Field<MapGridPoint>(navigation,"currentGrid").Equals(target)&&Time.realtimeSinceStartup<deadline)yield return null;
            runtime.PointerPressProvider=()=>null;Require(sent&&Field<MapGridPoint>(navigation,"currentGrid").Equals(target),"Simulated map pointer did not move the formal player.");
            yield return new WaitForSecondsRealtime(2);
            Require(CheckFog(runtime)>initialVisible,"Simulated movement did not reveal additional original fog cells.");
            string mapSave=Path.Combine(slots.GetSlotDirectory(slot),"map-"+MapId+"-save.json");
            var before=JsonUtility.FromJson<VisibilitySave>(File.ReadAllText(mapSave));Require(!string.IsNullOrEmpty(before.Encoded(MapId)),"Formal map save omitted original compressed visibility.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-formal-moved.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            runtime=Open(slot,saves,canvas.transform);yield return new WaitForSecondsRealtime(3);
            Require(Field<MapGridPoint>(Field<object>(runtime,"navigation"),"currentGrid").Equals(target),"Formal map position did not survive reopen.");
            var after=JsonUtility.FromJson<VisibilitySave>(File.ReadAllText(mapSave));Require(before.Encoded(MapId)==after.Encoded(MapId),"Formal visibility bytes changed on cold reopen.");
            CheckFog(runtime);
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-formal-restored.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            if(SessionState.GetBool(Key+".trees",false))yield return TreeSteps(slot,saves,canvas.transform,mapSave);
            if(MapId==11||MapId==12)yield return GoddessSteps(slot,saves,canvas.transform);
            Require(!failed,"Formal map entry emitted runtime errors.");
            File.WriteAllText(Path.Combine(Output,"map"+MapId+"-formal-entry.json"),JsonUtility.ToJson(new Proof{map_id=MapId,editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),play_mode=true,simulated_pointer=true,save_reopen=true,entry_x=initial.X,entry_y=initial.Y,moved_x=target.X,moved_y=target.Y},true));
            Debug.Log("Map"+MapId+" formal entry passed: shared map host, simulated map pointer, local grid visibility/position save and cold reopen.");
        }
        private static IEnumerator GoddessSteps(string slot,string saves,Transform ui)
        {
            var runtime=Open(slot,saves,ui);yield return new WaitForSecondsRealtime(3);
            var session=Field<OfficialWorldMapSession>(runtime,"session");
            var goddess=session.Definition.Entities.Single(e=>e.Id==(MapId==12?315:202));
            var point=new MapGridPoint(goddess.Grid[0],goddess.Grid[1]);
            var adjacent=new[]{new MapGridPoint(point.X-1,point.Y),new MapGridPoint(point.X+1,point.Y),
                new MapGridPoint(point.X,point.Y-1),new MapGridPoint(point.X,point.Y+1)}.First(session.Movement.IsFit);
            var navigation=Field<object>(runtime,"navigation");
            Call(navigation,"SetAvatarPosition",adjacent);
            // Explicit completed-placement fixture; the production progress calculator and all tested input remain live.
            var local=Field<object>(runtime,"localSave");
            local.GetType().GetField("completedEntityPlacements").SetValue(local,session.Definition.Entities
                .Where(e=>e.Region==goddess.Region&&e.TypeId!=202).Select(e=>e.Id).ToArray());
            Call(Field<object>(runtime,"regions"),"RefreshRegionProgress",true);
            Call(runtime,"RefreshEntityActivity");Call(runtime,"SaveLocalProgress");
            yield return new WaitForSecondsRealtime(2);
            var visibility=Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility");
            Require(visibility.RegionPositions(goddess.Region).Any(p=>!visibility.IsVisible(p)),"Region fixture has no undiscovered fog to unlock.");
            var camera=Field<Camera>(runtime,"worldCamera");
            Vector2 position=camera.WorldToScreenPoint((Vector3)Call(navigation,"GridToWorld",point));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            Require(hits.Count==0,"Official HUD occludes the goddess fixture.");
            bool sent=false;runtime.PointerPressProvider=()=>{if(sent)return null;sent=true;return position;};
            float deadline=Time.realtimeSinceStartup+15;
            var statues=Field<object>(runtime,"statues");
            while(Field<Zjcs.Official.WorldUI.GoddessTouchDialog>(statues,"goddessDialog")==null&&Time.realtimeSinceStartup<deadline)yield return null;
            runtime.PointerPressProvider=()=>null;
            var dialog=Field<Zjcs.Official.WorldUI.GoddessTouchDialog>(statues,"goddessDialog");
            Require(sent&&dialog&&dialog.myTouchBtn.gameObject.activeInHierarchy,"Simulated goddess selection did not open the source activation dialog.");
            yield return new WaitForSecondsRealtime(.5f);Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-goddess-ready.png"));yield return new WaitForSecondsRealtime(.5f);
            var button=dialog.myTouchBtn;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position),button=PointerEventData.InputButton.Left};
            hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Original goddess activation button is occluded.");
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Require(visibility.RegionPositions(goddess.Region).All(visibility.IsVisible),"Goddess activation did not immediately clear the original region grids.");
            yield return new WaitForSecondsRealtime(3);CheckFog(runtime);
            var progress=Field<OfflineMainlineProgressSession>(runtime,"progressionSave");
            Require(progress.Save.World.ActivatedGoddessRegions.Any(r=>r.MapClassId==MapId&&r.Region==goddess.Region),"Goddess activation was not saved.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-goddess-unlocked.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            runtime=Open(slot,saves,ui);yield return new WaitForSecondsRealtime(3);
            visibility=Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility");
            Require(visibility.RegionPositions(goddess.Region).All(visibility.IsVisible),"Activated region visibility did not survive cold reopen.");
            CheckFog(runtime);Call(runtime,"CloseMap");yield return null;yield return null;
            Debug.Log("Map"+MapId+" original goddess input passed: explicit region completion fixture, simulated map/button clicks, immediate grid unlock and cold restore.");
        }
        private static IEnumerator TreeSteps(string slot,string saves,Transform ui,string mapSave)
        {
            var source=Resources.Load<NativeMap13TreeWorldAssets>("Map13/Native/tree-world-assets");
            Require(source&&source.placements.Length==3,"Original tree world fixture source is unbound.");
            foreach(var placement in source.placements)
            {
                string json=File.ReadAllText(mapSave);
                json=Regex.Replace(json,"\"gridX\"\\s*:\\s*-?[0-9]+","\"gridX\": "+placement.position.X);
                json=Regex.Replace(json,"\"gridY\"\\s*:\\s*-?[0-9]+","\"gridY\": "+(placement.position.Y-1));
                File.WriteAllText(mapSave,json);
                var runtime=Open(slot,saves,ui);
                var world=Field<OfficialMap13NativeWorldView>(runtime,"nativeMap13World");
                float deadline=Time.realtimeSinceStartup+120;while(!world.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
                Require(world.IsReady,"Original tree region ground failed to load.");
                yield return new WaitForSecondsRealtime(4);
                var trees=Field<NativeMap13TreeWorld>(runtime,"map13Trees");
                Require(trees.Entities.Count==3,"Formal native tree placement closure differs.");
                var tree=trees.Entities[placement.placementId];
                Require(tree.gameObject.activeInHierarchy&&tree.ClientVisibilitySupporter.IsVisible,"Original source tree did not become visible in its source region.");
                Require(tree.Binding.Model.Skeletons.Single().View.VisibleTriangles>0,"Original live tree geometry is empty.");
                var skeleton = tree.Binding.Model.Skeletons.Single();
                var treeCamera = Field<Camera>(runtime,"worldCamera");
                var planes = GeometryUtility.CalculateFrustumPlanes(treeCamera);
                Require(Mathf.Abs(treeCamera.transform.position.z + 250f) < .01f,
                    "Original Map13 camera depth must cover the original XY-to-Z entity range.");
                Require(skeleton.Parts.Renderers.Any(r => r.enabled && r.gameObject.activeInHierarchy
                    && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0 && GeometryUtility.TestPlanesAABB(planes,r.bounds))
                    && skeleton.View.SkeletonColor.a > .99f,"Original source tree geometry is outside the actual map camera or faded out.");
                var countView=tree.Binding.Driver.GetView<Zjcs.Official.World.Map13EntityStateViews.ECMapAwardItemCountSwitcherView>();
                var fruit=Field<List<GameObject>>(countView,"mItemGos");
                int awardCount=checked((int)placement.awards.AwardItemInfos.Single().Count);
                Require(fruit.Count==10 && fruit.Select((item,index)=>item.activeSelf==(index<awardCount)).All(value=>value),
                    "Original placement fruit count does not match its live source award query.");
                long before=(long)Call(runtime,"StoryItemCount",115);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map13-tree-"+placement.placementId+"-before.png"));yield return new WaitForSecondsRealtime(.5f);
                var navigation=Field<object>(runtime,"navigation");var camera=Field<Camera>(runtime,"worldCamera");
                var position=(Vector3)Call(navigation,"GridToWorld",new MapGridPoint(placement.position.X,placement.position.Y));
                Vector2 pointer=camera.WorldToScreenPoint(position);
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=pointer},hits);
                Require(hits.Count==0,"Official HUD occludes the source tree input.");
                bool sent=false;runtime.PointerPressProvider=()=>{if(sent)return null;sent=true;return pointer;};
                deadline=Time.realtimeSinceStartup+30;
                while(!JsonUtility.FromJson<TreeSave>(File.ReadAllText(mapSave)).completedEntityPlacements.Contains(placement.placementId)&&Time.realtimeSinceStartup<deadline)yield return null;
                runtime.PointerPressProvider=()=>null;
                Require(sent&&JsonUtility.FromJson<TreeSave>(File.ReadAllText(mapSave)).completedEntityPlacements.Contains(placement.placementId),"Simulated map click did not complete the source tree.");
                Require((long)Call(runtime,"StoryItemCount",115)==before+placement.awards.AwardItemInfos.Single().Count,"Original placement award differs.");
                yield return new WaitForSecondsRealtime(.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map13-tree-"+placement.placementId+"-removing.png"));yield return new WaitForSecondsRealtime(.5f);
                deadline=Time.realtimeSinceStartup+10;while(tree.gameObject.activeInHierarchy&&Time.realtimeSinceStartup<deadline)yield return null;
                Require(!tree.gameObject.activeInHierarchy,"Original tree removal performance did not complete.");
                long total=(long)Call(runtime,"StoryItemCount",115);
                Call(runtime,"CloseMap");yield return null;yield return null;
                runtime=Open(slot,saves,ui);yield return new WaitForSecondsRealtime(3);
                trees=Field<NativeMap13TreeWorld>(runtime,"map13Trees");
                Require(!trees.Entities[placement.placementId].gameObject.activeInHierarchy,"Completed native tree reappeared on cold reopen.");
                Require((long)Call(runtime,"StoryItemCount",115)==total,"Cold reopen duplicated the original tree award.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map13-tree-"+placement.placementId+"-restored.png"));yield return new WaitForSecondsRealtime(.5f);
                Call(runtime,"CloseMap");yield return null;yield return null;
                Debug.Log("Map13 formal source tree passed: "+placement.placementId+", native original geometry, simulated map click, source award "+placement.awards.AwardItemInfos.Single().Count+", original removal and cold restore.");
            }
        }
        [Serializable]private sealed class TreeSave{public int[] completedEntityPlacements;}
        private static int CheckFog(OfficialMainlineMapRuntime runtime)
        {
            var visibility=Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility");
            var clouds=UnityEngine.Object.FindObjectsOfType<CloudController2>();
            if(MapId==15)
            {
                Require(clouds.Length==0&&Field<object>(runtime,"gridFog")==null,"Original Map15 must retain its explicit absent CloudController.");
                int count=0;
                for(int y=0;y<visibility.Component.Height;y++)for(int x=0;x<visibility.Component.Width;x++)
                    if(visibility.IsVisible(new Pos2d(x,y)))count++;
                Debug.Log("Formal Map15 preserved original absent fog; visible="+count);
                return count;
            }
            Require(clouds.Length==1&&Field<object>(runtime,"gridFog")!=null,"Formal map must own one original common fog instance.");
            var cloud=clouds[0];
            Require(cloud.CloudTemplate.sprite&&cloud.CloudTemplate.sprite.texture.width>0&&cloud.Cloud.sharedMaterial.shader.isSupported,
                "Original fog sprite/texture/shader binding is incomplete.");
            Require(cloud.Cloud.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"Original live fog geometry was not rendered.");
            int visible=0;
            for(int y=0;y<visibility.Component.Height;y++)for(int x=0;x<visibility.Component.Width;x++)
            {
                var p=new Pos2d(x,y);bool clear=visibility.IsVisible(p);if(clear)visible++;
                Require(cloud.Contains(p)!=clear,"Original cloud state differs from saved visibility at "+p);
            }
            Debug.Log("Formal common fog matched every original map cell; visible="+visible);
            return visible;
        }
        private static OfficialMainlineMapRuntime Open(string slot,string saves,Transform ui)
        {
            var model=CharacterCreationNativeModelFactory.CreateWorldModel(slot,saves,out string name);
            var runtime=new GameObject("Map13 formal runtime").AddComponent<OfficialMainlineMapRuntime>();
            runtime.MapMusicVolume=()=>0;runtime.MapVoiceVolume=()=>0;runtime.Begin(MapId,slot,saves,ui,model,name,null);return runtime;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        [Serializable]private sealed class VisibilitySave{public string map13GridVisibility,gridVisibility;public string Encoded(int map)=>map==13?map13GridVisibility:gridVisibility;}
        [Serializable]private sealed class Proof{public string editor,backend;public bool play_mode,simulated_pointer,save_reopen;public int map_id,entry_x,entry_y,moved_x,moved_y;}
        private sealed class Runner:MonoBehaviour{}
    }
}
