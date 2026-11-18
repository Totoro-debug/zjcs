using System;
using UnityEngine;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.BattleSkillPlayables
{
    // Adds a transient observer to the original blocked movement track. The
    // trigger progress comes from the track's own first endpoint crossing.
    public sealed class BattleBlockedTrackContact : MyPlayableTrigger
    {
        private Action contact;

        public static bool Supports(GameObject prefab)
        {
            if (!prefab || prefab.name != "playable_move_blocked") return false;
            MyPlayableTrack selected = null;
            foreach (var track in prefab.GetComponentsInChildren<MyPlayableTrack>(true))
            {
                if (!track || track.name != "move") continue;
                if (selected) return false;
                selected = track;
            }
            return selected && selected.myTrack is MyCurveTrack curve
                && curve.name == "playable_move_blocked_track"
                && selected.myTrackTriggerList != null && selected.myTrackTriggerList.Count == 0
                && TryContactProgress(curve, out _);
        }

        public override void Play(MyPlayable playable)
        {
            var callback = contact;
            contact = null;
            callback?.Invoke();
        }

        public static void Attach(MyPlayable playable, Action onContact)
        {
            if (onContact == null) throw new ArgumentNullException(nameof(onContact));
            if (!(playable is MyPlayableTrack track) || track.name != "move"
                || !(track.myTrack is MyCurveTrack curve) || curve.name != "playable_move_blocked_track"
                || !TryContactProgress(curve, out float progress)
                || track.myTrackTriggerList == null || track.myTrackTriggerList.Count != 0)
                throw new InvalidOperationException("The original blocked track does not match its verified collision curve.");
            var trigger = track.gameObject.AddComponent<BattleBlockedTrackContact>();
            trigger.contact = onContact;
            var cfg = new MyPlayableTriggerTimeCfg { myTime = progress, myTrigger = trigger };
            bool reliable = track.myTrackTriggerReliable;
            track.myTrackTriggerReliable = false;
            track.myTrackTriggerList.Add(cfg);
            IDisposable subscription = null;
            subscription = track.OnStop.Subscribe(_ => {
                track.myTrackTriggerList.Remove(cfg); track.myTrackTriggerReliable = reliable;
                trigger.contact = null; UnityEngine.Object.Destroy(trigger); subscription?.Dispose();
            });
        }

        internal static bool TryContactProgress(MyCurveTrack curve, out float progress)
        {
            progress = 0;
            if (!curve || curve.myCurveY == null || float.IsNaN(curve.myDuration)
                || float.IsInfinity(curve.myDuration) || curve.myDuration <= 0f) return false;
            var keys = curve.myCurveY.keys;
            if (keys == null || keys.Length < 2 || keys[0].time != 0f || keys[0].value >= 1f) return false;
            for (int i = 1; i < keys.Length; i++)
            {
                var left = keys[i - 1]; var right = keys[i];
                double dt = right.time - left.time;
                if (dt <= 0 || left.weightedMode != WeightedMode.None || right.weightedMode != WeightedMode.None
                    || float.IsNaN(left.value) || float.IsInfinity(left.value)
                    || float.IsNaN(right.value) || float.IsInfinity(right.value)
                    || float.IsNaN(left.outTangent) || float.IsInfinity(left.outTangent)
                    || float.IsNaN(right.inTangent) || float.IsInfinity(right.inTangent)) return false;
                double a = 2d * left.value - 2d * right.value + dt * (left.outTangent + right.inTangent);
                double b = -3d * left.value + 3d * right.value - dt * (2d * left.outTangent + right.inTangent);
                double c = dt * left.outTangent;
                double derivativeEnd = 3d * a + 2d * b + c;
                if (c < 0 || derivativeEnd < 0) return false;
                if (a > 0)
                {
                    double vertex = -b / (3d * a);
                    if (vertex > 0 && vertex < 1 && 3d * a * vertex * vertex + 2d * b * vertex + c < 0)
                        return false;
                }
                if (right.value < 1f) continue;
                float low = left.time, high = right.time;
                for (int n = 0; n < 32; n++)
                {
                    float middle = low + (high - low) * .5f;
                    if (curve.myCurveY.Evaluate(middle) >= 1f) high = middle;
                    else low = middle;
                }
                progress = high / curve.myDuration;
                return progress >= 0f && progress <= 1f;
            }
            return false;
        }
    }
}
