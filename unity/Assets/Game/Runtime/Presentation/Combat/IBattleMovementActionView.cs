using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // Ownership includes the original action's position, facing and action graph.
    // Callers retain timing and skill-effect responsibilities for other movement.
    public interface IBattleMovementActionView
    {
        bool OwnsMovement(FightBattleMovementEvent movement);
    }

    // Parent skill lifecycle is separate from the movement action and its track.
    public interface IBattleMovementSkillView
    {
        bool OwnsMovementSkill(FightBattleMovementEvent movement);
    }
}
