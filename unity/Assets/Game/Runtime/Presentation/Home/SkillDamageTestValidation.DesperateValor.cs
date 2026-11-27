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
        [Serializable] private sealed class DesperateValorReceipt
        { public int hits, maximumLayers; public bool restored, captured; }
        private static DesperateValorReceipt desperateValor;
        private static Dictionary<int,long> desperateValorBase;
        private static void PrepareAcquiredDesperateValor(FightBattleUnitState actor)
        {
            desperateValor=new DesperateValorReceipt();
            desperateValorBase=actor.Properties.ToDictionary(p=>p.Id,p=>p.Value);
            // Isolated low-health entry; the production HP listener owns all layers.
            actor.Hp=Math.Max(1L,actor.MaxHp/4);actor.Turn.Hp=actor.Hp;
        }
        private static void VerifyAcquiredDesperateValorStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint,string output)
        {
            var actor=checkpoint.Units.Single(unit=>unit.Id==1);
            var buff=actor.Statuses.SingleOrDefault(status=>status.ClassId==24242);
            if(buff==null)return;
            int expected=(int)Math.Min(5m,decimal.Floor(((decimal)actor.MaxHp-actor.Hp)/(actor.MaxHp*.15m)));
            Require(buff.StackCount==expected && !buff.CanDispersed && buff.DurationRound==-1
                && buff.CreatorId==actor.Id && buff.ApplyingSkillItemId==10547
                && buff.SourceSkillLevel==2 && buff.SourceSubRank==5,
                "Desperate Valor lost its original current-HP layers, cultivation or permanent source.");
            foreach(var property in buff.ResolvedProperties)
                Require(actor.Properties.Single(p=>p.Id==property.Id).Value
                    ==(desperateValorBase.TryGetValue(property.Id,out long before)?before:0)+property.Value,
                    "The real battle omitted or duplicated Desperate Valor's cultivated layer property.");
            desperateValor.maximumLayers=Math.Max(desperateValor.maximumLayers,buff.StackCount);
            desperateValor.hits+=step.DamageEvents.Count(hit=>hit.SourceId==actor.Id
                && hit.SkillEntityId==11440 && hit.Damage<0 && !hit.IsCure && !hit.TrueDamage);
            if(desperateValor.hits==0)return;
            string saved=FightBattleCheckpointSerializer.Save(checkpoint);
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var runner=OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence,checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored)==saved,
                "Desperate Valor's original layer/property receipt changed after restore.");
            desperateValor.restored=true;
            File.WriteAllText(Path.Combine(output,"companion-desperate-valor-checkpoint.json"),saved);
        }
        private static void CompleteAcquiredDesperateValor(string output)
        {
            Require(desperateValor.hits>=6 && desperateValor.maximumLayers==5
                && desperateValor.restored && desperateValor.captured,
                "Desperate Valor must render its capped original effect and actual enhanced damage.");
            File.WriteAllText(Path.Combine(output,"companion-desperate-valor-receipt.json"),JsonUtility.ToJson(desperateValor,true));
            Debug.Log("[Acquired passive combat] 10547 cultivated current-HP five-layer damage, native status and restore replay passed.");
        }
    }
}
