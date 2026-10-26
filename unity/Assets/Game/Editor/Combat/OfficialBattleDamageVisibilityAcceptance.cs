using System;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleDamageVisibilityAcceptance
    {
        public static void Run()
        {
            var root = new GameObject("DamageVisibilityFixture");
            var hud = new GameObject("DamageVisibilityHud", typeof(RectTransform));
            hud.transform.SetParent(root.transform,false);
            OfficialBattleSceneView scene = null;
            try
            {
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                    null, new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width=4, Height=4 }),
                    null, (transform, unit) => { }, OfficialBattleHudCatalog.LoadDefault(), hud.transform);
                var checkpoint = new FightBattleCheckpoint();
                var target = new FightBattleUnitState { Id=2, X=1, Y=1, Hp=100, MaxHp=100 };
                checkpoint.Units.Add(target);
                int count=0;
                Action<string,FightBattleDamageEvent,int> check = (name,hit,effect) =>
                {
                    foreach (var old in root.GetComponentsInChildren<OfficialBattleDamageFloat>(true))
                        UnityEngine.Object.DestroyImmediate(old.gameObject);
                    hit.TargetId=2;
                    var step=new FightBattleStepEvent(); step.DamageEvents.Add(hit);
                    scene.ShowDamageFloats(step,checkpoint,OfficialBattleDamageColors.Load());
                    // Include deferred display; hidden hits must never enter the pending queue.
                    scene.Tick(0.25f);
                    var floats=root.GetComponentsInChildren<OfficialBattleDamageFloat>(true);
                    Require(effect<0 ? floats.Length==0 : floats.Length==1 && floats[0].EffectType==effect,
                        name+": expected effect "+effect+", actual="+string.Join(",",floats.Select(v=>v.EffectType)));
                    if (effect==0 && hit.ShieldAddFinalDamageValue==0)
                        Require(floats[0].GetComponentsInChildren<TextMesh>(true).Any(v=>v.text=="0"),
                            name+": the original zero glyph was not bound.");
                    count++;
                };
                check("Default zero",new FightBattleDamageEvent(),0);
                check("Configured zero",new FightBattleDamageEvent { DamageEntityId=10004 },0);
                check("Suppressed zero",new FightBattleDamageEvent { DamageEntityId=10165 },-1);
                foreach (int id in new[] {10152,13063,13352})
                {
                    check("Hidden nonzero "+id,new FightBattleDamageEvent { DamageEntityId=id, Damage=-100, Delay=.1f },-1);
                    check("Hidden element "+id,new FightBattleDamageEvent
                        { DamageEntityId=id, IsElementAffect=true, ElementConfrontation=1, Delay=.1f },-1);
                }
                check("Element bypasses show-zero",new FightBattleDamageEvent
                    { DamageEntityId=10165, IsElementAffect=true, ElementConfrontation=.2f },0);
                check("Shield damage bypasses show-zero",new FightBattleDamageEvent
                    { DamageEntityId=10165, HasDamageValue=true, ShieldDamage=100 },0);
                target.MonsterId=3933; target.MonsterTemplateId=3933;
                check("3933 zero immunity filter",new FightBattleDamageEvent { Immunity=true },-1);
                check("3933 nonzero immunity bypass",new FightBattleDamageEvent { Immunity=true, Damage=-100 },4);
                check("3933 element immunity bypass",new FightBattleDamageEvent
                    { Immunity=true, IsElementAffect=true, ElementConfrontation=1 },4);
                target.MonsterId=999999;
                check("Filter follows original template",new FightBattleDamageEvent { Immunity=true },-1);
                target.MonsterId=3933; target.MonsterTemplateId=3100;
                check("Other template does not inherit 3933 filter",new FightBattleDamageEvent { Immunity=true },4);
                target.MonsterId=0; target.MonsterTemplateId=0;
                check("Ordinary zero immunity",new FightBattleDamageEvent { Immunity=true },4);
                check("Raw healing precedes flags",new FightBattleDamageEvent
                    { HasDamageValue=true, DamageValue=100, Damage=0, Immunity=true, Dodge=true, Critical=true },3);
                check("Zero cure uses signed-value UI selection",new FightBattleDamageEvent { IsCure=true },0);
                check("Immunity precedes dodge",new FightBattleDamageEvent { Immunity=true, Dodge=true },4);
                check("Dodge precedes blind",new FightBattleDamageEvent { Dodge=true, Blinding=true },8);
                check("Critical precedes block UI",new FightBattleDamageEvent { Critical=true, Block=true, Damage=-100 },1);
                check("Abyss critical",new FightBattleDamageEvent { Critical=true, Block=true, ElementType=200, Damage=-100 },6);
                check("Skill cost bypasses damage filter",new FightBattleDamageEvent
                    { IsSkillCost=true, DamageEntityId=10152, Damage=-100 },0);
                var filter=new CustomDamageUITypeFilter { IsCrit=true,IsBlock=true,IsDodge=true,
                    IsBlinding=true,IsImmunity=true,IsBreakResilience=true,IsTrueDamage=true };
                foreach (var hit in new[] {new FightBattleDamageEvent {Critical=true},new FightBattleDamageEvent {Block=true},
                    new FightBattleDamageEvent {Dodge=true},new FightBattleDamageEvent {Blinding=true},
                    new FightBattleDamageEvent {Immunity=true},new FightBattleDamageEvent {BreakResilience=true},
                    new FightBattleDamageEvent {TrueDamage=true}})
                {
                    Require(!CharacterDamageUIManager.CanShowDamage(hit,false,true,filter),"Configured zero type was not filtered.");
                    hit.Damage=-1;
                    Require(CharacterDamageUIManager.CanShowDamage(hit,false,false,filter),"Type filter hid nonzero damage.");
                    hit.Damage=0;hit.IsElementAffect=true;
                    Require(CharacterDamageUIManager.CanShowDamage(hit,false,false,filter),"Type filter hid element feedback.");
                    Require(!CharacterDamageUIManager.CanShowDamage(hit,true,true,filter),"Hide flag lost priority.");
                }
                var colors=OfficialBattleDamageColors.Load();
                var variant=colors.Settings.elements.First();
                var combined=new FightBattleDamageEvent { Critical=true,Block=true };
                Require(colors.Resolve(combined,variant.element)==new Color(variant.block[0],variant.block[1],variant.block[2],variant.block[3]),
                    "Original GetDamageColor block priority was lost.");
                Require(colors.Resolve(new FightBattleDamageEvent {IsCure=true},variant.element)
                    ==colors.Resolve(new FightBattleDamageEvent {IsCure=true,SkillEntityId=1},variant.element),
                    "Generic MyFightDamage cure color incorrectly depends on skill identity.");
                Debug.Log("Original damage visibility passed: "+count+" presentation cases, seven type filters, signed UI selection and color priority.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.DestroyImmediate(hud);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public sealed class Probe
        {
            private int counter,targetId;
            private bool verified;
            public string Observation {get;private set;}

            public static Probe Install(OfficialBattleRuntimeController controller)
            {
                var checkpoint=controller.Checkpoint;
                var player=checkpoint.Units.Single(v=>v.Id==1);
                int maxHp=OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula().PropertyId("MaxHp");
                foreach (var unit in checkpoint.Units)
                {
                    unit.Hp=unit.MaxHp=unit.Turn.Hp=20000;
                    foreach (var properties in new[] {unit.Properties,unit.BaseProperties})
                        if (properties!=null)
                            foreach (var property in properties.Where(v=>v.Id==maxHp)) property.Value=20000;
                }
                foreach (var skill in player.Skills)
                {
                    var original=FightBattleHitCollector.CollectAll(skill).FirstOrDefault(v=>!v.IsSummon && v.ActionType!=0);
                    if (original==null) continue;
                    int entity=original.EntityId;
                    skill.HasCustomDamage=false;
                    skill.HitGraphRoots.Clear();
                    skill.HitGraphRootEntityIds.Clear();
                    skill.HitGraphRootEntityId=0;
                    skill.Hits.Clear();
                    skill.Hits.Add(new FightBattleHitDefinition {EntityId=entity,DamageId=10152,ActionType=4,
                        DamageType=2,HasCustomDamage=true,CustomDamage=100});
                    skill.Hits.Add(new FightBattleHitDefinition {EntityId=entity,DamageId=10165,ActionType=4,
                        DamageType=2,HasCustomDamage=true,CustomDamage=0});
                    skill.Hits.Add(new FightBattleHitDefinition {EntityId=entity,DamageId=10004,ActionType=4,
                        DamageType=2,HasCustomDamage=true,CustomDamage=0});
                }
                var saved=FightBattleCheckpointSerializer.Save(checkpoint);
                var control=FightBattleCheckpointSerializer.Load(saved);
                var runner=OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(control);
                for (int i=0;i<32 && !control.Finished;i++)
                {
                    var result=runner.Step(control.Sequence,control.Counter+1);
                    var first=result.Event.DamageEvents.FirstOrDefault(v=>v.SourceId==1);
                    if (first!=null)
                    {
                        Require(result.Event.DamageEvents.Where(v=>v.SourceId==1 && v.TargetId==first.TargetId)
                            .Select(v=>v.DamageEntityId).SequenceEqual(new[] {10152,10165,10004}),
                            "The control runner did not execute all three configured visibility hits.");
                        return new Probe {counter=control.Counter,targetId=first.TargetId};
                    }
                }
                throw new BuildFailedException("The visibility fixture has no player hit.");
            }

            public bool TryVerify(FightBattleCheckpoint checkpoint)
            {
                if (verified) return true;
                if (checkpoint==null) { Observation="Battle ended before display observation."; return false; }
                if (checkpoint.Counter<counter) return false;
                var hits=checkpoint.Receipts.Single(v=>v.Counter==counter).Event.DamageEvents
                    .Where(v=>v.SourceId==1 && v.TargetId==targetId).ToArray();
                Require(hits.Length==3 && hits[0].DamageEntityId==10152 && hits[0].Damage<0
                    && hits[1].DamageEntityId==10165 && hits[1].ShieldAddFinalDamageValue==0
                    && hits[2].DamageEntityId==10004 && hits[2].ShieldAddFinalDamageValue==0,
                    "The live fixture lost original damage identities or settled damage: "
                    +string.Join(";",hits.Select(v=>v.DamageEntityId+"="+v.Damage+"/"+v.ShieldAddFinalDamageValue)));
                var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                Require(restored.Receipts.Single(v=>v.Counter==counter).Event.DamageEvents
                    .Where(v=>v.SourceId==1 && v.TargetId==targetId).Select(v=>v.DamageEntityId)
                    .SequenceEqual(new[] {10152,10165,10004}),"Saved replay lost display identities.");
                var floats=UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .Where(v=>v.TargetId==targetId).ToArray();
                var numbers=floats.SelectMany(v=>v.GetComponentsInChildren<TextMesh>(true)).ToArray();
                Observation="counter="+checkpoint.Counter+", target floats="+floats.Length+", texts="
                    +string.Join(",",numbers.Select(v=>v.text));
                var zero=numbers.FirstOrDefault(v=>v.text=="0" && v.color.a>.05f && v.GetComponent<Renderer>().isVisible);
                if (zero==null) return false;
                Require(floats.Length==1,"Hidden or suppressed-zero hit spawned an extra float. "+Observation);
                OfficialBattleDamageDisplayAcceptance.Probe.VerifyCameraPixels(zero.GetComponent<Renderer>());
                verified=true;
                Debug.Log("Damage-visibility Play Mode passed: hidden hit settled "+hits[0].Damage
                    +" HP, configured zero suppression retained, one visible original zero glyph; replay retains three source identities.");
                return true;
            }
        }

        private static void Require(bool condition,string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
