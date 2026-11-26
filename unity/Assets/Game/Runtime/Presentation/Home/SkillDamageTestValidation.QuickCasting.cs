using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static Dictionary<int, int> quickCastingExpectedRounds;
        private static int quickCastingOpeners;
        private static int quickCastingEligibleSlots;

        private static void PrepareAcquiredQuickCasting(FightBattleUnitState actor)
        {
            var state = new FightSkillState(actor.SkillState);
            quickCastingEligibleSlots = actor.SkillState.Slots.Count(slot => state.LeftCooldown(slot.Slot) > 0);
            quickCastingExpectedRounds = actor.SkillState.Slots.ToDictionary(slot => slot.Slot,
                slot => slot.LastRound - (state.LeftCooldown(slot.Slot) > 0 ? 1 : 0));
            quickCastingOpeners = 0;
        }

        private static void VerifyAcquiredQuickCastingStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25113 && status.ApplyingSkillItemId == 10604);
            Require(listener.RoundStartTriggeredCount <= 1, "The quick casting source exceeded its original one-shot quota.");
            var openers = step.SkillCasts.Where(cast => cast.ActorId == 1 && cast.SkillEntityId == 12150).ToArray();
            if (openers.Length == 0) return;
            quickCastingOpeners += openers.Length;
            Require(quickCastingOpeners == 1 && openers[0].SkillItemId == 10604 && openers[0].Slot < 0
                && listener.RoundStartTriggeredCount == 1 && actor.SkillState.Round == 0,
                "The quick casting opener lost its source, opening phase or single invocation.");
            Require(actor.SkillState.Slots.All(slot => slot.ReleasedTimes == 0
                && quickCastingExpectedRounds[slot.Slot] == slot.LastRound),
                "The quick casting opener changed a ready skill or lost its one-round cooldown reduction.");
            var invokes = step.StatusViewEvents.Where(view => view.OwnerId == 1 && view.Status.ClassId == 24389
                && view.Kind == FightBattleStatusViewEventKind.Invoke).ToArray();
            Require(actor.Statuses.All(status => status.ClassId != 24389) && invokes.Length == 1
                && step.StatusViewEvents.Any(view => view.OwnerId == 1 && view.Status.ClassId == 24389
                    && view.Status.InstanceId == invokes[0].Status.InstanceId && view.Kind == FightBattleStatusViewEventKind.End),
                "The quick casting cooldown sub-status did not invoke once and end.");
            var actions = step.StatusCustomActions.Where(value => value.UnitId == 1 && value.Action == 3).ToArray();
            Require(actions.Length == (quickCastingEligibleSlots > 0 ? 1 : 0),
                "Quick casting must notify only an actual cooldown change.");
            Require(actions.Length == 0 || actions[0].HasDelay && Math.Abs(actions[0].Delay - openers[0].Delay - .35f) < .0001f,
                "The quick casting cooldown change lost its authored hit timing.");
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "The restored quick casting receipt reduced cooldowns twice.");
            File.WriteAllText(Path.Combine(output, "companion-quick-casting-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "companion-quick-casting-receipt.json"), JsonUtility.ToJson(
                new QuickCastingReceipt { active = actor.Skills.Single(skill => skill.Slot == 0).ItemId,
                    eligibleSlots = quickCastingEligibleSlots, openers = quickCastingOpeners,
                    cooldownActions = actions.Length, delay = actions.Length == 0 ? -1f : actions[0].Delay - openers[0].Delay }, true));
            Debug.Log("[Acquired passive combat] 10604 opening cooldown, timing and restore passed.");
        }

        [Serializable]
        private sealed class QuickCastingReceipt
        {
            public int active, eligibleSlots, openers, cooldownActions;
            public float delay;
        }
    }
}
