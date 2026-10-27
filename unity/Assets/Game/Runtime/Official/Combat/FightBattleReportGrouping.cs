using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightBattleReportPlayerGroup
    {
        public int TargetId;
        public readonly List<FightBattleReportUnit> Normal = new List<FightBattleReportUnit>();
        public readonly List<FightBattleReportUnit> Summon = new List<FightBattleReportUnit>();
        public readonly List<FightBattleReportUnit> Pet = new List<FightBattleReportUnit>();
    }

    public sealed class FightBattleReportUnassignedUnit
    {
        public FightBattleReportUnit Unit;
        public string Reason;
    }

    public sealed class FightBattleReportPlayerGroups
    {
        public readonly List<FightBattleReportPlayerGroup> Players = new List<FightBattleReportPlayerGroup>();
        public readonly List<FightBattleReportUnassignedUnit> Unassigned = new List<FightBattleReportUnassignedUnit>();
    }

    // BattleReportDistributor has no server implementation in the APK. These ownership rules
    // use recorded MasterId/CharacterType and the user's explicit NPC/nested-summon decisions.
    // Gameplay ownership is never changed to manufacture a statistical owner.
    public static class FightBattleReportGrouping
    {
        public static FightBattleReportPlayerGroups Build(FightBattleReport report, int side)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var result = new FightBattleReportPlayerGroups();
            var units = new Dictionary<int, FightBattleReportUnit>();
            var owners = new Dictionary<int, FightBattleReportPlayerGroup>();
            foreach (var unit in report.Units)
            {
                if (unit == null) continue;
                units.Add(unit.UnitId, unit);
                if (unit.Side != side || !IsPlayerRow(unit)) continue;
                var player = new FightBattleReportPlayerGroup { TargetId = unit.UnitId };
                player.Normal.Add(unit);
                owners.Add(unit.UnitId, player);
                result.Players.Add(player);
            }
            foreach (var unit in report.Units)
            {
                if (unit == null || unit.Side != side || IsPlayerRow(unit)) continue;
                // Ordinary monsters are not player records. A monster with a master still needs
                // an explicit category rule instead of being silently treated as a summon.
                if (unit.CharacterType == 2 && unit.MasterId == 0) continue;
                string reason;
                FightBattleReportPlayerGroup owner;
                bool petReport;
                if (TryResolve(unit, units, owners, out owner, out petReport, out reason))
                {
                    if (petReport) owner.Pet.Add(unit);
                    else owner.Summon.Add(unit);
                }
                else result.Unassigned.Add(new FightBattleReportUnassignedUnit { Unit = unit, Reason = reason });
            }
            return result;
        }

        private static bool IsPlayerRow(FightBattleReportUnit unit)
        {
            return unit.CharacterType == 1 || unit.CharacterType == 4;
        }

        private static bool TryResolve(FightBattleReportUnit unit,
            Dictionary<int, FightBattleReportUnit> units,
            Dictionary<int, FightBattleReportPlayerGroup> owners,
            out FightBattleReportPlayerGroup owner, out bool petReport, out string reason)
        {
            owner = null;
            petReport = unit.CharacterType == 5;
            reason = "UnsupportedCharacterType";
            if (unit.CharacterType != 3 && unit.CharacterType != 5) return false;
            var seen = new HashSet<int> { unit.UnitId };
            var current = unit;
            while (true)
            {
                reason = "MissingMaster";
                if (current.MasterId == 0 || !units.TryGetValue(current.MasterId, out var master)) return false;
                reason = "OwnershipCycle";
                if (!seen.Add(master.UnitId)) return false;
                reason = "CrossSideMaster";
                if (master.Side != unit.Side) return false;
                if (owners.TryGetValue(master.UnitId, out owner)) return true;
                // Confirmed: summons inherit their source report category. A pet's summons
                // remain in the final owner's pet report, including further summon descendants.
                reason = "UnconfirmedMasterCategory";
                if (current.CharacterType != 3 || (master.CharacterType != 3 && master.CharacterType != 5)) return false;
                if (master.CharacterType == 5) petReport = true;
                current = master;
            }
        }
    }
}
