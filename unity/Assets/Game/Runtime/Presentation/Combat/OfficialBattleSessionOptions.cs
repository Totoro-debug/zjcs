using System;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// StageInfo inputs consumed by the common client presentation. PrepareSeconds is a remaining
    /// server/local deadline, not an invented default timeout. Zero allows manual preparation.
    /// </summary>
    [Serializable]
    public sealed class OfficialBattleSessionOptions
    {
        public bool SkipPrepareStatus = true;
        public double PrepareSeconds;
        public float ShowMainPanelDelay;
        public bool LeaveInPreparing = true;
        public bool LeaveInPlaying = true;
        public bool ChangeCameraMode;
        public bool CanFastforward;
        public bool CanChangePosition;
        public int ChangePositionWidth;
        public int ChangePositionHeight;
        public int ChangePositionCenterX;
        public int ChangePositionCenterY;
    }
}
