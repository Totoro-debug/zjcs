using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightDamageFormula
    {
        private struct DamageAffinity
        {
            public double ElementAdd, ScaledElementAdd, ScaledElementReduce, Mastery, Resistance;
        }

        // The common Damage formula calls one channel at the original affinity
        // stage. Keep physical averaging distinct from a specific element.
        private DamageAffinity CalculatePhysicalDamage(IDictionary<int,long> source,
            IDictionary<int,long> target)
        {
            double baseAdd=Prop(source,"BaseElementAdd"), baseReduce=Prop(target,"BaseElementReduce");
            double add=0, reduce=0;
            if(baseAdd!=0 && baseReduce!=0)
            {
                foreach(string element in Elements)
                {
                    add+=Prop(source,element+"DamageAdd");
                    reduce+=Prop(target,element+"DamageReduce");
                }
                add/=5.0*baseAdd; reduce/=5.0*baseReduce;
            }
            return CompleteDamageAffinity(source,target,baseAdd,baseReduce,add,reduce,"KongFu");
        }

        private DamageAffinity CalculateElementDamage(IDictionary<int,long> source,
            IDictionary<int,long> target,string element)
        {
            double baseAdd=Prop(source,"BaseElementAdd"), baseReduce=Prop(target,"BaseElementReduce");
            double add=0, reduce=0;
            if(baseAdd!=0 && baseReduce!=0 && element!=null)
            {
                add=Prop(source,element+"DamageAdd")/baseAdd;
                reduce=Prop(target,element+"DamageReduce")/baseReduce;
            }
            return CompleteDamageAffinity(source,target,baseAdd,baseReduce,add,reduce,"Element");
        }

        private DamageAffinity CompleteDamageAffinity(IDictionary<int,long> source,
            IDictionary<int,long> target,double baseAdd,double baseReduce,double add,double reduce,string mastery)
        {
            var result=new DamageAffinity {ElementAdd=add};
            if(baseAdd!=0 && baseReduce!=0)
            {
                result.ScaledElementAdd=add*Math.Pow(baseAdd/baseReduce,1.0);
                result.ScaledElementReduce=reduce*Math.Pow(baseReduce/baseAdd,1.0);
            }
            double baseMastery=Prop(source,"Base"+mastery+"Master");
            double baseResistance=Prop(target,"Base"+mastery+"Resistance");
            if(baseMastery!=0 && baseResistance!=0)
            {
                result.Mastery=Math.Max(0,Prop(source,mastery+"Master")/baseMastery
                    *Math.Pow(baseMastery/baseResistance,1.0));
                result.Resistance=Math.Max(0,Prop(target,mastery+"Resistance")/baseResistance
                    *Math.Pow(baseResistance/baseMastery,1.0));
            }
            return result;
        }

        public long CalculateFixedTrueDamage(long value)
        {
            // CustomDamageWithoutBattleFormula bypasses attack, defence and affinity.
            return value==long.MinValue ? long.MinValue+1 : -Math.Abs(value);
        }

        public long CalculateBreakingDamage(long commonDamage,int targetCharacterType,long totalShieldValue)
        {
            long damage=CalculateTrueDamageTargetScale(commonDamage,targetCharacterType);
            return CalcTrueDamage(damage,totalShieldValue);
        }
    }
}
