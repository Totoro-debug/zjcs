using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static bool HasHurricaneGriefCompanion(int active)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE");
            if (active != 10091 || string.IsNullOrWhiteSpace(path)) return false;
            var save = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            return save.Player.Skills.Plans[save.Player.Skills.SelectedPlan].PassiveSkills[0] == 10598;
        }

        private static bool IsHurricaneGriefVisible()
        {
            var camera = Camera.main;
            if (!camera) return false;
            foreach (var particle in FindObjectsOfType<ParticleSystem>())
            {
                if (particle.particleCount == 0
                    || (camera.cullingMask & (1 << particle.gameObject.layer)) == 0
                    || !particle.GetComponentsInParent<Transform>().Any(parent =>
                        parent.name.StartsWith("playable12660", StringComparison.Ordinal))) continue;
                Vector3 point = camera.WorldToViewportPoint(particle.transform.position);
                if (point.z > 0 && point.x > .08f && point.x < .92f
                    && point.y > .15f && point.y < .85f) return true;
            }
            return false;
        }

        private static void VerifyAcquiredHurricaneGrief(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output, ref int windHits)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25099
                && status.ApplyingSkillItemId == 10598 && status.SourceSkillLevel == 2);
            var darkHit = listener.Components.DarkHit;
            Require(darkHit != null && darkHit.Supported && darkHit.Rate == 1f
                && darkHit.IgnoreTeammate && darkHit.Elements.SequenceEqual(new[] { 100 })
                && darkHit.Statuses.Count == 1 && darkHit.Statuses[0].EntityId == 24488
                && Math.Abs(darkHit.Statuses[0].BasePercent - .35f) < .0001f
                && darkHit.Statuses[0].AffectedByProp,
                "The current wind listener lost its element, target, cultivation chance or original tear status.");

            windHits += step.DamageEvents.Count(hit => hit.SourceId == actor.Id
                && hit.SkillItemId > 0 && hit.ElementType == 100 && !hit.IsCure
                && !hit.Dodge && !hit.Blinding && !hit.Immunity);

            foreach (var transition in step.StatusViewEvents.Where(view => view.Status.ClassId == 24488
                && view.OwnerId != actor.Id && view.Status.CreatorId == actor.Id
                && (view.Kind == FightBattleStatusViewEventKind.Start
                    || view.Kind == FightBattleStatusViewEventKind.Update)))
            {
                var owner = checkpoint.Units.Single(unit => unit.Id == transition.OwnerId);
                var tear = owner.Statuses.SingleOrDefault(status => status.InstanceId == transition.Status.InstanceId
                    && status.ClassId == 24488);
                if (tear == null) continue;
                Require(tear.ApplyingSkillItemId == 10598 && tear.CreatorId == actor.Id
                    && tear.SourceSkillRank == listener.SourceSkillRank && tear.SourceSkillLevel == 2
                    && tear.SourceSubRank == actor.SubRank && tear.DurationRound == 1
                    && tear.CanDispersed && tear.StatusType == 2,
                    "The live tear lost its cultivated source, one-round duration or dispersible abnormal status.");
                File.WriteAllText(Path.Combine(output, "companion-hurricane-grief-status.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
            }

            foreach (var cast in step.SkillCasts.Where(value => (value.SkillEntityId == 12660
                    || value.SkillEntityId == 11950)
                && value.ActorId == actor.Id && value.SkillItemId == 10598))
            {
                bool bound = step.StatusViewEvents.Any(view => view.Status.ClassId == 24488
                    && view.OwnerId != actor.Id && view.Status.CreatorId == actor.Id)
                    || checkpoint.Units.Any(unit => cast.TargetIds.Contains(unit.Id)
                        && unit.Statuses.Any(status => status.ClassId == 24488
                            && status.CreatorId == actor.Id && status.ApplyingSkillItemId == 10598));
                if (bound) continue;
            }

            var tears = step.DamageEvents.Where(hit => hit.SourceId == actor.Id
                && hit.SkillItemId == 10598
                && (hit.SkillEntityId == 12660 || hit.SkillEntityId == 11950)).ToArray();
            foreach (var hit in tears)
                Require(hit.SourceId == actor.Id && hit.SkillItemId == 10598
                    && (hit.SkillEntityId == 12660 && hit.HitEntityId == 12661
                        || hit.SkillEntityId == 11950 && hit.HitEntityId == 11951)
                    && hit.DamageEntityId == 10004 && hit.ElementType == 100 && hit.DamageType == 1
                    && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure,
                    "The movement tear lost the current wind damage graph, runtime mapping or cultivated source.");
            windHits += tears.Length;
            if (tears.Length > 0)
                File.WriteAllText(Path.Combine(output, "companion-hurricane-grief-movement.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
        }

        private static bool HurricaneGriefTriggerCountMatches(FightBattleCheckpoint checkpoint, int windHits)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25099
                && status.ApplyingSkillItemId == 10598 && status.SourceSkillLevel == 2);
            return listener.Components.DarkHit != null
                && listener.Components.DarkHit.TriggerCount == windHits;
        }
    }
}
