using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13LayerTools
    {
        [Serializable] private sealed class Layer { public string name; public uint uniqueID; }
        [Serializable] private sealed class Fields { public string[] tags, layers; public Layer[] m_SortingLayers; }
        [Serializable] private sealed class Source { public Fields fields; }

        public static void Validate()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-layers.json")).fields;
            for (int i = 0; i < source.layers.Length; i++)
                if (LayerMask.LayerToName(i) != source.layers[i]) throw new InvalidDataException("Original layer slot differs: " + i);
            if (LayerMask.NameToLayer("WaterMask") != 13) throw new InvalidDataException("Original water mask culling bit differs.");
            var actual = SortingLayer.layers;
            if (actual.Length != source.m_SortingLayers.Length) throw new InvalidDataException("Original sorting layer count differs.");
            for (int i = 0; i < actual.Length; i++)
                if (actual[i].name != source.m_SortingLayers[i].name || actual[i].id != unchecked((int)source.m_SortingLayers[i].uniqueID))
                    throw new InvalidDataException("Original sorting layer order/ID differs: " + i);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = settings.FindProperty("tags");
            if (tags.arraySize != source.tags.Length) throw new InvalidDataException("Original tags differ.");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue != source.tags[i]) throw new InvalidDataException("Original tag order differs.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/13/NativeScene/Partition/MapGround_13/MapGround_13_bare.prefab");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
                if (!actual.Any(layer => layer.id == renderer.sortingLayerID)) throw new InvalidDataException("Original renderer sorting ID is unresolved: " + renderer.name);
            Debug.Log("Map13 original layer configuration bound: 19 tags, 32 physical slots, 23 ordered sorting IDs; WaterMask bit 13 and " + renderers.Length + " native bare renderers verified.");
        }
    }
}
