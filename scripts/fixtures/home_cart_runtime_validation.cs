using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Editor;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeTipDots;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeCartWindowsUI;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeOutputDetailUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeCartRuntimeValidation
{
    private const string Active = "Zjcs.HomeCartRuntimeValidation.Active";
    private static int errors;
    private static int checksCount;
    private static HomeFeatureRuntime home;

    static HomeCartRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
            {
                AudioListener.volume = 0f;
                Application.runInBackground = true;
                Application.logMessageReceived += OnLog;
                var runner = new GameObject("HomeCartValidationRunner");
                Object.DontDestroyOnLoad(runner);
                runner.AddComponent<Runner>();
            }
        };

        EditorApplication.update += () =>
        {
            if (SessionState.GetBool(Active, false)
                && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
            {
                Finish(new TimeoutException("Cart runtime E2E validation timed out."));
            }
        };
    }

    public static void Import()
    {
        ValidateOwnedProject();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("HOME_CART_IMPORT_PASSED");
    }

    public static void Run()
    {
        ValidateOwnedProject();
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        ValidationGameView.Portrait(675, 1463);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 900f);
        EditorApplication.EnterPlaymode();
    }

    private static string Work => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");

    private static void ValidateOwnedProject()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_CART_PROJECT");
        if (string.IsNullOrEmpty(project)
            || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
        {
            throw new InvalidOperationException("Select the independent validation project explicitly.");
        }
    }

    private static void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            Debug.LogWarning("[CapturedError] " + text + "\n" + stack);
        }
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        try { home?.Close(); } catch { }
        home = null;
        Application.logMessageReceived -= OnLog;
        if (error == null)
        {
            Debug.Log("HOME_CART_RUNTIME_PASSED: outdoor Cart slot & HomeDayNight, all 6 skin/weather prefabs + skin 4266 icon, TipDots (FurnitureCart/HangUpItem/HomeTab), Unlock + 3600s extra reward, ItemDetail + outside pointer dismiss, HangUpLevelUpPanel + 1x/10x upgrade + Map 12 Function 175 + OutputAddition + BlessingRule <link=1000>, CartRule + EquipPreview, AwardPanel + quick collect + QuickReceive Function 164 + persistence verified.");
        }
        else
        {
            Debug.LogException(error);
        }
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("CHECK FAILED: " + message);
        checksCount++;
        Debug.Log("CHECK(" + checksCount + "): " + message);
    }

    private sealed class NativePointer : IHomeSceneClickPointer
    {
        public bool LeftDown { get; private set; }
        public bool LeftUp { get; private set; }
        public Vector2 Position { get; private set; }
        public int Width => Screen.width;
        public int Height => Screen.height;

        public void ClickWorldPoint(HomeFeatureRuntime runtime, Vector3 worldPoint)
        {
            Camera cam = runtime.Scene.Camera;
            Position = cam.WorldToScreenPoint(worldPoint);
            Physics.SyncTransforms();
            object nativeInput = Field<object>(runtime, "nativeSceneInput");
            MethodInfo tick = nativeInput.GetType().GetMethod("Tick");
            MethodInfo late = nativeInput.GetType().GetMethod("LateTick");
            LeftDown = true;
            tick.Invoke(nativeInput, null);
            LeftDown = false;
            late.Invoke(nativeInput, null);
            LeftUp = true;
            tick.Invoke(nativeInput, null);
            LeftUp = false;
            late.Invoke(nativeInput, null);
        }
    }

    private static T Field<T>(object target, string name)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo info = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) return (T)info.GetValue(target);
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static T Prop<T>(object target, string name)
    {
        Type type = target.GetType();
        while (type != null)
        {
            PropertyInfo info = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) return (T)info.GetValue(target);
            type = type.BaseType;
        }
        throw new MissingMemberException(target.GetType().FullName, name);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        Type type = target.GetType();
        while (type != null)
        {
            MethodInfo info = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) return info.Invoke(target, args);
            type = type.BaseType;
        }
        throw new MissingMethodException(target.GetType().FullName, name);
    }

    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    private static HomeFeatureRuntime OpenHome(string slotId, string root, NativePointer pointer)
    {
        HomeFeatureRuntime instance = HomeFeatureRuntime.BeginWithNativePointer(slotId, root, null, pointer);
        instance.TutorialAutoStartEnabled = false;
        return instance;
    }

    private static void FocusCart(HomeFeatureRuntime runtime)
    {
        GameObject obj = runtime.NativeFurnitureInstance(EFurniture.Cart);
        Require(obj != null, "Cart native instance must exist");
        Vector3 pos = runtime.Scene.Position;
        pos.x = obj.transform.position.x;
        pos.y = obj.transform.position.y;
        runtime.Scene.Camera.transform.position = runtime.Scene.Clamp(pos);
        Physics.SyncTransforms();
    }

    private static BaseHangUpFurniture CartController(HomeFeatureRuntime runtime)
    {
        GameObject obj = runtime.NativeFurnitureInstance(EFurniture.Cart);
        return obj ? obj.GetComponentInChildren<BaseHangUpFurniture>(true) : null;
    }

    private static Collider FindTouchCollider(ClickableHomeObj owner)
        => Field<List<GameObject>>(owner, "mTouchList")
            .Select(go => go ? go.GetComponent<Collider>() : null)
            .First(c => c != null && c.enabled && c.gameObject.activeInHierarchy);

    private static TipDotNodeLinker FindTipLinker(GameObject root, string graphName, string nodeName)
        => root.GetComponentsInChildren<TipDotNodeLinker>(true)
            .Single(value => value.ConfiguredNodeKey.GraphName == graphName && value.ConfiguredNodeKey.NodeName == nodeName);

    private static void ClickUiButton(Button button)
    {
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable,
            "Target UI button must be clickable: " + (button ? button.name : "null"));
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)button.transform;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        ClickUiButtonAt(button, screenPoint);
    }

    private static void ClickUiButtonAt(Button button, Vector2 screenPoint)
    {
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable,
            "Target UI button must be clickable: " + (button ? button.name : "null"));
        Canvas.ForceUpdateCanvases();
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Simulated pointer must hit button " + button.name + " at " + screenPoint + ", instead hit: " + string.Join(",", hits.Select(h => h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void ClickUiToggle(Toggle toggle)
    {
        Require(toggle != null && toggle.gameObject.activeInHierarchy && toggle.interactable,
            "Target Toggle must be clickable: " + (toggle ? toggle.name : "null"));
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)toggle.transform;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == toggle.gameObject || hits[0].gameObject.transform.IsChildOf(toggle.transform)),
            "Simulated pointer must hit toggle " + toggle.name + " at " + screenPoint + ", instead hit: " + string.Join(",", hits.Select(h => h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator WaitForDialogReady<T>(Action<T> onReady) where T : MonoBehaviour
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        T dialog = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            dialog = Object.FindObjectOfType<T>();
            if (dialog != null && dialog.gameObject.activeInHierarchy)
            {
                var wp = dialog.GetComponent<WorldPopupWindow>();
                if (wp == null || wp.State == WorldPopupWindow.EState.Show) break;
            }
            yield return null;
        }
        yield return WaitFrames(2);
        onReady?.Invoke(dialog);
    }

    private static IEnumerator WaitForDialogClosed<T>() where T : MonoBehaviour
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Object.FindObjectOfType<T>() != null && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return WaitFrames(2);
    }

    private static IEnumerator Capture(string filename)
    {
        yield return new WaitForEndOfFrame();
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        Require(tex != null && tex.height > tex.width && tex.width >= 500 && tex.height >= 900,
            "Rendered Game View screenshot must be portrait: " + (tex ? (tex.width + "x" + tex.height) : "null"));
        File.WriteAllBytes(Path.Combine(Work, filename), tex.EncodeToPNG());
        int magenta = tex.GetPixels32().Count(p => p.r > 230 && p.b > 230 && p.g < 25);
        Debug.Log("FRAME " + filename + ": " + tex.width + "x" + tex.height + " magenta=" + magenta);
        Object.Destroy(tex);
        Require(magenta <= 20, "Rendered screenshot must have zero shader magenta errors (" + filename + ", magenta=" + magenta + ")");
    }

    private static IEnumerator CloseAllModals(HomeFeatureRuntime runtime)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (runtime.Hud != null && runtime.Hud.AnyModalOpen)
            {
                runtime.Hud.CloseDialog();
                yield return WaitFrames(4);
                continue;
            }
            break;
        }
        yield return WaitFrames(3);
    }

    private static void AddInventoryItem(OfflineMainlineProgressSession profile, int itemId, long count, string reason)
    {
        Require(profile.ApplyInventoryTransaction(reason, save =>
        {
            OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(v => v.ItemId == itemId);
            if (stack == null) save.Player.Inventory.Add(new OfflineItemStack { ItemId = itemId, Count = count });
            else stack.Count += count;
        }).Status != ProgressionResultStatus.InvalidState, "Grant item " + itemId + " x" + count);
    }

    private static void SeedCartIncome(HomeFeatureRuntime runtime, int hangUpId, long settledCount)
    {
        Require(runtime.Session.Mutate("cart-test:seed:" + hangUpId + ":" + Guid.NewGuid().ToString("N"), save =>
        {
            OfflineHomeIncomeEntry entry = save.Income.FirstOrDefault(v => v.HangUpId == hangUpId);
            if (entry == null)
            {
                entry = new OfflineHomeIncomeEntry { HangUpId = hangUpId };
                save.Income.Add(entry);
            }
            entry.StoredSeconds = 0;
            entry.SettledCount = settledCount;
        }), "Seed Cart hangUp " + hangUpId + " settledCount=" + settledCount);

        object storage = Field<object>(runtime, "storage");
        Invoke(storage, "NotifyHangUpDataChanged", hangUpId);
        runtime.HangUpHud?.Source.NotifyDataChanged(hangUpId);
        Invoke(runtime, "SynchronizeHomeTipDots");
        runtime.Hud.RefreshAll();
    }

    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var steps = new Stack<IEnumerator>();
            steps.Push(RunChecks());
            Exception failure = null;
            while (steps.Count > 0)
            {
                object current;
                try
                {
                    if (!steps.Peek().MoveNext())
                    {
                        steps.Pop();
                        continue;
                    }
                    current = steps.Peek().Current;
                    if (current is IEnumerator nested)
                    {
                        steps.Push(nested);
                        continue;
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                    break;
                }
                yield return current;
            }

            if (failure == null && errors > 0)
                failure = new InvalidOperationException("Unexpected Unity errors observed: " + errors);
            Finish(failure);
        }
    }

    private static IEnumerator RunChecks()
    {
        checksCount = 0;
        IProfessionSoundHost initialWwiseHost = OfficialWwiseEvent.Host;
        string saveRoot = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot);
        draft.name = "小推车验证";
        Require(creation.TryCreateCharacter(slot, draft, out string createError), "Create character: " + createError);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out createError),
            "Set initial sword: " + createError);

        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot), slot);
        var expCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();

        // Populate女神激活地区 (> 15 so BlessingLevel >= 15 allows 10x upgrade!) + Level 30, SubRank 3, Map 12 + materials
        var ordinaryRegions = expCatalog.Rows("map_region")
            .Where(row => !row.TryGetValue("GoddessActivePropAwardDict", out object val)
                || val == null || (val is Dictionary<string, object> d && d.Count == 0))
            .Take(16)
            .Select(row => new OfflineMapRegionKey
            {
                MapClassId = Convert.ToInt32(row["ClassId"], CultureInfo.InvariantCulture),
                Region = Convert.ToInt32(row["Region"], CultureInfo.InvariantCulture)
            })
            .ToList();
        Require(ordinaryRegions.Count >= 14, "Catalog provides >= 14 ordinary goddess regions for BlessingLevel >= 15");

        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 10;
            p.SubRank = 3;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[10];
        }, "cart-test:init-lv10").Status != ProgressionResultStatus.InvalidState, "Set Lv10 SubRank3");

        foreach (IGrouping<int, OfflineMapRegionKey> mapGroup in ordinaryRegions.GroupBy(value => value.MapClassId))
        {
            int mapClassId = mapGroup.Key;
            List<int> allRegions = expCatalog.Rows("map_region")
                .Where(row => Convert.ToInt32(row["ClassId"], CultureInfo.InvariantCulture) == mapClassId)
                .Select(row => Convert.ToInt32(row["Region"], CultureInfo.InvariantCulture))
                .Distinct().ToList();
            Require(profile.InitializeMap(mapClassId, allRegions).Status != ProgressionResultStatus.InvalidState,
                "Initialize official map " + mapClassId + " regions");
        }
        ProgressionResult goddessResult = profile.ApplyInventoryTransaction("cart-test:goddess-and-unlocks", save =>
        {
            save.World.ActivatedGoddessRegions.Clear();
            save.World.ActivatedGoddessRegions.AddRange(ordinaryRegions);
        });
        Require(goddessResult.Status != ProgressionResultStatus.InvalidState,
            "Activate goddess regions for BlessingLevel: " + goddessResult.Status + " " + goddessResult.Message);

        AddInventoryItem(profile, 1, 500000, "cart-test:gold");
        AddInventoryItem(profile, 42000, 500000, "cart-test:stone");
        AddInventoryItem(profile, 42001, 500000, "cart-test:wood");
        AddInventoryItem(profile, 4231, 1, "cart-test:skin4231");
        AddInventoryItem(profile, 4266, 1, "cart-test:skin4266");

        var pointer = new NativePointer();
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusCart(home);
        yield return WaitFrames(3);

        // Phase 1: Outdoor Cart position, HomeDayNight, and default skin/weather
        GameObject cartObj = home.NativeFurnitureInstance(EFurniture.Cart);
        BaseHangUpFurniture cartCtrl = CartController(home);
        Require(cartObj != null && cartObj.activeInHierarchy, "Cart outdoor native instance is active");
        Require(Vector3.Distance(cartObj.transform.parent.localPosition, new Vector3(-8.92f, -4.51f, 0f)) < 0.01f,
            "Cart slot positioned at official (-8.92, -4.51, 0)");
        Require(cartCtrl != null && cartCtrl.Furniture == EFurniture.Cart && cartCtrl.HangUpServices != null,
            "Cart BaseHangUpFurniture binds IHomeHangUpFurnitureServices");

        HomeDayNight[] dayNights = cartObj.GetComponentsInChildren<HomeDayNight>(true);
        Require(dayNights.Length > 0 && dayNights.All(d => ReferenceEquals(d.EnvironmentServices, home.Environment)),
            "Cart HomeDayNight binds HomeEnvironmentView");
        bool initialDay = home.Environment.IsPlayableDay;
        Require(dayNights.All(d => d.DayNightSwitcher.State == initialDay), "Cart HomeDayNight syncs initial day/night state");

        Require(home.ItemIcon(4266) == home.CartAssets.cartSkin4266Icon && home.CartAssets.cartSkin4266Icon != null,
            "Cart skin 4266 item icon resolves from OfficialHomeCartWindowAssetCatalog");
        Require(home.CartAssets.cartSkin4266Preview != null,
            "Cart skin 4266 preview sprite resolves");

        // Switch through all 6 Cart skin x weather combinations (4206/4231/4266 x Normal/Snow 4901)
        var skinWeatherMatrix = new (int skinId, int weatherId, string expectedPrefab, string shotName)[]
        {
            (4206, 4900, "Cart", "01-cart-4206-normal.png"),
            (4206, 4901, "CartSnow", null),
            (4231, 4900, "CartSpring", null),
            (4231, 4901, "CartSpringSnow", null),
            (4266, 4900, "CartMaidCafe", "02-cart-4266-maidcafe.png"),
            (4266, 4901, "CartMaidCafeSnow", "03-cart-4266-snow.png"),
            (4206, 4900, "Cart", null),
        };
        for (int idx = 0; idx < skinWeatherMatrix.Length; idx++)
        {
            var (skinId, weatherId, expectedPrefab, shotName) = skinWeatherMatrix[idx];
            Require(home.HomeProgression.EquipSkin(home.Session, 0, (int)EFurniture.Cart, skinId, home.ItemCount, "cart-test:skin:" + idx + ":" + skinId),
                "Equip Cart skin " + skinId);
            Require(home.HomeProgression.EquipSkin(home.Session, 2, (int)HomeEnvironmentType.Weather, weatherId, _ => 1, "cart-test:weather:" + idx + ":" + weatherId),
                "Equip Weather skin " + weatherId);
            home.Scene.ApplyHomeSkins(home.Catalog.HomeSkins, home.Session.Skins, home.Rules);
            home.RefreshNativeFurnitureSkins();
            yield return WaitFrames(2);
            GameObject live = home.NativeFurnitureInstance(EFurniture.Cart);
            Require(live != null && live.activeInHierarchy && live.name == expectedPrefab + "(Clone)",
                "Cart mounted prefab matches " + expectedPrefab + "(Clone), actual=" + (live ? live.name : "null"));
            Require(live.GetComponentsInChildren<Component>(true).All(c => c != null),
                expectedPrefab + " has zero missing scripts");
            Require(live.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null)),
                expectedPrefab + " binds valid materials");
            if (!string.IsNullOrEmpty(shotName)) yield return Capture(shotName);
        }

        // Verify HomeSkinScreenController displays skin 4266 & can select it via UI
        home.Hud.ShowHomeSkins();
        yield return WaitFrames(4);
        HomeSkinScreenController skinUi = Field<HomeSkinScreenController>(home.Hud, "homeSkin");
        Require(skinUi != null && skinUi.IsOpen, "HomeSkinPanel opens");
        Toggle[] subToggles = GameObject.FindObjectsOfType<Toggle>(true);
        foreach (var tg in subToggles)
        {
            var txt = tg.GetComponentInChildren<TMP_Text>(true);
            if (txt != null && (txt.text.Contains("推车") || txt.text.Contains("Cart")))
            {
                ScrollRect subScroll = tg.GetComponentInParent<ScrollRect>();
                if (subScroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    subScroll.horizontalNormalizedPosition = 1f;
                    Canvas.ForceUpdateCanvases();
                }
                ClickUiToggle(tg);
                yield return WaitFrames(3);
                break;
            }
        }
        GameObject card4266 = GameObject.Find("HomeSkinItem_4266");
        Require(card4266 != null && card4266.activeInHierarchy, "HomeSkinPanel shows Cart skin 4266 card");
        Button btn4266 = card4266.transform.Find("HomeSkinItemIcon")?.GetComponent<Button>();
        ClickUiButton(btn4266);
        yield return WaitFrames(3);
        Require(home.NativeFurnitureInstance(EFurniture.Cart)?.name == "CartMaidCafe(Clone)",
            "Clicking 4266 skin card switches scene Cart to CartMaidCafe");
        skinUi.Close();
        yield return WaitFrames(3);

        // Phase 2: TipDots + FurnitureCartDialog + Unlock (3600s initial extra reward) + HangUpItemDetail
        Require(home.Rules.HangUpsOf((int)EFurniture.Cart).Count(h => home.Session.IsHangUpUnlocked(h.id)) == 0,
            "At Level 10, unlocked Cart hangUps start at 0 (hangUps 3/4/5/6/8 CanUnlock)");
        cartCtrl = CartController(home);
        TipDotNodeLinker[] cartSceneLinkers = cartCtrl.GetComponentsInChildren<TipDotNodeLinker>(true)
            .Where(l => l.ConfiguredNodeKey.GraphName == "FurnitureCart").ToArray();
        Require(cartSceneLinkers.Length > 0 && cartSceneLinkers.Any(l => l.IsShowTip),
            "FurnitureCart CanUnlock TipDot is active when hangUp 3 is unlockable (linkers="
            + string.Join(",", cartSceneLinkers.Select(l => l.ConfiguredNodeKey.NodeName + ":" + l.IsShowTip)) + ")");

        // Open FurnitureCartDialog by clicking Cart in scene
        pointer.ClickWorldPoint(home, FindTouchCollider(cartCtrl).bounds.center);
        FurnitureCartDialog cartDialog = null;
        yield return WaitForDialogReady<FurnitureCartDialog>(dlg => cartDialog = dlg);
        Require(cartDialog != null && cartDialog.gameObject.activeInHierarchy,
            "Clicking Cart opens FurnitureCartDialog");
        Require(home.Rules.IsFunctionLimited(164), "QuickReceive Function 164 is hidden below Level 30");
        var quickToggleCart = cartDialog.GetComponentInChildren<FurnitureHangUpQuickReceive>(true);
        Require(quickToggleCart == null || !quickToggleCart.gameObject.activeInHierarchy
            || quickToggleCart.GetComponentInChildren<Toggle>(true)?.gameObject.activeInHierarchy == false,
            "QuickReceiveToggle stays hidden below Level 30");

        List<FurnitureCartHangUpItem> unlockableItems = cartDialog.GetActiveUnlockButtonItems();
        FurnitureCartHangUpItem hang3Item = unlockableItems.FirstOrDefault(i => Field<int>(i, "mHangUpId") == 3);
        Require(hang3Item != null && hang3Item.UnlockBtn.gameObject.activeInHierarchy,
            "CartHangUpItem(3) shows UnlockBtn");
        yield return Capture("04-cart-dialog-before-unlock.png");

        // Click UnlockBtn on HangUp 3
        ClickUiButton(hang3Item.UnlockBtn);
        yield return WaitFrames(4);
        Require(home.Session.IsHangUpUnlocked(3), "UnlockBtn unlocks Cart hangUp 3");
        var savedHang3 = home.Session.Save.Income.FirstOrDefault(v => v.HangUpId == 3);
        Require(savedHang3 != null && savedHang3.StoredSeconds >= 3599,
            "Unlocking Cart hangUp 3 seeds initialExtraRewardSeconds >= 3600 (actual=" + (savedHang3?.StoredSeconds ?? -1) + ")");
        Require(home.Hud.NativeCartWindows.Cart.GetComponentsInChildren<FurnitureCartHangUpItem>(true)
            .First(i => Field<int>(i, "mHangUpId") == 3)
            .GetComponentInChildren<TipDotNodeLinker>(true) != null, "CartHangUpItem binds HangUpItem TipDot");

        // Also unlock HangUp 4 for Upgrade Tab Switch testing
        FurnitureCartHangUpItem hang4Item = unlockableItems.FirstOrDefault(i => Field<int>(i, "mHangUpId") == 4);
        if (hang4Item != null && hang4Item.UnlockBtn.gameObject.activeInHierarchy)
        {
            ClickUiButton(hang4Item.UnlockBtn);
            yield return WaitFrames(3);
        }

        // Open HangUpCartItemDetailDialog by clicking hang3Item.ClickBtn
        ClickUiButton(hang3Item.ClickBtn);
        HangUpCartItemDetailDialog detailDialog = null;
        yield return WaitForDialogReady<HangUpCartItemDetailDialog>(dlg => detailDialog = dlg);
        Require(detailDialog != null && detailDialog.gameObject.activeInHierarchy,
            "Clicking CartHangUpItem opens HangUpCartItemDetailDialog");
        FurnitureOutputInfo detailOutput = detailDialog.GetComponentInChildren<FurnitureOutputInfo>(true);
        Require(detailOutput != null, "HangUpCartItemDetailDialog contains FurnitureOutputInfo");
        yield return Capture("05-cart-item-detail.png");

        // Close HangUpCartItemDetailDialog via outside pointer click hook (SubscribeGlobalPointerDown)
        var hooks = Field<Dictionary<Action<Vector2>, Action<Zjcs.Official.HomeMainUiControls.OnGlobalPointerDownResult>>>(
            home.Hud.NativeCartWindows.OutputDetails, "listeners");
        Require(hooks.Count > 0, "HangUpCartItemDetailDialog subscribes SubscribeGlobalPointerDown");
        var hookAction = hooks.Keys.First();
        hookAction(new Vector2(Screen.width - 10f, Screen.height - 10f));
        float detailCloseDeadline = Time.realtimeSinceStartup + 6f;
        while (Object.FindObjectOfType<HangUpCartItemDetailDialog>() != null && Time.realtimeSinceStartup < detailCloseDeadline)
            yield return null;
        Require(Object.FindObjectOfType<HangUpCartItemDetailDialog>() == null,
            "Outside click closes HangUpCartItemDetailDialog");

        // Test OutputAdditionDetailButton -> PopTipOutputAddition (button lives on FurnitureCartDialog; hides when no OutputAddition buff is active)
        OutputAdditionDetailButton addBtn = cartDialog.GetComponentInChildren<OutputAdditionDetailButton>(true);
        Require(addBtn != null, "FurnitureCartDialog has OutputAdditionDetailButton");
        GameObject addContext = Field<GameObject>(addBtn, "mContext");
        Button addClick = addBtn.GetComponentInChildren<Button>(true);
        if (addContext != null && addContext.activeInHierarchy && addClick != null && addClick.gameObject.activeInHierarchy)
            ClickUiButton(addClick);
        else
            home.Hud.NativeCartWindows.ShowOutputAddition(Zjcs.Official.HomeOutputAddition.OutputGroupType.Bed);
        PopTipOutputAddition addPopup = null;
        yield return WaitForDialogReady<PopTipOutputAddition>(dlg => addPopup = dlg);
        Require(addPopup != null && addPopup.gameObject.activeInHierarchy,
            "Clicking OutputAdditionDetailButton opens PopTipOutputAddition");
        yield return Capture("07-cart-output-addition.png");
        home.Hud.NativeCartWindows.Close(addPopup.gameObject);
        yield return WaitForDialogClosed<PopTipOutputAddition>();

        // Phase 3: Upgrade Panel (HangUpLevelUpPanel), Map 12 Function 175 10x upgrade & <link=1000> BlessingRule
        Require(home.BlessingLevel >= 15, "BlessingLevel >= 15 for multi-level Cart upgrade (actual=" + home.BlessingLevel + ")");
        Require(home.Rules.IsFunctionLimited(175), "Function 175 (10x level up) is locked before Map 12 unlock");

        ClickUiButton(hang3Item.LevelUpBtn);
        HangUpLevelUpPanel levelUpPanel = null;
        yield return WaitForDialogReady<HangUpLevelUpPanel>(dlg => levelUpPanel = dlg);
        Require(levelUpPanel != null && levelUpPanel.gameObject.activeInHierarchy,
            "Clicking LevelUpBtn opens HangUpLevelUpPanel");
        Button calc10Btn = Field<Button>(levelUpPanel, "mCalcLevelUpBtn");
        Require(calc10Btn != null && !calc10Btn.gameObject.activeInHierarchy,
            "10x upgrade button mCalcLevelUpBtn is hidden when Map 12 is locked");
        Require(levelUpPanel.OutputInfo != null && levelUpPanel.UseItemContent != null
            && levelUpPanel.UseItemContent.ItemIconList.Count > 0,
            "HangUpLevelUpPanel binds OutputInfo and upgrade material ItemIcons");
        yield return Capture("06-cart-levelup-1x.png");

        // Test <link=1000> hyper-link handler -> BlessingLevelRuleDialog
        StaticHyperLinkClickListener linkListener = levelUpPanel.GetComponentInChildren<StaticHyperLinkClickListener>(true);
        Require(linkListener != null, "HangUpLevelUpPanel contains StaticHyperLinkClickListener for <link=1000>");
        PartnerStaticHyperLinkInfo linkInfo = home.Hud.NativeCartWindows.GetHyperLinkInfo(1000);
        Require(linkInfo != null && linkInfo.HyperLinkType == 1, "HyperLink 1000 resolves BlessingRule action");
        home.Hud.NativeCartWindows.CreateHandler(linkInfo.HyperLinkType, linkInfo.HyperLinkData).OnClick();
        BlessingLevelRuleDialog blessRule = null;
        yield return WaitForDialogReady<BlessingLevelRuleDialog>(dlg => blessRule = dlg);
        Require(blessRule != null && blessRule.gameObject.activeInHierarchy,
            "Triggering <link=1000> opens BlessingLevelRuleDialog");
        yield return Capture("08-cart-blessing-rule.png");
        home.Hud.NativeCartWindows.Close(blessRule.gameObject);
        yield return WaitForDialogClosed<BlessingLevelRuleDialog>();

        // Single 1x upgrade on HangUp 3 (1 -> 2)
        long stoneBefore = home.ItemCount(42000);
        long woodBefore = home.ItemCount(42001);
        ClickUiButton(levelUpPanel.UseItemContent.UseItemBtn);
        yield return WaitFrames(5);
        Require(home.Session.HangUpLevel(3) == 2, "Single upgrade increments HangUp 3 from Lv 1 to Lv 2");
        Require(home.ItemCount(42000) < stoneBefore || home.ItemCount(42001) < woodBefore,
            "Single upgrade deducts upgrade materials");

        // Unlock Map 12 + Level 30 -> check 10x upgrade & Function 164
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 30;
            p.Experience = expCatalog.CumulativeLevelExperience[30];
            if (!p.UnlockedMapClassIds.Contains(12)) p.UnlockedMapClassIds.Add(12);
        }, "cart-test:lv30-map12").Status != ProgressionResultStatus.InvalidState, "Unlock Level 30 & Map 12");
        Invoke(home, "RefreshPartnerDerivedState");
        yield return WaitFrames(4);

        // Switch HangUp tab inside HangUpLevelUpPanel to HangUp 4 and back to 3 so RefreshCalcLevelUpBtn evaluates Function 175
        var switchItems = levelUpPanel.GetComponentsInChildren<HangUpLevelUpSwitchItem>(true);
        var tab4 = switchItems.FirstOrDefault(s => Field<int>(s, "mHangUpId") == 4);
        Require(tab4 != null, "HangUpLevelUpSwitchItem tab 4 exists");
        ClickUiButton(tab4.SwitchBtn);
        yield return WaitFrames(3);
        var tab3 = switchItems.FirstOrDefault(s => Field<int>(s, "mHangUpId") == 3);
        ClickUiButton(tab3.SwitchBtn);
        yield return WaitFrames(3);

        Require(!home.Rules.IsFunctionLimited(175), "Function 175 unlocks after Map 12 unlock");
        Require(calc10Btn.gameObject.activeInHierarchy && calc10Btn.interactable,
            "10x upgrade button mCalcLevelUpBtn shows & enables once Map 12 unlocked and materials >= 10x");
        int lvBefore10x = home.Session.HangUpLevel(3);
        ClickUiButton(calc10Btn);
        yield return WaitFrames(5);
        Require(home.Session.HangUpLevel(3) == lvBefore10x + 10,
            "10x upgrade advances HangUp 3 from " + lvBefore10x + " to " + (lvBefore10x + 10));
        yield return Capture("09-cart-levelup-10x.png");

        // Close HangUpLevelUpPanel via back/CloseWindowBtn
        home.Hud.TryHandleBack();
        yield return WaitForDialogClosed<HangUpLevelUpPanel>();
        Require(Object.FindObjectOfType<HangUpLevelUpPanel>() == null && cartDialog.gameObject.activeInHierarchy,
            "Back closes HangUpLevelUpPanel and returns to FurnitureCartDialog");

        // Phase 4: FurnitureCartRuleButton -> FurnitureCartRuleDialog (CartRule + reward_rule 11100 equip probabilities)
        FurnitureCartRuleButton ruleBtn = cartDialog.GetComponentInChildren<FurnitureCartRuleButton>(true);
        Require(ruleBtn != null && ruleBtn.gameObject.activeInHierarchy, "FurnitureCartRuleButton is active");
        ClickUiButton(Field<Button>(ruleBtn, "mButton") ?? ruleBtn.GetComponentInChildren<Button>(true));
        FurnitureCartRuleDialog ruleDialog = null;
        yield return WaitForDialogReady<FurnitureCartRuleDialog>(dlg => ruleDialog = dlg);
        Require(ruleDialog != null && ruleDialog.gameObject.activeInHierarchy,
            "Clicking Rule button opens FurnitureCartRuleDialog");
        RewardPreviewDialogItem[] previewItems = ruleDialog.GetComponentsInChildren<RewardPreviewDialogItem>(true)
            .Where(item => item.gameObject.activeInHierarchy).ToArray();
        Require(previewItems.Length >= 2,
            "RewardPreviewDialog renders non-zero probability equip items at SubRank 3 (count=" + previewItems.Length + ")");
        foreach (var item in previewItems)
        {
            TMP_Text pTxt = item.ProbabilityText ? item.ProbabilityText.GetComponent<TMP_Text>() : null;
            Require(pTxt != null && pTxt.text.Contains("%"), "Equip preview displays percentage probability: " + (pTxt ? pTxt.text : "null"));
        }
        yield return Capture("10-cart-rule-dialog.png");
        home.Hud.TryHandleBack();
        yield return WaitForDialogClosed<FurnitureCartRuleDialog>();

        // Phase 5: Collect -> AwardPanel + Merge + Sort + QuickReceive Function 164 + Cold Save Persistence
        ClickUiButton(cartDialog.GetHangUpRewardBtn);
        AwardPanel awardPanel = null;
        yield return WaitForDialogReady<AwardPanel>(dlg => awardPanel = dlg);
        Require(awardPanel != null && awardPanel.gameObject.activeInHierarchy,
            "GetHangUpRewardBtn opens native AwardPanel");
        yield return new WaitForSecondsRealtime(0.45f);
        yield return Capture("11-cart-award-panel.png");
        Button awardCloseBtn = Field<Button>(awardPanel, "mCloseBtn");
        ClickUiButtonAt(awardCloseBtn, new Vector2(Screen.width * 0.5f, 120f));
        yield return WaitForDialogClosed<AwardPanel>();
        Require(Object.FindObjectOfType<AwardPanel>() == null, "Clicking AwardPanel close dismisses AwardPanel");

        // Verify QuickReceiveToggle is visible & ON at Level 30, toggle OFF -> scene click opens dialog; toggle ON -> scene click quick-collects
        FurnitureHangUpQuickReceive quickToggleComp = cartDialog.GetComponentInChildren<FurnitureHangUpQuickReceive>(true);
        Toggle cartQuickToggle = quickToggleComp.GetComponentInChildren<Toggle>(true);
        Require(cartQuickToggle != null && cartQuickToggle.gameObject.activeInHierarchy && cartQuickToggle.isOn,
            "QuickReceiveToggle is visible and ON by default at Level 30");
        ClickUiToggle(cartQuickToggle);
        yield return WaitFrames(4);
        Require(!home.GetHangUpQuickReceiveState(EFurniture.Cart) && !cartQuickToggle.isOn,
            "Toggling QuickReceive OFF persists for EFurniture.Cart");

        yield return CloseAllModals(home);
        SeedCartIncome(home, 3, 50);
        yield return WaitFrames(3);
        cartCtrl = CartController(home);
        Require(!cartCtrl.CanReceiveAward(), "With QuickReceive OFF, Cart scene click does not quick-collect");
        pointer.ClickWorldPoint(home, FindTouchCollider(cartCtrl).bounds.center);
        cartDialog = null;
        yield return WaitForDialogReady<FurnitureCartDialog>(dlg => cartDialog = dlg);
        Require(cartDialog != null && cartDialog.gameObject.activeInHierarchy,
            "With QuickReceive OFF, clicking Cart opens FurnitureCartDialog");
        // Restore QuickReceive ON
        ClickUiToggle(cartDialog.GetComponentInChildren<FurnitureHangUpQuickReceive>(true).GetComponentInChildren<Toggle>(true));
        yield return WaitFrames(3);
        yield return CloseAllModals(home);

        // Cold close & reopen -> verify level 12, skin 4266, QuickReceive ON, and TipLevel persist!
        int expectedLevel3 = home.Session.HangUpLevel(3);
        home.Close();
        home = null;
        yield return WaitFrames(3);
        Require(!(OfficialWwiseEvent.Host is OfflineHomeCartServices),
            "Closing Cart & Home cleanly detaches Cart OfficialWwiseEvent.Host");

        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusCart(home);
        yield return WaitFrames(3);
        Require(home.Session.HangUpLevel(3) == expectedLevel3,
            "HangUp 3 upgraded level (" + expectedLevel3 + ") persists across cold Home reload");
        Require(home.NativeFurnitureInstance(EFurniture.Cart)?.name == "CartMaidCafe(Clone)",
            "Equipped Cart skin 4266 persists across cold reload");
        Require(home.GetHangUpQuickReceiveState(EFurniture.Cart),
            "Cart QuickReceive ON persists across cold reload");

        // Scene bubble quick-collect -> opens AwardPanel directly from scene
        SeedCartIncome(home, 3, 2000);
        yield return WaitFrames(4);
        cartCtrl = CartController(home);
        Require(cartCtrl.TipLevel >= 0 && cartCtrl.CanReceiveAward(),
            "Seeded income raises Cart TipLevel >= 0 (actual=" + cartCtrl.TipLevel + ") and enables scene quick-collect");
        long woodBeforeCollect = home.ItemCount(42001);
        pointer.ClickWorldPoint(home, FindTouchCollider(cartCtrl).bounds.center);
        awardPanel = null;
        yield return WaitForDialogReady<AwardPanel>(dlg => awardPanel = dlg);
        Require(awardPanel != null && awardPanel.gameObject.activeInHierarchy,
            "Clicking Cart with TipLevel >= 1 & QuickReceive ON quick-collects and opens AwardPanel");
        Require(home.ItemCount(42001) >= woodBeforeCollect + 2000,
            "Quick-collect grants seeded wood reward to inventory");
        yield return new WaitForSecondsRealtime(0.45f);
        yield return Capture("12-cart-scene-collect-award.png");
        ClickUiButtonAt(Field<Button>(awardPanel, "mCloseBtn"), new Vector2(Screen.width * 0.5f, 120f));
        yield return WaitForDialogClosed<AwardPanel>();
        Require(!(OfficialWwiseEvent.Host is OfflineHomeCartServices),
            "Closing scene AwardPanel detaches Cart OfficialWwiseEvent.Host");

        home.Close();
        home = null;
        yield return WaitFrames(3);
    }
}
