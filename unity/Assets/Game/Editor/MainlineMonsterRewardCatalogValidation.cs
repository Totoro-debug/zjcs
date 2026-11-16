using System;
using Zjcs.Offline.Progression;

namespace Zjcs.Editor
{
    public static class MainlineMonsterRewardCatalogValidation
    {
        public static void Run()
        {
            var catalog = OfficialMainlineMonsterRewardCatalog.Load();
            var awards = catalog.Roll(1081, new[] { 3401 }, 20, 1,
                condition => condition._type == "MapGroupLocationCondition" && condition.MapGroupId == 11,
                () => 0d);
            if (awards.Count != 3 || awards[42200] != 24 || awards[11] != 3 || awards[58402] != 1)
                throw new InvalidOperationException("Official stage 1081 reward selection differs from the selected source.");
            UnityEngine.Debug.Log("Official stage 1081 monster reward catalog validation passed.");
            ProjectTools.Validate();
        }
    }
}
