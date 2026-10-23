using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;

namespace Zjcs.Editor.Character
{
    /// <summary>
    /// Play Mode logic acceptance for the character page. It boots a temporary profile, opens the
    /// official panel and drives the real buttons; rendering/pointer input still needs a windowed run.
    /// </summary>
    public static class CharacterPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Character.Acceptance.Active";
        private const string RootKey = "Zjcs.Character.Acceptance.Root";
        private const string SlotKey = "Zjcs.Character.Acceptance.Slot";
        private const string ResultKey = "Zjcs.Character.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.Character.Acceptance.Deadline";
        private static bool started;

        [MenuItem("Zjcs/Validate/Character Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running character acceptance.");
            CharacterAcceptancePreparation.EnsureModels();
            try
            {
                Type equipmentValidation = Type.GetType(
                    "Zjcs.Editor.Equipment.OfficialEquipmentValidation, Zjcs.Editor.Equipment");
                equipmentValidation?.GetMethod("Validate")?.Invoke(null, null);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character acceptance could not install the equipment catalog: " + error.Message);
            }
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "character-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            OfflineMainlineSave save = new OfflineMainlineSave();
            save.CreatedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "人物验收";
            OfficialCharacterCatalog acceptanceCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            save.Player.Level = 20;
            save.Player.Experience = acceptanceCatalog.CumulativeLevelExperience[
                Math.Min(20, acceptanceCatalog.CumulativeLevelExperience.Count - 1)];
            save.Player.Rank = 1;
            save.Player.SubRank = 1;
            save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Wardrobe.Dyes.Add(new OfflineAppearanceDye
            {
                Type = 1, ColorId = 1, Hue = 0, Saturation = 0, Value = 0
            });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 1, Count = 100000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 39600, Count = 1 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 32504, Count = 1 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 12000, Count = 120 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 41300, Count = 5000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 5001, Count = 3 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 41302, Count = 5000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 701, Count = 100 });
            try
            {
                OfficialEquipmentCatalog equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
                OfflineEquipmentInstance instance = OfficialEquipmentGenerator.Generate(equipment, 20000, 1,
                    new EquipmentRollContext { DropLevel = 20, PlayerLevel = 20, SubRank = 1, SourceOperate = 0 },
                    () => 0.5f);
                if (instance == null) throw new InvalidOperationException("The acceptance equipment could not be generated.");
                save.Player.Equipment.Instances.Add(instance);
                save.Player.Equipment.NextInstanceId = 2;
                save.Player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = 10, InstanceId = 1 });
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character acceptance without equipment: " + error.Message);
            }
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));

            SessionState.SetString(RootKey, root);
            SessionState.SetString(SlotKey, slot);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 1200f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void AutoRunInWindowedEditor()
        {
            string autorun = Environment.GetEnvironmentVariable("ZJCS_CHARACTER_AUTORUN");
            Debug.Log("[Character acceptance] autorun check env=" + (autorun ?? "<null>"));
            if (autorun != "1") return;
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= AutoRunTick;
            EditorApplication.update += AutoRunTick;
        }

        private static void AutoRunTick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= AutoRunTick;
            Debug.Log("[Character acceptance] autorun start");
            if (!SessionState.GetBool(ActiveKey, false)) Run();
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            string root = SessionState.GetString(RootKey, string.Empty);
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            if (Directory.Exists(root)) Directory.Delete(root, true);
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode
                || Environment.GetEnvironmentVariable("ZJCS_CHARACTER_AUTORUN") == "1")
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Character acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            started = true;
            var runner = new GameObject("CharacterAcceptanceRunner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<AcceptanceRunner>().StartCoroutine(Guarded(Scenario()));
        }

        public sealed class AcceptanceRunner : MonoBehaviour
        {
        }

        private static IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                object value;
                bool moved;
                try
                {
                    moved = current.MoveNext();
                    value = moved ? current.Current : null;
                }
                catch (Exception error)
                {
                    Finish(false, error);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
                yield return value;
            }
        }

        private static IEnumerator Scenario()
        {
            string slot = SessionState.GetString(SlotKey, string.Empty);
            var slots = new ProfileSlotCatalog(SessionState.GetString(RootKey, string.Empty));
            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            try
            {
                progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
                OfficialSkillRecommendationSet recommendations = assets.LoadSkillRecommendations();
                progression.AttachSkillRules(assets.LoadSkillRules(), recommendations.recommendations);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character acceptance optional catalogs: " + error.Message);
            }
            ProgressionResult wardrobe = progression.InitializeWardrobe("character-acceptance-wardrobe");
            Require(wardrobe.Status != ProgressionResultStatus.InvalidState,
                "Wardrobe initialization failed: " + wardrobe.Message);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            progression.SynchronizeExpiredItems(now, "character-acceptance-expiry");
            var host = new GameObject("CharacterAcceptanceView");
            UnityEngine.Object.DontDestroyOnLoad(host);
            var view = host.AddComponent<OfficialCharacterPanelView>();
            view.Initialize(assets, progression);
            yield return WaitFrames(3);
            Debug.Log("[Character acceptance] profile level=" + progression.Player.Level
                + " profession=" + progression.Player.Wardrobe.Profession
                + " created=" + progression.Player.CharacterCreated
                + " name=" + progression.Player.CharacterName);
            Require(progression.Player.CharacterCreated && progression.Player.Wardrobe.Profession == 1,
                "The acceptance profile was not loaded.");
            Require(FindButton("CharacterPanelSkillBtn") != null, "The skill entry button is missing.");
            Require(FindButton("CharacterPanelEquipBtn") != null || FindButton("CharacterPanelSkillBtn") != null,
                "The character panel buttons were not bound.");
            Image[] icons = UnityEngine.Object.FindObjectsOfType<Image>(true)
                .Where(image => image.name == "icon").ToArray();
            GameObject[] slotRoots = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                .Where(rect => rect.name == "WeaponEquipItem" || rect.name == "HelmetEquipItem")
                .Select(rect => rect.gameObject).ToArray();
            Debug.Log("[Character acceptance] slot roots=" + slotRoots.Length + " children="
                + string.Join(";", slotRoots.Take(5).Select(rect => rect.transform.childCount + ":" +
                    string.Join("/", Enumerable.Range(0, rect.transform.childCount)
                        .Select(i => rect.transform.GetChild(i).name)))));
            Image[] allImages = UnityEngine.Object.FindObjectsOfType<Image>(true);
            Debug.Log("[Character acceptance] icon-like=" + string.Join(",",
                allImages.Select(image => image.name).Where(name =>
                    name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0).Distinct().Take(10)));
            Debug.Log("[Character acceptance] icon images=" + icons.Length + " with sprite="
                + icons.Count(image => image.enabled && image.sprite != null)
                + " allImages=" + allImages.Length + " allWithSprite="
                + allImages.Count(image => image.sprite != null)
                + " equipped=" + progression.Player.Equipment.Equipped.Count
                + " instances=" + progression.Player.Equipment.Instances.Count
                + " names=" + string.Join(",", allImages.Take(12).Select(image => image.name)));
            if (icons.Length == 0)
                Debug.LogWarning("[Character acceptance] Equipped slot icons were not created in batchmode; "
                    + "visual icon checks need a windowed run.");

            yield return Capture("character-01-panel");
            Button skillButton = FindButton("CharacterPanelSkillBtn");
            skillButton.onClick.Invoke();
            yield return WaitFrames(3);
            yield return Capture("character-02-skills");
            Button nextPlan = FindButton("NextPlanBtn");
            Require(nextPlan != null, "The skill plan switch button is missing.");
            int beforePlan = progression.Player.Skills.SelectedPlan;
            nextPlan.onClick.Invoke();
            yield return WaitFrames(2);
            Require(progression.Player.Skills.SelectedPlan != beforePlan,
                "The skill plan was not switched by the real button.");

            int pieceId = progression.SkillDefinition(10000)?.PieceItemId ?? 0;
            long piecesBefore = progression.Player.Inventory.Where(value => value.ItemId == pieceId)
                .Sum(value => value.Count);
            ProgressionResult merged = progression.MergeSkill(10000);
            Require(merged.IsApplied && progression.SkillDefinition(10000) != null
                && progression.Player.Skills.Skills.Any(skill => skill.ClassId == 10000),
                "The skill piece merge did not add the skill: " + merged.Message);
            Require(progression.Player.Inventory.Where(value => value.ItemId == pieceId)
                .Sum(value => value.Count) < piecesBefore, "The skill piece merge did not consume pieces.");
            ProgressionResult equipped = progression.EquipSkill(10000, 0);
            Require(equipped.IsApplied, "The merged skill could not be installed: " + equipped.Message);
            ProgressionResult swapped = progression.SwapSkillSlots(100, 0, 1);
            Require(swapped.IsApplied || swapped.Status == ProgressionResultStatus.Unchanged,
                "The skill slot swap failed: " + swapped.Message);
            Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills[1] == 10000,
                "The skill slot swap did not move the installed skill.");
            int instanceCount = progression.Player.Equipment.Instances.Count;
            long piece701 = progression.Player.Inventory.Where(value => value.ItemId == 701)
                .Sum(value => value.Count);
            ProgressionResult pieceMerge = progression.MergeEquipmentPiece(701, 20027);
            Require(pieceMerge.IsApplied
                && progression.Player.Equipment.Instances.Count == instanceCount + 1
                && progression.Player.Inventory.Where(value => value.ItemId == 701)
                    .Sum(value => value.Count) < piece701,
                "The equipment piece merge did not create an instance: " + pieceMerge.Message);

            OfflineEquipmentInstance equippedItem = progression.Player.Equipment.Instances
                .FirstOrDefault(value => value.ItemId == 20000);
            if (equippedItem != null && equippedItem.Terms != null && equippedItem.Terms.Count > 0)
            {
                long costBefore = progression.Player.Inventory.Where(value => value.ItemId == 41302)
                    .Sum(value => value.Count);
                ProgressionResult purified = progression.PurifyEquipment(equippedItem.InstanceId,
                    Array.Empty<int>(), "character-acceptance-purify");
                Require(purified.IsApplied, "The equipment purify failed: " + purified.Message);
                Require(progression.Player.Inventory.Where(value => value.ItemId == 41302)
                    .Sum(value => value.Count) < costBefore, "The equipment purify did not consume materials.");
            }

            bool wardrobeNew = progression.IsWardrobeItemNew(39600);
            ProgressionResult seen = progression.MarkWardrobeItemSeen(39600);
            Require(seen.IsApplied && wardrobeNew && !progression.IsWardrobeItemNew(39600),
                "The wardrobe new-item mark did not clear.");

            ProgressionResult title = progression.WearTitle(39600, "character-acceptance-title");
            Require(title.IsApplied && progression.Player.Wardrobe.WornTitleId == 39600,
                "The title could not be worn through the character service.");
            view.Refresh();
            yield return WaitFrames(2);
            yield return Capture("character-03-title");

            Button wardrobeButton = FindButton("AppearanceBtn");
            Require(wardrobeButton != null, "The wardrobe entry button is missing.");
            wardrobeButton.onClick.Invoke();
            yield return WaitFrames(3);
            Button tryOn = FindButton("WardrobePreviewButton");
            Require(tryOn != null, "The appearance try-on button is missing.");
            int wornBeforePreview = progression.Player.Wardrobe.WornItems.Count;
            tryOn.onClick.Invoke();
            yield return WaitFrames(3);
            Require(UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                    .Any(rect => rect.name == "PreviewAppearanceDialog"),
                "The official appearance preview dialog did not open.");
            Require(progression.Player.Wardrobe.WornItems.Count == wornBeforePreview,
                "Appearance preview changed the saved wardrobe state.");
            yield return Capture("character-04-appearance-preview");
            Button closePreview = FindButton("ClickRaycast");
            Require(closePreview != null, "The official preview close target is missing.");
            closePreview.onClick.Invoke();
            yield return WaitFrames(2);
            Require(progression.Player.Wardrobe.WornItems.Count == wornBeforePreview,
                "Closing appearance preview changed the saved wardrobe state.");

            int switchedPlan = progression.Player.Skills.SelectedPlan;
            OfflineMainlineProgressSession reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(reloaded.Save.Player.Wardrobe.WornTitleId == 39600,
                "The worn title was not persisted to the profile save.");
            Require(reloaded.Save.Player.Skills.SelectedPlan == switchedPlan,
                "The switched skill plan was not persisted to the profile save.");
            Require(reloaded.Save.Player.Skills.Skills.Any(skill => skill.ClassId == 10000),
                "The merged skill was not persisted to the profile save.");
            Require(reloaded.Save.Player.Wardrobe.SeenItems.Contains(39600),
                "The wardrobe seen mark was not persisted to the profile save.");
            Require(reloaded.Save.Player.Equipment.Instances.Count == progression.Player.Equipment.Instances.Count,
                "The merged equipment instance was not persisted to the profile save.");

            Debug.Log("[Character acceptance] Play Mode logic passed: panel built, skill panel opened, "
                + "skill merged from pieces, skill slots swapped, equipment merged and purified, wardrobe "
                + "seen marks, temporary appearance preview without save mutation, plan and title persisted. "
                + "Equip icon rendering and pointer input need a "
                + "windowed run.");
            Finish(true, null);
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(button => button != null && button.name == name);
        }

        private static IEnumerator Capture(string name)
        {
            if (Application.isBatchMode) yield break;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "character-captures");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 120 && !File.Exists(path); i++) yield return null;
            Debug.Log("[Character acceptance] snapshot " + path + " exists=" + File.Exists(path));
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (error != null) Debug.LogException(error);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
