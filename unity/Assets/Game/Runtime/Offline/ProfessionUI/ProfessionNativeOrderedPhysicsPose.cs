using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Native UI and battle rigs interleave springs with IK/transform/path constraints. Reuse
    // the independent affine solvers and preserve each constraint's source order.
    public sealed class ProfessionNativeOrderedPhysicsPose
    {
        private sealed class History
        {
            internal readonly ProfessionNativeSpringConstraint Solver = new ProfessionNativeSpringConstraint();
            internal bool Bound;
        }
        private readonly ProfessionNativeSkeletonData data;
        private readonly Dictionary<int, History> histories = new Dictionary<int, History>();
        private readonly float[] lengths;
        private readonly float reference;
        private ProfessionNativeAppearanceSkeleton owner;
        private float clock;
        public ProfessionNativeOrderedPhysicsPose(ProfessionNativeSkeletonData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            lengths = ((List<object>)data.SourceFields["bones"]).Select(value => {
                var row = (Dictionary<string, object>)value;
                return row.TryGetValue("length", out var length) ? Convert.ToSingle(length) : 0;
            }).ToArray();
            var skeleton = (Dictionary<string, object>)data.SourceFields["skeleton"];
            reference = skeleton.TryGetValue("referenceScale", out var scale) ? Convert.ToSingle(scale) : 100;
            if (!float.IsFinite(reference) || reference <= 0) throw new ArgumentException("Original UI physics scale differs.");
        }
        public float PhysicsTime => clock;
        public void Translate(float x, float y)
        { foreach (var history in histories.Values) history.Solver.Translate(x, y); }
        public void Rotate(float x, float y, float degrees)
        { foreach (var history in histories.Values) history.Solver.Rotate(x, y, degrees); }
        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, ProfessionNativeAnimationChannels incoming,
            float time, bool loop, float delta, ProfessionNativeAnimationChannels outgoing = null,
            float outgoingTime = 0, bool outgoingLoop = false, float weight = 1)
        {
            var layers = new List<ProfessionNativeOrderedConstraintPose.AnimationLayer>();
            if (outgoing != null) layers.Add(new ProfessionNativeOrderedConstraintPose.AnimationLayer(outgoing, outgoingTime, outgoingLoop, 1));
            layers.Add(new ProfessionNativeOrderedConstraintPose.AnimationLayer(incoming, time, loop, weight));
            Apply(skeleton, layers, delta);
        }
        public void ApplyMixNext(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimationChannels outgoing, float outgoingTime, bool outgoingLoop,
            ProfessionNativeAnimationChannels incoming, float incomingTime, bool incomingLoop,
            float weight, float delta)
            => Apply(skeleton, incoming, incomingTime, incomingLoop, delta, outgoing, outgoingTime, outgoingLoop, weight);

        public void Apply(ProfessionNativeAppearanceSkeleton skeleton,
            IReadOnlyList<ProfessionNativeOrderedConstraintPose.AnimationLayer> layers, float delta)
        {
            if (skeleton == null || skeleton.Source != data || layers == null || layers.Count == 0)
                throw new ArgumentException("Bind the exact original UI pose and animation.");
            if (!float.IsFinite(delta) || delta < 0)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (owner != skeleton) { histories.Clear(); owner = skeleton; clock = 0; }
            float next = clock + delta;
            if (!float.IsFinite(next)) throw new ArgumentOutOfRangeException(nameof(delta));
            var first = layers[0];
            float at = first.Loop && first.Animation.Duration > 0 ? first.Time % first.Animation.Duration : first.Time;
            var physics = first.Animation.Physics.Sample(at, skeleton.SkinConstraints);
            for (int index = 1; index < layers.Count; index++)
            {
                var layer = layers[index];
                at = layer.Loop && layer.Animation.Duration > 0 ? layer.Time % layer.Animation.Duration : layer.Time;
                layer.Animation.Physics.ApplyReplace(physics, at, layer.Weight);
            }
            var stages = new Dictionary<int, Action>();
            foreach (var state in physics)
            {
                if (state.ScaleX != 0 || state.ShearX != 0)
                    throw new NotSupportedException("Restore original UI physics scale/shear: " + state.Name);
                var captured = state;
                stages.Add(state.Order, () => {
                    if (!skeleton.Pose.IsActive(captured.Bone) || captured.Mix == 0) return;
                    if (!histories.TryGetValue(captured.ConstraintIndex, out var history))
                        histories.Add(captured.ConstraintIndex, history = new History());
                    var matrix = skeleton.Pose.World(captured.Bone);
                    var input = new ProfessionNativeSpringConstraint.Affine { A = matrix.m00, B = matrix.m01,
                        C = matrix.m10, D = matrix.m11, X = matrix.m03, Y = matrix.m13, Length = lengths[captured.Bone] };
                    if (!history.Bound) { history.Solver.Reset(input, 0); history.Bound = true; return; }
                    var parameters = new ProfessionNativeSpringConstraint.Parameters { X = captured.X, Y = captured.Y,
                        Turn = captured.Rotate, Limit = captured.Limit, Step = captured.Step, Inertia = captured.Inertia,
                        Strength = captured.Strength, Damping = captured.Damping, InverseMass = captured.MassInverse,
                        Wind = captured.Wind, Gravity = captured.Gravity, Mix = captured.Mix, Reference = reference };
                    var result = history.Solver.Evaluate(input, parameters, next);
                    matrix.m00 = result.A; matrix.m01 = result.B; matrix.m10 = result.C; matrix.m11 = result.D;
                    matrix.m03 = result.X; matrix.m13 = result.Y;
                    skeleton.Pose.OverrideWorld(captured.Bone, matrix);
                });
            }
            ProfessionNativeOrderedConstraintPose.EvaluateStackWithAdditionalConstraints(skeleton, layers, stages);
            clock = next;
        }
    }
}
