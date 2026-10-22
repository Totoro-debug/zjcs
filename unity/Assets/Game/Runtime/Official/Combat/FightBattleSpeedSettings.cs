using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// One selectable speed of a battle play mode. <see cref="Conditions"/> are official condition
    /// ids that must all pass before the speed becomes selectable.
    /// </summary>
    public sealed class FightBattleSpeedEntry
    {
        public int Index;
        public List<int> Conditions = new List<int>();
    }

    public sealed class FightBattleSpeedMode
    {
        public int Type;
        public List<FightBattleSpeedEntry> Speeds = new List<FightBattleSpeedEntry>();
    }

    /// <summary>
    /// Official battle speed configuration from <c>game_settings.Battle</c>:
    /// <c>Speeds</c>, <c>SpeedUIs</c> and <c>PlayModeSpeeds</c>. The label is presentation text and
    /// is not the multiplier; the multiplier always comes from <c>Speeds</c>.
    /// </summary>
    public sealed class FightBattleSpeedSettings
    {
        public readonly List<double> Speeds = new List<double>();
        public readonly List<string> Labels = new List<string>();
        public readonly List<FightBattleSpeedMode> Modes = new List<FightBattleSpeedMode>();
        public int RecordPlaybackDefaultIndex;

        public static FightBattleSpeedSettings Default()
        {
            var settings = new FightBattleSpeedSettings();
            settings.Speeds.AddRange(new[] { 1.0, 1.5, 2.0, 2.5 });
            settings.Labels.AddRange(new[] { "1", "2", "3", "4" });
            settings.Modes.Add(new FightBattleSpeedMode { Type = 0, Speeds = { new FightBattleSpeedEntry() } });
            return settings;
        }

        public int Count => Speeds.Count;

        public bool IsValid => Speeds.Count > 0 && Speeds.Count == Labels.Count;

        public double Multiplier(int index)
        {
            if (index < 0 || index >= Speeds.Count) return 1.0;
            double value = Speeds[index];
            return value > 0 ? value : 1.0;
        }

        public string Label(int index)
        {
            return index < 0 || index >= Labels.Count ? string.Empty : Labels[index];
        }

        /// <summary>
        /// Speed indexes selectable for one play mode, in configured order. A mode without an entry
        /// falls back to the unconditional first speed; a condition without an evaluator stays
        /// locked instead of being silently opened.
        /// </summary>
        public List<int> Resolve(int modeType, Func<int, bool> conditionMet)
        {
            var result = new List<int>();
            FightBattleSpeedMode mode = null;
            foreach (FightBattleSpeedMode candidate in Modes)
                if (candidate != null && candidate.Type == modeType) { mode = candidate; break; }
            if (mode == null || mode.Speeds.Count == 0)
            {
                if (Speeds.Count > 0) result.Add(0);
                return result;
            }
            foreach (FightBattleSpeedEntry entry in mode.Speeds)
            {
                if (entry == null || entry.Index < 0 || entry.Index >= Speeds.Count) continue;
                bool allowed = true;
                foreach (int condition in entry.Conditions)
                {
                    if (conditionMet == null || !conditionMet(condition)) { allowed = false; break; }
                }
                if (allowed && !result.Contains(entry.Index)) result.Add(entry.Index);
            }
            if (result.Count == 0) result.Add(0);
            return result;
        }
    }
}
