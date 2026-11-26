using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static IceGuardianReceipt iceGuardian;

        private static void PrepareAcquiredIceGuardian(FightBattleUnitState actor)
        {
            iceGuardian = new IceGuardianReceipt();
            // Supply a damaged target only in this explicit isolated diagnostic.
            actor.Hp = actor.MaxHp / 2;
            actor.Turn.Hp = actor.Hp;
        }

        private static void VerifyAcquiredIceGuardianStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25114
                && status.ApplyingSkillItemId == 10605);
            bool active = step.SkillCasts.Any(cast => cast.ActorId == actor.Id && cast.Slot >= 0
                && !cast.IsPetSupport);
            var protections = step.SkillCasts.Where(cast => cast.ActorId == actor.Id
                && cast.SkillEntityId == 12160).ToArray();
            if (step.ActorId == actor.Id)
            {
                Require(protections.Length == (active ? 0 : 1),
                    "Ice guardian did not inspect the completed owner's round for actual active casts.");
                if (active) iceGuardian.activeRounds++; else iceGuardian.idleRounds++;
            }
            foreach (var cast in protections)
                Require(cast.Slot < 0 && cast.SkillItemId == 10605,
                    "The ice protection child lost its cultivated passive source.");
            foreach (var view in step.StatusViewEvents.Where(view => view.OwnerId == actor.Id
                && view.Status.ClassId == 24390))
            {
                Require(view.Status.CreatorId == actor.Id,
                    "The ice protection view lost its original creator.");
                if (view.Kind == FightBattleStatusViewEventKind.Start) iceGuardian.starts++;
                if (view.Kind == FightBattleStatusViewEventKind.End) iceGuardian.ends++;
            }
            foreach (var status in actor.Statuses.Where(status => status.ClassId == 24390))
                Require(status.ApplyingSkillItemId == 10605 && status.CreatorId == actor.Id
                    && status.SourceSkillRank == listener.SourceSkillRank
                    && status.SourceSkillLevel == 2 && status.SourceSubRank == actor.SubRank,
                    "Ice protection lost its original owner or cultivation.");
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var props = assets.Load().CalculateSkillProps(12190, listener.SourceSkillRank, 2, actor.SubRank);
            long expected = (long)((decimal)actor.MaxHp * props[210] / 10000m + props[170]);
            foreach (var heal in step.DamageEvents.Where(hit => hit.SkillEntityId == 12190))
            {
                Require(step.ActorId == actor.Id && heal.SourceId == actor.Id && heal.TargetId == actor.Id
                    && heal.SkillItemId == 10605 && heal.HitEntityId == 12191 && heal.DamageEntityId == 12192
                    && heal.IsCure && heal.HasDamageValue && heal.DamageValue == expected
                    && heal.Damage == Math.Min(expected, heal.MaxHp - heal.BeforeHp),
                    "The thaw cure lost its next-owner-round timing, maximum-HP formula or HP cap.");
                iceGuardian.heals++;
                iceGuardian.restoredHp += heal.Damage;
            }
            if (iceGuardian.starts == 0) return;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var runner = assets.RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring ice protection repeated the status end or thaw cure.");
            File.WriteAllText(Path.Combine(output, "companion-ice-guardian-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "companion-ice-guardian-receipt.json"),
                JsonUtility.ToJson(iceGuardian, true));
        }

        private static void CompleteAcquiredIceGuardian(bool captured)
        {
            Require(captured && iceGuardian.activeRounds > 0 && iceGuardian.idleRounds > 0
                && iceGuardian.starts > 0 && iceGuardian.ends > 0 && iceGuardian.heals > 0
                && iceGuardian.restoredHp > 0,
                "The cultivated ice guardian did not render protection, suppress active rounds and restore actual HP.");
            Debug.Log("[Acquired passive combat] 10605 idle-round protection, thaw healing, cultivation and restore passed.");
        }

        [Serializable]
        private sealed class IceGuardianReceipt
        {
            public int activeRounds, idleRounds, starts, ends, heals;
            public long restoredHp;
        }
    }
}
