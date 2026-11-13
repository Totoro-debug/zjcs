using System;
using System.Collections.Generic;
using Zjcs.Official.BattleBgm;

namespace Zjcs.Offline.BattleNativePlayables
{
    public sealed partial class OfflineBattleWwiseRuntime
    {
        [Serializable] private sealed class ThemeConditionInfo
        {
            public int ClassId;
            public DomainTypeCondition Condition;
        }

        private FightThemeInfoParser themeSelector;
        [ThreadStatic] private static Random themeRandom;

        private int SelectOrdinaryDomainTheme(int stageType)
        {
            if (themeSelector == null)
            {
                if (bgmBindings.theme_selection == null || bgmBindings.theme_conditions == null)
                    throw new InvalidOperationException("Original fight theme selection bindings are missing.");
                var rows = new Dictionary<int, FightThemeInfo>();
                foreach (var row in bgmBindings.theme_selection)
                {
                    if (!Array.Exists(bgmBindings.fight_theme, music => music.id == row.FightThemeId))
                        throw new InvalidOperationException("Original fight theme music is missing: " + row.FightThemeId);
                    rows.Add(row.FightThemeId, row);
                }
                // Approved offline domain is Normal (0). Use the original
                // condition and selector, including their random short-circuit.
                themeSelector = new FightThemeInfoParser(rows, id =>
                    Array.Find(bgmBindings.theme_conditions, row => row.ClassId == id)?.Condition?.IsMet(0),
                    NextThemeRandom);
            }
            return themeSelector.GetThemeIdByStageInfo(stageType)?.FightThemeId ?? 0;
        }

        private static int NextThemeRandom(int maximum)
        {
            // LaunchContainerInstaller -> RandomFactory(Default) -> SystemRandom:
            // one random per thread, UTC tick seed truncated to the original int.
            if (themeRandom == null) themeRandom = new Random(unchecked((int)DateTime.UtcNow.Ticks));
            return themeRandom.Next(maximum);
        }
    }
}
