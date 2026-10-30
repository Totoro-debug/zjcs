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
            string output = Path.Combine(validation, "map13-runtime-acceptance-render");
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "entry.png"));
            Debug.Log("Map13 runtime fixture entity count: " + view.EntityCount + ".");
            yield return VerifyStoryMovementLock(runtime, view);
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

        private static IEnumerator VerifyStoryMovementLock(OfficialMainlineMapRuntime runtime,
            OfficialMap10EntityView entities)
        {
            object navigation = typeof(OfficialMainlineMapRuntime)
                .GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime);
            if (navigation == null) throw new MissingFieldException("The Map13 navigation module is missing.");
            Type navigationType = navigation.GetType();
            FieldInfo gridField = navigationType.GetField("currentGrid", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo sessionField = typeof(OfficialMainlineMapRuntime)
                .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic);
            var session = (OfficialWorldMapSession)sessionField?.GetValue(runtime);
            MethodInfo gridToWorld = typeof(OfficialMainlineMapRuntime).GetMethod("GridToWorld",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo processPress = navigationType.GetMethod("ProcessMapPointerPress",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo lockMovement = navigationType.GetMethod("LockPlayerMovementForStory",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo unlockMovement = navigationType.GetMethod("UnlockPlayerMovementForStory",
                BindingFlags.Instance | BindingFlags.Public);
            if (gridField == null || session == null || gridToWorld == null || processPress == null
                || lockMovement == null || unlockMovement == null)
                throw new MissingMethodException("Map13 story movement lock bindings are incomplete.");

            MapGridPoint start = (MapGridPoint)gridField.GetValue(navigation);
            MapGridPoint target = default;
            Vector2 screen = Vector2.zero;
            bool found = false;
            foreach (MapGridPoint offset in new[]
                { new MapGridPoint(1, 0), new MapGridPoint(-1, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1) })
            {
                MapGridPoint candidate = start + offset;
                if (!session.Movement.IsFit(candidate) || !session.Movement.CanMoveToNext(start, candidate)) continue;
                Vector3 world = (Vector3)gridToWorld.Invoke(runtime, new object[] { candidate });
                Vector3 screenPosition = runtime.GetComponentInChildren<Camera>().WorldToScreenPoint(world);
                screen = new Vector2(screenPosition.x, screenPosition.y);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                if (hits.Count != 0 || Physics2D.OverlapPoint(world) != null || entities.EntityInfos.Any(value => value.Active
                    && value.Grid[0] == candidate.X && value.Grid[1] == candidate.Y)) continue;
                target = candidate;
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("No free adjacent Map13 tile is available for the movement-lock check.");

            lockMovement.Invoke(navigation, null);
            runtime.PointerPressProvider = () => screen;
            processPress.Invoke(navigation, null);
            runtime.PointerPressProvider = () => null;
            if (!((MapGridPoint)gridField.GetValue(navigation)).Equals(start))
            {
                unlockMovement.Invoke(navigation, null);
                throw new InvalidDataException("WorldStopMove allowed a simulated world press to move the player.");
            }

            unlockMovement.Invoke(navigation, null);
            runtime.PointerPressProvider = () => screen;
            processPress.Invoke(navigation, null);
            runtime.PointerPressProvider = () => null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!((MapGridPoint)gridField.GetValue(navigation)).Equals(target)
                && Time.realtimeSinceStartup < deadline) yield return null;
            if (!((MapGridPoint)gridField.GetValue(navigation)).Equals(target))
                throw new InvalidDataException("Unlocking story movement did not restore normal map navigation.");
            Debug.Log("Map13 WorldStopMove passed: locked world press was ignored and navigation resumed after unlock.");
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
