using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // One original skeleton owns its physical clock and constraint histories.
    // Track replay changes animation time, never this clock or its velocities.
    public sealed class ProfessionNativePlayerPhysicsPose
    {
        private sealed class History
        {
            internal readonly ProfessionNativeSpringConstraint Solver = new ProfessionNativeSpringConstraint();
            internal bool Bound;
        }

        private readonly ProfessionNativeSkeletonData data;
        private readonly ProfessionNativeConstraintAnimation setupConstraints;
        private readonly ProfessionNativePhysicsAnimation setupPhysics;
        private readonly Dictionary<int, History> histories = new Dictionary<int, History>();
        private readonly float[] lengths;
        private readonly float reference;
        private ProfessionNativeAppearanceSkeleton owner;
        private float clock;

        public ProfessionNativePlayerPhysicsPose(ProfessionNativeSkeletonData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            setupConstraints = ProfessionNativeConstraintAnimation.CreateSetup(data);
            setupPhysics = ProfessionNativePhysicsAnimation.CreateSetup(data);
            var bones = (List<object>)data.SourceFields["bones"];
            lengths = bones.Select(row => Number((Dictionary<string, object>)row, "length", 0)).ToArray();
            reference = Number((Dictionary<string, object>)data.SourceFields["skeleton"], "referenceScale", 100);
            if (reference <= 0) throw new InvalidDataException("Original physics reference scale must be positive.");
        }

        public float PhysicsTime => clock;

        public void Translate(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x));
            foreach (var history in histories.Values) history.Solver.Translate(x, y);
        }

        public void Rotate(float x, float y, float degrees)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)
                || float.IsNaN(degrees) || float.IsInfinity(degrees))
                throw new ArgumentOutOfRangeException(nameof(degrees));
            foreach (var history in histories.Values) history.Solver.Rotate(x, y, degrees);
        }

        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels channels,
            float animationTime, bool loop, float deltaTime)
            => Apply(skeleton, channels, animationTime, loop, deltaTime, true);

        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels channels,
            float animationTime, bool loop, float deltaTime, bool updatePhysics,
            Action updateLocal = null, Action updateComplete = null)
        {
            Validate(skeleton, channels, animationTime, deltaTime, updatePhysics);
            float position = Position(channels, animationTime, loop);
            var physics = (channels?.Physics ?? setupPhysics).Sample(position, skeleton.SkinConstraints);
            ApplyCompleted(skeleton, physics, deltaTime, updatePhysics, () =>
            {
                if (channels == null) ProfessionNativeConstraintPose.EvaluateSetupBeforePhysics(skeleton, setupConstraints, updateLocal);
                else ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, animationTime, loop, updateLocal);
            });
            updateComplete?.Invoke();
        }

        public void ApplyMixNext(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels outgoing, float outgoingTime, bool outgoingLoop,
            ProfessionNativeAnimationChannels incoming, float incomingTime, bool incomingLoop,
            float weight, float deltaTime, bool updatePhysics = true)
        {
            if (outgoing == null || incoming == null) throw new ArgumentNullException();
            Validate(skeleton, outgoing, outgoingTime, deltaTime, updatePhysics);
            Validate(skeleton, incoming, incomingTime, deltaTime, updatePhysics);
            var physics = outgoing.Physics.Sample(Position(outgoing, outgoingTime, outgoingLoop), skeleton.SkinConstraints);
            incoming.Physics.ApplyReplace(physics, Position(incoming, incomingTime, incomingLoop), weight);
            ApplyCompleted(skeleton, physics, deltaTime, updatePhysics, () =>
                ProfessionNativeConstraintPose.EvaluateMixNextBeforePhysics(skeleton,
                    outgoing, outgoingTime, outgoingLoop, incoming, incomingTime, incomingLoop, weight));
        }

        private void Validate(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels channels,
            float animationTime, float deltaTime, bool updatePhysics)
        {
            if (skeleton == null || !ReferenceEquals(skeleton.Source, data))
                throw new ArgumentException("Bind physics to its exact original skeleton data.", nameof(skeleton));
            if (float.IsNaN(animationTime) || float.IsInfinity(animationTime) || animationTime < 0 ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!updatePhysics && deltaTime != 0)
                throw new ArgumentException("Physics.Pose does not advance the physical clock.", nameof(deltaTime));
            if (channels != null && !ReferenceEquals(channels.OriginalFields, data.GetAnimation(channels.Name)))
                throw new ArgumentException("Original physics animation belongs to another skeleton.", nameof(channels));
        }

        private static float Position(ProfessionNativeAnimationChannels channels, float time, bool loop)
            => channels != null && loop && channels.Duration > 0 ? time % channels.Duration : time;

        private void ApplyCompleted(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativePhysicsAnimation.State[] physics, float deltaTime, bool updatePhysics, Action solve)
        {
            int lastOther = -1;
            for (int index = 0; index < data.Constraints.Count; index++)
            {
                var definition = data.Constraints[index];
                if (definition.Fields.TryGetValue("skin", out var required) && (bool)required && !skeleton.SkinConstraints.Contains(index)) continue;
                if (definition.Kind == "physics") continue;
                if (definition.Kind != "ik" && definition.Kind != "transform")
                    throw new NotSupportedException("Restore original player constraint kind: " + definition.Kind);
                lastOther = Math.Max(lastOther, (int)Number(definition.Fields, "order", 0));
            }
            foreach (var state in physics)
            {
                if (state.Order <= lastOther) throw new NotSupportedException("Restore interleaved original player physics ordering.");
                if (state.ScaleX != 0 || state.ShearX != 0)
                    throw new NotSupportedException("Restore original player physics scale/shear channels: " + state.Name);
            }
            bool nextOwner = !ReferenceEquals(owner, skeleton);
            float next = (nextOwner ? 0 : clock) + deltaTime;
            if (float.IsInfinity(next)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            // Solve the already mixed local pose and geometric constraints once,
            // then advance this skeleton's same physical history once.
            solve();
            if (nextOwner) { histories.Clear(); owner = skeleton; clock = 0; }
            foreach (var state in physics)
            {
                if (!skeleton.Pose.IsActive(state.Bone) || state.Mix == 0) continue;
                if (!histories.TryGetValue(state.ConstraintIndex, out var history))
                    histories.Add(state.ConstraintIndex, history = new History());
                var matrix = skeleton.Pose.World(state.Bone);
                var input = new ProfessionNativeSpringConstraint.Affine { A = matrix.m00, B = matrix.m01,
                    C = matrix.m10, D = matrix.m11, X = matrix.m03, Y = matrix.m13, Length = lengths[state.Bone] };
                if (!history.Bound)
                {
                    // Inactive constraints bind on their first active update.
                    // Their elapsed clock still starts at skeleton creation.
                    history.Solver.Reset(input, 0); history.Bound = true;
                    continue;
                }
                var parameters = new ProfessionNativeSpringConstraint.Parameters { X = state.X, Y = state.Y,
                    Turn = state.Rotate, Limit = state.Limit, Step = state.Step, Inertia = state.Inertia,
                    Strength = state.Strength, Damping = state.Damping, InverseMass = state.MassInverse,
                    Wind = state.Wind, Gravity = state.Gravity, Mix = state.Mix, Reference = reference };
                var result = updatePhysics ? history.Solver.Evaluate(input, parameters, next) : history.Solver.Pose(input, parameters);
                matrix.m00 = result.A; matrix.m01 = result.B; matrix.m10 = result.C; matrix.m11 = result.D;
                matrix.m03 = result.X; matrix.m13 = result.Y;
                skeleton.Pose.OverrideWorld(state.Bone, matrix);
            }
            skeleton.Pose.UpdateWorld();
            clock = next;
        }

        private static float Number(IReadOnlyDictionary<string, object> fields, string key, float fallback)
        {
            float value = fields.TryGetValue(key, out var item) ? Convert.ToSingle(item, CultureInfo.InvariantCulture) : fallback;
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Original physics field is not finite: " + key);
            return value;
        }
    }
}
