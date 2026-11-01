using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

public static class PartnerCardBindingValidation
{
    [Serializable] private sealed class GraphBinding { public string guid, name, type; }
    [Serializable] private sealed class Receipt { public GraphBinding[] graph_bindings; }
    public static void Run()
    {
        try
        {
            const string root = "Assets/PartnerCard";
            var card = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/NpcPanelNpcItem.prefab");
            Require(card != null && card.GetComponent<NpcPanelNpcItem>() != null, "Original card root/controller");
            int objects = 0, missing = 0;
            foreach (var transform in card.GetComponentsInChildren<Transform>(true))
            {
                objects++;
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            }
            Require(missing == 0, "No missing prefab scripts");
            foreach (var component in card.GetComponentsInChildren<MonoBehaviour>(true))
                Require(!UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(component), "Managed references: " + component.GetType());
            var nodeGuids = AssetDatabase.FindAssets("t:TipDotGraphNode", new[] { root });
            Require(nodeGuids.Length > 0, "Original graph nodes imported");
            var runtimeTypes = new System.Collections.Generic.HashSet<Type>();
            foreach (string guid in nodeGuids)
            {
                var node = AssetDatabase.LoadAssetAtPath<TipDotGraphNode>(AssetDatabase.GUIDToAssetPath(guid));
                Require(node != null && node.TipDotNode != null, "Original graph runtime node: " + guid);
                Require(!UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(node), "Graph managed references: " + node.name);
                Require(node.graph != null, "Original graph owner: " + node.name);
                Require(node.GetPort("Parents") != null && node.GetPort("Childs") != null, "Original graph ports: " + node.name);
                foreach (var port in new[] { node.GetPort("Parents"), node.GetPort("Childs") })
                    foreach (var peer in port.GetConnections())
                        Require(peer.node is TipDotGraphNode, "Original connected port owner: " + node.name + "/" + port.fieldName + " -> " + peer.fieldName + ": " + peer.node?.GetType().FullName);
                var clone = node.TipDotNode.Clone();
                Require(clone != null && !ReferenceEquals(clone, node.TipDotNode) && clone.GetType() == node.TipDotNode.GetType(), "Original node clone: " + node.name);
                runtimeTypes.Add(clone.GetType());
            }
            Require(runtimeTypes.Count == 9, "All nine original runtime node types");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "partner-card-binding-source-manifest.json")));
            Require(receipt.graph_bindings.Length > 0, "Original graph identities recorded");
            foreach (var binding in receipt.graph_bindings)
            {
                var value = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(binding.guid));
                Require(value != null && value.name == binding.name, "Original graph name retained: " + binding.name);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:TipDotNodeGraph", new[] { root }))
            {
                var graph = AssetDatabase.LoadAssetAtPath<TipDotNodeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                int injected = 0;
                var instance = new NpcNodeInstanceInfo(8401);
                var factory = new TipDotNodeFactory(name => name == graph.name ? graph : null, node => injected++);
                var nodes = factory.ProductNodes(graph.name, instance);
                Require(nodes.Count == injected && nodes.Count == graph.nodes.Cast<TipDotGraphNode>().Count(value => !value.IsBan), "Original factory clones/injects all non-banned nodes");
                foreach (var node in nodes)
                {
                    Require(ReferenceEquals(node.NodeKey.InstanceInfo, instance) && node.NodeKey.GraphName == graph.name, "Original instance graph keys");
                    Require(node.Parents != null && node.Childs != null, "Original factory restores graph relations");
                    Require(node.PresetData != null, "Original factory initializes preset data");
                }
            }
            var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
            Require(texts.Length == 8, "Original eight text components");
            foreach (var text in texts)
            {
                Require(text.font != null && text.fontSharedMaterial != null, "Original font/material: " + text.name);
                Require(text.font.atlasTextures.Length > 0 && text.font.atlasTextures.All(value => value != null), "Original font atlases");
            }
            var sounds = AssetDatabase.GetDependencies(root + "/NpcPanelNpcItem.prefab", true)
                .Select(path => AssetDatabase.LoadAssetAtPath<AudioClip>(path)).Where(clip => clip != null).Distinct().ToArray();
            Require(sounds.Length == 2, "Both original Wwise events are bound");
            foreach (var clip in sounds)
            {
                Require(clip != null && clip.samples > 0 && clip.frequency == 48000, "Decoded original audio");
            }
            var images = card.GetComponentsInChildren<Image>(true);
            Require(images.Length == 31, "Original thirty-one images");
            foreach (var image in images.Where(value => value.sprite != null))
                Require(image.sprite.texture != null, "Original sprite atlas texture: " + image.name);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Require(material.shader != null && material.shader.isSupported, "Windows material shader: " + material.name);
            }
            Debug.Log("PARTNER_CARD_BINDING_PASSED: original objects=" + objects + ", texts=" + texts.Length + ", images=" + images.Length);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
