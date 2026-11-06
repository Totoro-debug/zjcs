using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// The mode seam for a shared battle session. The controller owns rules, presentation,
    /// timing and the final report; the mode owns persistence, rewards and leaving its host.
    /// Callbacks are delivered after the controller has applied the corresponding state.
    /// </summary>
    public interface IOfficialBattleModeAdapter
    {
        FightBattleReport FinalReport { get; }
        void OnBattleStepCompleted(FightBattleExecutionResult result);
        void OnBattleReportReady(FightBattleReport report);
        void OnBattleCompleted(FightBattleCheckpoint checkpoint);
        void OnBattleHudControlClicked(string path);
    }
}
