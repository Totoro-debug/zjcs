using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void PrepareAcquiredHellfire(FightBattleUnitState actor)
        {
            actor.Hp = actor.MaxHp / 2;
            actor.Turn.Hp = actor.Hp;
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (int id in new[] { 24040, 24041 })
            {
                var status = combat.CreateStatus(id, actor.Id, actor.Id, 700000 + id);
                status.DurationRound = status.CurDurationRound = 10;
                status.StartRound = actor.SkillState.Round;
                actor.Statuses.Add(status);
            }
        }

        private static void VerifyAcquiredHellfireStep(FightBattleStepEvent step, FightBattleUnitState actor,
            ref int cures, ref int hits)
        {
            var children = step.SkillCasts.Where(value => value.ActorId == actor.Id && value.Slot < 0
                && value.SkillItemId == 10068 && (value.SkillEntityId == 11700 || value.SkillEntityId == 11703)).ToArray();
            if (children.Length == 0) return;
            Require(children.Length == 2 && children[0].SkillEntityId == 11700 && children[1].SkillEntityId == 11703,
                "The current hellfire graph lost its ordered healing and separate dispel children.");
            var cure = step.DamageEvents.Single(value => value.SourceId == actor.Id && value.SkillEntityId == 11700);
            var cleanse = step.DamageEvents.Single(value => value.SourceId == actor.Id && value.SkillEntityId == 11703);
            Require(cure.IsCure && cure.TargetId == actor.Id && cure.HitEntityId == 11701 && cure.DamageEntityId == 11702
                && cure.AfterHp > cure.BeforeHp && cure.Damage > 0 && cure.AfterHp <= actor.MaxHp
                && cleanse.TargetId == actor.Id && cleanse.HitEntityId == 11704 && cleanse.DamageEntityId == 11705
                && cleanse.Damage == 0 && !cleanse.IsCure,
                "The acquired current hellfire changed its healing, separate dispel or target.");
            cures++; hits++;
            if (cures == 1)
                Require(actor.Statuses.All(value => value.InstanceId != 724040 && value.InstanceId != 724041)
                    && step.DispersedUnitIds.Count(value => value == actor.Id) == 1,
                    "The first acquired hellfire tick did not remove both supplied negative effects.");
        }
    }
}
