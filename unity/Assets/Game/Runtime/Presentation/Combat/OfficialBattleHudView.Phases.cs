using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleHudView
    {
        private const string PrepareRootPath = "/FightMainPanel/Content/Bottom/Anim/Prepare";
        public const string StartButtonPath = PrepareRootPath + "/Content/StartBtn/Btn";
        private const string ReadyRootPath = PrepareRootPath + "/Content/ReadyBtn";
        private const string JoinRootPath = PrepareRootPath + "/Content/JoinBtn";
        private const string TimeoutRootPath = "/FightMainPanel/Content/Center/TimeOutTipUI/TimeoutUI";
        private const string CloseButtonPath = "/FightMainPanel/Content/Bottom/Anim/CloseBtn/CloseBtn_normal";

        /// <summary>
        /// Local solo branch of StartGameBtn.IsShowBtn and the stage-controlled leave button.
        /// The original prefab owns every position, graphic and label. Team-ready and soul-join
        /// controls are separate providers; they are not displayed for a local solo participant.
        /// </summary>
        public void RefreshPhase(FightBattlePhase phase, OfficialBattleSessionOptions options)
        {
            if (root == null) return;
            bool preparing = phase == FightBattlePhase.Preparing;
            renderer.SetActive(PrepareRootPath, preparing);
            renderer.SetActive(StartButtonPath, preparing);
            renderer.SetActive(ReadyRootPath, false);
            renderer.SetActive(JoinRootPath, false);
            renderer.SetActive("/FightMainPanel/Content/Center/PrepareKickout/Content", false);
            if (!preparing) renderer.SetActive(TimeoutRootPath, false);
            bool canLeave = options == null || (preparing
                ? options.LeaveInPreparing : options.LeaveInPlaying);
            renderer.SetActive(CloseButtonPath, canLeave && phase != FightBattlePhase.End);
        }

        /// <summary>GameTimeoutUI's label and clock are shown only for an actual deadline.</summary>
        public void RefreshPrepareCountdown(double seconds)
        {
            if (root == null) return;
            renderer.SetActive(TimeoutRootPath, seconds > 0);
            if (seconds > 0)
                renderer.SetText(TimeoutRootPath + "/timeText", Mathf.RoundToInt((float)seconds).ToString());
        }
    }
}
