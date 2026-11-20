using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class ThirdProfessionSkillCatalogHarness
{
    private sealed class Case
    {
        public int Item;
        public int Profession;
        public int Entity;
        public bool PassiveOnly;
        public Case(int item, int profession, int entity, bool passiveOnly = false)
        { Item = item; Profession = profession; Entity = entity; PassiveOnly = passiveOnly; }
    }

    private static readonly Case[] Cases = {
        new Case(10017,3,10690), new Case(10032,3,11170), new Case(10039,3,10940),
        new Case(10073,3,11730), new Case(10079,3,11860), new Case(10542,3,25033,true),
        new Case(10567,3,25035,true), new Case(10572,3,25040,true), new Case(10581,3,25064,true),
        new Case(10587,3,0,true), new Case(10063,7,11400), new Case(10065,7,11440),
        new Case(10069,7,11530), new Case(10074,7,11820), new Case(10086,7,11930),
        new Case(10519,7,25009,true), new Case(10545,7,25024,true), new Case(10547,7,25034,true),
        new Case(10574,7,25080,true), new Case(10590,7,25093,true), new Case(10037,12,10920),
        new Case(10057,12,11130), new Case(10062,12,11340), new Case(10070,12,11540),
        new Case(10098,12,12070), new Case(10525,12,0,true), new Case(10566,12,25032,true),
        new Case(10577,12,25046,true), new Case(10604,12,25113,true), new Case(10605,12,25114,true),
        new Case(10040,16,10950), new Case(10064,16,11390), new Case(10071,16,10840),
        new Case(10078,16,11750), new Case(10080,16,12060), new Case(10527,16,25012,true),
        new Case(10528,16,0,true), new Case(10570,16,25039,true), new Case(10585,16,25073,true),
        new Case(10586,16,25065,true)
    };

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Third promotion skills: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items)
    {
        int active = 0, passive = 0, hitEntities = 0;
        foreach (Case test in Cases)
        {
            FightOfficialSkillItem item = items.Required(test.Item);
            Check(item.ItemId == test.Item, "item id changed: " + test.Item);
            Check(items.ProfessionAvailabilityFor(test.Item, test.Profession, 0, 0)
                == FightSkillProfessionAvailability.Available,
                "profession binding missing: " + test.Item);
            if (test.PassiveOnly)
            {
                Check(item.EntityId == 0 || item.EntityId == test.Entity,
                    "passive entity changed: " + test.Item);
                passive++;
                continue;
            }
            Check(item.EntityId == test.Entity, "skill root changed: " + test.Item);
            IReadOnlyDictionary<int, long> props = catalog.CalculateSkillProps(test.Entity, 7, 30, 5);
            var skill = catalog.CreateSkill(test.Entity, 0, test.Item, 7, 30, 5,
                props.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            Check(skill.EntityId == test.Entity && skill.ItemId == test.Item,
                "skill provenance changed: " + test.Item);
            Check(skill.HitGraphEntities != null && skill.HitGraphEntities.Count > 0,
                "official hit graph is empty: " + test.Item);
            active++; hitEntities += skill.HitGraphEntities.Count;
        }
        Check(active == 20 && passive == 20, "third-promotion split changed");
        return "Third-promotion skill catalog passed: 40 item/profession bindings, "
            + active + " active graphs (" + hitEntities + " hit entities), " + passive + " passive rows.";
    }
}
