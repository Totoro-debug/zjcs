using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Combat
{
    public sealed partial class NativeHitAcceptanceRunner
    {
        private IEnumerator ValidateCustomReceipts(Transform root, Transform source, Transform target)
        {
            foreach (int skillId in new[] { 12060, 13300, 13670 })
            {
                var battle = SummonBattle(skillId, out var cast, cp => {
                    cp.Units[1].X=6; cp.Units[1].Y=5;
                    if(skillId==12060) cp.Units[1].Side=1;
                    var extra=JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(cp.Units[1]));
                    extra.Id=3; extra.Turn.Id=3; extra.X=5; extra.Y=6;
                    extra.BodyOffsets.Clear(); extra.BodyOffsets.Add(new FightGridPoint(0,0));
                    extra.BodyOffsets.Add(new FightGridPoint(1,0));
                    cp.Units.Add(extra);
                    var enemy=JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(cp.Units[1]));
                    enemy.Id=4; enemy.Turn.Id=4; enemy.Side=2; enemy.X=14; enemy.Y=14;
                    cp.Units.Add(enemy); cp.Waves[0].UnitIds.Add(4);
                });
                var definition=combatCatalog.CreateSkill(skillId,0,0,5,20,1,null).HitGraphEntities
                    .Single(hit=>hit.HitComponentType=="FightHitCustomTargetComponent");
                var receipt=cast.HitSelections.Single(hit=>hit.HitEntityId==definition.EntityId);
                var children=cast.HitSelections.Where(hit=>definition.CustomTargetHits.Contains(hit.HitEntityId)).ToArray();
                Require(children.Length>0,"Actual custom skill produced no child entities: "+skillId);
                string saved=FightBattleCheckpointSerializer.Save(battle);
                foreach(bool legacy in new[]{false,true})
                {
                    if(legacy) foreach(var hit in cast.HitSelections) hit.EffectDelays.Clear();
                    Time.timeScale=0f;
                    var actual=views.Start(cast,source,target);
                    if(receipt.Delay>0f) views.Tick(receipt.Delay);
                    var native=root.GetComponentsInChildren<MyFightHitCustomTarget>().Single();
                    var center=receipt.EffectCenters.Single();
                    Require(native.Component.SkillId.Equals(actual)
                        && native.Component.GridTransform.Pos.Equals(new Pos2d(center.X,center.Y))
                        && native.Component.GetScopes().Count==definition.ScopeEntries.Count,
                        "Original custom parent lost its identity, scope or actual cell");
                    Require(!native.myPlayable && !native.myScopePlayable && !native.SourceToMePlayable,
                        "Custom parent empty original graph fields changed");
                    // These original parent and summon prefabs have empty playable fields.
                    // Verify the original entities without adding replacement effects.
                    float end=children.Max(hit=>hit.Delay);
                    if(end>receipt.Delay) views.Tick(end-receipt.Delay);
                    var summons=root.GetComponentsInChildren<MyFightHitSummon>();
                    Require(summons.Length==children.Length,"Original custom child entities were not created once per selected cell");
                    foreach(var child in children) Require(summons.Any(view=>
                        view.Component.SkillId.Equals(actual) && view.Component.Entity.ClassId==child.HitEntityId
                        && view.Component.GridTransform.Pos.Equals(new Pos2d(child.EffectCenters[0].X,child.EffectCenters[0].Y))),
                        "Original child lost its settled cell or owning skill");
                    Time.timeScale=1f;
                    yield return new WaitForSeconds(.15f);
                    Capture("hit-custom-"+skillId+(legacy?"-legacy":"")+".png", false);
                    yield return new WaitForSeconds(4f);
                    Require(root.GetComponentsInChildren<MyFightHitCustomTarget>().Length==0
                        && root.GetComponentsInChildren<MyFightHitSummon>().Length==0,"Custom original entities leaked");
                }
                // Playback never settles rules; legacy fixture only erased timing evidence.
                foreach(var hit in cast.HitSelections) hit.EffectDelays.Clear();
                var before=FightBattleCheckpointSerializer.Load(saved);
                foreach(var hit in before.Receipts[0].Event.SkillCasts.Single(value=>value.SkillEntityId==skillId).HitSelections) hit.EffectDelays.Clear();
                Require(FightBattleCheckpointSerializer.Save(before)==FightBattleCheckpointSerializer.Save(battle),"Custom playback modified settlement");
            }
        }
    }
}
