using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void ImportTeamTargetSprites()
        {
            var data = JsonUtility.FromJson<TeamTargetGeometryData>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Official/World/Map/12/Source/team-target-sprite-geometry.json").text);
            foreach (var row in data.sprites)
            {
                if (!row.path.StartsWith("Assets/Official/World/Map/12/UI/Team/Sprites/", StringComparison.Ordinal)
                    || !row.path.EndsWith(".sprite.asset", StringComparison.Ordinal) || !File.Exists(row.path))
                    throw new InvalidDataException("Import the verified original native Sprite outputs first.");
                AssetDatabase.ImportAsset(row.path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            foreach (string name in new[] { "TeamPanelTarget", "TeamPanelTargetItem", "TeamPanelTargetSubItem" })
                AssetDatabase.ImportAsset("Assets/Game/Resources/Map12/Team/" + name + ".prefab",
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            VerifyTeamTargetSpriteGeometry();
            Debug.Log("Original native target Sprite geometry imported and checked: " + data.sprites.Length);
        }
    }
}
