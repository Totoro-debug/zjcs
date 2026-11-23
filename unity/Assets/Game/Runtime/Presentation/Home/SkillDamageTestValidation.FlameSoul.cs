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
        private static int DestroyerActiveCompanion(int active)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE");
            if ((active != 10028 && active != 10035) || string.IsNullOrWhiteSpace(path)) return 0;
            var save = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            int itemId = save.Player.Skills.Plans[save.Player.Skills.SelectedPlan].PassiveSkills[0];
            return active == 10028 && (itemId == 10599 || itemId == 10503 || itemId == 10573) || active == 10035 && itemId == 10557
                ? itemId : 0;
        }

        private static bool IsNativeSkillParticleVisible(string playablePrefix)
        {
            var camera = Camera.main;
            if (!camera) return false;
            foreach (var particle in FindObjectsOfType<ParticleSystem>())
            {
                if (particle.particleCount == 0 || (camera.cullingMask & (1 << particle.gameObject.layer)) == 0
                    || !particle.GetComponentsInParent<Transform>().Any(parent =>
                        parent.name.StartsWith(playablePrefix, StringComparison.Ordinal))) continue;
                Vector3 point = camera.WorldToViewportPoint(particle.transform.position);
                if (point.z > 0 && point.x > .08f && point.x < .92f && point.y > .15f && point.y < .85f)
                    return true;
            }
            return false;
        }

        private static void VerifyAcquiredFlameSoul(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25103);
            var buff = actor.Statuses.SingleOrDefault(status => status.ClassId == 24386);
            if (step.ActorId != 1 || !step.SkillCasts.Any(cast => cast.ActorId == 1 && cast.SkillEntityId == 10800)) return;
            Require(buff != null && buff.ApplyingSkillItemId == 10599 && buff.CreatorId == actor.Id
                && buff.SourceSkillRank == listener.SourceSkillRank && buff.SourceSkillLevel == 2
                && buff.SourceSubRank == 8 && buff.StackCount >= 1 && buff.StackCount <= 8
                && buff.DurationRound == 2 && buff.CanDispersed,
                "The cultivated fire cast lost its original critical stack, refresh or source.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var expected = catalog.CalculateSkillProps(24386, listener.SourceSkillRank, 2, 8);
            Require(buff.StatusProperties.Single(prop => prop.Id == 58).Value == expected[58]
                && buff.StatusProperties.Single(prop => prop.Id == 75).Value == expected[75]
                && actor.Properties.Single(prop => prop.Id == 58).Value
                    == (actor.BaseProperties.FirstOrDefault(prop => prop.Id == 58)?.Value ?? 0) + expected[58] * buff.StackCount
                && actor.Properties.Single(prop => prop.Id == 75).Value
                    == (actor.BaseProperties.FirstOrDefault(prop => prop.Id == 75)?.Value ?? 0) + expected[75] * buff.StackCount,
                "The live cultivated critical bonuses did not retain both original property values.");
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(state)) == state,
                "The live critical stack changed during checkpoint restoration.");
            File.WriteAllText(Path.Combine(output, "companion-flame-soul-checkpoint.json"), state);
        }
    }
}
