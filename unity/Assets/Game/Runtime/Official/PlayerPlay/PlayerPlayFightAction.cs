using System;

namespace Zjcs.Official.PlayerPlay
{
    // Common.PlayerPlayFightAction: the local parser boundary supplies the original StageInfo fields.
    public sealed class PlayerPlayFightAction : IPlayerPlayAction
    {
        private readonly int mStageId;
        private readonly IPlayerPlayParam mParam;
        public PlayerPlayActionType Type => PlayerPlayActionType.Fight;
        public IPlayerPlayParam Param => mParam;

        public PlayerPlayFightAction(int stageId, Func<int, PlayerPlayStageInfo> getStageInfo,
            bool isBackground = false, GameFastType fastType = GameFastType.None)
        {
            mStageId = stageId;
            bool isFastFight = fastType != GameFastType.None;
            PlayerPlayStageInfo info = getStageInfo(stageId);
            mParam = PlayerPlayUtil.StageInfo2Param(info, isBackground, isFastFight);
        }
    }

    // Projection of the five original StageInfo fields consumed by PlayerPlayUtil, not new defaults.
    public readonly struct PlayerPlayStageInfo
    {
        public readonly bool SupportTeam, JoinInMiddle, Background, LeaveInPreparing, LeaveInPlaying;

        public PlayerPlayStageInfo(bool supportTeam, bool joinInMiddle, bool background,
            bool leaveInPreparing, bool leaveInPlaying)
        {
            SupportTeam = supportTeam;
            JoinInMiddle = joinInMiddle;
            Background = background;
            LeaveInPreparing = leaveInPreparing;
            LeaveInPlaying = leaveInPlaying;
        }
    }

    public static class PlayerPlayUtil
    {
        public static readonly PlayerPlayActionNoneParam NoneParam;

        public static PlayerPlayFightStateParam StageInfo2Param(PlayerPlayStageInfo info,
            bool isBackground, bool isFastFight)
        {
            bool num = info.SupportTeam || info.JoinInMiddle;
            bool flag = info.LeaveInPreparing && info.LeaveInPlaying;
            int num2 = 0;
            if (num) num2 |= 1;
            if (info.Background) num2 |= 2;
            if (isBackground) num2 |= 4;
            if (flag) num2 |= 8;
            if (isFastFight) num2 |= 0x10;
            return new PlayerPlayFightStateParam(num2);
        }
    }
}
