using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    // Checks the mounted HUD, including layout after its authored show animation.
    [InitializeOnLoad]
    public static class OfficialBattleBossHudAcceptance
    {
        private const string Key = "Zjcs.BossHudAcceptance";
        private static OfficialBattleHudView view;
        private static GameObject canvas, root;
        private static FightBattleCheckpoint checkpoint;
        private static OfficialBattleHudCatalog catalog;
        private static int stage;
        private static double next;
        private static Camera outputCamera;
        private static RenderTexture output;

        static OfficialBattleBossHudAcceptance()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.playModeStateChanged += Entered;
        }

        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            ConfigureRenderSize();
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.playModeStateChanged -= Entered;
            EditorApplication.playModeStateChanged += Entered;
            EditorApplication.EnterPlaymode();
        }

        private static void ConfigureRenderSize()
        {
            // Configure the existing render surface without showing/focusing any window.
            var assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] {
                Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            object size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, new[] { Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1), (object)450, 975, "Boss HUD portrait" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type gameView = assembly.GetType("UnityEditor.GameView");
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(gameView))
            {
                var behavior = assembly.GetType("UnityEditor.PlayModeView").GetProperty("enterPlayModeBehavior",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                behavior.SetValue(window, Enum.Parse(behavior.PropertyType, "PlayUnfocused"));
                gameView.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic).SetValue(window, index);
                window.Repaint();
            }
        }

        private static void Entered(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(Key, false);
            EditorApplication.playModeStateChanged -= Entered;
            AudioListener.volume = 0;
            Application.runInBackground = true;
            try
            {
                canvas = new GameObject("Boss HUD fixture", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                output = new RenderTexture(450, 975, 24);
                outputCamera = new GameObject("HUD render camera", typeof(Camera)).GetComponent<Camera>();
                outputCamera.targetTexture = output;
                outputCamera.clearFlags = CameraClearFlags.SolidColor;
                outputCamera.backgroundColor = Color.black;
                var surface = canvas.GetComponent<Canvas>();
                surface.renderMode = RenderMode.ScreenSpaceCamera;
                surface.worldCamera = outputCamera;
                surface.planeDistance = 1;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = .5f;
                catalog = OfficialBattleHudCatalog.LoadDefault();
                view = new OfficialBattleHudView(catalog);
                root = view.Mount(canvas.transform);
                checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 14, RandomSeed = 1 };
                checkpoint.Units.Add(new FightBattleUnitState { Id = 2, MonsterId = 3803,
                    MonsterType = 3, Side = 2, Hp = 1000, MaxHp = 1000, LastMaxHp = 1000, MaxHpPlies = 1 });
                checkpoint.Units[0].Turn.Id = 2;
                RefreshBoss();
                stage = 0;
                next = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (stage == 2)
                {
                    checkpoint.Units[0].Hp = 0;
                    RefreshBoss();
                    var bossAfterDeath = root.transform.Find("Content/Top/DynamicContent/TopContent/FightMainPanelBossUI");
                    VerifyPortrait(bossAfterDeath.Find("Anim/FrontItem"), 3613);
                    checkpoint.Units[1].Hp = 0;
                    RefreshBoss();
                    Require(!bossAfterDeath.gameObject.activeSelf, "Dead Boss bar remained visible.");
                    Debug.Log("[Boss HUD] Large HP, both left portraits, checkpoint restore, death hide and simulated HUD click passed.");
                    Finish(null);
                    return;
                }
                var parent = root.transform.Find("Content/Top/DynamicContent/TopContent");
                var boss = parent.Find("FightMainPanelBossUI");
                Require(boss != null && boss.gameObject.activeInHierarchy, "Boss is absent from the official layout parent.");
                var fill = boss.Find("Anim/FrontItem/HpBg/TopImg").GetComponent<Image>();
                Canvas.ForceUpdateCanvases();
                VerifyPortrait(boss.Find("Anim/FrontItem"), 3803);
                var corners = new Vector3[4];
                fill.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 point = outputCamera.WorldToViewportPoint(corner);
                    Require(point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1,
                        "Boss HP image is outside the rendered viewport: " + corner + " projected=" + point
                        + " screen=" + Screen.width + "x" + Screen.height + " camera=" + outputCamera.pixelRect
                        + " canvas=" + ((RectTransform)canvas.transform).rect);
                }
                Require(fill.sprite != null && fill.rectTransform.rect.width > 600, "Original large HP image lost its binding/size.");
                if (stage++ == 0)
                {
                    Require(Mathf.Abs(fill.fillAmount - 1) < .001f, "Initial HP is not full.");
                    Capture("boss-full.png");
                    // Raycast and inject a pointer click into the mounted original speed button.
                    var events = new GameObject("Input", typeof(EventSystem)).GetComponent<EventSystem>();
                    var button = root.GetComponentsInChildren<Button>(true).First(b => b.transform.parent.name == "FightMainPanelSpeedUI");
                    var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(outputCamera, button.transform.position) };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    events.RaycastAll(pointer, hits);
                    Require(hits.Any(h => h.gameObject == button.gameObject || h.gameObject.transform.IsChildOf(button.transform)), "HUD pointer target is obscured.");
                    int clicks = 0; view.SpeedCycled += () => clicks++;
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Require(clicks == 1, "Simulated HUD click failed.");
                    checkpoint.Units[0].Hp = 400;
                    var second = new FightBattleUnitState { Id = 3, MonsterId = 3613,
                        MonsterType = 3, Side = 2, Hp = 200, MaxHp = 200, LastMaxHp = 200 };
                    second.Turn.Id = 3;
                    checkpoint.Units.Add(second);
                    checkpoint = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                    RefreshBoss();
                    for (int i = 0; i < 200; i++) view.Tick(.016f);
                    next = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                Require(Mathf.Abs(fill.fillAmount - .4f) < .005f, "Restored damaged HP did not update.");
                VerifyPortrait(boss.Find("Anim/BehindItem"), 3613);
                Capture("boss-damaged.png");
                // Let the screenshot reach the render boundary before hiding.
                next = EditorApplication.timeSinceStartup + 1;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void RefreshBoss() => view.RefreshBoss(checkpoint, _ => "Boss",
            unit => catalog.FindMonsterPortrait(unit.MonsterId));

        private static void VerifyPortrait(Transform row, int iconId)
        {
            var portrait = row.Find("ImgeHead").GetComponent<Image>();
            var bar = (RectTransform)row.Find("HpBg");
            Require(portrait.isActiveAndEnabled && portrait.sprite != null
                && portrait.sprite == catalog.FindMonsterPortrait(iconId) && !portrait.raycastTarget,
                "Boss portrait is hidden, wrong or intercepts input: " + iconId);
            Vector3 right = portrait.rectTransform.TransformPoint(portrait.rectTransform.rect.max);
            Vector3 left = bar.TransformPoint(bar.rect.min);
            Require(right.x < left.x && outputCamera.WorldToViewportPoint(right).x > 0,
                "Boss portrait is not to the left of its large HP bar: " + iconId);
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Capture(string name)
        {
            var image = new Texture2D(output.width, output.height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = output;
            image.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            try { File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) Debug.LogException(error);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
