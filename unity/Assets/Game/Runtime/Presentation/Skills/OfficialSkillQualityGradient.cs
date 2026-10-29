using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        [NonSerialized] private Gradient nativeGradient;

        public Color32 Evaluate(float t)
        {
            if (nativeGradient == null)
            {
                var colorKeys = new GradientColorKey[colors.Length];
                var alphaKeys = new GradientAlphaKey[alphas.Length];
                for (int i = 0; i < colors.Length; i++) colorKeys[i] = new GradientColorKey(colors[i].color, colors[i].time);
                for (int i = 0; i < alphas.Length; i++) alphaKeys[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);
                nativeGradient = new Gradient { mode = (GradientMode)mode };
                nativeGradient.SetKeys(colorKeys, alphaKeys);
            }
            return nativeGradient.Evaluate(t);
        }

        public Color32 EvaluateVertex(Vector3 position, float minimum, float maximum, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            float x = position.x * Mathf.Cos(radians) - position.y * Mathf.Sin(radians);
            return Evaluate(Mathf.InverseLerp(minimum, maximum, x));
        }

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
                float x = vertex.position.x;
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
                Color32 palette = data.Evaluate(t);
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


    }
    // TMPColorGradient.OnPreRenderText: original line extents and rotated vertices
    // and palette replacement, applied directly to TMP's native material meshes.
    [AddComponentMenu("")]
    public sealed class OfficialSkillQualityTmpGradient : MonoBehaviour
    {
        private TMP_Text target;
        private OfficialSkillQualityGradientData data;
        private float angle;

        public void Configure(TMP_Text text, OfficialSkillQualityGradientData palette, float componentAngle)
        {
            if (target != null) target.OnPreRenderText -= Apply;
            target = text;
            data = palette;
            angle = componentAngle;
            if (target == null) return;
            target.OnPreRenderText += Apply;
            target.SetVerticesDirty();
        }

        private void OnDestroy()
        {
            if (target != null) target.OnPreRenderText -= Apply;
        }

        private void Apply(TMP_TextInfo info)
        {
            if (data == null) return;
            for (int line = 0; line < info.lineCount; line++)
            {
                TMP_LineInfo row = info.lineInfo[line];
                // The original native callback reads unrotated TMP_LineInfo.lineExtents
                // before rotating each vertex around the RectTransform origin.
                float minimum = row.lineExtents.min.x, maximum = row.lineExtents.max.x;
                for (int i = row.firstCharacterIndex; i <= row.lastCharacterIndex && i < info.characterCount; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                    for (int v = 0; v < 4; v++)
                    {
                        int index = character.vertexIndex + v;
                        Vector3 position = mesh.vertices[index];
                        mesh.colors32[index] = data.EvaluateVertex(position, minimum, maximum, angle);
                    }
                }
            }
        }
    }

}
