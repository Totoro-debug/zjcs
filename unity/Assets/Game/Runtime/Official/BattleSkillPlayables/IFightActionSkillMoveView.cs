using UnityEngine;

namespace Zjcs.Official.BattleSkillPlayables
{
    // Presentation-clock boundary shared by original Mono and current EC move views.
    public interface IFightActionSkillMoveView
    {
        GameObject BlockPlayablePrefab { get; }
        float RemainingMoveTime { get; }
        bool BlockedPlaybackPending { get; }
        void Advance(float deltaTime);
    }
}
