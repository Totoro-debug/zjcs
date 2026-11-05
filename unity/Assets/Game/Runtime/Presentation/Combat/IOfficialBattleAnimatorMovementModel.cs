namespace Zjcs.Presentation.Combat
{
    // Complete skill graphs own cast Animator leaves; the scene only dispatches
    // extracted movement leaves to these original role controllers.
    public interface IOfficialBattleAnimatorMovementModel
    {
        bool Supports(OfficialBattleSkillPlayableCue cue);
        void PlayMovement(OfficialBattleSkillPlayableCue cue, float duration);
    }
}
