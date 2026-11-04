using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialBattlePetHudAcceptance
    {
        private const string Key = "Zjcs.BattlePetHudAcceptance";
        private const string Pet = "/FightMainPanel/Content/Bottom/Anim/SkillListContent/BgSlot/GamePetSkillIconItem";
        [Serializable] private sealed class Evolution { public int classId, phase, supportSkillId; public string resource; }
        [Serializable] private sealed class Payload { public Evolution[] evolutions; }

        static OfficialBattlePetHudAcceptance()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.playModeStateChanged += Mode;
        }

        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            ProjectTools.Validate();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
            EditorApplication.EnterPlaymode();
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.update += Start;
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(Key, false);
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Start()
        {
            EditorApplication.update -= Start;
            new GameObject("BattlePetHudProbe").AddComponent<Probe>().StartCoroutine(Exercise());
        }

        private static IEnumerator Exercise()
        {
            var output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            GameObject canvas = null;
            OfficialBattleHudView view = null;
            Exception failure = null;
            try
            {
                BattleGameViewInput.Focus();
                canvas = new GameObject("OriginalBattlePetHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                view = new OfficialBattleHudView(OfficialBattleHudCatalog.LoadDefault());
                view.Mount(canvas.transform);
                Verify(view);
                VerifyDischarge(view);
            }
            catch (Exception error) { failure = error; Debug.LogException(error); }
            if (failure == null)
            {
                // Let the unchanged original enter animation finish before inspecting its pixels.
                for (int i = 0; i < 90; i++) { view.Tick(1f / 60f); yield return null; }
                yield return BattleGameViewInput.CaptureGameViewScreenshot(Path.Combine(output, "pet-hud.png"));
                File.WriteAllText(Path.Combine(output, "pet-hud-passed.txt"), "Original pet HUD and battle pet assembly checks passed.");
                Debug.Log("BATTLE_PET_HUD_ACCEPTANCE_PASSED");
            }
            if (canvas) UnityEngine.Object.Destroy(canvas);
            SessionState.SetInt(Key + ".result", failure == null ? 0 : 1);
            EditorApplication.isPlaying = false;
        }

        private static void Verify(OfficialBattleHudView view)
        {
            var data = JsonUtility.FromJson<Payload>(Resources.Load<TextAsset>("Combat/battle-pet-hud").text);
            var player = new OfflinePlayerProgress();
            var checkpoint = new FightBattleCheckpoint();
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var catalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            Require(!catalog.Rows("pet_evolution").Any(), "Fixture must exercise the actual missing progression tables.");
            view.RefreshPet(checkpoint, player);
            Require(!view.Find(Pet).activeSelf, "No owned pets must hide the original pet widget.");
            var pet = new OfflinePetInstance { Index = 1, ClassId = 1600, EvolutionPhase = 2 };
            player.Pets.Pets.Add(pet);
            view.RefreshPet(checkpoint, player);
            Require(view.Find(Pet + "/BgNone").activeSelf, "An owned but unequipped pet must show the original empty state.");
            player.Skills.Plans[0].PetIndex = 1;
            int portraits = 0, entities = 0, supports = 0, activeSupports = 0, passiveSupports = 0;
            foreach (var evolution in data.evolutions.Where(value => value.classId != 1619))
            {
                pet.ClassId = evolution.classId; pet.EvolutionPhase = evolution.phase;
                var owner = Owner(combat);
                bool configured = OfficialBattlePetEnergyAssembler.TryConfigure(owner, player, catalog, combat, combatAssets.LoadSkillItems());
                checkpoint.Units.Clear(); checkpoint.Units.Add(owner);
                if (configured && owner.PetEnergy.Enabled)
                {
                    FightBattlePetEnergyRuntime.Initialize(owner);
                    Require(owner.PetEnergy.NeedEnergy > 0 && owner.PetEnergy.EntityProperties.Count > 0,
                        "Pet entity properties/energy were not assembled for " + pet.ClassId);
                    entities++;
                }
                else if (configured)
                {
                    Require(evolution.phase == 1 && owner.PetEnergy.SupportSkillsApplied
                        && (owner.Statuses.Count > 0 || owner.PetEnergy.SupportSkills.Count > 0)
                        && !owner.PetEnergy.Enabled && owner.PetEnergy.PetClassId == pet.ClassId,
                        "Childhood support must retain its pet identity without creating an energy transfer entity.");
                    if (owner.PetEnergy.SupportSkills.Count > 0)
                    {
                        activeSupports++;
                        Require(owner.PetEnergy.SupportSkills.All(skill => skill.Slot < 0)
                            && owner.PetEnergy.SupportSkillState.Slots.Count == owner.PetEnergy.SupportSkills.Count
                            && owner.Skills.Count == 0 && owner.SkillState.Slots.Count == 0,
                            "Childhood support polluted the owner's ordinary skill agent.");
                    }
                    else passiveSupports++;
                    string before = FightBattleCheckpointSerializer.Save(checkpoint);
                    Require(OfficialBattlePetEnergyAssembler.TryConfigure(owner, player, catalog, combat, combatAssets.LoadSkillItems())
                        && before == FightBattleCheckpointSerializer.Save(checkpoint),
                        "Repeated pet support assembly must not duplicate properties/statuses.");
                    supports++;
                }
                view.RefreshPet(checkpoint, player);
                var icon = view.Find(Pet + "/Content/Bg/Anim/Mask/PetIcon").GetComponent<Image>();
                Require(icon.enabled && icon.sprite && icon.sprite.texture == Resources.Load<Texture2D>(evolution.resource),
                    "Evolution did not bind its actual original portrait: " + pet.ClassId + "/" + pet.EvolutionPhase);
                Require(icon.GetComponent<OfficialBattleSkillQualityGeometry>() != null,
                    "Original portrait trim and UVs were lost.");
                Require(!view.Find(Pet + "/BgNone").activeSelf && view.Find(Pet + "/SupportSkillRoot").activeSelf,
                    "Equipped pet visibility differs from the original widget.");
                portraits++;
            }
            Require(portraits == 39 && entities == 18 && supports == 19
                && activeSupports == 10 && passiveSupports == 9 && !catalog.Rows("pet_evolution").Any(),
                "Pet source coverage or original catalog isolation differs.");
            pet.ClassId = 1600; pet.EvolutionPhase = 2;
            var main = Owner(combat);
            Require(OfficialBattlePetEnergyAssembler.TryConfigure(main, player, catalog, combat, combatAssets.LoadSkillItems()),
                "Real equipped pet could not be configured.");
            FightBattlePetEnergyRuntime.Initialize(main);
            checkpoint.Units.Clear(); checkpoint.Units.Add(main);
            Require(main.PetEnergy.CurrentEnergy == 600 && main.PetEnergy.NeedEnergy == 1000,
                "Pinned official entity-skill initial/required energy differs.");
            view.RefreshPet(checkpoint, player);
            AssertEnergy(view, .6f, "60%");
            FightBattlePetEnergyRuntime.RecoverAtPetRoundStart(main);
            // Executed original NumberFormatHelper golden: float -> double, then
            // three-significant-digit truncation retains the float's lower tail.
            view.RefreshPet(checkpoint, player); AssertEnergy(view, .65f, "64.9%");
            main.PetEnergy.CurrentEnergy = 1200;
            view.RefreshPet(checkpoint, player); AssertEnergy(view, 1f, "120%");
            Require(FightBattlePetEnergyRuntime.ConsumeForRelease(main.PetEnergy), "Ready pet did not consume energy.");
            view.RefreshPet(checkpoint, player); AssertEnergy(view, .2f, "20%");
            main.PetEnergy.NeedEnergy = 0;
            view.RefreshPet(checkpoint, player); AssertEnergy(view, 0f, "0%");
            // UI quality follows the saved support-skill rank, rather than Item.Quality.
            pet.SkillRanks.Add(new OfflinePetSkillRank { SkillId = 16000, Rank = 16 });
            view.RefreshPet(checkpoint, player);
            var quality = view.Find(Pet + "/SupportSkillRoot/SkillContent/Quality").GetComponent<Image>();
            Require(quality.sprite == OfficialBattleSkillQualityCatalog.Load().FindQuality(6), "Saved skill rank quality was ignored.");
            pet.SkillRanks.Clear();
            main.PetEnergy.NeedEnergy = 1000; main.PetEnergy.CurrentEnergy = 650;
            view.RefreshPet(checkpoint, player);
            Debug.Log("Pet HUD verified: 39 original evolution portraits; " + entities
                + " configured entity phases; untouched base catalog; original empty/equipped states; actual initialize/recover/consume energy; 0/64.9/120 percent; saved rank quality.");
        }

        private static void VerifyDischarge(OfficialBattleHudView view)
        {
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = new OfflinePlayerProgress();
            player.Pets.Pets.Add(new OfflinePetInstance { Index = 1, ClassId = 1600, EvolutionPhase = 2 });
            player.Skills.Plans[0].PetIndex = 1;
            var checkpoint = new FightBattleCheckpoint();
            var owner = Owner(combat); var other = Owner(combat); other.Id = 2;
            checkpoint.Units.Add(owner); checkpoint.Units.Add(other);
            view.RefreshPet(checkpoint, player);
            var animation = view.Find("/FightMainPanel").GetComponent<OfficialBattleHudAnimationPlayer>();
            animation.enabled = false;
            var support = view.Find(Pet + "/SupportSkillRoot");
            var group = support.GetComponent<CanvasGroup>() ?? support.AddComponent<CanvasGroup>();
            group.alpha = .7f;
            var world = new GameObject("Pet support native event probe");
            var actor = new GameObject("Owner"); actor.transform.SetParent(world.transform);
            var victim = new GameObject("Other"); victim.transform.SetParent(world.transform);
            try
            {
                var mapper = new OfficialBattleGridMapper(world.transform, new FightBattleGrid { Width = 8, Height = 8 });
                using var scope = new Zjcs.Offline.BattleNativePlayables.OfflineBattleNativePlayableScope(world.transform, null);
                using var entities = new OfficialBattleSkillEntityPlayback(world.transform, mapper, scope,
                    id => checkpoint.Units.SingleOrDefault(unit => unit.Id == id));
                entities.CastStarted += view.OnPetSkillStarted;
                void Start(int actorId, int itemId) => entities.Start(new FightBattleSkillCastEvent {
                    ActorId = actorId, Slot = -1, SkillEntityId = 50000, SkillItemId = itemId,
                    Presentation = new FightBattleSkillCastPresentation { TargetId = actorId == 1 ? 2 : 1,
                        TargetX = 1, FacingIndex = 3, SkillTargetType = 1, Duration = .5f }
                }, null, actorId == 1 ? actor.transform : victim.transform,
                    actorId == 1 ? victim.transform : actor.transform);
                // The native entity/agent path is real; this notification fixture
                // intentionally omits the world's full skill-effect graph.
                Start(2, 16000); Start(1, 10032);
                Require(Mathf.Abs(group.alpha - .7f) < .00001f, "Foreign/unrelated skills animated the pet HUD.");
                Start(1, 16000);
                Require(group.alpha == 0 && Mathf.Abs(support.transform.localScale.x - .31054f) < .00001f,
                    "Native negative-slot childhood support did not start the original discharge.");
                var nativeRotation = view.Find(Pet).GetComponent<Animation>();
                var originalStart = new Quaternion(0f, 0f, -.373231709f, .92773819f);
                Require(nativeRotation && Mathf.Abs(Quaternion.Dot(support.transform.localRotation, originalStart)) > .999999f,
                    "Original native quaternion start was lost.");
                nativeRotation["FightMainPanel_GamePetSkillIconItem_Discharge"].time = .2f;
                nativeRotation.Sample();
                Require(Mathf.Abs(Quaternion.Dot(support.transform.localRotation, originalStart)) < .99f,
                    "Original quaternion animation did not advance.");
                animation.Tick(.2f);
                Require(group.alpha == 1 && Mathf.Abs(support.transform.localScale.x - 1f) < .00001f,
                    "Original pet discharge curves were not sampled.");
                Start(1, 16300);
                Require(group.alpha == 0 && Mathf.Abs(support.transform.localScale.x - .31054f) < .00001f,
                    "Current-evolution support did not restart the original animation from zero.");
                animation.Tick(1.25f);
                Require(group.alpha == 0 && Mathf.Abs(support.transform.localScale.x - .36368f) < .00001f,
                    "Original discharge ending differs.");
                player.Skills.Plans[0].PetIndex = 0; view.RefreshPet(checkpoint, player);
                group.alpha = .7f; Start(1, 16000);
                Require(Mathf.Abs(group.alpha - .7f) < .00001f, "Unequipped pet animated a stale support item.");
                player.Skills.Plans[0].PetIndex = 1; view.RefreshPet(checkpoint, player);
                Debug.Log("Pet support native start/foreign owner/item filtering/negative slot/original discharge/restart/end checks passed.");
            }
            finally { animation.enabled = true; UnityEngine.Object.Destroy(world); }
        }

        private static FightBattleUnitState Owner(FightOfficialCombatCatalog combat)
        {
            var unit = new FightBattleUnitState { Id = 1, Side = 1, Level = 1, SubRank = 1, Hp = 1000, MaxHp = 1000 };
            unit.Properties = OfficialBattlePropertyAssembler.ToProperties(new System.Collections.Generic.Dictionary<int, long>
            { [combat.PropertyType("MaxHp")] = 1000, [combat.PropertyType("Attack")] = 100,
              [combat.PropertyType("Defence")] = 100, [combat.PropertyType("Speed")] = 100 });
            return unit;
        }
        private static void AssertEnergy(OfficialBattleHudView view, float fill, string text)
        {
            Require(Mathf.Abs(view.Find(Pet + "/Content/Bg/Energy").GetComponent<Image>().fillAmount - fill) < .00001f
                && view.Find(Pet + "/Content/Bg/EnergyTxt").GetComponent<TMP_Text>().text == text,
                "Original energy progress/text differs from " + text);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private sealed class Probe : MonoBehaviour { }
    }
}
