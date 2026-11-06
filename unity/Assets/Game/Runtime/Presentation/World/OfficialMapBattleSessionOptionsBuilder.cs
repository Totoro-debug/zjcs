using System;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public static class OfficialMapBattleSessionOptionsBuilder
    {
        public static OfficialBattleSessionOptions Create(Map10RuntimeStage stage,
            bool canChangePosition = false, int changePositionWidth = 0, int changePositionHeight = 0,
            int changePositionCenterX = 0, int changePositionCenterY = 0)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            return new OfficialBattleSessionOptions
            {
                SkipPrepareStatus = stage.SkipPrepareStatus,
                ShowMainPanelDelay = stage.ShowMainPanelDelay,
                LeaveInPreparing = stage.LeaveInPreparing,
                LeaveInPlaying = stage.LeaveInPlaying,
                ChangeCameraMode = stage.ChangeCameraMode,
                CanFastforward = stage.CanFastforward,
                CanChangePosition = canChangePosition,
                ChangePositionWidth = changePositionWidth,
                ChangePositionHeight = changePositionHeight,
                ChangePositionCenterX = changePositionCenterX,
                ChangePositionCenterY = changePositionCenterY
            };
        }
    }
}
