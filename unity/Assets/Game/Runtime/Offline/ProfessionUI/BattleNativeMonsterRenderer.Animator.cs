using System;
using System.IO;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class BattleNativeMonsterRenderer
    {
        private ProfessionNativeAnimatorPose animatorPose;
        private float animatorDelta;
        public Animator Animator { get; private set; }
        public ProfessionNativeAnimatorPose.AppliedFrame AnimatorFrame { get; private set; }

        // The owner advances Unity's original graph once, then consumes that same
        // graph's clips, weights and clocks before building follower geometry.
        public void BindOriginalAnimator(BattleNativeMonsterAssets assets, int entityId)
        {
            if (state == null || state.Source.Sex != "battle-monster:" + entityId)
                throw new ArgumentException("Bind the Animator to its original monster skeleton.");
            if (Animator) throw new InvalidOperationException("The original monster Animator is already bound.");
            var row = assets.Find(entityId) ?? throw new InvalidDataException("Original monster Animator is unbound.");
            if (!Equals(row["rendererKind"], "SkeletonMecanim"))
                throw new NotSupportedException("The original monster does not use Mecanim.");
            var translator = Obj(settings["translator"]);
            if (!Flag(translator, "autoReset") || !Flag(translator, "useCustomMixMode")
                || Arr(translator["layerMixModes"]).Count != 1 || Convert.ToInt32(Arr(translator["layerMixModes"])[0]) != 1
                || Arr(translator["layerBlendModes"]).Count != 1 || Convert.ToInt32(Arr(translator["layerBlendModes"])[0]) != 1)
                throw new NotSupportedException("Bind the original monster translator modes.");
            var original = Obj(row["animator"]);
            if (Flag(original, "m_ApplyRootMotion") || Flag(original, "m_LinearVelocityBlending")
                || Flag(original, "m_StabilizeFeet") || Int(Obj(original["m_Avatar"]), "m_PathID") != 0)
                throw new NotSupportedException("Bind additional original monster Animator branches.");
            Animator = gameObject.AddComponent<Animator>();
            Animator.runtimeAnimatorController = assets.Resolve<RuntimeAnimatorController>((string)row["nativeController"]);
            Animator.cullingMode = (AnimatorCullingMode)Int(original, "m_CullingMode");
            Animator.updateMode = (AnimatorUpdateMode)Int(original, "m_UpdateMode");
            Animator.applyRootMotion = false;
            Animator.keepAnimatorStateOnDisable = Flag(original, "m_KeepAnimatorStateOnDisable");
            Animator.writeDefaultValuesOnDisable = Flag(original, "m_WriteDefaultValuesOnDisable");
            Animator.enabled = false; // Battle time owns Update, including pause and speed.
            animatorPose = new ProfessionNativeAnimatorPose(Animator, SolveAnimator, SolveAnimatorMix,
                originalBezierSegments: true);
            Animator.Rebind(); Animator.Update(0); ApplyAnimatorPose(0);
        }

        private void AdvanceAnimator(float deltaTime)
        {
            if (!Animator) throw new InvalidOperationException("Bind the original monster Animator first.");
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Animator.Update(deltaTime);
            ApplyAnimatorPose(deltaTime);
        }

        // A Playable may have just evaluated the original Animator at delta zero.
        public void ApplyAnimatorPose(float deltaTime)
        {
            if (animatorPose == null) throw new InvalidOperationException("Bind the original monster Animator first.");
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            animatorDelta = deltaTime;
            AnimatorFrame = animatorPose.Apply(state);
            DrawCompletedPose();
        }

        private void SolveAnimator(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels channels, float time, bool repeat)
        {
            if (physics != null) physics.Apply(skeleton, channels, time, repeat, animatorDelta);
            else ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, repeat);
        }

        private void SolveAnimatorMix(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimatorPose.MixNextFrame mix)
        {
            if (physics != null) physics.ApplyMixNext(skeleton,
                mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight, animatorDelta);
            else ProfessionNativeOrderedConstraintPose.EvaluateMixNextBeforePhysics(skeleton,
                mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight);
        }
    }
}
