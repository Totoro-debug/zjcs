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
            try
            {
                Zjcs.Editor.Equipment.OfficialEquipmentValidation.Validate();
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
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 1, Count = 100000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 39600, Count = 1 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 12000, Count = 20 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 41300, Count = 5000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 5001, Count = 3 });
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
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
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
            Require(UnityEngine.Object.FindObjectsOfType<Image>(true).Any(image =>
                image.name == "icon" && image.enabled && image.sprite != null),
                "The equipped item icon was not rendered from the official sprite catalog.");

            Button skillButton = FindButton("CharacterPanelSkillBtn");
            skillButton.onClick.Invoke();
            yield return WaitFrames(3);
            Button nextPlan = FindButton("NextPlanBtn");
            Require(nextPlan != null, "The skill plan switch button is missing.");
            int beforePlan = progression.Player.Skills.SelectedPlan;
            nextPlan.onClick.Invoke();
            yield return WaitFrames(2);
            Require(progression.Player.Skills.SelectedPlan != beforePlan,
                "The skill plan was not switched by the real button.");

            ProgressionResult title = progression.WearTitle(39600, "character-acceptance-title");
            Require(title.IsApplied && progression.Player.Wardrobe.WornTitleId == 39600,
                "The title could not be worn through the character service.");
            view.Refresh();
            yield return WaitFrames(2);

            int switchedPlan = progression.Player.Skills.SelectedPlan;
            OfflineMainlineProgressSession reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(reloaded.Save.Player.Wardrobe.WornTitleId == 39600,
                "The worn title was not persisted to the profile save.");
            Require(reloaded.Save.Player.Skills.SelectedPlan == switchedPlan,
                "The switched skill plan was not persisted to the profile save.");

            Debug.Log("[Character acceptance] Play Mode logic passed: panel built, equip icon rendered, "
                + "skill plan switched and title/save persisted. Rendering and pointer input need a windowed run.");
            Finish(true, null);
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(button => button != null && button.name == name);
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
