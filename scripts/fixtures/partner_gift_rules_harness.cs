using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;

static class PartnerGiftRulesHarness
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static NpcGiftChoice Gift(int id, int quality, int friendship, long count, bool preferred = false, double factor = 1)
        => new NpcGiftChoice { ItemId = id, Quality = quality, Friendship = friendship, Count = count,
            Preferred = preferred, PreferenceFactor = factor };
    public static void Main()
    {
        Require(NpcGiftRules.Experience(5, 1, 0.5) == 2, "Midpoint rounds to even below");
        Require(NpcGiftRules.Experience(7, 1, 0.5) == 4, "Midpoint rounds to even above");
        Require(NpcGiftRules.Experience(5, 3, 0.5) == 8, "Whole stack rounds after multiplying count");
        var gifts = new[] { Gift(1, 9, 50, 2), Gift(2, 2, 10, 3, true), Gift(4, 2, 10, 3, true), Gift(3, 3, 30, 2, true) };
        var shown = gifts.ToList(); shown.Sort((x,y) => NpcGiftRules.Compare(x,y,false));
        Require(shown[0].ItemId == 3 && shown.Last().ItemId == 1, "Preference precedes quality for display");
        var selected = new Dictionary<int,long> { [4] = 2 };
        var auto = NpcGiftRules.AutoSelect(gifts, selected, 25);
        Require(auto[4] == 3 && auto[2] == 2 && auto.Count == 2, "Existing selection, descending ID and ceil quantity");
        Require(selected[4] == 2 && selected.Count == 1, "Auto-select must not mutate the caller selection");
        auto = NpcGiftRules.AutoSelect(gifts, null, 1000);
        Require(auto.Count == 4 && auto.Sum(x => x.Value) == 10, "Insufficient stock selects only available inventory");
        auto = NpcGiftRules.AutoSelect(new[] { Gift(1, 1, 5, 10, true, 0.5) }, null, 7);
        Require(auto[1] == 4 && NpcGiftRules.Experience(5, auto[1], 0.5) == 10,
            "Auto-selection preserves official single-item rounding even when aggregate preview differs");
        Require(NpcGiftRules.AutoSelect(gifts, selected, 0)[4] == 2, "No difference preserves selection");
        bool overflow = false;
        try { NpcGiftRules.Experience(int.MaxValue, long.MaxValue, 1); } catch (OverflowException) { overflow = true; }
        Require(overflow, "Invalid count must not wrap into negative friendship");
        Console.WriteLine("Partner gift rules passed: rounding, ordering, existing selection, stock and overflow.");
    }
}
