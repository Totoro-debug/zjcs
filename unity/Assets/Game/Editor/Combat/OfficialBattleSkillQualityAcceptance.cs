using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSkillQualityAcceptance
    {
        [MenuItem("Zjcs/Combat/Validate Original Skill Qualities")]
        public static void Run()
        {
            OfficialBattleSkillQualityCatalog catalog = OfficialBattleSkillQualityCatalog.Load();
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("qualities");
            Require(entries.arraySize == 6, "Six original quality Sprites were not bound.");
            var imageObject = new GameObject("SkillQualityGeometryFixture", typeof(RectTransform), typeof(Image));
            try
            {
                var image = imageObject.GetComponent<Image>();
                image.rectTransform.sizeDelta = new Vector2(184, 184);
                var geometry = imageObject.AddComponent<OfficialBattleSkillQualityGeometry>();
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    var binding = new BattleSkillQualityBinding
                    {
                        quality = entry.FindPropertyRelative("quality").intValue,
                        sprite = (Sprite)entry.FindPropertyRelative("sprite").objectReferenceValue,
                        sourceSize = entry.FindPropertyRelative("sourceSize").vector2Value,
                        trimOffset = entry.FindPropertyRelative("trimOffset").vector2Value,
                        trimSize = entry.FindPropertyRelative("trimSize").vector2Value,
                        uvMin = entry.FindPropertyRelative("uvMin").vector2Value,
                        uvMax = entry.FindPropertyRelative("uvMax").vector2Value
                    };
                    Require(binding.quality == index + 1 && binding.sprite == catalog.FindQuality(index + 1),
                        "Original quality ordering differs.");
                    Require(binding.sourceSize == new Vector2(184, 184) && binding.sprite.pixelsPerUnit == 100,
                        "The original untrimmed size or PPU was lost.");
                    Require(binding.sprite.texture.format == TextureFormat.RGBA32
                        && UnityEngine.Sprites.DataUtility.GetOuterUV(binding.sprite) == new Vector4(0, 0, 1, 1),
                        "Native quality import added compression or automatic Sprite trimming.");
                    image.sprite = binding.sprite;
                    geometry.Bind(binding);
                    using (var vertices = new VertexHelper())
                    {
                        UIVertex vertex = UIVertex.simpleVert;
                        vertex.color = new Color32(31, 73, 127, 211);
                        vertices.AddVert(vertex);
                        geometry.ModifyMesh(vertices);
                        Require(vertices.currentVertCount == 4 && vertices.currentIndexCount == 6,
                            "The original Simple Image quad differs.");
                        vertices.PopulateUIVertex(ref vertex, 0);
                        Require(Vector2.Distance(vertex.position, new Vector2(-92, -92) + binding.trimOffset) < .001f
                            && Vector2.Distance(vertex.uv0, binding.uvMin) < .00001f
                            && vertex.color.Equals(new Color32(31, 73, 127, 211)),
                            "Original trim, fractional UV or graphic color was lost.");
                        vertices.PopulateUIVertex(ref vertex, 2);
                        Require(Vector2.Distance(vertex.position, new Vector2(-92, -92) + binding.trimOffset + binding.trimSize) < .001f
                            && Vector2.Distance(vertex.uv0, binding.uvMax) < .00001f,
                            "Original quality upper bounds differ: quality=" + binding.quality + " position=" + vertex.position.ToString("R")
                            + " uv=" + vertex.uv0.ToString("R") + " expected=" + binding.uvMax.ToString("R")
                            + " spriteRect=" + binding.sprite.rect + " texture=" + binding.sprite.texture.width
                            + "x" + binding.sprite.texture.height + " outer=" + UnityEngine.Sprites.DataUtility.GetOuterUV(binding.sprite).ToString("R"));
                        if (binding.quality == 2)
                            Require(Mathf.Abs(vertex.position.x - 90.92387f) < .001f
                                && Mathf.Abs(vertex.position.y - 91.92387f) < .001f,
                                "Pinned APK Blue Sprite golden coordinates differ.");
                    }
                }
                foreach (var sample in new[] { (10000, 2), (10001, 3), (10003, 4), (14525, 5), (39501, 6) })
                {
                    catalog.Apply(image, sample.Item1);
                    Require(image.sprite == catalog.FindQuality(sample.Item2), "Official item quality changed: " + sample.Item1);
                }
                bool rejected = false;
                try { catalog.Find(16019); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Quality Rainbow was silently mapped to an unconfirmed substitute.");
                Debug.Log("Original skill qualities passed: six native Sprites, five item-quality mappings, original 184px padding/fractional UVs/color, and unsupported Rainbow remains explicit.");
            }
            finally { UnityEngine.Object.DestroyImmediate(imageObject); }
        }

        public static void VerifyActual(GameObject root, Zjcs.Combat.Official.FightBattleCheckpoint checkpoint)
        {
            OfficialBattleSkillQualityCatalog catalog = OfficialBattleSkillQualityCatalog.Load();
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            foreach (var skill in player.Skills)
            {
                WorldGameSkillIcon card = root.GetComponentsInChildren<WorldGameSkillIcon>(true)
                    .Single(value => value.SkillIndex == skill.Slot);
                Require(card.QualityImg.sprite == catalog.Find(skill.ItemId)
                    && card.QualityImg.GetComponent<OfficialBattleSkillQualityGeometry>() != null,
                    "The actual battle skill did not bind its original item quality and geometry.");
            }
            Debug.Log("Original skill quality Play Mode passed: actual skill item quality and original geometry are bound.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
