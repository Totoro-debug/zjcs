using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private readonly List<FightStatusMutexGroup> statusMutexGroups = new List<FightStatusMutexGroup>();
        public IReadOnlyList<FightStatusMutexGroup> StatusMutexGroups => statusMutexGroups;

        private void ReadStatusMutexGroups(Dictionary<string, object> battleSettings)
        {
            statusMutexGroups.Clear();
            if (!battleSettings.TryGetValue("StatusMutexForbiddenGroup", out object rawGroups)) return;
            foreach (object raw in Array(rawGroups, "StatusMutexForbiddenGroup"))
            {
                var row = Object(raw, "status mutex group");
                statusMutexGroups.Add(new FightStatusMutexGroup
                {
                    Mode = Int(row, "Mode"), StatusIds = new List<int>(IntArray(row, "StatusIds"))
                });
            }
        }
    }
}
