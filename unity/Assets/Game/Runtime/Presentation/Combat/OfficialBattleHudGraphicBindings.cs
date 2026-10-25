using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Official Graphic/CanvasGroup fields omitted by the earlier prefab-tree export.</summary>
    internal static class OfficialBattleHudGraphicBindings
    {
        [Serializable] private sealed class Payload { public string manifest; public Binding[] bindings; }
        [Serializable] private sealed class Manifest { public string manifest, output_sha256; }
        [Serializable] private sealed class Binding
        {
            public string prefab, path;
            public bool canvas_group, interactable, blocks_raycasts, raycast_target, maskable;
            public bool initial_visibility, active;
        }

        public static void Apply(BattleHudCatalogData catalog)
        {
            TextAsset text = Resources.Load<TextAsset>("Combat/battle-hud-graphic-bindings");
            TextAsset source = Resources.Load<TextAsset>("Combat/battle-hud-graphic-bindings-source-manifest");
            if (text == null || source == null)
                throw new InvalidOperationException("Official battle HUD input bindings are missing.");
            Manifest manifest = JsonUtility.FromJson<Manifest>(source.text);
            string hash;
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.text)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            Payload payload = JsonUtility.FromJson<Payload>(text.text);
            if (manifest == null || manifest.manifest != "854_156383" || manifest.output_sha256 != hash
                || payload == null || payload.manifest != manifest.manifest || payload.bindings == null)
                throw new InvalidOperationException("Official battle HUD input binding hashes do not match.");
            var nodes = new Dictionary<string, BattleHudNode>(StringComparer.Ordinal);
            foreach (BattleHudPrefab prefab in catalog.prefabs)
                foreach (BattleHudNode node in prefab.nodes)
                    nodes.Add(prefab.key + ":" + node.path, node);
            foreach (Binding binding in payload.bindings)
            {
                if (!nodes.TryGetValue(binding.prefab + ":" + binding.path, out BattleHudNode node))
                    throw new InvalidOperationException("Official HUD input target is missing: " + binding.path);
                if (binding.initial_visibility)
                {
                    // ChatQuickMsgEntryPanel.OnEnable and GameMatchMiniPanel.RefreshUI:
                    // no quick-message cooldown or live matchmaking in this local session.
                    node.active = binding.active;
                }
                else if (binding.canvas_group)
                {
                    node.has_canvas_group_flags = true;
                    node.canvas_group_interactable = binding.interactable;
                    node.canvas_group_blocks_raycasts = binding.blocks_raycasts;
                }
                else
                {
                    node.image_graphic_flags = true;
                    node.image_raycast_target = binding.raycast_target;
                    node.image_maskable = binding.maskable;
                }
            }
        }
    }
}
