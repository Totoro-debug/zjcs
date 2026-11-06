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
using Zjcs.Presentation.World.NativeMap14;
using Zjcs.Official.World;
using Zjcs.Offline.World.NativeMap13TreeEntities;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.World.Map13MiniMap;

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
                Application.SetStackTraceLogType(LogType.Warning,(StackTraceLogType)SessionState.GetInt(Key+".warning-trace",0));
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
            SessionState.SetBool(Key+".raw-points",false);
            SessionState.SetBool(Key+".world-owner",false);
            SessionState.SetBool(Key+".hud",false);
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED","1");
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key+".qualityPipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);PlayerSettings.colorSpace=ColorSpace.Linear;
            SessionState.SetInt(Key+".warning-trace",(int)Application.GetStackTraceLogType(LogType.Warning));
            Application.SetStackTraceLogType(LogType.Warning,StackTraceLogType.None);
            ShaderUtil.allowAsyncCompilation=false;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
        }
        public static void RunTrees(){Run();SessionState.SetBool(Key+".trees",true);}
        public static void RunRawRegionPoints(){Run();SessionState.SetBool(Key+".raw-points",true);}
        public static void RunMap11()
        {
            Run();SessionState.SetInt(Key+".map",11);
            // Map11's migrated region-light adapter uses the committed built-in
            // renderer. Keep another session's temporary pipeline out of this fixture.
            GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
        }
        public static void RunMap12(){Run();SessionState.SetInt(Key+".map",12);}
        public static void RunMap14(){Run();SessionState.SetInt(Key+".map",14);}
        public static void RunMap14WorldOwner(){RunMap14();SessionState.SetBool(Key+".world-owner",true);}
        public static void RunMap14Hud(){RunMap14WorldOwner();SessionState.SetBool(Key+".hud",true);}
        public static void RunMap11Hud(){RunMap11();SessionState.SetBool(Key+".hud",true);}

        public static void RunSavedSnapshots()
        {
            foreach(int map in new[]{11,12})
            {
                SessionState.SetInt(Key+".map",map);
                string root=Directory.GetDirectories(Output,"formal-map13-profile-*")
                    .Where(path=>File.Exists(Path.Combine(path,"saves","slot-1","map-"+map+"-save.json")))
                    .OrderByDescending(Directory.GetCreationTimeUtc).First();
                VerifySnapshotFreshness(new ProfileSlotCatalog(root),"slot-1");
            }
            Debug.Log("MAINLINE_FORMAL_SAVED_SNAPSHOTS_PASS");
        }
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
            Application.logMessageReceived+=Observe;var stack=new Stack<IEnumerator>();
            stack.Push(SessionState.GetBool(Key+".raw-points",false)?RawRegionPointsSteps():Steps());
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
        private static IEnumerator RawRegionPointsSteps()
        {
            string saves=Path.Combine(Output,"raw-region-points-profile-"+Guid.NewGuid().ToString("N"));
            var slots=new ProfileSlotCatalog(saves);string slot=slots.CreateSlot();
            var profile=new OfflineMainlineSave();
            var assets=OfficialWorldMapAssetCatalog.LoadDefault();
            int maps=0,regions=0;
            foreach(int mapId in new[]{10,11,12,13,14,15,16})
            {
                var definition=assets.LoadMap(mapId).Definition;
                profile.World.Maps.Add(new OfflineMapProgress{MapClassId=mapId,Regions=definition.MaxRegionProgress
                    .Select(value=>new OfflineRegionProgress{Region=value.Region,Progress=10000}).ToList()});
                File.WriteAllText(slots.GetProgressPath(slot),JsonUtility.ToJson(profile,true));
                var local=new RawPointsSave{slotId=slot,gridX=0,gridY=0,
                    completedEntityPlacements=definition.Entities.Select(value=>value.Id).ToArray()};
                string mapPath=Path.Combine(slots.GetSlotDirectory(slot),"map-"+mapId+"-save.json");
                File.WriteAllText(mapPath,JsonUtility.ToJson(local,true));
                var before=Directory.GetFiles(slots.GetSlotDirectory(slot),"*",SearchOption.AllDirectories)
                    .ToDictionary(path=>path,path=>Convert.ToBase64String(File.ReadAllBytes(path)));
                var objects=UnityEngine.Object.FindObjectsOfType<GameObject>(true).Select(value=>value.GetInstanceID()).OrderBy(value=>value).ToArray();
                var reader=new MainlineMapMiniMapSnapshots(assets,slots,slot);
                var points=reader.ReadRegionPoints(mapId);
                foreach(var maximum in definition.MaxRegionProgress)
                    Require(points[maximum.Region]==maximum.Value,"Raw fully completed region differs from its original maximum: map="
                        +mapId+" region="+maximum.Region+" points="+points[maximum.Region]+" maximum="+maximum.Value);
                int first=points.Keys.First();points[first]=int.MaxValue;
                Require(reader.ReadRegionPoints(mapId)[first]==definition.MaxRegionProgress.Single(value=>value.Region==first).Value,
                    "Raw progress reads share mutable dictionaries.");
                bool rejected=false;try{reader.Read(mapId);}catch(InvalidDataException){rejected=true;}
                Require(rejected,"A raw-only world unexpectedly supplied missing minimap fog.");
                Require(objects.SequenceEqual(UnityEngine.Object.FindObjectsOfType<GameObject>(true).Select(value=>value.GetInstanceID()).OrderBy(value=>value)),
                    "Raw progress read mounted scene objects.");
                Require(before.All(pair=>Convert.ToBase64String(File.ReadAllBytes(pair.Key))==pair.Value)
                    &&before.Count==Directory.GetFiles(slots.GetSlotDirectory(slot),"*",SearchOption.AllDirectories).Length,
                    "Raw progress read changed local profile/world files.");
                maps++;regions+=points.Count;
                Debug.Log("MAINLINE_RAW_REGION_POINTS_PASS map="+mapId+" regions="+points.Count);
                yield return null;
            }
            Require(!failed,"Raw region progress emitted runtime errors.");
            File.WriteAllText(Path.Combine(Output,"raw-region-points.json"),"{\"maps\":"+maps+",\"regions\":"+regions
                +",\"original_maxima\":true,\"no_minimap_fog\":true,\"read_only\":true,\"play_mode\":true}");
        }
        [Serializable] private sealed class RawPointsSave
        {
            public int schema=1;
            public string slotId;
            public int gridX,gridY;
            public int[] completedEntityPlacements;
        }
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
            save.Player.UnlockedMapClassIds.AddRange(new[]{11,12,13,14,15});
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
            if(MapId==14)
            {
                var world=Field<OfficialMap14NativeWorldView>(runtime,"nativeMap14World");
                while((world==null||!world.IsReady)&&Time.realtimeSinceStartup<deadline)
                { yield return null;world=Field<OfficialMap14NativeWorldView>(runtime,"nativeMap14World"); }
                Require(world!=null&&world.IsReady,"Formal Map14 original partitions did not become ready.");
                var entities=(System.Collections.IDictionary)Field<object>(runtime,"mapEntities");
                Require(entities.Count==2961&&Field<int>(entities[101],"State")==1,
                    "Formal Map14 lost the original entry stage instead of restoring state 1.");
                Require(Field<int>(entities[201],"LockType")==2
                    &&Field<bool>(entities[201],"ConditionEntityInactiveInUnlock"),
                    "Formal Map14 lost its original entity-state lock metadata.");
            }
            var navigation=Field<object>(runtime,"navigation");var initial=Field<MapGridPoint>(navigation,"currentGrid");
            if(MapId==13)Require((int)Call(runtime,"GetDefaultEntryId")==11001&&initial.Equals(new MapGridPoint(113,233)),"Original TbMapWorldInfo.DefEntryId 11001 and its (113,233) placement were not used.");
            if(MapId==11)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(167,133)),"Original Map11 DefEntryId 101 and its (167,133) placement were not used.");
            if(MapId==12)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(248,32)),"Original Map12 entry 101 at (248,32) was not used.");
            if(MapId==15)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(12,6)),"Original Map15 entry 101 at (12,6) was not used.");
            if(MapId==14)Require((int)Call(runtime,"GetDefaultEntryId")==101&&initial.Equals(new MapGridPoint(247,115)),"Original Map14 entry 101 at (247,115) was not used.");
            Require(Field<object>(runtime,"mapGridVisibility")!=null,"Formal original grid component is not bound.");
            var visibilityAtEntry=Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility");
            var entityViewsAtEntry=Field<OfficialMap10EntityView>(runtime,"entityView");
            Require(visibilityAtEntry.ExploredEntityIds.All(id=>entityViewsAtEntry.FindEntityObject(id)?.activeSelf==true),
                "A fresh formal map recorded an entity whose actual world object is inactive.");
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
            var explored=Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility").ExploredEntityIds;
            Require(explored.Length>0&&before.exploredEntityPlacements!=null&&explored.SequenceEqual(before.exploredEntityPlacements),
                "Formal map did not persist its actually revealed entity placements after simulated movement.");
            if(SessionState.GetBool(Key+".hud",false))
            {
                yield return VerifyFormalHud(runtime);
                if(MapId!=14){Call(runtime,"CloseMap");yield return null;yield break;}
                yield return VerifyMiniMapSnapshot(runtime,slots,slot,"hud");
            }
            if(SessionState.GetBool(Key+".world-owner",false))
            {
                yield return VerifyFormalWorldOwner(runtime,slot,saves,canvas.transform,target);
                yield break;
            }
            yield return VerifyMiniMapSnapshot(runtime,slots,slot,"moved");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-formal-moved.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            runtime=Open(slot,saves,canvas.transform);yield return new WaitForSecondsRealtime(3);
            Require(Field<MapGridPoint>(Field<object>(runtime,"navigation"),"currentGrid").Equals(target),"Formal map position did not survive reopen.");
            var after=JsonUtility.FromJson<VisibilitySave>(File.ReadAllText(mapSave));Require(before.Encoded(MapId)==after.Encoded(MapId),"Formal visibility bytes changed on cold reopen.");
            Require(before.exploredEntityPlacements.SequenceEqual(after.exploredEntityPlacements)
                &&after.exploredEntityPlacements.SequenceEqual(Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility").ExploredEntityIds),
                "Formal entity exploration history changed on cold reopen.");
            CheckFog(runtime);
            yield return VerifyMiniMapSnapshot(runtime,slots,slot,"restored");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-formal-restored.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            if(SessionState.GetBool(Key+".trees",false))yield return TreeSteps(slot,saves,canvas.transform,mapSave);
            if(MapId==11||MapId==12)yield return GoddessSteps(slot,saves,canvas.transform);
            if(MapId==11||MapId==12)VerifySnapshotFreshness(slots,slot);
            Require(!failed,"Formal map entry emitted runtime errors.");
            File.WriteAllText(Path.Combine(Output,"map"+MapId+"-formal-entry.json"),JsonUtility.ToJson(new Proof{map_id=MapId,editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),play_mode=true,simulated_pointer=true,save_reopen=true,entry_x=initial.X,entry_y=initial.Y,moved_x=target.X,moved_y=target.Y},true));
            Debug.Log("Map"+MapId+" formal entry passed: shared map host, simulated map pointer, local grid visibility/position save and cold reopen.");
        }
        private static IEnumerator VerifyFormalHud(OfficialMainlineMapRuntime runtime)
        {
            var hud=Field<object>(runtime,"hud");
            var ui=Field<OfficialWorldUiRenderer>(hud,"ui");
            var root=ui.Find("WorldCanvas","/WorldCanvas");
            var prefab=Resources.Load<GameObject>("Map"+MapId+"/UI/WorldCanvas");
            Require(root&&root.activeInHierarchy&&prefab&&root.transform.childCount==prefab.transform.childCount,
                "Formal Map14 did not mount its original WorldCanvas hierarchy.");
            var name=ui.Find("WorldCanvas","/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText");
            Require(name.GetComponent<TMPro.TMP_Text>().text=="地图"+MapId+"验证","Formal HUD lost the local character identity.");
            var portrait=Field<Texture2D>(hud,"playerHeadTexture");
            Require(portrait&&portrait.GetPixels32().Count(value=>value.a>0)>100,
                "Original HUD head photography produced an empty portrait.");
            File.WriteAllBytes(Path.Combine(Output,"map"+MapId+"-hud-head.png"),portrait.EncodeToPNG());
            var head=root.GetComponentsInChildren<RawImage>(true).Single(value=>value.name=="HeadImg");
            Require(head.texture==portrait&&head.uvRect==new Rect(0,0,1,1)
                &&head.rectTransform.sizeDelta==new Vector2(portrait.width,portrait.height),
                "Official portrait UV rectangle/native texture size were not restored.");
            var avatar=Field<GameObject>(runtime,"avatar");
            Require(avatar&&avatar.GetComponentsInChildren<Renderer>().Any(value=>value.isVisible),
                "Original player model is not visible over the native ground.");
            string entry="/WorldCanvas/Content/Content/WorldPanel/Anim/Right/FunctionEntryGroup/FunctionEntryFunc/Content/Entry/";
            var button=ui.Find("WorldCanvas",entry+"EntryBtnFolded").GetComponent<Button>();
            if(!button.gameObject.activeInHierarchy)button=ui.Find("WorldCanvas",entry+"EntryBtnExpanded").GetComponent<Button>();
            ClickHudButton(button);
            yield return new WaitForSecondsRealtime(.5f);
            var functions=Field<object>(hud,"worldFunctions");
            Require(functions!=null,"Simulated function-entry click did not open the original dialog.");
            var dialog=(GameObject)functions.GetType().GetProperty("Root").GetValue(functions);
            Require(dialog&&dialog.activeInHierarchy,"Original function dialog is inactive.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map"+MapId+"-hud-functions.png"));
            yield return new WaitForSecondsRealtime(.5f);
            ClickHudButton(dialog.transform.Find("CloseBtn").GetComponent<Button>());
            yield return null;
            Require(Field<object>(hud,"worldFunctions")==null&&!dialog,"Simulated close click retained the function dialog.");
            Debug.Log("MAINLINE_HUD_PASS map="+MapId+": original WorldCanvas, visible player, rendered head portrait, character identity and raycast-checked function open/close clicks; complete HUD flows remain outside this check.");
        }
        private static void ClickHudButton(Button button)
        {
            Canvas.ForceUpdateCanvases();
            Require(button&&button.gameObject.activeInHierarchy&&button.interactable,"Official HUD button is unavailable.");
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,
                "Official HUD button is occluded: "+button.name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static void VerifyFormalOriginalClient(OfficialMainlineMapRuntime runtime)
        {
            var world=Field<MainlineSharedWorld>(runtime,"mainlineSharedWorld");
            var player=Field<object>(runtime,"mainlineSharedPlayer");
            var manager=world.Assemblies.Shared.GetType("MeManager",true).GetProperty("Instance").GetValue(null);
            object Get(object owner,string name)=>NativeMap14SharedPrefabs.Get(owner,name);
            var map=Get(manager,"MapData");
            var originals=((IDictionary)Get(world.Root.Container,"EntityDict")).Values.Cast<object>().ToArray();
            var progress=Field<object>(runtime,"progressionSave");
            var save=(OfflineMainlineSave)progress.GetType().GetProperty("Save").GetValue(progress);
            Require(world.IsReady&&ReferenceEquals(Get(manager,"Player"),player)
                &&ReferenceEquals(Get(map,"MapContainer"),world.Root.Container)
                &&ReferenceEquals(map.GetType().GetProperty("mMapLoEntityIdManager",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(map),world.LoEntityIds),
                "Formal map did not bind its original player, map and placement manager before startup.");
            Require(originals.Length>1&&originals.All(entity=>(bool)Get(entity,"Awaked")&&(bool)Get(entity,"Started")),
                "Formal map retained a root-only original snapshot or unstarted placed entities.");
            Require((string)Get(player,"Name")==save.Player.CharacterName&&(int)Get(player,"Level")==save.Player.Level
                &&Convert.ToInt32(Get(player,"SubRank"))==save.Player.SubRank,
                "Formal original player identity differs from the local character.");
            var points=Field<Dictionary<int,int>>(Field<object>(runtime,"regions"),"regionPoints");
            foreach(var pair in points)
                Require((int)player.GetType().GetMethod("GetWorldMapRegionProgress").Invoke(player,new object[]{MapId,pair.Key})==pair.Value,
                    "Formal original player lost restored raw region points.");
            Debug.Log("MAINLINE_FORMAL_CLIENT_PASS map="+MapId+" entities="+originals.Length);
        }
        private static IEnumerator VerifyFormalWorldOwner(OfficialMainlineMapRuntime runtime,string slot,string saves,
            Transform ui,MapGridPoint position)
        {
            var world=Field<MainlineSharedWorld>(runtime,"mainlineSharedWorld");
            var terrain=Field<OfficialMap14NativeWorldView>(runtime,"nativeMap14World");
            var root=world.Root;
            VerifyFormalOriginalClient(runtime);
            var manager=world.Assemblies.Shared.GetType("MeManager",true).GetProperty("Instance").GetValue(null);
            Require(ReferenceEquals(root,terrain.SharedRoot),"Formal terrain created a second original world root.");
            var originalRoot=terrain.SharedRoot.Root;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map14-owner-moved.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            Require(!world.IsReady
                &&Field<bool>(originalRoot,"Ended")
                &&NativeMap14SharedPrefabs.Get(manager,"Player")==null,"Closing the formal map retained its original world.");
            runtime=Open(slot,saves,ui);
            float deadline=Time.realtimeSinceStartup+120;
            terrain=Field<OfficialMap14NativeWorldView>(runtime,"nativeMap14World");
            while((terrain==null||!terrain.IsReady)&&Time.realtimeSinceStartup<deadline)
            { yield return null;terrain=Field<OfficialMap14NativeWorldView>(runtime,"nativeMap14World"); }
            Require(terrain!=null&&terrain.IsReady,"Formal map owner did not remount its original terrain.");
            var reopened=Field<MainlineSharedWorld>(runtime,"mainlineSharedWorld");
            VerifyFormalOriginalClient(runtime);
            Require(!ReferenceEquals(world,reopened)&&!ReferenceEquals(root,terrain.SharedRoot)
                &&ReferenceEquals(reopened.Root,terrain.SharedRoot),
                "Reopening the formal map did not create exactly one new original world.");
            Require(Field<MapGridPoint>(Field<object>(runtime,"navigation"),"currentGrid").Equals(position),
                "Formal world ownership changed the saved player position.");
            CheckFog(runtime);
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map14-owner-restored.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            runtime=Open(slot,saves,ui);
            world=Field<MainlineSharedWorld>(runtime,"mainlineSharedWorld");
            Require(Field<OfficialMap14NativeWorldView>(runtime,"nativeMap14World")==null,
                "Formal cancellation did not interrupt original world loading.");
            Call(runtime,"CloseMap");yield return null;yield return null;
            Require(!world.IsLoaded,
                "Cancelled formal map retained its original world load.");
            Require(!failed,"Formal world ownership emitted runtime errors.");
            Debug.Log("MAINLINE_FORMAL_WORLD_OWNER_PASS: borrowed original root, simulated map movement, save reopen and loading cancellation; full minimap is outside this check.");
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
            CheckFog(runtime);
            yield return VerifyMiniMapSnapshot(runtime,new ProfileSlotCatalog(saves),slot,"public-region");
            Call(runtime,"CloseMap");yield return null;yield return null;
            Debug.Log("Map"+MapId+" original goddess input passed: explicit region completion fixture, simulated map/button clicks, immediate grid unlock and cold restore.");
        }
        private static IEnumerator VerifyMiniMapSnapshot(OfficialMainlineMapRuntime runtime,ProfileSlotCatalog slots,
            string slot,string phase)
        {
            var formalOffline=runtime.GetComponentsInChildren<OfflineMiniMap>(true);
            // Map14's original complete minimap has not yet been mounted by the
            // formal host. Still verify its actual local snapshot and fog below.
            Require((MapId==14||formalOffline.Length>0)&&formalOffline.All(value=>value.DataSource!=null),
                "Formal map minimap did not receive the shared local snapshot data source.");
            var files=Directory.GetFiles(slots.GetSlotDirectory(slot),"*",SearchOption.AllDirectories)
                .ToDictionary(path=>path,path=>Convert.ToBase64String(File.ReadAllBytes(path)));
            var reader=new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(),slots,slot);
            var beforeObjects=UnityEngine.Object.FindObjectsOfType<GameObject>(true).Select(value=>value.GetInstanceID()).OrderBy(id=>id).ToArray();
            var snapshot=reader.Read(MapId);
            var rawPoints=reader.ReadRegionPoints(MapId);
            var livePoints=Field<Dictionary<int,int>>(Field<object>(runtime,"regions"),"regionPoints");
            Require(rawPoints.OrderBy(pair=>pair.Key).SequenceEqual(livePoints.OrderBy(pair=>pair.Key)),
                "Cold raw region points differ from the actual world's entity progress: "+string.Join(",",rawPoints.Where(pair=>!livePoints.TryGetValue(pair.Key,out int live)||live!=pair.Value).Select(pair=>pair.Key+"="+pair.Value+"/"+(livePoints.TryGetValue(pair.Key,out int live)?live:-1))));
            Require(beforeObjects.SequenceEqual(UnityEngine.Object.FindObjectsOfType<GameObject>(true)
                .Select(value=>value.GetInstanceID()).OrderBy(id=>id)),"A cold minimap read mounted scene objects.");
            var views=Field<OfficialMap10EntityView>(runtime,"entityView");
            var active=views.EntityInfos.Where(info=>views.FindEntityObject(info.PlacementId)?.activeSelf==true)
                .Select(info=>info.PlacementId).OrderBy(id=>id).ToArray();
            var actual=snapshot.Entities.Select(value=>value.PlacementId).OrderBy(id=>id).ToArray();
            Require(active.SequenceEqual(actual),"Cold minimap entities differ from the live world's actual activation: missing="
                +string.Join(",",active.Except(actual).Take(20))+" extra="+string.Join(",",actual.Except(active).Take(20)));
            foreach(var info in views.EntityInfos.Where(value=>snapshot.PublicRegions.Contains(value.Region)
                &&(value.TypeId==202||value.TypeId==223)&&active.Contains(value.PlacementId)))
                Require(snapshot.Entities.Single(value=>value.PlacementId==info.PlacementId).State==(info.TypeId==202?1:2),
                    "Cold minimap did not use the latest public goddess/hero state.");
            var visibility=Field<OfflineMapGridVisibility>(runtime,"mapGridVisibility");
            Require(snapshot.GridVisibility.SequenceEqual(visibility.Snapshot()),"Cold minimap changed original fog bytes.");
            PlayerOfflineMiniMapDataResponse response=null;
            MainlineMapMiniMapSnapshots.CreateOfflineDataSource(OfficialWorldMapAssetCatalog.LoadDefault(),slots,slot)
                .Request(MapId).OnComplete+=(value,error)=>
            {Require(error==0,"Formal local minimap request failed.");response=value;};
            for(int i=0;i<4&&response==null;i++)yield return null;
            Require(response!=null&&response.UseLatestLocalEntityStates,"Formal world snapshot did not reach original offline response consumption.");
            var expected=snapshot.Entities.Where(value=>value.Explored||snapshot.PublicRegions.Contains(value.Region))
                .Select(value=>value.PlacementId).OrderBy(id=>id);
            Require(expected.SequenceEqual(response.LiteEntityDataDict.Keys.OrderBy(id=>id)),"Formal response leaked private unexplored entities.");
            Require(files.Count==Directory.GetFiles(slots.GetSlotDirectory(slot),"*",SearchOption.AllDirectories).Length
                &&files.All(pair=>Convert.ToBase64String(File.ReadAllBytes(pair.Key))==pair.Value),"Browsing changed profile/world/backup files.");
            bool rejected=false;try{reader.Read(16);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"An unvisited world supplied offline data.");
            Debug.Log("MAINLINE_FORMAL_MINIMAP_SNAPSHOT_PASS map="+MapId+" phase="+phase
                +" active="+active.Length+" markers="+response.LiteEntityDataDict.Count);
        }

        private static void VerifySnapshotFreshness(ProfileSlotCatalog slots,string slot)
        {
            string mapPath=Path.Combine(slots.GetSlotDirectory(slot),"map-"+MapId+"-save.json");
            string profilePath=slots.GetProgressPath(slot),backupPath=mapPath+".bak";
            byte[] originalMap=File.ReadAllBytes(mapPath),originalProfile=File.ReadAllBytes(profilePath),originalBackup=File.ReadAllBytes(backupPath);
            long clock=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var reader=new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(),slots,slot,()=>clock);
            try
            {
                var definition=OfficialWorldMapAssetCatalog.LoadDefault().LoadMap(MapId).Definition;
                var resource=definition.Entities.First(value=>value.Region==0&&value.EntityId==8011
                    ||value.Region==0&&value.EntityId==8021);
                string mapJson=System.Text.Encoding.UTF8.GetString(originalMap);
                var completed=Regex.Match(mapJson,"\"completedEntityPlacements\"\\s*:\\s*\\[([^\\]]*)\\]");
                Require(completed.Success,"Resource fixture cannot read completed placements.");
                string remaining=string.Join(",",completed.Groups[1].Value.Split(',').Select(value=>value.Trim())
                    .Where(value=>value.Length>0&&value!=resource.Id.ToString()));
                mapJson=mapJson.Remove(completed.Index,completed.Length).Insert(completed.Index,"\"completedEntityPlacements\":["+remaining+"]");
                File.WriteAllText(mapPath,mapJson);
                var profile=JsonUtility.FromJson<OfflineMainlineSave>(System.Text.Encoding.UTF8.GetString(originalProfile));
                if(!profile.World.ActivatedGoddessRegions.Any(value=>value.MapClassId==MapId&&value.Region==0))
                    profile.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey{MapClassId=MapId,Region=0});
                profile.World.MapInteractions.RemoveAll(value=>value.MapClassId==MapId&&value.PlacementId==resource.Id);
                File.WriteAllText(profilePath,JsonUtility.ToJson(profile,true));
                Require(reader.Read(MapId).Entities.Any(value=>value.PlacementId==resource.Id),"Public resource fixture is not visible.");
                profile.World.MapInteractions.Add(new OfflineMapInteractionState{MapClassId=MapId,PlacementId=resource.Id,AvailableAt=clock+60});
                int serial=profile.World.Exploration.NextSerial++;
                var generated=new OfflineExploreEntity{Serial=serial,MapClassId=MapId,Area=resource.Area,Region=0,
                    X=resource.Grid[0],Y=resource.Grid[1],EntityClassId=8364,Group=1,Variant=1,CreatedAt=clock,ExpiresAt=clock+60};
                profile.World.Exploration.Entities.Add(generated);
                File.WriteAllText(profilePath,JsonUtility.ToJson(profile,true));
                byte[] prepared=File.ReadAllBytes(profilePath);
                var before=reader.Read(MapId);
                Require(!before.Entities.Any(value=>value.PlacementId==resource.Id)
                    &&before.Entities.Any(value=>value.PlacementId==generated.PlacementId),"Same reader did not consume latest resource/generated entity state.");
                var replacement=OfficialWorldExplorationCatalog.Load().Data.entities.First(value=>value.entityClassId!=8364
                    &&value.parameters.WorldStageType!=202&&value.parameters.WorldStageType!=223);
                generated.EntityClassId=replacement.entityClassId;generated.State=replacement.parameters.PublicDefaultState;
                File.WriteAllText(profilePath,JsonUtility.ToJson(profile,true));prepared=File.ReadAllBytes(profilePath);
                var changed=reader.Read(MapId).Entities.Single(value=>value.PlacementId==generated.PlacementId);
                Require(changed.EntityClassId==generated.EntityClassId&&changed.State==generated.State,
                    "Same reader reused a generated entity's stale class/state.");
                clock+=60;
                var after=reader.Read(MapId);
                Require(after.Entities.Any(value=>value.PlacementId==resource.Id)
                    &&!after.Entities.Any(value=>value.PlacementId==generated.PlacementId),"Resource refresh and generated expiry failed at the exact local time boundary.");
                Require(prepared.SequenceEqual(File.ReadAllBytes(profilePath)),"Time projection rewrote the profile.");
                File.WriteAllBytes(backupPath,originalMap);File.WriteAllText(mapPath,"invalid map primary");
                Require(reader.Read(MapId).GridVisibility.SequenceEqual(before.GridVisibility),"Cold map backup recovery changed original fog.");
                Require(File.ReadAllText(mapPath)=="invalid map primary","Browsing repaired or overwrote the primary file.");
                File.WriteAllText(backupPath,"invalid map backup");
                bool rejected=false;try{reader.Read(MapId);}catch(InvalidDataException){rejected=true;}
                Require(rejected,"Two corrupt map files supplied a minimap.");
                string noFog=Regex.Replace(System.Text.Encoding.UTF8.GetString(originalMap),
                    "\"gridVisibility\"\\s*:\\s*\"[^\"]*\"","\"gridVisibility\":\"\"");
                File.WriteAllBytes(mapPath,originalMap);
                var pointsWithFog=reader.ReadRegionPoints(MapId);
                File.WriteAllText(mapPath,noFog);
                rejected=false;try{reader.Read(MapId);}catch(InvalidDataException){rejected=true;}
                Require(rejected&&File.ReadAllText(mapPath)==noFog,"Missing exploration was replaced with invented fog or written back.");
                var pointsWithoutFog=reader.ReadRegionPoints(MapId);
                Require(pointsWithFog.OrderBy(pair=>pair.Key).SequenceEqual(pointsWithoutFog.OrderBy(pair=>pair.Key)),
                    "Raw entity progress incorrectly depends on saved minimap visibility.");
                pointsWithoutFog[pointsWithoutFog.Keys.First()]=int.MaxValue;
                Require(pointsWithFog.OrderBy(pair=>pair.Key).SequenceEqual(reader.ReadRegionPoints(MapId).OrderBy(pair=>pair.Key))
                    &&File.ReadAllText(mapPath)==noFog,"Raw progress read shared mutable results or changed the map save.");
                Debug.Log("MAINLINE_FORMAL_MINIMAP_FRESHNESS_PASS map="+MapId+" resource="+resource.Id+" generated="+generated.PlacementId);
            }
            finally
            {
                File.WriteAllBytes(mapPath,originalMap);File.WriteAllBytes(profilePath,originalProfile);File.WriteAllBytes(backupPath,originalBackup);
            }
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
        [Serializable]private sealed class VisibilitySave{public string map13GridVisibility,gridVisibility;public int[] exploredEntityPlacements;public string Encoded(int map)=>map==13?map13GridVisibility:gridVisibility;}
        [Serializable]private sealed class Proof{public string editor,backend;public bool play_mode,simulated_pointer,save_reopen;public int map_id,entry_x,entry_y,moved_x,moved_y;}
        private sealed class Runner:MonoBehaviour{}
    }
}
