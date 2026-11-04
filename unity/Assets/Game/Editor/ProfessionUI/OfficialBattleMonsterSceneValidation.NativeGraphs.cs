using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using MyPlayableAnimator = Zjcs.Official.StoryAnimation.MyPlayableAnimator;

namespace Zjcs.Editor.ProfessionUI
{
    public static partial class OfficialBattleMonsterSceneValidation
    {
        private static OfficialBattleSkillGraphPlayback nativeGraphPlayback;
        private sealed class SoundProbe : Zjcs.Official.ProfessionUI.IProfessionSoundHost
        {
            public readonly List<uint> Events = new List<uint>();
            public uint PostEvent(uint eventId, GameObject emitter) { Events.Add(eventId); return 0; }
        }
        private static IEnumerable<int> NativeGraphSteps()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set the owned validation workspace.");
            var mapper = InitializeRenderScene();
            var assets = BattleNativeMonsterAssets.Load();
            int count = 0;
            foreach (int id in assets.EntityIds.OrderBy(value => value))
            {
                model = OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState {
                    Id = id, MonsterTemplateId = id, Hp = 100, MaxHp = 100 }, root.transform, true);
                var native = model.ModelTransform.GetComponent<BattleNativeMonsterRenderer>();
                var controller = model.Transform.GetComponent<AnimationController>();
                Require(controller && controller.myInited && controller.myAnimator == native.Animator
                    && controller.InitOnAwake && !controller.CreateOverrideController && !controller.NotCreateGraph
                    && controller.enabled, "Original entity application controller flags/binding differ: " + id);
                Require(controller.RuntimeAnimatorController == native.Animator.runtimeAnimatorController
                    && !(controller.RuntimeAnimatorController is AnimatorOverrideController),
                    "Original monster application controller was replaced with an invented override: " + id);
                model.Tick(.1f);
                Require(native.AnimatorFrame.Current.Animation?.Name == "idle_01", "Original application controller lost its monster pose.");
                Transform owned = model.Transform;
                model.Dispose(); model = null;
                yield return 0;
                Require(!owned && !controller, "Original monster application controller leaked on unload.");
                count++;
            }

            // A real complete source graph (including its particle and sound
            // leaves) drives a compatible original monster state. This checks
            // the binding seam, not the monster's rule/configured skill loadout.
            var checkpoint = new FightBattleCheckpoint();
            var unit = new FightBattleUnitState { Id = 3904, MonsterTemplateId = 3904, Hp = 100, MaxHp = 100, X = 2, Y = 2 };
            checkpoint.Units.Add(unit);
            var sound = new SoundProbe();
            var scope = new OfflineBattleNativePlayableScope(root.transform, sound);
            var entities = new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope,
                id => checkpoint.Units.FirstOrDefault(value => value.Id == id));
            nativeGraphPlayback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope, entities);
            scene = new OfficialBattleSceneView(null, root.transform, null, mapper, null, null, nativePlayableScope: scope);
            scene.Refresh(checkpoint);
            Transform entity = root.transform.Find("BattleUnit_3904");
            var renderer = entity.GetComponentInChildren<BattleNativeMonsterRenderer>();
            var application = entity.GetComponent<AnimationController>();
            var step = new FightBattleStepEvent { ActorId = unit.Id };
            step.SkillCasts.Add(new FightBattleSkillCastEvent { ActorId = unit.Id, SkillEntityId = 15510, Delay = .1f,
                TargetIds = new List<int> { unit.Id }, Presentation = new FightBattleSkillCastPresentation {
                    SourceX = 2, SourceY = 2, TargetId = unit.Id, TargetX = 2, TargetY = 2,
                    FacingIndex = 0, SkillTargetType = 1, Duration = .3f, ActionDuration = .5f } });
            scene.Refresh(checkpoint, step);
            nativeGraphPlayback.Play(step, checkpoint, id => id == unit.Id ? entity : null);
            bool animated = false, bound = false;
            for (int frame = 0; frame < 60; frame++)
            {
                nativeGraphPlayback.Tick(1f / 60); scene.Tick(1f / 60); nativeGraphPlayback.LateTick();
                animated |= renderer.AnimatorFrame.Current.Animation?.Name == "skill_01";
                foreach (var leaf in scope.PlayableSlot.GetComponentsInChildren<MyPlayableAnimator>(true))
                    if (leaf.myTriggerParam == "skill_01" && leaf.myApplyComponent == application) bound = true;
                Require(!entity.Find("OfficialBattleAnimatorPlayables"), "The flat skill cue duplicated the complete source graph.");
                CheckFollower(renderer, entity.GetComponentInChildren<OfficialBattleShadowBoneFollower>());
                if (frame == 12 || frame == 30)
                {
                    var bounds = renderer.GetComponent<MeshRenderer>().bounds;
                    camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                    camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.8f;
                    Capture(Path.Combine(output, "native-monster-graph-" + frame + ".png"));
                }
                yield return 0;
            }
            Require(animated && bound, "The complete original skill graph did not resolve/drive the monster application controller.");
            Require(sound.Events.Count > 0 && sound.Events.All(value => value != 0), "Original sound leaves did not reach the scope backend.");
            Require(root.GetComponentsInChildren<AnimationController>(true).Length == 1,
                "The complete graph created another application controller.");
            var actions = JsonUtility.FromJson<MovementSources>(Resources.Load<TextAsset>("Combat/move-animators").text);
            var action = actions.actions.First(value => value.playables.Any(cue => cue.my_bool_param == "run"
                && cue.my_bool_value && cue.end_resume_param));
            unit.X = 3;
            var move = new FightBattleStepEvent { ActorId = unit.Id };
            move.Movements.Add(new FightBattleMovementEvent { UnitId = unit.Id, MoveBehaviourId = action.id,
                MoveDuration = .25f, Path = new List<FightGridPoint> { new FightGridPoint(2, 2), new FightGridPoint(3, 2) } });
            scene.Refresh(checkpoint, move);
            bool ran = false;
            for (int frame = 0; frame < 30; frame++)
            {
                nativeGraphPlayback.Tick(1f / 60); scene.Tick(1f / 60); nativeGraphPlayback.LateTick();
                ran |= renderer.Animator.GetBool("run") && renderer.AnimatorFrame.Current.Animation?.Name == "run_01";
                yield return 0;
            }
            Require(ran && !renderer.Animator.GetBool("run") && entity.GetComponentsInChildren<AnimationController>(true).Length == 1,
                "Movement did not share/resume the original entity application controller.");
            nativeGraphPlayback.Dispose(); nativeGraphPlayback = null;
            scene.Dispose(); scene = null;
            yield return 0;
            Require(!entity && !application, "Native monster graph/scene disposal leaked its source controller.");
            Require(count == 18, "Original monster controller coverage changed.");
            File.WriteAllText(Path.Combine(output, "monster-native-graph-result.json"),
                "{\"controllers\":18,\"completeGraph\":15510,\"nativeMotion\":\"skill_01\",\"flatCueDuplicated\":false,\"sharedMovement\":true,\"failures\":[]}");
            Debug.Log("[Monster native graph] passed: 18 original controllers, complete 15510 graph, shared movement, disposal.");
        }
    }
}
