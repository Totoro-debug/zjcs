using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // APK TMPColorGradient: metadata 296516..296523, ARM64 0x1121d7b0..0x1121df90.
    // Preserve the original per-line range, rotation and four-vertex iteration.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPColorGradient : MonoBehaviour
    {
        public Gradient Gradient = new Gradient();
        public float Angle = 45;
        private TMP_Text text;
        private Matrix4x4 rotated;
        private TMP_Text Text => text != null ? text : text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            if (Text != null) Text.OnPreRenderText += OnPreRenderText;
            UpdateMesh();
        }
        private void OnDisable()
        {
            if (Text != null) Text.OnPreRenderText -= OnPreRenderText;
            UpdateMesh();
        }
        public void SetGradientColor(Gradient value)
        {
            Gradient.SetKeys(value.colorKeys, value.alphaKeys);
            UpdateMesh();
        }
        private void UpdateMesh()
        {
            rotated = Matrix4x4.Rotate(Quaternion.Euler(0, 0, Angle));
            if (Text == null) return;
            Text.renderMode = TextRenderFlags.Render;
            Text.ForceMeshUpdate();
        }
        private void OnPreRenderText(TMP_TextInfo info)
        {
            for (int lineIndex = 0; lineIndex < info.lineCount; lineIndex++)
            {
                TMP_LineInfo line = info.lineInfo[lineIndex];
                float min = line.lineExtents.min.x, max = line.lineExtents.max.x;
                for (int index = line.firstCharacterIndex; index <= line.lastCharacterIndex; index++)
                {
                    TMP_CharacterInfo character = info.characterInfo[index];
                    TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int vertex = character.vertexIndex + corner;
                        float x = rotated.MultiplyPoint3x4(mesh.vertices[vertex]).x;
                        mesh.colors32[vertex] = Gradient.Evaluate(Mathf.InverseLerp(min, max, x));
                    }
                }
            }
        }
    }
}
