using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        [Serializable] private sealed class WildBladeSiphonReceipt
        { public int criticals,cures; public long healed; public bool restored,captured; }
        private static WildBladeSiphonReceipt wildBladeSiphon;
        private static void PrepareAcquiredWildBladeSiphon(FightBattleUnitState actor)
        {
            wildBladeSiphon=new WildBladeSiphonReceipt();
            // Only this isolated diagnostic guarantees critical hits and missing HP.
            SetAcquiredBattleProperty(actor,"CritRatePercent",20000);
            actor.Hp=Math.Max(1L,actor.MaxHp/4);actor.Turn.Hp=actor.Hp;
        }
        private static void VerifyAcquiredWildBladeSiphonStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint,string output)
        {
            var actor=checkpoint.Units.Single(u=>u.Id==1);
            var listener=actor.Statuses.Single(s=>s.ClassId==25009);
            Require(!listener.CanDispersed && listener.DurationRound==-1 && listener.ApplyingSkillItemId==10519
                && listener.SourceSkillLevel==2 && listener.SourceSubRank==5 && listener.CreatorId==actor.Id,
                "Wild Blade Siphon's real listener lost its original source or cultivation.");
            var criticals=step.DamageEvents.Where(h=>h.SourceId==actor.Id && h.SkillEntityId==11440
                && h.Critical && !h.IsCure && !h.Dodge && !h.Blinding && !h.Immunity && h.Damage<0).ToArray();
            var cures=step.DamageEvents.Where(h=>h.SkillEntityId==10960).ToArray();
            Require(cures.Length==criticals.Length,"Wild Blade Siphon did not heal once per eligible critical segment.");
            var assets=OfficialCombatAssetCatalog.LoadDefault();var formula=assets.LoadDamageFormula();
            var source=actor.Properties.ToDictionary(p=>p.Id,p=>p.Value);
            foreach(var pair in assets.Load().CalculateSkillProps(10960,listener.SourceSkillRank,2,actor.SubRank))
                source[pair.Key]=(source.TryGetValue(pair.Key,out long old)?old:0)+pair.Value;
            var target=actor.Properties.ToDictionary(p=>p.Id,p=>p.Value);
            long expected=formula.CalculateCure(source,target,210,170).Cure;
            foreach(var cure in cures)
            {
                Require(cure.SourceId==actor.Id && cure.TargetId==actor.Id && cure.SkillItemId==10519
                    && cure.HitEntityId==10961 && cure.DamageEntityId==10028 && cure.IsCure
                    && cure.HasDamageValue && cure.DamageValue==expected && cure.Damage>=0
                    && cure.AfterHp==Math.Min(cure.MaxHp,cure.BeforeHp+expected)
                    && cure.Damage==cure.AfterHp-cure.BeforeHp,
                    "Wild Blade Siphon's cultivated cure lost its original graph, fixed/HP terms or HP cap.");
                wildBladeSiphon.healed+=cure.Damage;
            }
            wildBladeSiphon.criticals+=criticals.Length;wildBladeSiphon.cures+=cures.Length;
            Require(listener.Components.HitSkill.InvokeCount==wildBladeSiphon.cures,
                "Wild Blade Siphon's actual listener counter differs from its cures.");
            if(cures.Length==0)return;
            string saved=FightBattleCheckpointSerializer.Save(checkpoint);
            var copy=FightBattleCheckpointSerializer.Load(saved);var run=assets.RestoreBattle(copy);
            Require(run.Step(checkpoint.Sequence,checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(copy)==saved,
                "Wild Blade Siphon repeated healing or changed counters after restore.");
            wildBladeSiphon.restored=true;
            File.WriteAllText(Path.Combine(output,"companion-wild-blade-siphon-checkpoint.json"),saved);
        }
        private static void CompleteAcquiredWildBladeSiphon(string output)
        {
            Require(wildBladeSiphon.cures>=6 && wildBladeSiphon.cures==wildBladeSiphon.criticals
                && wildBladeSiphon.healed>0 && wildBladeSiphon.restored && wildBladeSiphon.captured,
                "Wild Blade Siphon must settle cultivated healing and render its original cure particles.");
            File.WriteAllText(Path.Combine(output,"companion-wild-blade-siphon-receipt.json"),JsonUtility.ToJson(wildBladeSiphon,true));
            Debug.Log("[Acquired passive combat] 10519 cultivated critical self-healing, original native cure and restore replay passed.");
        }
    }
}
