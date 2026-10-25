using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    /// <summary>Windowed Map11 checks with an isolated profile and native Windows pointer input.</summary>
    public static partial class OfficialMap11PlayModeAcceptance
    {
        private const string Key = "Zjcs.Map11.Acceptance";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool started;
        private static double playReadyAt;
        private static EditorWindow gameView;
        private static OfficialMap10GameRuntime runtime;
        private static System.Diagnostics.Process pointerProcess;
        private static int pointerSequence;
        private static string Root => SessionState.GetString(Key + ".root", "");
        private static string Slot => SessionState.GetString(Key + ".slot", "");

        [MenuItem("Zjcs/Validate/Map11 Windowed Play Mode")]
        public static void Run()
        {
            try { SessionState.SetBool(Key + ".background", false); RunCore(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void RunCore()
        {
            if (Application.isBatchMode && !SessionState.GetBool(Key + ".background", false))
                throw new InvalidOperationException("Map11 native input requires a windowed editor.");
            OfficialMap11UiTools.ImportAndValidate();
            OfficialMap11PuzzleValidation.Validate();
            ProjectTools.Validate();
            string root = Path.Combine(Path.GetTempPath(), "zjcs-map11-play-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "森之国验证";
            save.Player.InitialWeaponType = 200;
            save.Player.Level = 20;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[20];
            save.Player.Rank = save.Player.SubRank = save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.UnlockedMapClassIds.Add(11);
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            SessionState.SetString(Key + ".root", root);
            SessionState.SetString(Key + ".slot", slot);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureGameView();
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore()
        {
            if (SessionState.GetBool(Key, false)) Hook();
        }

        private static void Hook()
        {
            started = false;
            playReadyAt = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= RuntimeLog;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Fail("Map11 Play Mode exceeded its deadline.");
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            // Let the editor's other on-load importers finish before observing this map's runtime.
            if (playReadyAt == 0) playReadyAt = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.timeSinceStartup < playReadyAt) return;
            started = true;
            Application.logMessageReceived += RuntimeLog;
            try
            {
                ConfigureGameView();
                GameObject startupFlow = GameObject.Find("CharacterCreationFlow");
                if (startupFlow != null) UnityEngine.Object.Destroy(startupFlow);
                GameObject startupCanvas = GameObject.Find("CharacterCreationCanvas");
                if (startupCanvas != null) UnityEngine.Object.Destroy(startupCanvas);
                if (EventSystem.current == null)
                    new GameObject("Map11Input", typeof(EventSystem), typeof(StandaloneInputModule));
                var canvas = new GameObject("Map11Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
                OpenMap(canvas.transform);
                new GameObject("Map11AcceptanceRunner").AddComponent<Runner>().StartCoroutine(
                    SessionState.GetBool(Key + ".background", false) ? VerifyBackground(canvas.transform) : Verify(canvas.transform));
            }
            catch (Exception error) { Fail(error.ToString()); }
        }

        private static void OpenMap(Transform canvas)
        {
            GameObject avatar = CharacterCreationNativeModelFactory.CreateWorldModel(Slot, Root, out string name);
            runtime = new GameObject("Map11AcceptanceRuntime").AddComponent<OfficialMap10GameRuntime>();
            runtime.Begin(11, Slot, Root, canvas, avatar, name, null);
        }

        private static T Field<T>(string name) => (T)typeof(OfficialMap10GameRuntime).GetField(name, Private).GetValue(runtime);
        private static void Invoke(string name, params object[] args) =>
            typeof(OfficialMap10GameRuntime).GetMethod(name, Private).Invoke(runtime, args);

        private static IEnumerator Verify(Transform canvas)
        {
            string channel = Path.Combine(Root, "pointer");
            Directory.CreateDirectory(channel);
            string helper = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scripts", "native_pointer.ps1"));
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            pointerProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(powershell,
                "-NoProfile -ExecutionPolicy Bypass -File \"" + helper + "\" -GameProcessId "
                + System.Diagnostics.Process.GetCurrentProcess().Id + " -Channel \"" + channel + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
            pointerSequence = 0;
            float helperDeadline = Time.realtimeSinceStartup + 15;
            while (!File.Exists(Path.Combine(channel, "reply-0.json")))
            {
                Require(!pointerProcess.HasExited && Time.realtimeSinceStartup < helperDeadline, "Native pointer helper startup");
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2);
            Require(runtime.GetComponentInChildren<OfficialMap10EntityView>().EntityCount == 1803, "Map11 entity count");
            OfficialMap10UiRenderer hud = Field<OfficialMap10UiRenderer>("ui");
            var uiData = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            foreach (OfficialMap11ExploreButtonBinding binding in uiData.worldBindings.exploreButtons)
                foreach (OfficialMap11UiState state in binding.states)
                    foreach (string path in state.paths)
                        Require(hud.Find("WorldCanvas", path).activeSelf == (state.state == (binding.isOneTime ? 1 : 0)),
                            "Original explore icon state " + path);
            Require(uiData.worldBindings.chatActionExpanded.All(path => !hud.Find("WorldCanvas", path).activeSelf),
                "Original chat action collapsed state");
            Require(uiData.worldBindings.actionEndButtons.All(path => !hud.Find("WorldCanvas", path).activeSelf),
                "Original action-end button without provider");
            Require(uiData.worldBindings.quickMessageMasks.All(path => !hud.Find("WorldCanvas", path).activeSelf),
                "Quick-message cooldown without prior send");
            OfficialWorldMapSession map = Field<OfficialWorldMapSession>("session");
            var goddess = map.Definition.Entities.First(value => value.Id == 202);
            Capture("entry");
            var point = new MapGridPoint(goddess.Grid[0], goddess.Grid[1]);
            MapGridPoint adjacent = new[] { new MapGridPoint(point.X - 1, point.Y), new MapGridPoint(point.X + 1, point.Y),
                new MapGridPoint(point.X, point.Y - 1), new MapGridPoint(point.X, point.Y + 1) }.First(map.Movement.IsFit);
            Invoke("SetAvatarPosition", adjacent); // Fixture placement; all tested clicks below use native OS input.
            yield return new WaitForSecondsRealtime(1);
            yield return ClickEntity(202);
            GoddessTouchDialog dialog = Field<GoddessTouchDialog>("goddessDialog");
            Require(dialog != null && dialog.CannotActiveBtn.gameObject.activeInHierarchy, "Incomplete goddess condition UI");
            Capture("lost-locked");
            yield return KeyPress(0x1B);
            Require(Field<GoddessTouchDialog>("goddessDialog") == null, "Escape closes goddess modal");

            // Supply completed placements as an explicit fixture, then run the real official progress calculator.
            object local = Field<object>("localSave");
            local.GetType().GetField("completedEntityPlacements").SetValue(local,
                map.Definition.Entities.Where(value => value.Region == goddess.Region && value.TypeId != 202)
                    .Select(value => value.Id).ToArray());
            Invoke("RefreshRegionProgress");
            Invoke("RefreshEntityActivity");
            Invoke("SaveLocalProgress");
            yield return ClickEntity(202);
            dialog = Field<GoddessTouchDialog>("goddessDialog");
            Require(dialog != null && dialog.myTouchBtn.gameObject.activeInHierarchy, "Full region activation UI");
            Capture("lost-ready");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, dialog.myTouchBtn.transform.position));
            Require(Field<GoddessTouchDialog>("goddessDialog") == null, "Activation closes goddess modal");
            OfflineMainlineProgressSession save = Field<OfflineMainlineProgressSession>("progressionSave");
            Require(save.Save.World.ActivatedGoddessRegions.Count == 1, "Activation receipt");
            VerifyFurnitureProps(save);
            UnityEngine.Object.Destroy(runtime.gameObject);
            yield return null;
            OpenMap(canvas);
            yield return new WaitForSecondsRealtime(1);
            save = Field<OfflineMainlineProgressSession>("progressionSave");
            Require(save.Save.World.ActivatedGoddessRegions.Count == 1, "Cold profile activation persistence");
            VerifyFurnitureProps(save);
            map = Field<OfficialWorldMapSession>("session");
            map.Movement.AddExtraBlocked(point); // Exercise the confirmed fallback with an occupied destination.
            MapGridPoint away = new MapGridPoint(adjacent.X + 4, adjacent.Y);
            Invoke("SetAvatarPosition", away);
            yield return new WaitForSecondsRealtime(1);
            yield return ClickEntity(202);
            dialog = Field<GoddessTouchDialog>("goddessDialog");
            Debug.Log("Map11 cold dialog=" + (dialog != null) + " active=" + (dialog != null && dialog.gameObject.activeInHierarchy)
                + " transfer=" + (dialog != null && dialog.mTransferBtn.gameObject.activeSelf)
                + " hierarchy=" + (dialog != null && dialog.mTransferBtn.gameObject.activeInHierarchy)
                + " profile=" + JsonUtility.ToJson(save.Save.World.ActivatedGoddessRegions[0]));
            Capture("lost-active-diagnostic");
            yield return new WaitForSecondsRealtime(0.2f);
            Require(dialog != null && dialog.mTransferBtn.gameObject.activeInHierarchy, "Activated transfer UI");
            Capture("lost-active");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, dialog.mTransferBtn.transform.position));
            MapGridPoint landed = Field<MapGridPoint>("currentGrid");
            Require(!landed.Equals(point) && map.Movement.IsFit(landed), "Transfer nearest standable destination");
            int distance = (landed.X - point.X) * (landed.X - point.X) + (landed.Y - point.Y) * (landed.Y - point.Y);
            for (int y = 0; y < map.Definition.Navigation.Height; y++)
            for (int x = 0; x < map.Definition.Navigation.Width; x++)
                Require(!map.Movement.IsFit(new MapGridPoint(x, y)) ||
                    (x - point.X) * (x - point.X) + (y - point.Y) * (y - point.Y) >= distance, "Nearest transfer distance");
            Invoke("SaveLocalProgress");
            UnityEngine.Object.Destroy(runtime.gameObject);
            yield return null;
            OpenMap(canvas);
            Require(Field<MapGridPoint>("currentGrid").Equals(landed), "Cold map position persistence");
            VerifyFurnitureProps(Field<OfflineMainlineProgressSession>("progressionSave"));
            Capture("restored");
            yield return new WaitForSecondsRealtime(1);
            map = Field<OfficialWorldMapSession>("session");
            var stone = map.Definition.Entities.Single(value => value.Id == 2302);
            MapGridPoint stoneGrid = new MapGridPoint(stone.Grid[0], stone.Grid[1]);
            MapGridPoint stoneAdjacent = new[] { new MapGridPoint(stoneGrid.X - 1, stoneGrid.Y), new MapGridPoint(stoneGrid.X + 1, stoneGrid.Y),
                new MapGridPoint(stoneGrid.X, stoneGrid.Y - 1), new MapGridPoint(stoneGrid.X, stoneGrid.Y + 1) }.First(map.Movement.IsFit);
            Invoke("SetAvatarPosition", stoneAdjacent);
            yield return new WaitForSecondsRealtime(1);
            Capture("puzzle-before");
            yield return ClickEntity(2302);
            OfflineMapPuzzleSession puzzle = Field<OfflineMapPuzzleSession>("puzzles");
            Require(puzzle.State(2302) == 2 && puzzle.State(2303) == 2 && puzzle.State(2304) == 0 && puzzle.State(2305) == 1,
                "Native mouse stone touch and ordered linked notifications");
            Capture("puzzle-after");
            UnityEngine.Object.Destroy(runtime.gameObject);
            yield return null;
            OpenMap(canvas);
            puzzle = Field<OfflineMapPuzzleSession>("puzzles");
            Require(puzzle.State(2302) == 2 && puzzle.State(2303) == 2 && puzzle.State(2304) == 0 && puzzle.State(2305) == 1,
                "Cold puzzle persistence after real input");
            var terminal = map.Definition.Entities.Single(value => value.Id == 2307);
            MapGridPoint terminalGrid = new MapGridPoint(terminal.Grid[0], terminal.Grid[1]);
            map = Field<OfficialWorldMapSession>("session");
            MapGridPoint terminalAdjacent = new[] { new MapGridPoint(terminalGrid.X - 1, terminalGrid.Y),
                new MapGridPoint(terminalGrid.X + 1, terminalGrid.Y), new MapGridPoint(terminalGrid.X, terminalGrid.Y - 1),
                new MapGridPoint(terminalGrid.X, terminalGrid.Y + 1) }.First(map.Movement.IsFit);
            Invoke("SetAvatarPosition", terminalAdjacent);
            yield return new WaitForSecondsRealtime(1);
            yield return ClickEntity(2307);
            yield return AdvanceToStoryOptions();
            Require(ItemCount(325) == 1, "Original fixed reward 45 grants one first puzzle ticket");
            Capture("terminal-options");
            yield return ClickStoryOption(1);
            Require(!Field<bool>("storyRunning") && ItemCount(325) == 1, "Puzzle reset is free");
            puzzle = Field<OfflineMapPuzzleSession>("puzzles");
            Require(puzzle.State(2302) == 1 && puzzle.State(2303) == 1 && puzzle.State(2304) == 2
                && puzzle.State(2305) == 0, "Native terminal reset restores configured stone states");
            yield return ClickEntity(2307);
            yield return AdvanceToStoryOptions();
            Require(ItemCount(325) == 1, "Reopening terminal does not repeat first ticket reward");
            yield return ClickStoryOption(0);
            yield return AdvanceToStoryOptions();
            Require(!Field<OfficialMap10UiRenderer>("ui").Find("OptionTalkDialog",
                StoryOptionPath(0) + "/Content/OptionText/Text").GetComponent<TMPro.TMP_Text>().text.Contains("{0}"),
                "Original submit option formats live ticket amount");
            Capture("terminal-submit");
            yield return ClickStoryOption(0);
            Require(!Field<bool>("storyRunning") && ItemCount(325) == 0, "Successful solve consumes one ticket");
            Require(puzzle.IsUnlocked(2306) && puzzle.State(2307) == 1, "Native terminal solve unlocks associated group");
            UnityEngine.Object.Destroy(runtime.gameObject);
            yield return null;
            OpenMap(canvas);
            Require(ItemCount(325) == 0 && Field<OfflineMapPuzzleSession>("puzzles").IsUnlocked(2306),
                "Cold terminal solve and inventory persistence");
            Debug.Log("Map11 acceptance reached restored screenshot.");
            Debug.Log("Map11 native input checks passed: 1803 entities; original HUD initial states; lost goddess locked/ready/active states; one activation; furniture +1%/+1%; nearest standable transfer; cold profile and map position; stone touch, linked states and cold puzzle persistence. Fixture region completion is not a natural progression check. Output: " + Root);
            SessionState.SetInt(Key + ".result", 0);
            File.WriteAllText(Path.Combine(channel, "request-" + (++pointerSequence) + ".json"),
                "{\"seq\":" + pointerSequence + ",\"kind\":\"stop\"}");
            EditorApplication.isPlaying = false;
        }

        private static long ItemCount(int itemId) => Field<OfflineMainlineProgressSession>("progressionSave")
            .Save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;
        private static string StoryOptionPath(int index) => "/OptionTalkDialog/OptionSlots/Content/OptionItem "
            + (index == 0 ? "" : " (" + index + ")");
        private static IEnumerator AdvanceToStoryOptions()
        {
            for (int count = 0; count < 12 && !Field<bool>("storyOptionsWaiting"); count++)
            {
                Require(Field<bool>("dialogueWaiting"), "Terminal story is waiting for original dialogue input");
                var hud = Field<OfficialMap10UiRenderer>("ui");
                string panel = Field<string>("dialoguePanelKey");
                RectTransform next = hud.Find(panel, "/" + panel + "/NextBtn").GetComponent<RectTransform>();
                yield return Click(RectTransformUtility.WorldToScreenPoint(null, next.TransformPoint(next.rect.center)));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Require(Field<bool>("storyOptionsWaiting"), "Terminal original story options appeared");
        }
        private static IEnumerator ClickStoryOption(int index)
        {
            var hud = Field<OfficialMap10UiRenderer>("ui");
            RectTransform option = hud.Find("OptionTalkDialog", StoryOptionPath(index)).GetComponent<RectTransform>();
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, option.TransformPoint(option.rect.center)));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private static void VerifyFurnitureProps(OfflineMainlineProgressSession save)
        {
            OfficialCharacterCatalog catalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            var source = new OfflineGoddessPropSource(catalog);
            var props = source.CalculateProps(save.Save.Player, save.Save.World, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Require(props.Count == 2 && props[1001] == 100 && props[1005] == 100, "Lost goddess exact furniture rewards");
            int baseLevel = Convert.ToInt32(catalog.Rows("game_settings").First()["BaseBlessingLevel"]);
            Require(source.BlessingLevel(save.Save.World) == baseLevel, "Lost goddess does not grant ordinary blessing levels");
            var ordinary = new OfflineWorldProgress();
            ordinary.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = 11, Region = 0 });
            int increment = Convert.ToInt32(catalog.Rows("game_settings").First()["GoddessToBlessingLevel"]);
            Require(source.BlessingLevel(ordinary) == baseLevel + increment, "Ordinary goddess blessing formula");
            Require(source.CalculateProps(save.Save.Player, ordinary, 0).Count == 0, "Ordinary goddess has no lost furniture gain");
        }

        private static IEnumerator ClickEntity(int placement)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            Camera camera = Field<Camera>("worldCamera");
            GameObject actor = Field<GameObject>("avatar");
            while (Field<object>("movementPath") != null || Vector2.Distance(camera.transform.position, actor.transform.position) > 0.005f)
            {
                Require(Time.realtimeSinceStartup < deadline, "Entity input camera settled after fixture placement");
                yield return null;
            }
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            Collider2D collider = view.FindEntityObject(placement).GetComponentInChildren<Collider2D>();
            Vector2 position = camera.WorldToScreenPoint(collider.bounds.center);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Debug.Log("Map11 entity click=" + placement + " active=" + collider.gameObject.activeInHierarchy
                + " current=" + Field<MapGridPoint>("currentGrid") + " UI="
                + string.Join(",", hits.Take(8).Select(value => value.gameObject.name)));
            view.EntitySelected += entity => Debug.Log("Map11 native entity selected=" + entity.PlacementId);
            yield return Click(position);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private static IEnumerator Click(Vector2 position)
        {
            gameView.Focus();
            FocusEditor();
            yield return new WaitForSecondsRealtime(0.2f);
            yield return PointerFocus();
            Require(Application.isFocused, "Native input editor focus");
            Rect target = (Rect)gameView.GetType().GetProperty("targetInView", Private | BindingFlags.Public).GetValue(gameView);
            float logicalScale = target.width / Screen.width;
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float scale = logicalScale * pixelsPerPoint;
            Vector2 desktop = new Vector2(gameView.position.x + target.x + position.x * logicalScale,
                gameView.position.y + target.y + (Screen.height - position.y) * logicalScale) * pixelsPerPoint;
            Debug.Log("Map11 pointer target=" + position + " view=" + gameView.position + " viewport=" + target
                + " render=" + Screen.width + "x" + Screen.height + " scale=" + scale);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                SetCursorPos(Mathf.RoundToInt(desktop.x), Mathf.RoundToInt(desktop.y));
                yield return new WaitForSecondsRealtime(0.15f);
                GetCursorPos(out NativePoint actual);
                Vector2 delta = position - (Vector2)Input.mousePosition;
                Debug.Log("Map11 pointer actual=" + Input.mousePosition + " desktop=" + actual.x + "," + actual.y
                    + " focused=" + Application.isFocused + " over=" + EditorWindow.mouseOverWindow?.GetType().Name
                    + " foreground=" + GetForegroundWindow() + " expected=" + NativeEditorWindow());
                if (delta.sqrMagnitude < 9) break;
                desktop = new Vector2(actual.x, actual.y) + new Vector2(delta.x, -delta.y) * scale;
            }
            Require(Vector2.Distance(position, Input.mousePosition) < 4, "Native pointer alignment: " + position + " / " + Input.mousePosition);
            Debug.Log("Map11 input windows focused=" + EditorWindow.focusedWindow?.GetType().Name
                + " over=" + EditorWindow.mouseOverWindow?.GetType().Name + " main="
                + NativeEditorWindow() + " foreground=" + GetForegroundWindow());
            EnumWindows((window, unused) =>
            {
                GetWindowThreadProcessId(window, out uint owner);
                if (owner == System.Diagnostics.Process.GetCurrentProcess().Id && IsWindowVisible(window))
                {
                    var title = new System.Text.StringBuilder(256);
                    GetWindowText(window, title, title.Capacity);
                    GetWindowRect(window, out NativeRect bounds);
                    Debug.Log("Map11 window=" + window + " title=" + title + " rect=" + bounds.left + "," + bounds.top + "," + bounds.right + "," + bounds.bottom);
                }
                return true;
            }, IntPtr.Zero);
            yield return PointerButton(true);
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("Map11 input held OS=" + GetAsyncKeyState(1) + " Unity=" + Input.GetMouseButton(0)
                + " focused=" + Application.isFocused + " over=" + EditorWindow.mouseOverWindow?.GetType().Name
                + " foreground=" + GetForegroundWindow());
            yield return PointerButton(false);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private static IEnumerator PointerButton(bool down)
        {
            GetCursorPos(out NativePoint cursor);
            var origin = new NativePoint();
            ClientToScreen(NativeEditorWindow(), ref origin);
            Debug.Log("Map11 helper cursor=" + cursor.x + "," + cursor.y + " origin=" + origin.x + "," + origin.y);
            int sequence = ++pointerSequence;
            string channel = Path.Combine(Root, "pointer");
            File.WriteAllText(Path.Combine(channel, "request-" + sequence + ".json"),
                "{\"seq\":" + sequence + ",\"kind\":\"button\",\"x\":" + (cursor.x - origin.x)
                + ",\"y\":" + (cursor.y - origin.y) + ",\"down\":" + (down ? "true" : "false") + "}");
            string reply = Path.Combine(channel, "reply-" + sequence + ".json");
            float deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(reply))
            {
                Require(!pointerProcess.HasExited && Time.realtimeSinceStartup < deadline, "Native pointer response");
                yield return null;
            }
            string result = File.ReadAllText(reply);
            Require(result.Contains("\"ok\":true"), "Native pointer rejected: " + result);
        }

        private static IEnumerator PointerFocus()
        {
            int sequence = ++pointerSequence;
            string channel = Path.Combine(Root, "pointer");
            File.WriteAllText(Path.Combine(channel, "request-" + sequence + ".json"),
                "{\"seq\":" + sequence + ",\"kind\":\"focus\"}");
            string reply = Path.Combine(channel, "reply-" + sequence + ".json");
            float deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(reply))
            {
                Require(Time.realtimeSinceStartup < deadline && !pointerProcess.HasExited, "Native pointer focus response");
                yield return null;
            }
            string result = File.ReadAllText(reply);
            Require(result.Contains("\"ok\":true"), "Native pointer focus: " + result);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private static IEnumerator KeyPress(byte key)
        {
            gameView.Focus();
            FocusEditor();
            keybd_event(key, 0, 0, UIntPtr.Zero);
            yield return new WaitForSecondsRealtime(0.15f);
            keybd_event(key, 0, 2, UIntPtr.Zero);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static void ConfigureGameView()
        {
            typeof(Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 540, 960 });
            gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.maximized = true;
            if (!SessionState.GetBool(Key + ".background", false)) gameView.Focus();
        }

        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Root, name + ".png"));
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void RuntimeLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) Fail(message + "\n" + stack);
        }
        private static void Fail(string message)
        {
            Application.logMessageReceived -= RuntimeLog;
            Debug.LogError("Map11 Play Mode failed: " + message + "; output: " + Root);
            SessionState.SetInt(Key + ".result", 1);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else EditorApplication.Exit(1);
        }

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")] private static extern bool SetPhysicalCursorPos(int x, int y);
        private delegate bool EnumWindowProc(IntPtr window, IntPtr unused);
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int left, top, right, bottom; }
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr unused);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int capacity);
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int x, y; }
        [DllImport("user32.dll", EntryPoint = "GetCursorPos")] private static extern bool GetPhysicalCursorPos(out NativePoint point);
        [DllImport("user32.dll", EntryPoint = "ClientToScreen")] private static extern bool PhysicalClientToScreen(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        private static void SetCursorPos(int x, int y)
        {
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try { SetPhysicalCursorPos(x, y); }
            finally { SetThreadDpiAwarenessContext(previous); }
        }
        private static void GetCursorPos(out NativePoint point)
        {
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try { GetPhysicalCursorPos(out point); }
            finally { SetThreadDpiAwarenessContext(previous); }
        }
        private static void ClientToScreen(IntPtr window, ref NativePoint point)
        {
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try { PhysicalClientToScreen(window, ref point); }
            finally { SetThreadDpiAwarenessContext(previous); }
        }
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint source, uint target, bool attach);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int state);
        private static void FocusEditor()
        {
            IntPtr window = NativeEditorWindow();
            uint foreground = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            uint current = GetCurrentThreadId();
            bool attached = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
            try
            {
                ShowWindow(window, 9);
                SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x0043);
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally { if (attached) AttachThreadInput(current, foreground, false); }
        }
        private static IntPtr NativeEditorWindow()
        {
            IntPtr result = IntPtr.Zero;
            long area = 0;
            uint processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((window, unused) =>
            {
                GetWindowThreadProcessId(window, out uint owner);
                if (owner != processId || !GetWindowRect(window, out NativeRect bounds)) return true;
                long candidate = (long)(bounds.right - bounds.left) * (bounds.bottom - bounds.top);
                if (candidate > area) { area = candidate; result = window; }
                return true;
            }, IntPtr.Zero);
            if (result == IntPtr.Zero) throw new InvalidOperationException("The native editor window was not created.");
            return result;
        }
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        public sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (runtime == null || !Input.GetMouseButtonDown(0)) return;
                Camera camera = Field<Camera>("worldCamera");
                Vector3 screen = Input.mousePosition;
                screen.z = Mathf.Abs(camera.transform.position.z);
                Vector3 world = camera.ScreenToWorldPoint(screen);
                var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
                Collider2D goddess = view.FindEntityObject(202).GetComponent<Collider2D>();
                Debug.Log("Map11 native down pointer=" + Input.mousePosition + " world=" + world + " overlap="
                    + goddess.OverlapPoint(world) + " story=" + Field<bool>("storyRunning")
                    + " moving=" + (Field<object>("movementPath") != null) + " enabled=" + runtime.enabled);
            }
        }
    }
}
