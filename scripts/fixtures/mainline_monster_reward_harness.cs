using System;
using System.Collections.Generic;
using Zjcs.Offline.Progression;

namespace UnityEngine
{
    public sealed class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) where T : class => null; }
    public static class JsonUtility
    {
        public static object Fixture;
        public static T FromJson<T>(string json) => (T)Fixture;
    }
}
namespace Zjcs.Offline.Progression
{
    public sealed class OfficialExploreCondition { public string _type; }
    public sealed class OfficialExploreConditionRow { }
    public sealed class MaterialGatheringLevelNumber { public int Id; public float[] Nums; }
}

public static class MainlineMonsterRewardHarness
{
    private static void Assert(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    public static void Run()
    {
        var yes = new OfficialExploreCondition { _type = "yes" };
        var no = new OfficialExploreCondition { _type = "no" };
        UnityEngine.JsonUtility.Fixture = new MainlineMonsterRewardData
        {
            manifest = "884_156692",
            stages = new[] { new MainlineMonsterRewardStage { Id = 1081, MonsterAwardGroupId = 2, Awards = new[] { 10 } } },
            monsterRewards = new[] { new MainlineMonsterRewardBinding { GroupId = 2, MonsterId = 3401, RuleIds = new[] { 20 } } },
            conditions = Array.Empty<OfficialExploreConditionRow>(), missingRewardRuleIds = Array.Empty<int>(),
            levelNumbers = new[] { new MaterialGatheringLevelNumber { Id = 6, Nums = new[] { 1f, 1.5f } } },
            rewardRules = new[] {
                new MainlineMonsterRewardRule { Id = 10, Order = -1, Percent = 1, IdList = new[] { 1 }, WeightList = new[] { 1 }, CountList = new[] { 2f } },
                new MainlineMonsterRewardRule { Id = 20, Order = 1, HasCondition = true, Condition = no, Percent = 1, IdList = new[] { 9 }, WeightList = new[] { 1 }, CountList = new[] { 9f } },
                new MainlineMonsterRewardRule { Id = 20, Order = 2, HasCondition = true, Condition = yes, Percent = .5f, IdList = new[] { 2, 3 }, WeightList = new[] { 1, 3 }, CountList = new[] { 1f, 1f }, SubRankCountId = new[] { 6, 6 } },
                new MainlineMonsterRewardRule { Id = 20, Order = -1, Percent = 1, IdList = new[] { 8 }, WeightList = new[] { 1 }, CountList = new[] { 1f } }
            }
        };
        var catalog = new OfficialMainlineMonsterRewardCatalog("fixture");
        var rolls = new Queue<double>(new[] { .1, .4, .8, .2 });
        var award = catalog.Roll(1081, new[] { 3401 }, 10, 1, c => c._type == "yes", () => rolls.Dequeue());
        Assert(award.Count == 2 && award[1] == 2 && award[3] == 2 && rolls.Count == 0,
            "Ordered condition, weighted choice or fractional rounding differs.");
        rolls = new Queue<double>(new[] { .1, .9 });
        award = catalog.Roll(1081, new[] { 3401 }, 10, 1, c => c._type == "yes", () => rolls.Dequeue());
        Assert(award.Count == 1 && award[1] == 2 && rolls.Count == 0, "Failed probability should not grant a drop.");
        rolls = new Queue<double>(new[] { .1, .1 });
        award = catalog.Roll(1081, new[] { 3401 }, 10, 1, c => false, () => rolls.Dequeue());
        Assert(award[1] == 2 && award[8] == 1 && rolls.Count == 0, "Unconditional fallback was not selected.");
        catalog.Data.rewardRules[3].IndexMininum = new[] { 5 };
        bool rejected = false;
        try { catalog.Roll(1081, new[] { 3401 }, 10, 1, c => false, () => .1); }
        catch (System.IO.InvalidDataException) { rejected = true; }
        Assert(rejected, "An unconverted guarantee must not silently grant a reward.");
    }
}
