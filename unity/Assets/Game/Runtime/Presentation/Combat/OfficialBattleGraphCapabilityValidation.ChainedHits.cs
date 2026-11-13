using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateChainedHits => Environment.GetEnvironmentVariable("ZJCS_CHAINED_HIT_ACCEPTANCE") == "1";
        private FightBattleHitSelectionSnapshot[] chainedHitSelections;
        private readonly HashSet<int> chainedHitViews = new HashSet<int>();
        private readonly HashSet<int> chainedHitFrames = new HashSet<int>();
        private bool chainedHitComplete;
        private float chainedHitLastClock;

        private void ConfigureChainedHitDiagnostic(FightBattleCheckpoint checkpoint,
            FightBattleUnitState enemy, int dx, int dy)
        {
            if (!ValidateChainedHits) return;
            Require(firstPromotionItemId == 10030 || firstPromotionItemId == 10048,
                "The chained-hit diagnostic requires original lightning or bouncing shield.");
            for (int index = 1; index <= 2; index++)
            {
                var target = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(enemy));
                target.Id = checkpoint.Units.Max(unit => unit.Id) + 1; target.Turn.Id = target.Id;
                // Keep all candidates on the requested cast axis. A perpendicular
                // roster can legitimately make the ordinary AI choose another facing.
                target.X += dx * index; target.Y += dy * index;
                Require(target.X >= 0 && target.X < checkpoint.Grid.Width
                    && target.Y >= 0 && target.Y < checkpoint.Grid.Height,
                    "Chained-hit diagnostic target is outside the original battlefield.");
                checkpoint.Units.Add(target);
            }
        }

        private void ObserveChainedHitStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateChainedHits || chainedHitSelections != null) return;
            var step = checkpoint.Receipts.Last().Event;
            var cast = step.SkillCasts.SingleOrDefault(value => value.ActorId == 1 && value.SkillEntityId == skillId);
            if (cast == null) return;
            chainedHitSelections = cast.HitSelections.ToArray();
            int expected = firstPromotionItemId == 10030 ? 3 : 4;
            Require(chainedHitSelections.Length == expected
                && chainedHitSelections.All(value => value.HasChainedOrigin && value.EffectCenters.Count == 1
                    && value.Targets.Count == 1 && value.Targets[0].HasGridPosition),
                "Original chain did not retain its settled targets and origins.");
            if (firstPromotionItemId == 10030)
                Require(chainedHitSelections.SelectMany(value => value.Targets).Select(value => value.TargetId)
                    .Distinct().Count() == 3, "Original lightning repeated a target.");
            for (int index = 1; index < chainedHitSelections.Length; index++)
            {
                var previous = chainedHitSelections[index - 1].EffectCenters.Single();
                var current = chainedHitSelections[index];
                Require(current.SourceX == previous.X && current.SourceY == previous.Y,
                    "A chain hop lost its previous actual hit cell.");
            }
            string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "chained-hit-checkpoint.json");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            File.WriteAllText(path, saved);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(File.ReadAllText(path))) == saved,
                "Original chained receipt did not survive the disk checkpoint.");
        }

        private void ObserveChainedHitViews(OfficialBattleRuntimeController controller)
        {
            if (!ValidateChainedHits || chainedHitSelections == null || chainedHitComplete) return;
            foreach (var view in UnityEngine.Object.FindObjectsOfType<MyFightHitChained>())
            {
                int classId = view.Component?.Entity?.ClassId ?? 0;
                var selection = chainedHitSelections.SingleOrDefault(value => value.HitEntityId == classId);
                if (selection == null || chainedHitViews.Contains(classId)) continue;
                var center = selection.EffectCenters.Single();
                Require(view.Component.StartGridPos.Equals(new Pos2d(selection.SourceX, selection.SourceY))
                    && view.GetComponent<MyGridTransform>().Pos.Equals(new Pos2d(center.X, center.Y)),
                    "Original chained view did not consume the settled start/end grid.");
                var start = (GameObject)typeof(MyFightHitChained)
                    .GetField("mStartGo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Require(start && view.myHitToNextHitPlayable, "Original chained flight graph or source helper is absent.");
                var point = entityViews.ToWorldPos(entityViews.GridToPos(new Pos2d(selection.SourceX, selection.SourceY)));
                var player = controller.TryGetUnitTransform(1).GetComponent<MyGridTransform>();
                point = FightHitViewFormula.GetPosByOffset(point, view.Info.Offset, player.FlipX);
                Require(Vector3.Distance(start.transform.position, point) < .001f,
                    "Original chain helper started at a different flight origin.");
                chainedHitViews.Add(classId); chainedHitLastClock = Time.time;
                Debug.Log("[Chained hits] Native hop " + classId + ": " + selection.SourceX + "," + selection.SourceY
                    + " -> " + center.X + "," + center.Y);
            }
        }

        private bool ObserveChainedHitFrame()
        {
            if (!ValidateChainedHits || chainedHitComplete || chainedHitSelections == null) return false;
            foreach (int id in chainedHitViews.OrderBy(value => value))
                if (chainedHitFrames.Add(id))
                { CaptureName = "chained-hit-" + id; return true; }
            if (!captured) return false;
            if (chainedHitViews.Count == chainedHitSelections.Length && Time.time - chainedHitLastClock > 1f)
            {
                Require(!UnityEngine.Object.FindObjectsOfType<MyFightHitChained>().Any(view =>
                    chainedHitViews.Contains(view.Component?.Entity?.ClassId ?? 0)),
                    "Original chained hit did not retire after its authored scopes and DestroyDelay.");
                chainedHitComplete = true; CaptureName = "chained-hits-ended";
                Debug.Log("[Chained hits] Original multi-target flight origins, native graphs, disk receipt and retirement passed: " + firstPromotionItemId);
            }
            return true;
        }

        private void VerifyChainedHitDiagnostic()
        {
            if (ValidateChainedHits) Require(chainedHitComplete
                && chainedHitFrames.Count == chainedHitSelections.Length,
                "Original chained native lifecycle observation is incomplete.");
        }
    }
}
