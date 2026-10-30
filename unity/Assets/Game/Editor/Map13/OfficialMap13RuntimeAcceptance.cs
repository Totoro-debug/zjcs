using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.World;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13RuntimeAcceptance
    {
        private const string Key = "Zjcs.Map13.RuntimeAcceptance";
        private static bool started;

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }

        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 runtime acceptance timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13RuntimeAcceptanceRunner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 mainline runtime entry passed: official map payloads, 2690 entity placements, native player model and isolated local save loaded.");
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator VerifySteps()
        {
            string validation = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(validation) || !Directory.Exists(validation))
                throw new InvalidDataException("Set ZJCS_VALIDATION_DIR to this validation session directory.");
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(value => value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            {
                startup.StopAllCoroutines();
                UnityEngine.Object.Destroy(startup.gameObject);
            }
            if (EventSystem.current == null) new GameObject("Map13RuntimeEventSystem", typeof(EventSystem));
            var canvas = new GameObject("Map13RuntimeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            string root = Path.Combine(validation, "map13-runtime-acceptance-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "泽之国入口验证";
            save.Player.InitialWeaponType = 200;
            save.Player.Level = 90;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[90];
            save.Player.Rank = 1;
            save.Player.SubRank = 12;
            save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 13, Count = 10000 });
            save.Player.UnlockedMapClassIds.AddRange(new[] { 11, 12, 13 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var progression = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            new OfflineCharacterProgressionService(progression, OfficialCharacterAssetCatalog.LoadDefault().Load())
                .InitializeWardrobe("map13-runtime-acceptance-wardrobe");
            GameObject avatar = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
            var runtime = new GameObject("Map13Runtime").AddComponent<OfficialMainlineMapRuntime>();
            runtime.MapMusicVolume = () => 0f;
            runtime.MapVoiceVolume = () => 0f;
            runtime.Begin(13, slot, root, canvas.transform, avatar, name, null);
            yield return new WaitForSecondsRealtime(2f);
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            if (view == null || view.EntityCount != 2690)
                throw new InvalidDataException("Map13 runtime did not create the official 2690-entity view.");
            if (runtime.transform.GetComponentsInChildren<Camera>(true).Length == 0)
                throw new InvalidDataException("Map13 runtime did not create a world camera.");
            VerifyWaterAwareMovement(runtime);
            string output = Path.Combine(validation, "map13-runtime-acceptance-render");
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "entry.png"));
            Debug.Log("Map13 runtime fixture entity count: " + view.EntityCount + ".");
            yield return VerifyStoryMovementLock(runtime);
            bool shopClosed = false;
            MethodInfo openShop = typeof(OfficialMainlineMapRuntime).GetMethod("OpenMap13NationShop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (openShop == null) throw new MissingMethodException("Map13 nation-shop runtime entry is missing.");
            openShop.Invoke(runtime, new object[] { (Action)(() => shopClosed = true) });
            GameObject shop = GameObject.Find("MapNationShopPanel");
            if (shop == null) throw new InvalidDataException("The official Map13 nation-shop panel did not open.");
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "nation-shop.png"));
            string[] scrollState = shop.GetComponentsInChildren<LoopVerticalScrollRect>(true)
                .Select(value => value.name + ":active=" + value.isActiveAndEnabled + ",total=" + value.totalCount
                    + ",cells=" + (value.content == null ? -1 : value.content.childCount)
                    + ",view=" + (value.viewport == null ? "none" : value.viewport.rect.ToString())
                    + ",contentRect=" + (value.content == null ? "none" : value.content.rect.ToString())
                    + ",children=" + (value.content == null ? string.Empty : string.Join("|", Enumerable.Range(0, value.content.childCount)
                        .Select(index =>
                        {
                            RectTransform cell = value.content.GetChild(index) as RectTransform;
                            Vector2 screen = cell == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(null, cell.TransformPoint(cell.rect.center));
                            return value.content.GetChild(index).name + "/" + value.content.GetChild(index).gameObject.activeInHierarchy
                                + "/pos=" + (cell == null ? "none" : cell.anchoredPosition.ToString())
                                + "/size=" + (cell == null ? "none" : cell.rect.size.ToString()) + "/screen=" + screen;
                        }))))
                .ToArray();
            Debug.Log("Map13 nation-shop list state: " + string.Join(";", scrollState));
            if (shop.GetComponentsInChildren<CanvasRenderer>(true).Length == 0
                || GameObject.Find("ShopGoodsItem_32101") == null)
                throw new InvalidDataException("The official Map13 shop goods were not rendered. Lists=" + string.Join(";", scrollState));
            WorldItemIcon firstItem = GameObject.Find("ShopGoodsItem_32101").GetComponentInChildren<WorldItemIcon>(true);
            if (firstItem == null || firstItem.Icon == null || firstItem.QualityImg == null)
                throw new InvalidDataException("The official Map13 shop item icon binding is missing.");
            Debug.Log("Map13 shop item icon state: " + firstItem.Icon.sprite?.name + "/" + firstItem.Icon.enabled
                + "/" + firstItem.Icon.gameObject.activeInHierarchy + "/" + firstItem.Icon.rectTransform.rect
                + "; quality=" + firstItem.QualityImg.sprite?.name + "/" + firstItem.QualityImg.enabled
                + "/" + firstItem.QualityImg.gameObject.activeInHierarchy + "; count=" + firstItem.myText?.text);
            RectTransform iconRect = firstItem.Icon.rectTransform;
            Vector3[] iconCorners = new Vector3[4];
            iconRect.GetWorldCorners(iconCorners);
            Debug.Log("Map13 shop icon render bounds: " + GetPath(firstItem.Icon.transform) + "; screen="
                + string.Join("|", iconCorners.Select(value => RectTransformUtility.WorldToScreenPoint(null, value)))
                + "; cull=" + firstItem.Icon.canvasRenderer.cull + "; material=" + firstItem.Icon.material?.name
                + "; texture=" + firstItem.Icon.sprite.texture.name + "; slot="
                + GameObject.Find("ShopGoodsItem_32101/Content/ImgBg/ItemInfoSlot").GetComponent<RectTransform>().rect);
            foreach (int goodId in new[] { 32101, 32102, 32103, 32104 })
            {
                GameObject good = GameObject.Find("ShopGoodsItem_" + goodId);
                Button buy = good == null ? null : good.transform.Find("Content/ImgBg/BuyBtn")?.GetComponent<Button>();
                if (buy == null || !buy.interactable) throw new InvalidDataException("Map13 shop purchase button is unavailable: " + goodId);
                ClickOfficialButton(buy);
                yield return null;
            }
            OfflineMainlineProgressSession coldProfile = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
            OfflineCommerceSession coldCommerce = OfflineCommerceSession.Open(slots, slot, DateTime.Now, coldProfile);
            var coldShop = new OfflineNationShopService(OfficialNationShopCatalog.LoadDefault(), coldCommerce);
            if (coldProfile.InventoryCount(13) != 6000 || coldProfile.InventoryCount(110) != 2
                || coldShop.Experience(13) != 4000 || coldShop.Level(13) != 2 || coldShop.PurchasedCount(32101) != 1)
                throw new InvalidDataException("Map13 nation-shop purchase, level-up or cold-save state did not persist.");
            WorldTabItem levelTwo = shop.GetComponentsInChildren<WorldTabItem>(true)
                .FirstOrDefault(tab => tab.name.Contains("ShopLevelTabItem_2", StringComparison.Ordinal));
            if (levelTwo == null) throw new InvalidDataException("The official Map13 shop level-2 tab was not rendered.");
            ClickOfficialTab(levelTwo);
            yield return null;
            if (!levelTwo.isOn || GameObject.Find("ShopGoodsItem_32201") == null)
                throw new InvalidDataException("Selecting the official Map13 level-2 tab did not refresh its goods.");
            Button close = shop.transform.Find("BgPanel1/Content/BottomGo/CloseBtn_dark")?.GetComponent<Button>();
            if (close == null) throw new InvalidDataException("The official Map13 nation-shop close button is missing.");
            ClickOfficialButton(close);
            yield return null;
            if (!shopClosed || GameObject.Find("MapNationShopPanel") != null)
                throw new InvalidDataException("Closing the Map13 nation shop did not resume the story node.");
            Debug.Log("Map13 nation-shop Play Mode passed: official panel and level tabs loaded, simulated item purchases crossed level 2, and inventory/currency/stock/experience survived cold reopen.");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Object.Destroy(canvas);
        }

        private static void VerifyWaterAwareMovement(OfficialMainlineMapRuntime runtime)
        {
            object navigation = typeof(OfficialMainlineMapRuntime)
                .GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime);
            if (navigation == null) throw new MissingFieldException("The Map13 navigation module is missing.");
            Type navigationType = navigation.GetType();
            FieldInfo gridField = navigationType.GetField("currentGrid", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo pathField = navigationType.GetField("movementPath", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo startMovement = navigationType.GetMethod("StartMovement", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo findPath = typeof(OfficialMainlineMapRuntime).GetMethod("FindMainlinePath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object navigationSession = typeof(OfficialMainlineMapRuntime)
                .GetField("map13NavigationSession", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime);
            if (gridField == null || pathField == null || startMovement == null || findPath == null || navigationSession == null)
                throw new MissingMethodException("Map13 water-aware navigation bindings are incomplete.");

            MapGridPoint start = (MapGridPoint)gridField.GetValue(navigation);
            OfficialWorldMapSession hostSession = (OfficialWorldMapSession)typeof(OfficialMainlineMapRuntime)
                .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime);
            MethodInfo createGroundProbe = navigationSession.GetType().GetMethod("CreateGround");
            object waterGroundProbe = createGroundProbe.Invoke(navigationSession, new object[] { 65, null, 0 });
            MethodInfo isFitProbe = waterGroundProbe.GetType().GetMethod("IsFit", new[] { typeof(MapGridPoint) });
            int entryId = (int)typeof(OfficialMainlineMapRuntime).GetMethod("GetDefaultEntryId",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
            OfficialMap10EntityInfo entry = runtime.GetComponentInChildren<OfficialMap10EntityView>()
                .EntityInfos.Single(value => value.PlacementId == entryId);
            if (entry.Grid[0] != start.X || entry.Grid[1] != start.Y || !hostSession.Movement.IsFit(start)
                || !(bool)isFitProbe.Invoke(waterGroundProbe, new object[] { start }))
                throw new InvalidDataException("A new Map13 profile must start at its original passable world entry.");
            System.Collections.Generic.List<MapGridPoint> expected = null;
            MapGridPoint target = default;
            for (int radius = 1; radius <= 20 && expected == null; radius++)
                for (int y = -radius; y <= radius && expected == null; y++)
                    for (int x = -radius; x <= radius && expected == null; x++)
                    {
                        if (Math.Abs(x) != radius && Math.Abs(y) != radius) continue;
                        target = new MapGridPoint(start.X + x, start.Y + y);
                        if (target.X < 0 || target.Y < 0 || target.X >= 240 || target.Y >= 240) continue;
                        var request = new OfficialMapPathRequest
                        {
                            Start = start, End = target, PreciseTarget = true,
                            BodyOffsets = new[] { new MapGridPoint(0, 0) }
                        };
                        expected = (System.Collections.Generic.List<MapGridPoint>)findPath.Invoke(runtime, new object[] { request });
                        if (expected == null || expected.Count < 2) expected = null;
                    }
            if (expected == null) throw new InvalidDataException("No nearby reachable Map13 grid exists for movement verification.");

            startMovement.Invoke(navigation, new object[] { target, null });
            var actual = pathField.GetValue(navigation) as System.Collections.Generic.List<MapGridPoint>;
            if (actual == null || !actual.SequenceEqual(expected))
                throw new InvalidDataException("Map13 player movement did not use the water-aware official AStar route.");
            MethodInfo createGround = navigationSession.GetType().GetMethod("CreateGround");
            object waterGround = createGround.Invoke(navigationSession, new object[] { 65, null, 0 });
            MethodInfo isFit = waterGround.GetType().GetMethod("IsFit", new[] { typeof(MapGridPoint) });
            if (actual.Skip(1).Any(point => !(bool)isFit.Invoke(waterGround, new object[] { point })))
                throw new InvalidDataException("Map13 player path crossed a blocked water body.");
            navigationType.GetMethod("StopMovement", BindingFlags.Instance | BindingFlags.Public).Invoke(navigation, null);
            Debug.Log("Map13 runtime movement passed: the shared player movement command used the original AStar path with saved water overlays.");
        }

        private static IEnumerator VerifyStoryMovementLock(OfficialMainlineMapRuntime runtime)
        {
            object navigation = typeof(OfficialMainlineMapRuntime)
                .GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime);
            if (navigation == null) throw new MissingFieldException("The Map13 navigation module is missing.");
            Type navigationType = navigation.GetType();
            FieldInfo gridField = navigationType.GetField("currentGrid", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo processPress = navigationType.GetMethod("ProcessMapPointerPress",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo lockMovement = navigationType.GetMethod("LockPlayerMovementForStory",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo unlockMovement = navigationType.GetMethod("UnlockPlayerMovementForStory",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo finishStoryGraph = typeof(OfficialMainlineMapRuntime).GetMethod("FinishStoryGraph",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            FieldInfo lockField = navigationType.GetField("storyMovementLocked", BindingFlags.Instance | BindingFlags.Public);
            if (gridField == null || processPress == null || lockMovement == null || unlockMovement == null
                || finishStoryGraph == null || lockField == null)
                throw new MissingMethodException("Map13 story movement lock bindings are incomplete: grid="
                    + (gridField != null) + ", processPress=" + (processPress != null) + ", lock=" + (lockMovement != null)
                    + ", unlock=" + (unlockMovement != null) + ", finishStoryGraph=" + (finishStoryGraph != null)
                    + ", state=" + (lockField != null) + ".");

            MapGridPoint start = (MapGridPoint)gridField.GetValue(navigation);
            Camera worldCamera = runtime.GetComponentInChildren<Camera>();
            GameObject avatar = (GameObject)typeof(OfficialMainlineMapRuntime)
                .GetField("avatar", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime);
            Vector3 avatarScreen = avatar == null ? Vector3.zero : worldCamera.WorldToScreenPoint(avatar.transform.position);
            Vector2 screen = avatarScreen.z > 0 ? new Vector2(avatarScreen.x, avatarScreen.y) : new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            lockMovement.Invoke(navigation, null);
            if (!(bool)lockField.GetValue(navigation))
                throw new InvalidDataException("WorldStopMove did not set the player movement lock.");
            runtime.PointerPressProvider = () => screen;
            processPress.Invoke(navigation, null);
            runtime.PointerPressProvider = () => null;
            if (!((MapGridPoint)gridField.GetValue(navigation)).Equals(start))
            {
                unlockMovement.Invoke(navigation, null);
                throw new InvalidDataException("WorldStopMove allowed a simulated world press to move the player.");
            }

            finishStoryGraph.Invoke(runtime, new object[] { false });
            if ((bool)lockField.GetValue(navigation))
                throw new InvalidDataException("Finishing the story did not release the player movement lock.");
            Debug.Log("Map13 WorldStopMove passed: story lock blocked the simulated map press and story completion released it.");
            yield return null;
        }

        private static void ClickOfficialButton(Button button)
        {
            Canvas canvas = button.GetComponentInParent<Canvas>();
            if (canvas == null || EventSystem.current == null)
                throw new InvalidOperationException("The official shop button has no active canvas or EventSystem.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
                    ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center))
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult hit = hits.FirstOrDefault(value => value.gameObject.transform == button.transform
                || value.gameObject.transform.IsChildOf(button.transform));
            if (hit.gameObject == null)
                throw new InvalidOperationException("The simulated mouse position did not hit official button " + button.name
                    + "; mode=" + canvas.renderMode + ",screen=" + Screen.width + "x" + Screen.height
                    + ",pointer=" + pointer.position + ",buttonRect=" + ((RectTransform)button.transform).rect
                    + ",buttonWorld=" + ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center)
                    + ",raycasts=" + string.Join(",", hits.Select(value => GetPath(value.gameObject.transform))));
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static string GetPath(Transform value)
        {
            string path = value.name;
            while (value.parent != null)
            {
                value = value.parent;
                path = value.name + "/" + path;
            }
            return path;
        }

        private static void ClickOfficialTab(WorldTabItem tab)
        {
            Canvas canvas = tab.GetComponentInParent<Canvas>();
            if (canvas == null || EventSystem.current == null)
                throw new InvalidOperationException("The official shop tab has no active canvas or EventSystem.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
                    ((RectTransform)tab.transform).TransformPoint(((RectTransform)tab.transform).rect.center))
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult hit = hits.FirstOrDefault(value => value.gameObject.transform.IsChildOf(tab.transform));
            if (hit.gameObject == null)
                throw new InvalidOperationException("The simulated mouse position did not hit official level tab " + tab.name + ".");
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
