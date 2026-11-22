namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // Confirmed offline timing for Battlecry's original 24384 component:
        // advance the next action when applied, without another round-start advance.
        private static bool AdvancesOnApplication(FightBattleStatusState status)
            => status?.ClassId == 24384;

        private static bool AllowsRoleBuffRecipient(FightBattleSkillDefinition skill,
            FightBattleUnitState owner)
        {
            int root = skill == null ? 0 : skill.RootEntityId > 0 ? skill.RootEntityId : skill.EntityId;
            // Official "role": player characters and companions. Preserve the raw
            // FriendALL scope while excluding summoned and manifested pet entities.
            return root != 12010 || owner.CharacterType == 1 || owner.CharacterType == 4;
        }

        private void ApplyImmediateRoundInterval(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var component = status?.Components?.RoundInterval;
            if (!AdvancesOnApplication(status) || component == null || component.HasUnsupportedFilter
                || status.ForbiddenType != 0 || owner.Turn == null
                || component.ExcludedApplyCharacterTypes.Contains(owner.CharacterType)
                || !RollComponent(component.Rate)) return;
            turns.ChangeInterval(owner.Id, component.FastForward
                ? component.RoundIntervalPercent : -component.RoundIntervalPercent);
            component.TriggeredCount++;
        }
    }
}
