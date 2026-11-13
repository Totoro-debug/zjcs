using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleBgm;

namespace Zjcs.Offline.BattleNativePlayables
{
    /// <summary>Resolve one original theme snapshot for both its music and HUD.</summary>
    public static class OfflineBattleThemeSelection
    {
        [Serializable] private sealed class ConditionInfo
        {
            public int ClassId;
            public DomainTypeCondition Condition;
        }
        [Serializable] private sealed class Bindings
        {
            public string manifest;
            public FightThemeInfo[] theme_selection;
            public ConditionInfo[] theme_conditions;
        }

        private static Dictionary<int, FightThemeInfo> themes;
        private static FightThemeInfoParser selector;
        [ThreadStatic] private static System.Random random;

        public static FightThemeInfo Resolve(int stageType, int requestedThemeId = 0)
        {
            if (requestedThemeId < 0) throw new ArgumentOutOfRangeException(nameof(requestedThemeId));
            if (selector == null)
            {
                var asset = Resources.Load<TextAsset>("Combat/Wwise/Bgm/catalog");
                var bindings = asset ? JsonUtility.FromJson<Bindings>(asset.text) : null;
                if (bindings?.manifest != "884_156692" || bindings.theme_selection == null
                    || bindings.theme_conditions == null)
                    throw new InvalidOperationException("Original fight theme selection bindings are missing.");
                var rows = new Dictionary<int, FightThemeInfo>();
                foreach (var row in bindings.theme_selection) rows.Add(row.FightThemeId, row);
                // Existing offline saves belong to Normal (0), as approved.
                // The selector retains its original ordering and short-circuit.
                selector = new FightThemeInfoParser(rows, id =>
                    Array.Find(bindings.theme_conditions, row => row.ClassId == id)?.Condition?.IsMet(0),
                    NextRandom);
                themes = rows;
            }
            if (requestedThemeId == 0) return selector.GetThemeIdByStageInfo(stageType);
            return themes.TryGetValue(requestedThemeId, out var selected) ? selected :
                throw new InvalidOperationException("Original fight theme is missing: " + requestedThemeId);
        }

        private static int NextRandom(int maximum)
        {
            // LaunchContainerInstaller -> RandomFactory(Default) -> SystemRandom.
            if (random == null) random = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
            return random.Next(maximum);
        }
    }
}
