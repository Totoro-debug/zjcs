using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void RestoreMiniMapCharacter(OfficialMap11UiData data, Dictionary<string, Sprite> sprites)
        {
            RestoreMiniMapPrefab("MinimapCharacter", data.minimapCharacter, data, sprites);
            foreach (var prefab in data.minimapMarkPrefabs)
                RestoreMiniMapPrefab(prefab.key, prefab.nodes, data, sprites);
        }

        private static void RestoreMiniMapPrefab(string key, OfficialMap11SceneNode[] sourceNodes,
            OfficialMap11UiData data, Dictionary<string, Sprite> sprites)
        {
            var nodes = new List<GameObject>();
            var paths = new Dictionary<string, Transform>();
            try
            {
                foreach (var source in sourceNodes)
                {
                    var node = new GameObject(source.name);
                    nodes.Add(node);
                    paths.Add(source.path, node.transform);
                    if (source.parentIndex >= 0) node.transform.SetParent(nodes[source.parentIndex].transform, false);
                    node.transform.localPosition = source.position;
                    node.transform.localRotation = source.rotation;
                    node.transform.localScale = source.scale;
                    node.SetActive(source.active);
                    if (!string.IsNullOrEmpty(source.sprite))
                    {
                        var renderer = node.AddComponent<SpriteRenderer>();
                        renderer.sprite = sprites[source.sprite];
                        renderer.color = source.color;
                        renderer.sortingOrder = source.sortingOrder;
                        renderer.flipX = source.flipX;
                        renderer.flipY = source.flipY;
                    }
                }
                foreach (var component in data.components.Where(value => value.panel == key && value.type == "NativeAnimation"))
                    RestoreNativeAnimation(component, path => paths[path]);
                // Its source camera is supplied by the shared minimap renderer;
                // retain the native camera node and its transform in the prefab.
                if (PrefabUtility.SaveAsPrefabAsset(nodes[0], ResourceRoot + "/" + key + ".prefab") == null)
                    throw new InvalidOperationException("Failed to restore native minimap character.");
            }
            finally { if (nodes.Count > 0) UnityEngine.Object.DestroyImmediate(nodes[0]); }
        }
    }
}
