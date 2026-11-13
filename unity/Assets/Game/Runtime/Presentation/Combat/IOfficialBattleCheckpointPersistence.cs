using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Optional local checkpoint persistence before the live session is released.</summary>
    public interface IOfficialBattleCheckpointPersistence
    {
        void SaveCheckpointBeforeSessionRelease(FightBattleCheckpoint checkpoint);
    }
}
