using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleTargetTypesHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FightBattleUnitState Unit(int id, int side, int type, int master = 0)
    {
        return new FightBattleUnitState { Id=id, Side=side, CharacterType=type,
            MasterId=master, Hp=100, MaxHp=100, X=0, Y=1 };
    }

    public static void Run(FightOfficialCombatCatalog catalog)
    {
        var actor=Unit(100,1,1,99);
        var units=new[] { actor, Unit(101,1,1), Unit(102,1,4), Unit(103,1,3,100),
            Unit(104,1,3,99), Unit(105,1,5,100), Unit(106,1,7,100), Unit(107,1,6,100),
            Unit(108,1,8,100), Unit(109,1,9,100), Unit(110,1,2,100), Unit(201,2,1),
            Unit(202,2,4), Unit(203,2,3,100), Unit(99,2,1) };
        // Expected memberships transcribed from Common.GameUtilsHandler.CheckTargetType.
        int[][] expected={ new[] {100}, new[] {201,202,203,99},
            new[] {100,101,102,103,104,105,106,107,108,109,110},
            new[] {101,102,103,104,105,106,107,108,109,110},
            units.Select(u=>u.Id).ToArray(), new int[0], new[] {99}, new[] {103,203},
            new[] {101}, new[] {100,101,102}, new[] {101,102} };
        var hit=new FightBattleHitDefinition { TargetMode=1 };
        hit.Scope.Add(new FightGridPoint(0,0));
        for(int type=0;type<expected.Length;type++)
        {
            var skill=new FightBattleSkillDefinition {TargetType=type};
            foreach(var target in units)
            {
                bool match=expected[type].Contains(target.Id);
                Check((FightBattleTargeting.Select(actor,new[] {target},type).Count==1)==match,
                    "Selection membership differs: type="+type+", target="+target.Id);
                Check((FightBattleTargeting.SelectInSkillRange(actor,new[] {target},skill).Count==1)==match,
                    "Range membership differs: type="+type+", target="+target.Id);
                // None intentionally bypasses relation filtering for explicit point-target skills.
                Check(FightBattleTargeting.IsEligibleSkillTarget(actor,target,skill)==(type==5||match),
                    "Explicit eligibility differs: type="+type+", target="+target.Id);
            }
            var scope=FightBattleTargeting.SelectScopeTargets(actor,units,skill,actor,hit);
            Check(scope.Select(u=>u.Id).OrderBy(id=>id).SequenceEqual(expected[type].OrderBy(id=>id)),
                "Area membership differs: type="+type);
        }
        foreach(int type in new[] {-1,11})
        {
            var skill=new FightBattleSkillDefinition {TargetType=type};
            Check(FightBattleTargeting.Select(actor,units,type).Count==0
                && FightBattleTargeting.SelectScopeTargets(actor,units,skill,actor,hit).Count==0,
                "Unknown target type accepted candidates");
        }
        // Exercise the actual configured MySummon skill, without changing its configuration.
        var original=catalog.CreateSkill(12980,0,0,5,20,1,null);
        Check(original.TargetType==7,"Original MySummon skill target type changed");
        Check(FightBattleTargeting.Select(actor,units,original.TargetType,skill:original)
            .Select(u=>u.Id).OrderBy(id=>id).SequenceEqual(new[] {103,203}),
            "Configured MySummon skill admitted pets, scenery or a different owner's summons");
        units[3].Hp=0; units[13].Ended=true;
        Check(FightBattleTargeting.Select(actor,units,7).Count==0,
            "Dead or absent summon passed public lifecycle filtering");
    }
}
