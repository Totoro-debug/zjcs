using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Skills;

namespace Zjcs.Editor.Skills
{
    public static class SkillTextValidation
    {
        [Serializable] private sealed class Reference
        {
            public Vertex[] cases;
            public Sample[] samples;
        }
        [Serializable] private sealed class Sample { public float time; public int[] color32; }
        [Serializable] private sealed class Vertex
        {
            public float angle;
            public float[] line;
            public float[] point;
            public int[] color32;
        }

        public static void Run()
        {
            var catalog = OfficialSkillAssetCatalog.LoadDefault();
            ValidateFontLifetime(catalog.NativeUiFont);
            string root = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("ZJCS_REPOSITORY_ROOT is required.");
            var reference = JsonUtility.FromJson<Reference>(File.ReadAllText(Path.Combine(root, "scripts/fixtures/skill_quality_native.json")));
            var gradient = catalog.LoadQualityGradient();
            foreach (Sample sample in reference.samples) AssertColor(gradient.Evaluate(sample.time), sample.color32);
            foreach (Vertex vertex in reference.cases)
                AssertColor(gradient.EvaluateVertex(new Vector3(vertex.point[0], vertex.point[1]),
                    vertex.line[0], vertex.line[1], vertex.angle), vertex.color32);
            Debug.Log("Skill text validation passed: shared isolated font lifetime, original atlas preservation, "
                + reference.samples.Length + " native gradient samples and " + reference.cases.Length + " vertex cases.");
        }

        private static void ValidateFontLifetime(TMP_FontAsset source)
        {
            var type = typeof(CharacterSkillPanel).Assembly.GetType("Zjcs.Presentation.Character.OfficialSkillFontLease", true);
            var method = type.GetMethod("Acquire");
            var objects = new GameObject[128];
            Texture2D sourceAtlas = source.atlasTexture;
            byte[] sourceBytes = sourceAtlas.GetRawTextureData();
            int glyphCount = source.glyphTable.Count;
            TMP_FontAsset font = null;
            Texture2D atlas = null;
            Material material = null;
            try
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i] = new GameObject("SkillFontLeaseValidation");
                    var acquired = (TMP_FontAsset)method.Invoke(objects[i].AddComponent(type), new object[] { source });
                    if (font == null) font = acquired;
                    if (font != acquired || font == source) throw new InvalidOperationException("Labels must share one isolated runtime font.");
                }
                atlas = font.atlasTexture;
                material = font.material;
                if (atlas == sourceAtlas || material == source.material || material.mainTexture != atlas)
                    throw new InvalidOperationException("Runtime font references an original atlas or material.");
                font.TryAddCharacters("技能验证龍劍");
                // Edit Mode does not dispatch MonoBehaviour.OnDestroy for components
                // that never entered Play Mode; invoke that lifecycle explicitly here.
                for (int i = 0; i < objects.Length - 1; i++) ReleaseLease(objects[i], type);
                if (font == null || atlas == null) throw new InvalidOperationException("Font was released while a label still uses it.");
                ReleaseLease(objects[objects.Length - 1], type);
                if (font != null || atlas != null || material != null) throw new InvalidOperationException("Last font lease did not release its native resources.");
                if (source.atlasTexture != sourceAtlas || source.glyphTable.Count != glyphCount
                    || !sourceBytes.SequenceEqual(sourceAtlas.GetRawTextureData()))
                    throw new InvalidOperationException("Runtime glyph generation changed the original font asset.");
            }
            finally
            {
                foreach (GameObject value in objects) if (value != null) ReleaseLease(value, type);
            }
        }

        private static void ReleaseLease(GameObject value, Type type)
        {
            type.GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(value.GetComponent(type), null);
            UnityEngine.Object.DestroyImmediate(value);
        }

        private static void AssertColor(Color32 actual, int[] expected)
        {
            if (actual.r != expected[0] || actual.g != expected[1] || actual.b != expected[2] || actual.a != expected[3])
                throw new InvalidOperationException("Native gradient mismatch: " + actual + " expected " + string.Join(",", expected));
        }
    }
}
