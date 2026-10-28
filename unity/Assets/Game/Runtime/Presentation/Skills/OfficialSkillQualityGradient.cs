using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Skills
{
    [Serializable]
    public sealed class OfficialSkillQualityGradientData
    {
        public int quality;
        public int theme;
        public int mode;
        public int colorSpace;
        public OfficialSkillGradientColorKey[] colors;
        public OfficialSkillGradientAlphaKey[] alphas;
        public OfficialSkillGradientComponent[] components;
        public Color[] normalColors;

        public static OfficialSkillQualityGradientData Parse(string json)
        {
            OfficialSkillQualityGradientData data = JsonUtility.FromJson<OfficialSkillQualityGradientData>(json);
            if (data == null || data.quality != 7 || data.colors == null || data.colors.Length != 4
                || data.alphas == null || data.alphas.Length != 2 || data.components == null
                || data.components.Length != 6)
                throw new InvalidOperationException("Official skill quality gradient payload is incomplete.");
            return data;
        }
    }

    [Serializable]
    public sealed class OfficialSkillGradientColorKey
    {
        public Color color;
        public float time;
    }

    [Serializable]
    public sealed class OfficialSkillGradientAlphaKey
    {
        public float alpha;
        public float time;
    }

    [Serializable]
    public sealed class OfficialSkillGradientComponent
    {
        public string path;
        public float angle;
    }

    [AddComponentMenu("")]
    public sealed class OfficialSkillQualityTextGradient : BaseMeshEffect
    {
        private static readonly List<UIVertex> Vertices = new List<UIVertex>();
        private readonly Dictionary<int, Vector2> lineBounds = new Dictionary<int, Vector2>();
        private OfficialSkillQualityGradientData data;
        private Text target;
        private float angle;

        public void Configure(Text text, OfficialSkillQualityGradientData palette, float componentAngle)
        {
            target = text;
            data = palette;
            angle = componentAngle;
            enabled = target != null && data != null;
            if (target != null) target.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || target == null || data == null || vertexHelper.currentVertCount == 0) return;
            Vertices.Clear();
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                UIVertex vertex = default;
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vertices.Add(vertex);
            }
            lineBounds.Clear();
            float radians = angle * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            for (int i = 0; i < Vertices.Count; i++)
            {
                UIVertex vertex = Vertices[i];
                int line = FindLine(vertex.position.y);
                float x = vertex.position.x * cosine - vertex.position.y * sine;
                if (!lineBounds.TryGetValue(line, out Vector2 bounds)) bounds = new Vector2(x, x);
                else bounds = new Vector2(Mathf.Min(bounds.x, x), Mathf.Max(bounds.y, x));
                lineBounds[line] = bounds;
            }
            for (int i = 0; i < Vertices.Count; i++)
            {
                UIVertex vertex = Vertices[i];
                int line = FindLine(vertex.position.y);
                float x = vertex.position.x * cosine - vertex.position.y * sine;
                Vector2 bounds = lineBounds[line];
                float t = bounds.y - bounds.x <= Mathf.Epsilon ? 0f
                    : Mathf.Clamp01((x - bounds.x) / (bounds.y - bounds.x));
                Color32 palette = Evaluate(t);
                vertex.color = palette;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }

        private int FindLine(float y)
        {
            int nearest = 0;
            float distance = float.MaxValue;
            IList<UILineInfo> lines = target.cachedTextGenerator.lines;
            for (int i = 0; i < lines.Count; i++)
            {
                float current = Mathf.Abs(y - lines[i].topY);
                if (current >= distance) continue;
                nearest = i;
                distance = current;
            }
            return nearest;
        }

        private Color32 Evaluate(float t)
        {
            OfficialSkillGradientColorKey[] colors = data.colors;
            OfficialSkillGradientAlphaKey[] alphas = data.alphas;
            int colorIndex = KeyInterval(colors, t, key => key.time);
            OfficialSkillGradientColorKey from = colors[colorIndex];
            OfficialSkillGradientColorKey to = colors[Mathf.Min(colorIndex + 1, colors.Length - 1)];
            float colorT = to.time <= from.time ? 0f : Mathf.Clamp01((t - from.time) / (to.time - from.time));
            Color color = Color.LerpUnclamped(from.color, to.color, colorT);
            int alphaIndex = KeyInterval(alphas, t, key => key.time);
            OfficialSkillGradientAlphaKey alphaFrom = alphas[alphaIndex];
            OfficialSkillGradientAlphaKey alphaTo = alphas[Mathf.Min(alphaIndex + 1, alphas.Length - 1)];
            float alphaT = alphaTo.time <= alphaFrom.time ? 0f
                : Mathf.Clamp01((t - alphaFrom.time) / (alphaTo.time - alphaFrom.time));
            color.a = Mathf.LerpUnclamped(alphaFrom.alpha, alphaTo.alpha, alphaT);
            return color;
        }

        private static int KeyInterval<T>(T[] keys, float t, Func<T, float> time)
        {
            for (int i = 0; i < keys.Length - 1; i++)
                if (t < time(keys[i + 1])) return i;
            return keys.Length - 2;
        }
    }
}
