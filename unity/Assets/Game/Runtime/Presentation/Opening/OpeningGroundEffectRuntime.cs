using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.Opening;

namespace Zjcs.Presentation.Opening
{
    /// <summary>Schedules the official MyPlayableGroundEffect curve and restore lifecycle.</summary>
    public sealed class OpeningGroundEffectRuntime : MonoBehaviour
    {
        private sealed class Motion
        {
            public GroundDissolve Ground;
            public AnimationCurve Curve;
            public float Start, Duration, Speed, Previous;
            public bool Started;
        }

        private readonly List<Motion> motions = new List<Motion>();

        public static bool Play(Transform parent, Dictionary<string, object> entry,
            Dictionary<string, object> fields, float eventSpeed)
        {
            OpeningGroundEffectRuntime runtime = parent.GetComponent<OpeningGroundEffectRuntime>();
            if (runtime == null) runtime = parent.gameObject.AddComponent<OpeningGroundEffectRuntime>();
            float speed = Mathf.Clamp(eventSpeed, 0.01f, 3f);
            runtime.motions.Add(new Motion
            {
                Curve = ReadCurve(fields),
                Start = Time.time + Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime")) / speed,
                Duration = (float)OpeningJson.Number(fields, "Duration"),
                Speed = speed
            });
            return true;
        }

        private static AnimationCurve ReadCurve(Dictionary<string, object> fields)
        {
            Dictionary<string, object> data = OpeningJson.Object(fields["EffectValue"], "ground effect curve");
            List<object> values = OpeningJson.Array(data["m_Curve"], "ground effect curve keys");
            var keys = new Keyframe[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> item = OpeningJson.Object(values[index], "ground effect curve key");
                var frame = new Keyframe((float)OpeningJson.Number(item, "time"),
                    (float)OpeningJson.Number(item, "value"), (float)OpeningJson.Number(item, "inSlope"),
                    (float)OpeningJson.Number(item, "outSlope"));
                frame.weightedMode = (WeightedMode)OpeningJson.Int(item, "weightedMode");
                frame.inWeight = (float)OpeningJson.Number(item, "inWeight", 1f / 3f);
                frame.outWeight = (float)OpeningJson.Number(item, "outWeight", 1f / 3f);
                keys[index] = frame;
            }
            return new AnimationCurve(keys)
            {
                preWrapMode = (WrapMode)OpeningJson.Int(data, "m_PreInfinity", 2),
                postWrapMode = (WrapMode)OpeningJson.Int(data, "m_PostInfinity", 2)
            };
        }

        private void Update()
        {
            for (int index = motions.Count - 1; index >= 0; index--)
            {
                Motion motion = motions[index];
                if (Time.time < motion.Start) continue;
                if (!motion.Started)
                {
                    motion.Ground = GroundDissolve.Instance;
                    if (motion.Ground != null) motion.Previous = motion.Ground.Dissolve;
                    motion.Started = true;
                }
                if (motion.Ground == null) { motions.RemoveAt(index); continue; }
                float elapsed = (Time.time - motion.Start) * motion.Speed;
                motion.Ground.Dissolve = motion.Curve.Evaluate(elapsed / motion.Duration);
                if (elapsed < motion.Duration) continue;
                motion.Ground.Dissolve = motion.Previous;
                motions.RemoveAt(index);
            }
        }

        private void OnDestroy()
        {
            foreach (Motion motion in motions)
                if (motion.Started && motion.Ground != null) motion.Ground.Dissolve = motion.Previous;
            motions.Clear();
        }
    }
}
