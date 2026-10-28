using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    // Explicit Editor/Windows Player diagnostic. Only command-line validation creates this component.
    public sealed class SkillDamageTestValidation : MonoBehaviour
    {
        private string output;
        private Exception failure;
        private float deadline;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--zjcs-validate-skill-damage");
            if (at < 0 || at + 1 >= args.Length) return;
            Application.runInBackground = true;
            var host = new GameObject("SkillDamageTestValidation");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<SkillDamageTestValidation>();
            runner.output = Path.GetFullPath(args[at + 1]);
            Directory.CreateDirectory(runner.output);
            runner.deadline = Time.realtimeSinceStartup + 300f;
            Application.logMessageReceived += runner.CaptureFailure;
            runner.StartCoroutine(runner.Guard(runner.Run()));
        }

        private void CaptureFailure(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failure = new InvalidOperationException(message + "\n" + trace);
        }

        private IEnumerator Guard(IEnumerator flow)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(flow);
            while (stack.Count > 0) {
                object next;
                try {
                    Require(Time.realtimeSinceStartup < deadline, "Damage-test Player verification timed out.");
                    if (failure != null) throw failure;
                    var current = stack.Peek();
                    if (!current.MoveNext()) { stack.Pop(); continue; }
                    next = current.Current;
                    if (next is IEnumerator child) { stack.Push(child); continue; }
                } catch (Exception error) {
                    Finish(false, error); yield break;
                }
                yield return next;
            }
            Finish(true, null);
        }

        private IEnumerator Run()
        {
            while (GameObject.Find("OfficialSaveSelectionPanel") == null) yield return null;
            string saveRoot = Path.Combine(output, "save-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(saveRoot); string slot = slots.CreateSlot();
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var save = new OfflineMainlineSave();
            save.Player.CharacterCreated = true; save.Player.CharacterName = "伤害测试验证";
            save.Player.Level = 40; save.Player.Rank = 1;
            save.Player.SubRank = assets.Load().LevelLimits.Where(value => value.PlayerLevel >= 40 && value.PlayerBlessLevel == 0).Min(value => value.SubRank);
            save.Player.InitialWeaponType = 200; save.Player.Wardrobe.Profession = 1;
            save.Player.Experience = assets.Load().CumulativeLevelExperience[40];
            save.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            progression.InitializeWardrobe("skill-damage-player-wardrobe");
            progression.AttachSkillRules(assets.LoadSkillRules(), assets.LoadSkillRecommendations().recommendations);
            Require(progression.EquipSkill(10014, 0).IsApplied, "Could not equip the damage-test Player skill.");
            foreach (Canvas canvas in FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (Camera camera in FindObjectsOfType<Camera>()) camera.enabled = false;
            yield return Exercise(slot, saveRoot, output, Click);
        }

        private void Finish(bool passed, Exception error)
        {
            Application.logMessageReceived -= CaptureFailure;
            if (passed) {
                try { ValidateCapturedFrames(output); }
                catch (Exception captureError) { passed = false; error = captureError; }
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = passed, error = error?.ToString() }, true));
            if (error != null) Debug.LogException(error);
            else Debug.Log("Skills damage-test Windows Player validation passed.");
            Application.Quit(passed ? 0 : 1);
        }

        private static void ValidateCapturedFrames(string directory)
        {
            string[] captures = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories);
            Require(captures.Length > 0, "Player verification produced no runtime pictures.");
            foreach (string path in captures) {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try {
                    Require(texture.LoadImage(File.ReadAllBytes(path)), "Runtime picture could not be decoded: " + path);
                    Require(Mathf.Abs((float)texture.width / texture.height - 675f / 1463f) < .005f,
                        "Runtime picture lost the reference portrait aspect: " + path);
                    Color32[] pixels = texture.GetPixels32();
                    int minimum = 765, maximum = 0;
                    for (int i = 0; i < pixels.Length; i += Math.Max(1, pixels.Length / 4096)) {
                        int brightness = pixels[i].r + pixels[i].g + pixels[i].b;
                        minimum = Math.Min(minimum, brightness); maximum = Math.Max(maximum, brightness);
                    }
                    Require(maximum - minimum > 48, "Runtime picture is a blank frame: " + path);
                } finally { Destroy(texture); }
            }
        }

        public static IEnumerator Exercise(string slot, string root, string outputDirectory, Action<Selectable> click)
        {
            var fixture = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            Require(fixture.ApplyCharacterMutation(player => {
                player.Level = 40;
                var catalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
                player.Experience = catalog.CumulativeLevelExperience[40];
                player.SubRank = catalog.LevelLimits.Where(value => value.PlayerLevel >= 40 && value.PlayerBlessLevel == 0).Min(value => value.SubRank);
                if (!player.Skills.Skills.Any(value => value.ClassId == 10014))
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
                if (!player.Inventory.Any(value => value.ItemId == 39503 && value.Count > 0))
                    player.Inventory.Add(new OfflineItemStack { ItemId = 39503, Count = 1 });
            }, "skills-home-damage-level").IsApplied, "Could not persist the damage-test opening level.");
            var fixtureAssets = OfficialCharacterAssetCatalog.LoadDefault();
            var fixtureProgression = new OfflineCharacterProgressionService(fixture, fixtureAssets.Load());
            fixtureProgression.AttachSkillRules(fixtureAssets.LoadSkillRules(), fixtureAssets.LoadSkillRecommendations().recommendations);
            fixtureProgression.EquipSkill(10014, 0);
            Require(fixtureProgression.WearSkillSkin(39503, "skills-home-damage-skin").IsApplied,
                "Could not wear the damage-test fixture's official skill appearance.");
            var home = Zjcs.Presentation.Home.HomeFeatureRuntime.Begin(slot, root, null);
            // This isolated level-40 fixture exercises skills; home introduction
            // graphs have their own acceptance flow and must not mask its input.
            home.TutorialAutoStartEnabled = false;
            yield return new WaitForSecondsRealtime(1f);
            Selectable homeCharacter = home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>()
                .First(value => value.name == "UnlockObj" && value.transform.parent.name == "Character");
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-home-entry.png"));
            yield return null;
            click(homeCharacter);
            yield return new WaitForSecondsRealtime(.8f);
            var skillEntry = FindObjectsOfType<Button>().First(value => value.name == "CharacterPanelSkillBtn");
            Require(Mathf.Approximately(skillEntry.GetComponentInParent<Canvas>().rootCanvas.scaleFactor,
                Mathf.Min(Screen.width / 1080f, Screen.height / 1920f)),
                "The Home character page inherited Home's mixed-axis scale instead of the original Expand scale.");
            click(skillEntry);
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-home-skill-entry.png"));
            yield return null;
            click(FindObjectsOfType<Button>().First(value => value.name == "StageDamageTestBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Transform settings = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "damage_test");
            Toggle Choice(string group, string name) => settings.GetComponentsInChildren<Toggle>()
                .First(value => value.transform.parent.name == name && value.GetComponentsInParent<Transform>().Any(parent => parent.name == group));
            foreach (string group in new[] { "Round", "MonsterHp", "MonsterHurt" }) {
                Toggle choice = Choice(group, "ToggleText");
                if (!choice.isOn) click(choice);
                Require(choice.isOn, "The damage-test selection was not retained: " + group);
            }
            yield return null;
            long[] inventory = home.Profile.Save.Player.Inventory.OrderBy(value => value.ItemId)
                .SelectMany(value => new[] { (long)value.ItemId, value.Count }).ToArray();
            click(settings.GetComponentsInChildren<Button>().First(value => value.name == "StartTest"));
            yield return new WaitForSecondsRealtime(1f);
            var battle = home.GetComponent<Zjcs.Presentation.Home.SkillDamageTestBattleRunner>();
            Require(battle != null && battle.Controller != null, "Starting the original damage test did not mount a battle.");
            Require(home.Hud.AnyModalOpen, "The home world accepted input behind the damage test.");
            var checkpoint = battle.Controller.Checkpoint;
            Require(checkpoint.StageType == 43 && checkpoint.MaxRound == 10 && checkpoint.DamageTest.EnableMonsterOneDamage
                && checkpoint.DamageTest.EnableMonsterUnlimitHp, "The battle lost its selected damage-test rules.");
            var skinnedSkill = checkpoint.Units.First(value => value.Id == 1).Skills.First(value => value.Slot == 0);
            Require(skinnedSkill.ItemId == 10014 && skinnedSkill.UseItemId == 10125 && skinnedSkill.SkillSkinId == 39503,
                "The actual battle did not assemble the equipped skill appearance.");
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-battle.png"));
            yield return null;
            Button start = UnityEngine.Object.FindObjectsOfType<Button>().First(value => value.name == "Btn"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "StartBtn"));
            click(start);
            float oldScale = Time.timeScale;
            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 180f;
            while (battle != null && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = oldScale;
            Require(battle == null && checkpoint.Outcome.Finished, "The damage test did not finish through its actual battle animation flow.");
            yield return new WaitForSecondsRealtime(.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-result.png"));
            yield return null;
            Transform result = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "damage_test_result");
            Require(result.GetComponentsInChildren<TMPro.TMP_Text>().Any(value => value.text.Contains("测试完成")),
                "The native damage-test result did not render its completion title.");
            Require(inventory.SequenceEqual(home.Profile.Save.Player.Inventory.OrderBy(value => value.ItemId)
                .SelectMany(value => new[] { (long)value.ItemId, value.Count })), "A damage test changed the character's item amounts.");
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-result.png"));
            yield return null;
            click(result.GetComponentsInChildren<Button>().First(value => value.name == "BtnBattleData"));
            yield return new WaitForSecondsRealtime(.4f);
            var report = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "damage_test_report");
            Require(report.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldBattleReportPlayerRow>().Length == 1,
                "The native battle report did not show the player's statistics.");
            RequireReportPortrait(report);
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-player-report.png"));
            yield return null;
            click(report.GetComponentsInChildren<Button>().First(value => value.name == "TabGroupItem_11 (1)"));
            yield return new WaitForSecondsRealtime(.4f);
            Require(report.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldBattleReportSkillRow>().Length > 0,
                "The native skill report did not show skill rows.");
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-report.png"));
            SaveLayoutDiagnostics(Path.Combine(outputDirectory, "skills-report-layout.txt"));
            yield return null;
            var reportIcon = report.GetComponentsInChildren<Button>().First(value => value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "ReportSkillIcon"));
            click(reportIcon);
            yield return new WaitForSecondsRealtime(.4f);
            var snapshotDetail = FindObjectsOfType<Transform>().First(value => value.name == "skill_plain_detail");
            Require(!snapshotDetail.GetComponentsInChildren<Button>().Any(value => value.name == "EquipBtn" || value.name == "UpgradeBtn"),
                "The completed-battle skill detail exposed current-plan editing actions.");
            Require(snapshotDetail.GetComponentsInChildren<TMPro.TMP_Text>().Any(value => value.name == "LevelText"
                && value.text.Contains(skinnedSkill.Level.ToString())), "The completed-battle detail lost the recorded skill level.");
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-snapshot-detail.png"));
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            click(FindObjectsOfType<Button>().Where(value => value.name == "BlurMask")
                .OrderByDescending(value => value.GetComponentInParent<Canvas>().sortingOrder).First());
            yield return new WaitForSecondsRealtime(.4f);
            Require(!FindObjectsOfType<Transform>().Any(value => value.name == "skill_plain_detail")
                && report != null && report.gameObject.activeInHierarchy, "Closing completed-battle detail did not preserve its report.");
            click(report.GetComponentsInChildren<Button>().First(value => value.name == "CloseBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            result = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "damage_test_result");
            Require(!result.GetComponentsInChildren<Button>().Any(value => value.name == "ShareBtn"),
                "The offline result still exposes online report sharing.");
            Require(!Directory.Exists(Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "skill-damage-reports")),
                "A temporary damage-test report was persisted to the profile.");
            Require(!FindObjectsOfType<Transform>().Any(value => value.name == "damage_test_report"),
                "Closing the statistics page left its report window alive.");
            click(result.GetComponentsInChildren<Button>().First(value => value.name == "Setting"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(UnityEngine.Object.FindObjectsOfType<Transform>().Any(value => value.name == "damage_test"),
                "The damage-test result setting button did not reopen native settings.");
            var reopenedSettings = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "damage_test");
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "skills-damage-settings-return.png"));
            SaveLayoutDiagnostics(Path.Combine(outputDirectory, "skills-home-layout.txt"));
            yield return null;
            click(reopenedSettings.GetComponentsInChildren<Button>().First(value => value.name == "CloseBtn"));
            yield return null;
            Require(home.Hud.CharacterPageOpen, "The damage test did not restore the character page.");
            Debug.Log("[Skills acceptance] Home entry, native damage-test settings, real battle, result, settings return and unchanged item amounts passed.");
            UnityEngine.Object.Destroy(home.gameObject);
            yield return null;
        }

        private static void Click(Selectable control)
        {
            Require(control != null && control.IsActive() && control.IsInteractable(), "Cannot click an inactive damage-test control.");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            Vector3[] corners = new Vector3[4]; ((RectTransform)control.transform).GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++) {
                pointer.position = new Vector2(Mathf.Lerp(min.x, max.x, (x + .5f) / 5), Mathf.Lerp(min.y, max.y, (y + .5f) / 5));
                if (pointer.position.x < 0 || pointer.position.x >= Screen.width || pointer.position.y < 0 || pointer.position.y >= Screen.height) continue;
                hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || (hits[0].gameObject != control.gameObject && !hits[0].gameObject.transform.IsChildOf(control.transform))) continue;
                GameObject hit = hits[0].gameObject; pointer.pointerPressRaycast = hits[0];
                ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
                return;
            }
            throw new InvalidOperationException("No unobstructed damage-test pointer target: " + DescribePointerTarget(control));
        }
        private static string DescribePointerTarget(Selectable control)
        {
            string PathOf(Transform node) => node.parent == null ? node.name : PathOf(node.parent) + "/" + node.name;
            var rect = (RectTransform)control.transform;
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) }, hits);
            return PathOf(control.transform) + " corners=" + corners[0] + ".." + corners[2]
                + " screen=" + Screen.width + "x" + Screen.height
                + " graphics=" + string.Join(",", control.GetComponents<Graphic>().Select(value => value.GetType().Name
                    + ":depth=" + value.depth + ":raycast=" + value.raycastTarget + ":culled=" + value.canvasRenderer.cull))
                + " hits=" + string.Join(",", hits.Take(8).Select(value => PathOf(value.gameObject.transform)));
        }

        private static void SaveLayoutDiagnostics(string path)
        {
            string PathOf(Transform node) => node.parent == null ? node.name : PathOf(node.parent) + "/" + node.name;
            var rows = UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>(true).Select(text =>
                PathOf(text.transform) + " active=" + text.gameObject.activeInHierarchy + " text=" + text.text
                + " rect=" + text.rectTransform.rect + " pos=" + text.rectTransform.position + " color=" + text.color
                + " canvasAlpha=" + text.canvasRenderer.GetInheritedAlpha());
            rows = rows.Concat(UnityEngine.Object.FindObjectsOfType<RawImage>(true).Select(image =>
                PathOf(image.transform) + " active=" + image.gameObject.activeInHierarchy + " texture=" + image.texture));
            rows = rows.Concat(UnityEngine.Object.FindObjectsOfType<Text>(true).Select(text =>
                PathOf(text.transform) + " active=" + text.gameObject.activeInHierarchy + " enabled=" + text.enabled
                + " legacyText=" + text.text + " rect=" + text.rectTransform.rect + " pos=" + text.rectTransform.position
                + " color=" + text.color + " canvasAlpha=" + text.canvasRenderer.GetInheritedAlpha()));
            File.WriteAllLines(path, rows);
        }

        private static void RequireReportPortrait(Transform report)
        {
            var head = report.GetComponentsInChildren<RawImage>().FirstOrDefault(value => value.name == "HeadImg");
            Require(head != null && head.isActiveAndEnabled && head.texture is Texture2D,
                "The original report portrait provider did not bind a visible head texture.");
            Require(head.uvRect == new Rect(0f, 0f, 1f, 1f),
                "The report portrait did not preserve the original full-texture UV rectangle.");
            Color32[] pixels = ((Texture2D)head.texture).GetPixels32();
            Require(pixels.Any(value => value.a > 128 && value.r > 80)
                && pixels.Any(value => value.a > 128 && value.r < 60),
                "The report portrait contains no photographed character detail.");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
