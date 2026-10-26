using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleWorldTipsAssetTools
    {
        private const string Root="Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback/";

        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data=JsonUtility.FromJson<BattleWorldTipData>(File.ReadAllText("Assets/Game/Resources/Combat/world-tips.json"));
            var catalog=OfficialBattleHudCatalog.LoadDefault();
            foreach (var entry in data.spriteAssets)
            {
                string sourcePath=entry.sourcePath.Substring("unity/".Length);
                using (var hash=SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(sourcePath))).Replace("-","").ToLowerInvariant()!=entry.sourceSha256)
                        throw new InvalidDataException("Original world tip texture changed: "+sourcePath);
                var original=catalog.FindSprite(entry.sourceFile);
                if (original==null || original.rect.width!=entry.rect.width || original.rect.height!=entry.rect.height)
                    throw new InvalidDataException("Original world tip texture binding is missing or cropped.");
                string path=Root+entry.key+".asset";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite==null)
                {
                    sprite=Sprite.Create(original.texture,original.rect,entry.pivot.Value,entry.pixelsPerUnit,0,
                        SpriteMeshType.FullRect,new Vector4(entry.border[0],entry.border[1],entry.border[2],entry.border[3]));
                    sprite.name=entry.key;
                    AssetDatabase.CreateAsset(sprite,path);
                }
                sprite.OverrideGeometry(entry.vertices.Select(v=>v.Value).ToArray(),entry.triangles);
                EditorUtility.SetDirty(sprite);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Original world tip Sprite assets imported: "+data.spriteAssets.Length+" original textures, pivots, geometry and borders.");
            OfficialBattleWorldTipsAcceptance.Run();
        }
    }
}
