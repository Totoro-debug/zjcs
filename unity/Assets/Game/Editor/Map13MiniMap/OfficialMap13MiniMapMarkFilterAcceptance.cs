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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap13MiniMapResources;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class Map13FilterMapFixture : Map13ManagedFixture, IMiniMapFilterMapContext
    {
        public MiniMapResourcePointManager ResourcePointManager { get; set; }
    }

    public static class OfficialMap13MiniMapMarkFilterAcceptance
    {
        private const string Key = "Zjcs.Map13.MiniMapMarkFilter.Running";
        private const string Native = "Assets/Official/World/Map/13/NativeMiniMapFilter/";
        private static IEnumerator routine;
        private static int errors;

        [Serializable] private sealed class Row
        {
            public string Name, Image, Description;
            public bool ShowOnFilterEntry;
            public List<int> ShowOnMapGroupId, ShowOnMapClassId;
        }
        [Serializable] private sealed class Table { public Row[] Rows; }

        private static void Require(bool value, string detail)
        {
            if (!value) throw new InvalidDataException(detail);
        }

        private sealed class Queries : IMiniMapMarkDataQueries
        {
            public int GridToArea(int map, Pos2d position) => 0;
            public string GetAreaTag(int map, int area) => "";
            public MiniMapMarkMapInfo GetMapInfo(int map) => new MiniMapMarkMapInfo { MapGroupId = 13 };
            public EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entityClassId, int mapGroupId, int map, string areaTag, string entityTag) => null;
            public MiniMapMarkExtendInfo GetEntityExtend(int entityClassId) => null;
            public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map, int loEntityId) => null;
            public string GetEntityTag(int map, int loEntityId) => null;
            public IMiniMapMarkEntity GetCommonPrefab(int entityClassId) => null;
            public Vector3 MapGridToPos(int map, Pos2d position) => Vector3.zero;
            public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map, int loEntityId) => null;
            public bool RegionIsActived(int map, int region) => true;
            public int QueryRegion(Pos2d position) => 0;
            public int CurrentMeMapId => 13;
            public bool HasMainPlayer => true;
            public bool HasMainMapContainer => true;
            public int MainMapClassId => 13;
            public int GridToRegion(int map, Pos2d position) => 0;
            public MapRegionStatus GetMapRegionStatus(int map, int region) => MapRegionStatus.Public;
            public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity) => UniTask.FromResult(false);
            public void DoEnterMap(int map, int loEntityId) { }
        }

        private sealed class Persistent : IMiniMapFilterPersistentState
        {
            public MiniMapMarkUIFilterSetting State { get; } = new MiniMapMarkUIFilterSetting();
            public int Writes;
            public UniTask WriteStateAsync() { Writes++; return UniTask.CompletedTask; }
        }

        private sealed class SoundHost : IProfessionSoundHost
        { public int Events; public uint PostEvent(uint eventId, GameObject emitter) { Events++; return (uint)Events; } }

        private sealed class Lease : IMiniMapDescriptionAssetLease<Sprite>
        {
            public Sprite Result { get; private set; }
            public Lease(Sprite sprite) { Result = sprite; }
            public void Dispose() { Result = null; }
        }

        private sealed class Services : IMiniMapMarkFilterServices
        {
            private readonly DescriptionLabels labels;
            private readonly List<Row> rows;
            private readonly HashSet<string> loadedPaths = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<Action<OnGlobalPointerDownResult>> hooks = new List<Action<OnGlobalPointerDownResult>>();
            private readonly List<RaycastResult> hits = new List<RaycastResult>();
            public int Created, Loaded;
            public int UniqueSprites => loadedPaths.Count;

            public Services(DescriptionLabels labels)
            {
                this.labels = labels;
                rows = JsonUtility.FromJson<Table>(Resources.Load<TextAsset>("Map13/minimap-marks").text).Rows.ToList();
            }

            public MiniMapFilterMarkInfo[] GetAllMarkInfos() => rows.Select(row => new MiniMapFilterMarkInfo
            {
                Name = row.Name, Image = row.Image, Description = row.Description, ShowOnFilterEntry = row.ShowOnFilterEntry,
                ShowOnMapGroupId = row.ShowOnMapGroupId, ShowOnMapClassId = row.ShowOnMapClassId
            }).ToArray();

            public GameObjectPool<T> CreatePool<T>(Transform root, GameObject prefab) where T : Component =>
                new GameObjectPool<T>(root, (slot, _, active) =>
                {
                    var staging = new GameObject("OriginalMap13FilterPoolInjection");
                    staging.SetActive(false);
                    var item = Object.Instantiate(prefab, staging.transform, false);
                    labels.Bind(item);
                    item.transform.SetParent(slot, false);
                    item.SetActive(active);
                    Object.Destroy(staging);
                    Created++;
                    return item;
                }, prefab.name);

            public IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path)
            {
                string name = path.Substring(path.LastIndexOf('/') + 1);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Native + "Image/MapWorld/MiniMap_Atlas/" + name + ".asset");
                if (!sprite) throw new InvalidDataException("Original minimap filter sprite is not bound: " + path);
                loadedPaths.Add(path);
                Loaded++;
                return new Lease(sprite);
            }

            public IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position)
            {
                hits.Clear();
                var pointer = new PointerEventData(EventSystem.current) { position = position };
                EventSystem.current.RaycastAll(pointer, hits);
                return hits;
            }
            public void HookOnInput(Action<OnGlobalPointerDownResult> receiver) => hooks.Add(receiver);
            public void UnhookOnInput(Action<OnGlobalPointerDownResult> receiver) => hooks.Remove(receiver);
            public void Pointer(Vector2 position)
            {
                foreach (var hook in hooks.ToArray()) hook(new OnGlobalPointerDownResult(position));
            }
        }

        private static void Observe(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            errors = 0;
            Application.logMessageReceived += Observe;
            SessionState.SetBool(Key, true);
            EditorApplication.playModeStateChanged += State;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void VerifyPersistentState()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Require(!string.IsNullOrWhiteSpace(workspace), "Isolated validation directory is configured");
            Type slotsType = Type.GetType("Zjcs.Offline.State.ProfileSlotCatalog, Zjcs.Offline.State", true);
            Type storeType = Type.GetType("Zjcs.Presentation.World.MainlineMiniMapFilterPersistentState, Zjcs.Presentation.World", true);
            string saveRoot = Path.Combine(workspace, "minimap-filter-persistence");
            object slots = Activator.CreateInstance(slotsType, saveRoot);
            string slot = (string)slotsType.GetMethod("CreateSlot").Invoke(slots, null);
            string path = Path.Combine((string)slotsType.GetMethod("GetSlotDirectory").Invoke(slots, new object[] { slot }), "minimap-filter.json");
            ConstructorInfo constructor = storeType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { slotsType, typeof(string) }, null);
            Require(constructor != null, "Production state adapter accepts the active profile slot");
            object state = constructor.Invoke(new[] { slots, slot });
            object settings = storeType.GetProperty("State").GetValue(state);
            Type settingsType = settings.GetType();
            var values = (IDictionary)settingsType.GetProperty("FilterState").GetValue(settings);
            values.Add("C_MiniMap_Entry", false);
            values.Add("Map13Entity", true);
            object selection = Enum.Parse(settingsType.GetProperty("SelectionState").PropertyType, "Free");
            settingsType.GetProperty("SelectionState").SetValue(settings, selection);
            storeType.GetMethod("WriteStateAsync").Invoke(state, null);
            Require(File.Exists(path) && File.ReadAllText(path).Contains("C_MiniMap_Entry"), "Atomic profile save writes the selected filters");

            object restored = constructor.Invoke(new[] { slots, slot });
            object restoredSettings = storeType.GetProperty("State").GetValue(restored);
            var restoredValues = (IDictionary)settingsType.GetProperty("FilterState").GetValue(restoredSettings);
            Require((bool)restoredValues["C_MiniMap_Entry"] == false && (bool)restoredValues["Map13Entity"]
                && Enum.GetName(settingsType.GetProperty("SelectionState").PropertyType,
                    settingsType.GetProperty("SelectionState").GetValue(restoredSettings)) == "Free",
                "A fresh production adapter cold-reads filter values and selection mode");

            string corruptPath = Path.Combine(workspace, "minimap-filter-corrupt.json");
            File.WriteAllText(corruptPath, "{}");
            bool rejected = false;
            try { Activator.CreateInstance(storeType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { corruptPath }, null); }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidDataException) { rejected = true; }
            Require(rejected, "Corrupt filter state is rejected instead of silently resetting");
            File.WriteAllText(Path.Combine(workspace, "minimap-filter-persistence-proof.json"),
                JsonUtility.ToJson(new PersistenceProof { slot = slot, writes = 1, coldRead = true, corruptRejected = true }, true));
            Debug.Log("MAP13_MINIMAP_FILTER_PERSISTENCE_PASS");
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            errors = 0;
            Application.logMessageReceived += Observe;
            EditorApplication.playModeStateChanged += State;
        }

        private static void State(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                routine = Verify();
                EditorApplication.update += Advance;
            }
        }

        private static void Advance()
        {
            try
            {
                if (routine.MoveNext()) return;
                Require(errors == 0, "Original filter emitted runtime errors: " + errors);
                EditorApplication.update -= Advance;
                SessionState.EraseBool(Key);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.update -= Advance;
                SessionState.EraseBool(Key);
                EditorApplication.Exit(1);
            }
        }

        private static void Click(EventSystem system, GameObject button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas ? canvas.worldCamera : null, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(system) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            system.RaycastAll(data, hits);
            Require(hits.Any(hit => hit.gameObject == button || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == button),
                "Simulated mouse raycasts to original button " + button.name + " at " + point + " size=" + rect.rect.size + " hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name)));
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Verify()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset");
            Require(font && font.atlasTextures.Length > 0 && font.atlasTextures[0].isReadable, "Selected original dynamic font and writable atlas");
            var labels = new DescriptionLabels();
            labels.SetPublic(true);
            var resources = Resources.Load<MiniMapResourcePointAssets>("Map13/Native/minimap-resource-point-assets");
            Require(resources && resources.ManagerPrefab && resources.Boards.Count == 23, "Original Map13 resource manager assets");
            var localResources = new LocalMiniMapResourcePointContext(resources, labels);
            var root = new GameObject("OriginalMap13FilterContext");
            var context = root.AddComponent<Map13FilterMapFixture>();
            context.Queries = new Queries();
            context.ResourcePointManager = Object.Instantiate(resources.ManagerPrefab, root.transform, false).GetComponent<MiniMapResourcePointManager>();
            context.ResourcePointManager.Init(localResources);
            var spawnObject = new GameObject("OriginalMap13MarkSpawnFixture"); spawnObject.SetActive(false);
            var spawn = spawnObject.AddComponent<MiniMapMarkSpawnManager>(); spawn.PerformanceMiniMapMarkProxy = new Performance();
            typeof(MiniMapMarkSpawnManager).GetField("mMinimap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(spawn, context);
            var renderCamera = new GameObject("OriginalMap13FilterRenderCamera", typeof(Camera)).GetComponent<Camera>();
            renderCamera.orthographic = true; renderCamera.transform.position = new Vector3(0, 0, -10); renderCamera.clearFlags = CameraClearFlags.SolidColor; renderCamera.backgroundColor = Color.black;
            float viewWidth = Screen.height * (9f / 16f);
            renderCamera.rect = new Rect((Screen.width - viewWidth) / (2f * Screen.width), 0, viewWidth / Screen.width, 1);
            var canvas = new GameObject("OriginalMap13FilterCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = renderCamera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0;
            var system = new GameObject("OriginalMap13FilterEventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            EventSystem.current = system;
            var host = new GameObject("OriginalMap13FilterView", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var rect = host.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Map/MiniMap/Filter/MiniMapMarkUIFilter.prefab");
            Require(prefab, "Complete original filter prefab");
            host.SetActive(false);
            var panel = Object.Instantiate(prefab, host.transform, false);
            panel.SetActive(false);
            labels.Bind(panel);
            var filter = panel.GetComponent<MiniMapMarkUIFilter>();
            var state = new Persistent();
            var services = new Services(labels);
            var previousSoundHost = OfficialWwiseEvent.Host;
            var soundHost = new SoundHost(); OfficialWwiseEvent.Host = soundHost;
            filter.Services = services; filter.PersistentState = state;
            host.SetActive(true);
            panel.SetActive(true);
            yield return null;
            filter.Refresh(context, spawn);
            yield return null;
            var info = typeof(MiniMapMarkUIFilter).GetField("mMarkInfos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(filter) as MiniMapFilterMarkInfo[];
            Require(info.Length == 201 && info.Count(row => row.ShowOnMapGroupId.Contains(13) || row.ShowOnMapClassId.Contains(13)) == 30, "All original 201 configuration rows and exact Map13 selection");
            var entryButton = typeof(MiniMapMarkUIFilter).GetField("mEntryBtn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(filter) as Button;
            Click(system, entryButton.gameObject);
            yield return null;
            var button = typeof(MiniMapMarkUIFilter).GetField("mButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(filter) as Button;
            Click(system, button.gameObject);
            yield return null;
            Require(state.State.SelectionState == MiniMapMarkUIFilterSetting.SelectionType.All && state.Writes == 1, "Original select-all updates and persists all mark rows");
            var items = typeof(MiniMapMarkUIFilter).GetField("mFilterItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(filter) as Dictionary<string, MiniMapMarkFilterItem>;
            Require(items.Count == 30 && services.Loaded == 29 && services.Created >= 32, "Original filter/entry pooled items and 29 unique original sprites");
            string key = items.Keys.First(name => !name.StartsWith("MiniMap", StringComparison.Ordinal));
            var item = items[key];
            Click(system, item.GetComponent<WorldTabItem>().gameObject);
            yield return null;
            Require(!item.Selected && state.State.SelectionState == MiniMapMarkUIFilterSetting.SelectionType.Free && state.Writes == 2, "Original individual click creates and persists free selection");
            Require(soundHost.Events > 0, "Original button click posts through the local sound host");
            var filterRect = typeof(MiniMapMarkUIFilter).GetField("mFilter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(filter) as GameObject;
            Vector2 inside = RectTransformUtility.WorldToScreenPoint(renderCamera, filterRect.GetComponent<RectTransform>().TransformPoint(filterRect.GetComponent<RectTransform>().rect.center));
            services.Pointer(inside);
            Require(filterRect.activeSelf, "Original filter remains open for clicks inside its full hit group");
            services.Pointer(new Vector2(-100, -100));
            var entry = typeof(MiniMapMarkUIFilter).GetField("mEntry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(filter) as GameObject;
            Require(!filterRect.activeSelf, "Outside click closes original filter panel");
            Require(entry.activeSelf, "Outside click restores original entry view");
            var target = new RenderTexture(720, 1280, 24, RenderTextureFormat.ARGB32); target.Create(); renderCamera.targetTexture = target;
            renderCamera.rect = new Rect(0, 0, 1, 1); renderCamera.Render(); var old = RenderTexture.active; RenderTexture.active = target; var image = new Texture2D(720, 1280, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0); image.Apply(); RenderTexture.active = old;
            int pixels = image.GetPixels32().Count(pixel => pixel.a > 0 && pixel.r + pixel.g + pixel.b > 40);
            Require(pixels > 10000, "Actual official filter UI rendering");
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"); File.WriteAllBytes(Path.Combine(workspace, "minimap-original-filter.png"), image.EncodeToPNG());
            Object.Destroy(image); renderCamera.targetTexture = null; Object.Destroy(target);
            Object.Destroy(panel); yield return null;
            Require(servicesHooks(services) == 0, "Original filter input hook unregistered on destroy");
            var reopenedHost = new GameObject("OriginalMap13FilterReentry", typeof(RectTransform)); reopenedHost.SetActive(false);
            reopenedHost.transform.SetParent(canvas.transform, false);
            var again = Object.Instantiate(prefab, reopenedHost.transform, false); var reopened = again.GetComponent<MiniMapMarkUIFilter>();
            labels.Bind(again); reopened.Services = services; reopened.PersistentState = state;
            reopenedHost.SetActive(true); again.SetActive(true); yield return null; reopened.Refresh(context, spawn); yield return null;
            var reopenedItems = typeof(MiniMapMarkUIFilter).GetField("mFilterItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(reopened) as Dictionary<string, MiniMapMarkFilterItem>;
            Require(reopenedItems[key] && !reopenedItems[key].Selected && state.State.SelectionState == MiniMapMarkUIFilterSetting.SelectionType.Free, "Original UI restores saved free selection after reopen");
            Object.Destroy(root); Object.Destroy(spawnObject); Object.Destroy(reopenedHost); Object.Destroy(canvas.gameObject); Object.Destroy(renderCamera.gameObject); Object.Destroy(system.gameObject); localResources.Dispose(); labels.Dispose(); OfficialWwiseEvent.Host = previousSoundHost; yield return null;
            var proof = new ProofData { editor = Application.unityVersion, config_rows = 201, map13_rows = 30, unique_sprites = services.UniqueSprites,
                pooled_items = services.Created, simulated_clicks = 3, state_writes = state.Writes, reopened_selection = true,
                rendered_pixels = pixels, actual_original_prefab = true, actual_original_pool = true, actual_event_system = true };
            File.WriteAllText(Path.Combine(workspace, "minimap-original-filter-proof.json"), JsonUtility.ToJson(proof, true));
            Debug.Log("MAP13_ORIGINAL_MINIMAP_FILTER_PASS");
        }

        private sealed class Performance : IMiniMapMarkPerformance
        { public void RegisterSpawnManager(MiniMapMarkSpawnManager manager) { } public void UnregisterSpawnManager(MiniMapMarkSpawnManager manager) { } }

        private static int servicesHooks(Services services)
        {
            var field = typeof(Services).GetField("hooks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return ((System.Collections.ICollection)field.GetValue(services)).Count;
        }

        [Serializable] private sealed class ProofData { public string editor; public int config_rows,map13_rows,unique_sprites,pooled_items,simulated_clicks,state_writes,rendered_pixels; public bool reopened_selection,actual_original_prefab,actual_original_pool,actual_event_system; }
        [Serializable] private sealed class PersistenceProof { public string slot; public int writes; public bool coldRead, corruptRejected; }
    }
}
