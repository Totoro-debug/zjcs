using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleNpcPortraitTools
    {
        [Serializable] private sealed class Source
        {
            public Binding[] bindings;
            public Image[] sprites;
            public int[] battleNpcIds, unresolvedBattleNpcIds;
        }
        [Serializable] private sealed class Binding { public int npcClassId; public string icon, sprite; }
        [Serializable] private sealed class Image
        {
            public string path, name, sha256, guid;
            public int width, height;
            public float ppu;
            public float[] pivot;
        }

        [MenuItem("Zjcs/Combat/Validate NPC Timeline Portraits")]
        public static void Validate()
        {
            var source = JsonUtility.FromJson<Source>(Resources.Load<TextAsset>("Combat/battle-npc-portraits").text);
            OfficialBattleNpcPortraitCatalog catalog = OfficialBattleNpcPortraitCatalog.Load();
            if (catalog.bindings.Length != source.bindings.Length)
                throw new InvalidDataException("NPC timeline portrait catalog omitted original NpcInfo rows.");
            var images = source.sprites.ToDictionary(i => i.path);
            for (int i = 0; i < source.bindings.Length; i++)
            {
                Binding row = source.bindings[i];
                OfficialBattleNpcPortraitBinding binding = catalog.bindings[i];
                if (binding.npcClassId != row.npcClassId || binding.icon != row.icon)
                    throw new InvalidDataException("NPC timeline portrait mapping changed: " + row.npcClassId);
                if (string.IsNullOrEmpty(row.sprite))
                {
                    if (binding.sprite != null) throw new InvalidDataException("Unresolved official NPC icon was substituted: " + row.npcClassId);
                    continue;
                }
                Image image = images[row.sprite];
                Sprite sprite = binding.sprite;
                var importer = AssetImporter.GetAtPath(row.sprite) as TextureImporter;
                using (SHA256 hash = SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(row.sprite))).Replace("-", "").ToLowerInvariant() != image.sha256)
                        throw new InvalidDataException("Official NPC portrait PNG changed: " + row.sprite);
                if (sprite == null || AssetDatabase.GetAssetPath(sprite) != row.sprite
                    || AssetDatabase.AssetPathToGUID(row.sprite) != image.guid || sprite.name != image.name
                    || sprite.rect != new Rect(0, 0, image.width, image.height) || sprite.pixelsPerUnit != image.ppu
                    || sprite.pivot != new Vector2(image.width * image.pivot[0], image.height * image.pivot[1])
                    || sprite.border != Vector4.zero || importer == null || !importer.sRGBTexture || importer.mipmapEnabled
                    || importer.filterMode != FilterMode.Bilinear || importer.wrapModeU != TextureWrapMode.Clamp
                    || importer.wrapModeV != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
                    throw new InvalidDataException("Original NPC Sprite geometry or texture settings changed: " + row.npcClassId);
                if (catalog.Find(row.npcClassId) != sprite)
                    throw new InvalidDataException("NPC class ID did not resolve its original icon: " + row.npcClassId);
            }
            int[] missing = source.battleNpcIds.Where(id => catalog.Find(id) == null).OrderBy(id => id).ToArray();
            if (!missing.SequenceEqual(source.unresolvedBattleNpcIds))
                throw new InvalidDataException("NPC battle portrait coverage differs from its extraction evidence.");
            Debug.Log("NPC timeline portraits validated: " + images.Count + " original Sprites; "
                + (source.battleNpcIds.Length - missing.Length) + "/" + source.battleNpcIds.Length
                + " stage/template NPCs; unresolved IDs: " + string.Join(",", missing));
        }
    }
}
