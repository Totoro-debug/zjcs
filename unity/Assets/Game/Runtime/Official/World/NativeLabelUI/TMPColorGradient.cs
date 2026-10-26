// Reconstructed from all eight pinned original TMPColorGradient AOT methods.
// Original fields/method evidence: Source/native-text-gradient.json.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.World.NativeLabelUI
{
    [RequireComponent(typeof(TMP_Text))]
    public class TMPColorGradient : MonoBehaviour
    {
        public Gradient Gradient = new Gradient();
        public float Angle = 45;
        private TMP_Text mTextMeshPro;
        private Matrix4x4 mRotatedMatrix4X4;
        private TMP_Text TextMeshPro
        {
            get
            {
                if (mTextMeshPro == null) mTextMeshPro = GetComponent<TMP_Text>();
                return mTextMeshPro;
            }
        }
        private void OnEnable()
        {
            if (TextMeshPro) TextMeshPro.OnPreRenderText += OnPreRenderText;
            UpdateMesh();
        }
        private void OnDisable()
        {
            if (TextMeshPro) TextMeshPro.OnPreRenderText -= OnPreRenderText;
            UpdateMesh();
        }
        public void SetColors(List<Color> colors)
        {
            if (TextMeshPro == null) return;
            int count = colors.Count;
            var colorKeys = new GradientColorKey[count];
            var alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) };
            for (int i = 0; i < count; i++)
                colorKeys[i] = new GradientColorKey(colors[i], (float)i / (count - 1));
            Gradient.SetKeys(colorKeys, alphaKeys);
            UpdateMesh();
        }
        public void SetGradientColor(Gradient gradient)
        {
            Gradient.SetKeys(gradient.colorKeys, gradient.alphaKeys);
            UpdateMesh();
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
                        float x = mRotatedMatrix4X4.MultiplyPoint3x4(mesh.vertices[vertex]).x;
                        mesh.colors32[vertex] = Gradient.Evaluate(Mathf.InverseLerp(min, max, x));
                    }
                }
            }
        }
        private void UpdateMesh()
        {
            mRotatedMatrix4X4 = Matrix4x4.Rotate(Quaternion.Euler(0, 0, Angle));
            TextMeshPro.renderMode = (TextRenderFlags)255;
            TextMeshPro.ForceMeshUpdate(false, false);
        }
    }
}
