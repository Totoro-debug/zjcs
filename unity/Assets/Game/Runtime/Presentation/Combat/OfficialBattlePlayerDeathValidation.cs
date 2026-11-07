using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Presentation.Combat
{
    // An isolated public-host diagnostic. It observes actual damage and original
    // death graphs; it does not call a death callback or alter a live profile.
    public sealed class OfficialBattlePlayerDeathValidation
    {
        private Transform source;
        private bool observed;
        public string CaptureName { get; private set; }

        public static OfficialBattlePlayerDeathValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 10000 : 5000000;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 100000000;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = hp;
                if (unit.Id != 1) continue;
                var attack = unit.Skills.First(value => value.Slot == 0);
                unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                unit.Skills.Add(attack); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            }
            return new OfficialBattlePlayerDeathValidation();
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (observed) return;
            var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
            if (player.Hp > 0) return;
            var root = controller.TryGetUnitTransform(player.Id);
            Require(root, "The dead player disappeared before its original death graph.");
            var animation = root.GetComponent<AnimationController>();
            Require(animation && animation.myAnimator, "The actual player root lost its original Animator controller.");
            var animator = animation.myAnimator;
            Require(animator.GetBool("die"), "The settled death did not start the original indefinite die parameter graph.");
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Die") || state.normalizedTime < .03f || animator.IsInTransition(0)) return;
            var native = animator.GetComponent<ProfessionNativeSceneRenderer>();
            Require(native && native.State != null, "The actual player's native skeleton is unavailable.");
            var meshes = native.GetComponentsInChildren<MeshFilter>()
                .Where(value => value.sharedMesh && value.sharedMesh.vertexCount > 0).ToArray();
            Require(meshes.Length > 0, "The actual death pose produced no body or weapon geometry.");
            var materials = meshes.Select(value => value.GetComponent<MeshRenderer>()).Where(value => value)
                .SelectMany(value => value.sharedMaterials).Where(value => value).ToArray();
            Require(materials.Length > 0 && materials.All(value => value.mainTexture
                && !value.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)),
                "The actual death pose still uses missing or placeholder body/part textures.");
            source = root; observed = true; CaptureName = "player-original-death";
            Debug.Log("[Player death] actual damaging hit, original die graph/Die Animator state, advanced native pose and body/part textures observed.");
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(observed && checkpoint.Finished && checkpoint.Units.Single(value => value.Id == 1).Hp == 0,
                "The public battle did not settle and present the actual player death.");
            Require(!source, "The battle retained the original dead player after release.");
            Debug.Log("[Player death] public damaging hit, original death graph, defeat settlement and release passed. Revival and forced replay are separate checks.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original player death: " + message); }
    }
}
