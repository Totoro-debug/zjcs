using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Presentation.Combat
{
    // Explicit diagnostic input for the real public battle host, never a live profile mutation.
    public sealed class OfficialBattlePlayerCloneValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition attack;
        private readonly HashSet<string> observed = new HashSet<string>();
        private readonly List<ProfessionNativeSceneRenderer> renderers = new List<ProfessionNativeSceneRenderer>();
        private bool cast;
        private bool checkedHiddenSource;
        public string CaptureName { get; private set; }

        private OfficialBattlePlayerCloneValidation(FightOfficialCombatCatalog catalog, FightBattleSkillDefinition attack)
        { this.catalog = catalog; this.attack = attack; }

        public static OfficialBattlePlayerCloneValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var attack = player.Skills.First(value => value.Slot == 0);
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (unit != player && property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 5000000;
            }
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            var skill = catalog.CreateSkill(13100, 0, 10154, 5, 20, 1, null);
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            return new OfficialBattlePlayerCloneValidation(catalog, attack);
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (cast || !controller.Checkpoint.Receipts.Last().Event.SkillCasts.Any(value => value.SkillEntityId == 13100)) return;
            cast = true;
            var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            player.Skills.Add(attack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
        }

        public void ObserveFrame()
        {
            CaptureName = null;
            foreach (var playable in UnityEngine.Object.FindObjectsOfType<MyPlayableSpineClone>(true))
            {
                var native = playable.SkeletonAnimation as ProfessionNativeSceneRenderer;
                Require(native, "A public skill clone still targets the SDK renderer.");
                var animation = native.GetComponent<OfficialBattleSpineCloneAnimation>();
                Require(animation && !native.GetComponent<Animator>(), "The clone has no independent native clock.");
                if (!native.gameObject.activeInHierarchy || animation.AnimState?.GetCurrent(0) == null
                    || animation.AnimationTime <= 0) continue;
                Require(animation.AnimState.GetCurrent(0).Animation.Name == playable.AnimName,
                    "The original clone did not select its authored track.");
                Require(animation.PhysicsTime > 0 && native.State != null, "The clone pose/physics did not advance.");
                Require(!native.GetComponentInParent<SortingGroup>(true)
                    && native.GetComponentsInChildren<SortingGroup>(true).All(value => !value.enabled),
                    "The original ungrouped clone was assigned a new character or fragment sorting priority.");
                var source = playable.myApplyComponent;
                Require(source && source.TryGetComponent<Current.PlayerAppearance>(out _),
                    "The clone did not resolve the actual source player's appearance.");
                var sourceAppearance = source.GetComponent<Current.PlayerAppearance>();
                var sourceRenderer = source.GetComponent<ProfessionNativeSceneRenderer>();
                Require(sourceRenderer && !ReferenceEquals(sourceRenderer.State, native.State),
                    "A clone shares its source skeleton state.");
                var sourceAnimator = source.GetComponent<Animator>();
                if (sourceAnimator && !sourceAnimator.gameObject.activeInHierarchy)
                {
                    Require(OfficialBattleAnimatorPlayback.Supports(sourceAnimator,
                        new OfficialBattleSkillPlayableCue { trigger_param = "run" })
                        && !OfficialBattleAnimatorPlayback.Supports(sourceAnimator,
                            new OfficialBattleSkillPlayableCue { trigger_param = "__missing_clone_validation_parameter" }),
                        "A hidden source lost its actual controller contract or accepted an unknown parameter.");
                    checkedHiddenSource = true;
                }
                foreach (var pair in sourceAppearance.AppearanceRuntimeData.SkinMap)
                    if (sourceAppearance.AppearanceCharacter.TryGetSkinName(pair.Key, out var expected))
                        Require(native.GetComponent<Current.AppearanceCharacter>().TryGetSkinName(pair.Key, out var skin)
                            && skin == expected, "A clone lost or copied a different source skin.");
                var meshes = native.GetComponentsInChildren<MeshFilter>(true)
                    .Where(value => value.gameObject.activeInHierarchy && value.sharedMesh && value.sharedMesh.vertexCount > 0).ToArray();
                Require(meshes.Length > 0, "The active clone produced no native body/part geometry.");
                var materials = meshes.Select(value => value.GetComponent<MeshRenderer>()).Where(value => value)
                    .SelectMany(value => value.sharedMaterials).Where(value => value).ToArray();
                Require(meshes.Select(value => value.GetComponent<MeshRenderer>()).Where(value => value)
                    .All(value => value.sortingLayerID == 1182434123 && value.sortingOrder == 0)
                    && Mathf.Approximately(native.ZSpacing, -.0002f),
                    "The clone body/weapon renderer orders or original slot depth changed.");
                Require(materials.Length > 0 && materials.All(value => value.mainTexture
                    && !value.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)),
                    "The clone's actual body/part textures were not loaded.");
                if (!renderers.Contains(native)) renderers.Add(native);
                if (observed.Add(playable.AnimName))
                {
                    CaptureName = "clone-" + playable.AnimName.Replace('/', '-');
                    Debug.Log("[Player clone] original " + playable.AnimName
                        + ": source appearance, independent skeleton/clock, native body/parts and textures verified.");
                }
            }
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(cast && checkpoint.Finished && observed.SetEquals(new[] { "100/skill_62_1" }) && checkedHiddenSource,
                "The public 13100 first segment or its hidden source binding was not verified: " + string.Join(",", observed));
            Require(renderers.Count == 1 && renderers.All(value => !value), "The battle retained original clone renderers after release.");
            Debug.Log("[Player clone] public 13100 first segment completed: appearance/body/parts, independent clock, hidden source movement binding and battle release passed. Server combo chaining is not covered.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original player clone: " + message); }
    }
}
