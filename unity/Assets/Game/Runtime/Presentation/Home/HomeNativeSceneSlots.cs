using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    /// <summary>Official scene slots, separate from prefab-internal transforms.</summary>
    internal sealed class HomeNativeSceneSlots
    {
        [Serializable]
        private sealed class Data { public Node root; }

        [Serializable]
        private sealed class Node
        {
            public string name;
            public float[] position;
            public float[] rotation;
            public float[] scale;
            public Node[] children;
        }

        private readonly Dictionary<string, Transform> slots = new Dictionary<string, Transform>(StringComparer.Ordinal);

        internal static HomeNativeSceneSlots Create(Transform parent, string resourcePath = "Home/native-scene-slots")
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (!asset) throw new InvalidOperationException("The official outdoor furniture slots are missing.");
            var data = JsonConvert.DeserializeObject<Data>(asset.text);
            if (data?.root == null) throw new InvalidOperationException("The official outdoor slot hierarchy is invalid.");
            var catalog = new HomeNativeSceneSlots();
            catalog.Build(data.root, parent);
            return catalog;
        }

        internal Transform Find(string name)
            => slots.TryGetValue(name, out var slot) ? slot
                : throw new InvalidOperationException("The official outdoor furniture slot is missing: " + name);

        private void Build(Node node, Transform parent)
        {
            if (string.IsNullOrEmpty(node.name) || node.position?.Length != 3
                || node.rotation?.Length != 4 || node.scale?.Length != 3 || slots.ContainsKey(node.name))
                throw new InvalidOperationException("The official outdoor furniture slot is invalid or ambiguous: " + node.name);
            var slot = new GameObject(node.name).transform;
            slot.SetParent(parent, false);
            slot.localPosition = new Vector3(node.position[0], node.position[1], node.position[2]);
            slot.localRotation = new Quaternion(node.rotation[0], node.rotation[1], node.rotation[2], node.rotation[3]);
            slot.localScale = new Vector3(node.scale[0], node.scale[1], node.scale[2]);
            slots.Add(node.name, slot);
            foreach (Node child in node.children ?? Array.Empty<Node>()) Build(child, slot);
        }
    }
}
