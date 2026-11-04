using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        private static IEnumerator CheckProductionSkillViews(GameObject actor, GameObject target,
            GameObject prefab, Func<int, float, IEnumerator> pump, Action<string> capture)
        {
            var root = new GameObject("Production shared skill entity playback");
            root.transform.position = actor.transform.position;
            var grid = new FightBattleGrid { Width = 8, Height = 8 };
            var mapper = new OfficialBattleGridMapper(root.transform, grid);
            var originalScale = actor.transform.localScale;
            var priorEntity = actor.GetComponent<MyEntity>();
            using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
            using var playback = new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope,
                id => new FightBattleUnitState { Id = id, CharacterType = id == 1 ? 1 : 0,
                    MonsterId = id == 2 ? 10001 : 0, Direction = 3 });
            FightBattleSkillCastEvent Cast(int id, int targetType, float duration) =>
                new FightBattleSkillCastEvent { ActorId = 1, Slot = 0, SkillEntityId = id,
                    TargetIds = new List<int> { 2 }, Presentation = new FightBattleSkillCastPresentation
                    { SourceX = 0, SourceY = 0, TargetX = 2, TargetY = 3, TargetId = 2,
                        FacingIndex = 1, FlipX = true, FlipSource = true,
                        SkillTargetType = targetType, Duration = duration } };
            IEnumerator Pump(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    playback.Tick(.01f); scope.LateTick();
                    yield return pump(1, .01f);
                }
            }
            try
            {
                playback.Start(Cast(11170, 1, .3f), prefab, actor.transform, target.transform);
                var first = root.GetComponentsInChildren<MyFightSkill>().Single();
                var graph = root.GetComponentsInChildren<MyPlayable>().First(value => value.mySource == actor);
                Require(first.Source.gameObject == actor && first.Target.gameObject == target
                    && ReferenceEquals(graph.TryGetParam("Skill"), first.gameObject) && graph.myTarget == target,
                    "production skill host supplies a distinct Skill entity and original entity target");
                Require(Mathf.Approximately(Mathf.Abs(actor.transform.localScale.x), Mathf.Abs(originalScale.x)),
                    "binding original source flip preserves native model scale magnitude");
                int applied = 0, ended = 0;
                var agent = actor.GetComponent<MyFightSkillAgent>();
                agent.OnSkillApplied += value => applied++;
                agent.OnSkillEnded += value => ended++;
                yield return Pump(20);
                Require(ended == 0 && graph.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p.particleCount) > 0,
                    "production entity remains active and native original particles run before duration");
                capture("shared-skill-entity-active");
                yield return Pump(11);
                Require(ended == 1 && first.myGridTransform.Component != null,
                    "production duration publishes ended once while retaining graph input lifetime");

                // 10820's original prefab prefers position even for entity-target skills.
                playback.Start(Cast(10820, 1, .1f), prefab, actor.transform, target.transform);
                var positional = root.GetComponentsInChildren<MyFightSkill>().Single(view => view != first);
                Require(applied == 1 && positional.PreferredPosToPlayable
                    && positional.GetPlayableTargetGo() != target
                    && positional.GetPlayableTargetGo().transform.position == mapper.GridToWorld(new FightGridPoint(2, 3)),
                    "production host consumes exact original preferred-position flag and grid snapshot");
                yield return Pump(11);
                Require(ended == 2, "separate cast lifetimes publish exactly one ended event each");
                capture("shared-skill-position-target");
                playback.Dispose();
                yield return null;
                Require(actor.GetComponent<MyEntity>() == priorEntity && actor.GetComponent<MyFightSkillAgent>() == null,
                    "production teardown releases only its own source bindings");
                yield return CheckSkillSlotLifecycle(actor, target, prefab, pump);
                Debug.Log("[Production shared skill view] original flags, distinct Skill entity, entity/position target branches, exact duration end, native particles and scoped teardown passed.");
            }
            finally
            {
                playback.Dispose(); actor.transform.localScale = originalScale;
                Object.DestroyImmediate(root);
            }
        }
    }
}
