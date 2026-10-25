using System;
using System.Globalization;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public struct OfficialBattleRoundDisplay
    {
        public int Round;
        public int MaxRound;
        public bool IsPersonal;
    }

    public static class OfficialBattleRoundDisplayResolver
    {
        public static OfficialBattleRoundDisplay Resolve(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.IsPlayerRoundLimit)
            {
                FightBattleUnitState player = null;
                foreach (FightBattleUnitState unit in checkpoint.Units)
                    if (unit != null && unit.Id == 1)
                    {
                        player = unit;
                        break;
                    }
                if (player == null || player.SkillState == null)
                    throw new InvalidOperationException("Personal battle round display requires the main player state.");
                return new OfficialBattleRoundDisplay
                {
                    Round = Math.Min(checkpoint.PlayerRoundLimit > 0
                        ? checkpoint.PlayerRoundLimit : Math.Max(1, checkpoint.MaxRound),
                        Math.Max(0, player.SkillState.Round)),
                    MaxRound = checkpoint.PlayerRoundLimit > 0
                        ? checkpoint.PlayerRoundLimit : Math.Max(1, checkpoint.MaxRound),
                    IsPersonal = true
                };
            }

            int maxRound = Math.Max(1, checkpoint.MaxRound);
            int currentRound = checkpoint.Outcome != null
                ? checkpoint.Outcome.Round
                : checkpoint.Timeline == null ? 1 : checkpoint.Timeline.Round;
            return new OfficialBattleRoundDisplay
            {
                Round = Math.Min(maxRound, Math.Max(1, currentRound)),
                MaxRound = maxRound,
                IsPersonal = false
            };
        }
    }

    public static class OfficialBattleRoundTextFormatter
    {
        // GameProgressUI wraps only the current round with GlobalColors.LightRed.
        // This is the fixed 854_156383 GlobalColors asset's Light theme Red value.
        private const string LightRedHtmlColor = "#F75237FF";

        public static string Format(string textFormat, OfficialBattleRoundDisplay display, int countDown)
        {
            if (string.IsNullOrEmpty(textFormat)) throw new ArgumentException("Battle round text format is empty.", nameof(textFormat));
            string round = display.Round.ToString(CultureInfo.CurrentCulture);
            if (display.MaxRound - display.Round <= countDown)
                round = "<color=" + LightRedHtmlColor + ">" + round + "</color>";
            return string.Format(CultureInfo.CurrentCulture, textFormat, round, display.MaxRound);
        }
    }
}
