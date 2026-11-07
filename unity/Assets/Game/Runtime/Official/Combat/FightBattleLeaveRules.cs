namespace Zjcs.Combat.Official
{
    /// <summary>FightMainPanelLeaveModule and the solo participant branches of
    /// FightMainPanelLeaveHandler. The consuming mode owns the leave transaction.</summary>
    public static class FightBattleLeaveRules
    {
        public static bool CanLeave(FightBattlePhase phase, bool inPreparing, bool inPlaying)
            => (phase == FightBattlePhase.Preparing && inPreparing)
                || ((phase == FightBattlePhase.Play || phase == FightBattlePhase.PlayStart) && inPlaying);

        // null means the official handler leaves directly, without a dialog.
        public static string ConfirmationKey(int stageType, FightBattlePhase phase, int? voidFissureLayer)
        {
            if (stageType == 11) return "ui_daily_dungeon_end_panel_leave_game_tip";
            if (stageType == 8)
            {
                bool firstLayer = voidFissureLayer.HasValue && voidFissureLayer.Value <= 1;
                if (firstLayer && phase == FightBattlePhase.Preparing) return null;
                return firstLayer ? "LeaveGame.ConfirmText" : "ui_voidfissure_leave_confirm";
            }
            if (stageType == 3 || stageType == 500) return null;
            return phase == FightBattlePhase.Play || phase == FightBattlePhase.PlayStart
                ? "LeaveGame.ConfirmText" : null;
        }
    }
}
