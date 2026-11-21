using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class FourthProfessionSkillCatalogHarness
{
    private sealed class Case
    {
        public int Item;
        public int Profession;
        public int Entity;
        public bool PassiveOnly;
        public bool BuiltIn;
        public Case(int item, int profession, int entity, bool passiveOnly = false, bool builtIn = false)
        { Item = item; Profession = profession; Entity = entity; PassiveOnly = passiveOnly; BuiltIn = builtIn; }
    }

    private static readonly Case[] Cases = {
        new Case(10047,4,10880), new Case(10082,4,11980), new Case(10083,4,11990),
        new Case(10084,4,11890), new Case(10085,4,11900), new Case(10099,4,12080),
        new Case(10575,4,25042,true), new Case(10580,4,25074,true),
        new Case(10592,4,25108,true), new Case(10593,4,25109,true),
        new Case(10606,4,25115,true), new Case(10607,4,25116,true),
        new Case(10184,4,13580,false,true),
        new Case(10068,8,11520), new Case(10088,8,11910), new Case(10089,8,11920),
        new Case(10100,8,12090), new Case(10101,8,12100), new Case(10104,8,12130),
        new Case(10543,8,25010,true), new Case(10568,8,25081,true),
        new Case(10594,8,25095,true), new Case(10595,8,25096,true),
        new Case(10608,8,25117,true), new Case(10609,8,25118,true),
        new Case(10185,8,13590,false,true),
        new Case(10002,13,11450), new Case(10056,13,11080), new Case(10091,13,11960),
        new Case(10092,13,12010), new Case(10093,13,11970), new Case(10102,13,12110),
        new Case(10503,13,25001,true), new Case(10557,13,25037,true),
        new Case(10573,13,25043,true), new Case(10597,13,25098,true),
        new Case(10598,13,25099,true), new Case(10599,13,25103,true),
        new Case(10059,17,11380), new Case(10077,17,11760), new Case(10095,17,12140),
        new Case(10096,17,12020), new Case(10097,17,12200), new Case(10103,17,12120),
        new Case(10579,17,25048,true), new Case(10591,17,25104,true),
        new Case(10600,17,25105,true), new Case(10601,17,25106,true),
        new Case(10610,17,25119,true), new Case(10611,17,25120,true)
    };

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Fourth promotion skills: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items)
    {
        int active = 0, passive = 0, hitEntities = 0;
        foreach (Case test in Cases)
        {
            FightOfficialSkillItem item = items.Required(test.Item);
            Check(item.ItemId == test.Item, "item id changed: " + test.Item);
            if (!test.BuiltIn)
                Check(items.ProfessionAvailabilityFor(test.Item, test.Profession, 0, 0)
                    == FightSkillProfessionAvailability.Available,
                    "profession binding missing: " + test.Item);
            if (test.PassiveOnly)
            {
                Check(item.EntityId == 0 || item.EntityId == test.Entity,
                    "passive entity changed: " + test.Item);
                Check(item.PassiveStatusIds.Length > 0 || item.PassiveRankPropId > 0
                    || item.PassivePropFactors.Count > 0,
                    "passive effect projection is empty: " + test.Item);
                passive++;
                continue;
            }
            Check(item.EntityId == test.Entity, "skill root changed: " + test.Item);
            IReadOnlyDictionary<int, long> props = catalog.CalculateSkillProps(test.Entity, 4, 30, 8);
            var skill = catalog.CreateSkill(test.Entity, 0, test.Item, 4, 30, 8,
                props.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            Check(skill.EntityId == test.Entity && skill.ItemId == test.Item,
                "skill provenance changed: " + test.Item);
            Check(skill.HitGraphEntities != null && skill.HitGraphEntities.Count > 0,
                "official hit graph is empty: " + test.Item);
            active++; hitEntities += skill.HitGraphEntities.Count;
        }
        Check(active == 26 && passive == 24, "fourth-promotion split changed");
        return "Fourth-promotion skill catalog passed: 50 item/profession bindings, "
            + active + " active graphs (" + hitEntities + " hit entities), " + passive + " passive rows.";
    }
}
