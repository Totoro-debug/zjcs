using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.PlayerHealth;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Editor.PlayerHealth
{
    [InitializeOnLoad]
    public static class PlayerHealthAcceptance
    {
        private const string Key = "Zjcs.PlayerHealthAcceptance.Active";
        static PlayerHealthAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("Original player health acceptance").AddComponent<PlayerHealthAcceptanceRunner>();
            };
        }
        public static void Run()
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".zjcs-validation.json")))
                throw new InvalidOperationException("An owned validation workspace is required.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        internal static void Finish(Exception error)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "health-result.json"),
                JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
            if (error != null) Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
        [Serializable] private sealed class Result { public bool success; public string error; }
    }

    public sealed class PlayerHealthAcceptanceRunner : MonoBehaviour
    {
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Near(float actual, float expected, string message)
        { Require(Math.Abs(actual - expected) < 0.000002f, message + ": " + actual + " / " + expected); }
        private void Start() => StartCoroutine(Guard(Validate()));
        private static IEnumerator Guard(IEnumerator steps)
        {
            while (true)
            {
                bool next; object current = null;
                try { next = steps.MoveNext(); if (next) current = steps.Current; }
                catch (Exception error) { PlayerHealthAcceptance.Finish(error); yield break; }
                if (!next) break;
                yield return current;
            }
            PlayerHealthAcceptance.Finish(null);
        }

        private static IEnumerator Validate()
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
            var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
                read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
                read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
            var slots = new ProfileSlotCatalog(Path.Combine(work, "health-profiles"));
            string slot = slots.CreateSlot();
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Near(session.Save.Player.Health.HpPercentVal, 1f, "New local profile starts full");
            Require(session.ApplyCharacterMutation(value =>
            { value.CharacterCreated = true; value.CharacterName = "伙伴头像血量验证"; }).IsApplied, "Create isolated player");
            string path = slots.GetProgressPath(slot);
            string json = File.ReadAllText(path);
            string legacy = Regex.Replace(json, @",?\s*""Health""\s*:\s*\{[^}]*\}\s*,?", match =>
                match.Value.TrimStart().StartsWith(",", StringComparison.Ordinal) ? "," : "");
            // Preserve the comma after removing an interior field; the save contains fields after Health.
            Require(legacy != json && !legacy.Contains("\"Health\""), "Construct a genuine absent-field legacy profile");
            File.WriteAllText(path, legacy);
            session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Near(session.Save.Player.Health.HpPercentVal, 1f, "Legacy missing field starts full");
            var now = new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);
            int maps = 0;
            IReadOnlyList<bool> games = Array.Empty<bool>();
            int add = catalog.Props.Id("HangUpHpAdd"), percent = catalog.Props.Id("HangUpHpAddPercent");
            Require(catalog.Props.Get(add).Float && catalog.Props.Get(percent).Float, "Original recovery properties use fixed-point values");
            var props = new Dictionary<int, long> { [add] = 27, [percent] = 1234 };
            int propertyReads = 0;
            var hp = new OfflinePlayerHealth(session, catalog.Props, _ => { propertyReads++; return props; },
                () => maps, () => games, () => now);
            int notifications = 0;
            hp.HpUpdated += _ => notifications++;
            Require(hp.SetHpPercent(0.25f).IsApplied && notifications == 1, "Set and publish committed HP");
            now = now.AddSeconds(10);
            float expected = (float)(0.25 + (27 / 10000.0) * (1.0 + 1234 / 10000.0) * 10);
            Near(hp.HpPercent, expected, "Original fixed-point recovery and elapsed time");
            maps = 1; propertyReads = 0;
            Near(hp.HpPercent, 0.25f, "Maps use stored HP");
            Require(propertyReads == 0, "Map branch skips property calculation");
            maps = 0; games = new[] { false, true, false };
            Near(hp.HpPercent, 0.25f, "Any game using player HP stops automatic recovery");
            Require(propertyReads == 0, "Battle branch skips property calculation");
            games = new[] { false, false };
            Near(hp.HpPercent, expected, "Games not using player HP retain recovery");
            var blocked = new OfflinePlayerHealth(session, catalog.Props,
                _ => throw new Exception("Blocked recovery queried properties"), () => 1,
                () => throw new Exception("Map branch queried battle configuration"),
                () => throw new Exception("Blocked recovery queried the clock"));
            Near(blocked.HpPercent, 0.25f, "Original map short circuit does not query battles or clock");
            blocked = new OfflinePlayerHealth(session, catalog.Props,
                _ => throw new Exception("Battle branch queried properties"), () => 0, () => new[] { true },
                () => throw new Exception("Battle branch queried the clock"));
            Near(blocked.HpPercent, 0.25f, "Original battle short circuit does not query the clock");
            now = now.AddSeconds(-20);
            Near(hp.HpPercent, 0.25f, "Backward clock does not subtract HP");
            now = now.AddSeconds(1000);
            Near(hp.HpPercent, 1f, "Recovery caps at one");
            Require(hp.RefreshHpPercent().IsApplied, "Refresh persists the original getter result and timestamp");
            session.Reload();
            Near(session.Save.Player.Health.HpPercentVal, 1f, "Refresh survives reload");
            Require(session.Save.Player.Health.HpUpdateTimeTicks == now.Ticks, "Original setter timestamp survives reload");
            Require(hp.SetHpPercent(0f).IsApplied, "Zero HP is valid");
            session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Near(session.Save.Player.Health.HpPercentVal, 0f, "Reload never initializes existing zero HP to full");
            byte[] before = File.ReadAllBytes(path);
            int beforeEvents = notifications;
            string temporary = path + ".tmp";
            Directory.CreateDirectory(temporary);
            bool failed = false;
            try { hp.SetHpPercent(0.6f); }
            catch (IOException) { failed = true; }
            catch (UnauthorizedAccessException) { failed = true; }
            finally { Directory.Delete(temporary); }
            Require(failed && before.SequenceEqual(File.ReadAllBytes(path)) && notifications == beforeEvents,
                "Actual disk failure preserves state and suppresses notification");
            Require(hp.SetHpPercent(0.6f).IsApplied, "Retry succeeds after disk failure");
            foreach (float invalid in new[] { -0.1f, 1.1f, float.NaN, float.PositiveInfinity })
                Require(!hp.SetHpPercent(invalid).IsApplied, "Reject invalid external health value");
            Require(!session.ApplyCharacterMutation(value => value.Health.HpPercentVal = -1f).IsApplied,
                "Save validator rejects invalid health from other mutations");

            var character = new OfflineCharacterProgressionService(session, catalog, refreshDerivedProgress: false);
            var actual = new OfflinePlayerHealth(session, character, () => 0, () => Array.Empty<bool>(), () => now);
            Require(actual.SetHpPercent(0.4f).IsApplied, "Set HP through the real character property adapter");
            now = now.AddSeconds(5);
            var actualProps = character.EvaluateProps(new DateTimeOffset(now).ToUnixTimeSeconds()).NonBattleProps;
            double actualSpeed = PlayerHealthRules.GetHangUpHpAddSpeed(
                CharacterPropFormula.Value(actualProps, add, catalog.Props),
                CharacterPropFormula.Value(actualProps, percent, catalog.Props));
            Near(actual.HpPercent, (float)Math.Min(1.0, 0.4f + actualSpeed * 5),
                "Production constructor reads the real current character NonBattleProps");

            // Isolate the original PublicPlayerIcon subtree to exercise the HP controller.
            // Full player-info, portrait and transcript hosts have their own acceptance flow.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Partners/UI/Panel/NpcPanel.prefab");
            var original = prefab.GetComponentsInChildren<TeamPlayerIconMeHpSliderNormal>(true).First();
            var canvas = new GameObject("HP widget acceptance canvas", typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("Original HP widget camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            var target = new RenderTexture(450, 800, 24);
            camera.targetTexture = target;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
            canvas.GetComponent<Canvas>().worldCamera = camera;
            canvas.GetComponent<Canvas>().planeDistance = 1;
            var scale = canvas.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(450, 800);
            var staging = new GameObject("Inactive original widget staging"); staging.SetActive(false);
            var icon = Instantiate(original.gameObject, staging.transform, false);
            icon.SetActive(false);
            var widget = icon.GetComponent<TeamPlayerIconMeHpSliderNormal>();
            Require(widget.HpSlider && widget.HpSlider.transform.IsChildOf(icon.transform), "Original serialized HP slider stays within its prefab subtree");
            // This partner prefab deliberately stores HpGo inactive. Production
            // keeps that flag. Only this isolated widget rendering check exposes
            // the original HP area, without changing its geometry or references.
            Require(widget.HpSlider.transform.parent.name == "HpGo"
                && !widget.HpSlider.transform.parent.gameObject.activeSelf, "Original partner HP area starts hidden");
            widget.HpSlider.transform.parent.gameObject.SetActive(true);
            foreach (var behavior in icon.GetComponentsInChildren<MonoBehaviour>(true))
                if (behavior != widget && !(behavior is Graphic) && !(behavior is Selectable)
                    && !(behavior is LayoutGroup) && !(behavior is LayoutElement) && !(behavior is ContentSizeFitter))
                    DestroyImmediate(behavior);
            var updates = new SafeAction();
            widget.Configure(new OfflinePartnerPlayerHpBindings(hp, updates, () => true, () => true));
            icon.transform.SetParent(canvas.transform, false);
            ((RectTransform)icon.transform).anchoredPosition = Vector2.zero;
            icon.SetActive(true);
            maps = 1;
            Require(hp.SetHpPercent(0.37f).IsApplied, "Set health before original widget Start");
            yield return null; yield return null;
            Near(widget.HpSlider.value, 0.37f, "Original Start and Init consume the live local HP port");
            Require(widget.HpSlider.fillRect.gameObject.activeInHierarchy, "Isolated original HP fill is visible");
            Capture(camera, target, Path.Combine(work, "health-stored.png"));
            Require(hp.SetHpPercent(0.12f).IsApplied, "Publish on-map health change");
            Near(widget.HpSlider.value, 0.12f, "Original event updates the on-map widget");
            maps = 0; now = now.AddSeconds(10);
            yield return null;
            Near(widget.HpSlider.value, hp.HpPercent, "Original Update renders off-map recovery");
            icon.SetActive(false);
            Require(hp.SetHpPercent(0.8f).IsApplied, "Change HP while original widget is disabled");
            icon.SetActive(true);
            yield return null;
            Near(widget.HpSlider.value, hp.HpPercent, "Original enable/disable event lifecycle restores current HP");
            Capture(camera, target, Path.Combine(work, "health-recovered.png"));
            camera.targetTexture = null;
            target.Release(); Destroy(target);
            Debug.Log("PLAYER_HEALTH_ACCEPTANCE_PASSED: original recovery branches, legacy/full/zero persistence, actual disk rollback/retry, original serialized widget lifecycle and pixels.");
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Require(pixels.GetPixels32().Select(value => (int)value.r << 16 | (int)value.g << 8 | value.b)
                    .Distinct().Count() > 20, "Original widget rendered nonempty native pixels");
            }
            finally { RenderTexture.active = previous; Destroy(pixels); }
        }
    }
}
