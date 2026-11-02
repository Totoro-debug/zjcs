using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    // MyPlayableTrack + MyTrack + MyCurveTrack, restricted to the original role
    // prefabs whose rotation/scale application and follow-target flags are disabled.
    internal sealed class OfficialBattleGridMotion
    {
        [Serializable] private sealed class Data { public string manifest; public Track[] tracks; }
        [Serializable] private sealed class Curve
        {
            public Key[] keys;
            public int pre, post;
            public AnimationCurve Create()
            {
                if (pre != 2 || post != 2)
                    throw new InvalidOperationException("Unsupported original curve extrapolation.");
                var frames = new Keyframe[keys.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    Key key = keys[i];
                    frames[i] = new Keyframe(key.time, key.value,
                        key.inSlopeInfinite ? float.PositiveInfinity : key.inSlope,
                        key.outSlopeInfinite ? float.PositiveInfinity : key.outSlope,
                        key.inWeight, key.outWeight) { weightedMode = (WeightedMode)key.weightedMode };
                }
                // Native serialized infinity mode 2 means constant extrapolation.
                // WrapMode's public enum has a different numeric layout (2 means Loop).
                return new AnimationCurve(frames)
                {
                    preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever
                };
            }
        }
        [Serializable] private sealed class Key
        {
            public float time, value, inSlope, outSlope, inWeight, outWeight;
            public int weightedMode;
            public bool inSlopeInfinite, outSlopeInfinite;
        }
        [Serializable] private sealed class Track
        {
            public int action_id;
            public float delay, speed, range_scale, curve_duration;
            public bool fixed_duration, flip_source, flip_target, flip_direction, has_curve;
            public Curve x, y;
            [NonSerialized] public AnimationCurve X, Y;
        }
        private readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();

        public OfficialBattleGridMotion()
        {
            TextAsset asset = Resources.Load<TextAsset>("Combat/grid-motion");
            if (asset == null) throw new InvalidOperationException("Original grid movement bindings are missing.");
            Data data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.manifest != "884_156692" || data.tracks == null)
                throw new InvalidOperationException("Original grid movement bindings have the wrong source.");
            foreach (Track track in data.tracks)
            {
                if (track.action_id <= 0 || track.speed <= 0 || track.curve_duration <= 0)
                    throw new InvalidOperationException("Invalid original grid track.");
                if (track.has_curve) { track.X = track.x.Create(); track.Y = track.y.Create(); }
                tracks.Add(track.action_id, track);
            }
        }

        public bool TryEvaluate(int actionId, float elapsed, Vector3 start, Vector3 end,
            bool sourceFlipped, out Vector3 position)
        {
            position = start;
            if (!tracks.TryGetValue(actionId, out Track track)) return false;
            if (elapsed < track.delay) return true;
            elapsed -= track.delay;
            Vector3 direction = end - start;
            direction.z = 0f;
            float distance = direction.magnitude;
            float duration = track.fixed_duration ? track.curve_duration / track.speed
                : Mathf.Max(0.00001f, distance / (track.speed / track.curve_duration));
            float progress = Mathf.Min(1f, elapsed / duration);
            if (!track.has_curve)
            {
                position = duration == 0f ? end : Vector3.Lerp(start, end, progress);
                return true;
            }
            bool flip = track.flip_source && sourceFlipped;
            if (track.flip_target && end.y < start.y) flip = !flip;
            if (track.flip_direction && start.x > end.x) flip = !flip;
            float time = progress * track.curve_duration;
            float xScale = (1f + (distance - 1f) * track.range_scale) * (flip ? -1 : 1);
            Vector3 offset = new Vector3(track.X.Evaluate(time) * xScale,
                track.Y.Evaluate(time) * distance, 0f);
            Quaternion rotation = !Mathf.Approximately(direction.sqrMagnitude, 0f)
                ? Quaternion.FromToRotation(Vector3.up, direction) : Quaternion.identity;
            position = start + rotation * offset;
            return true;
        }
    }
}
