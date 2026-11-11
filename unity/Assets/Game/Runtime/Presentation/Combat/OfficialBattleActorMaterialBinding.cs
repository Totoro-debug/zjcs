using System;
using UnityEngine;
using Zjcs.Official.BattleMaterialPlayables;

namespace Zjcs.Presentation.Combat
{
    // The native skeleton owns its material instances. This restores the original
    // root interface and serialized references consumed by material playables.
    internal static class OfficialBattleActorMaterialBinding
    {
        [Serializable] private sealed class Catalog { public string manifest; public Actor[] actors; }
        [Serializable] private sealed class Actor
        {
            public int entityId;
            public bool enabled;
            public string renderer, appearance, appearanceLite;
        }
        private static Catalog catalog;

        public static MeshMaterialProperty Bind(Transform root, int entityId)
        {
            if (root.GetComponent<MeshMaterialProperty>()) return null;
            if (catalog == null)
            {
                var asset = Resources.Load<TextAsset>("Combat/actor-material-bindings");
                if (!asset) throw new InvalidOperationException("Original actor material bindings are missing.");
                catalog = JsonUtility.FromJson<Catalog>(asset.text);
                if (catalog.manifest != "884_156692") throw new InvalidOperationException("Original actor material version differs.");
            }
            var actor = Array.Find(catalog.actors, value => value.entityId == entityId);
            if (actor == null) return null;
            var rendererNode = root.Find(actor.renderer);
            // A controlled/non-native actor may not contain this original model.
            if (!rendererNode) return null;
            var renderer = rendererNode.GetComponent<MeshRenderer>();
            if (!renderer) throw new InvalidOperationException("Original actor MeshRenderer binding is missing: " + entityId);
            var property = root.gameObject.AddComponent<MeshMaterialProperty>();
            property.MeshRenderer = renderer;
            string appearancePath = actor.appearance ?? actor.appearanceLite;
            if (!string.IsNullOrEmpty(appearancePath))
            {
                var appearance = root.Find(appearancePath);
                if (!appearance) throw new InvalidOperationException("Original actor appearance binding is missing: " + entityId);
                property.SetAppearanceTarget(appearance.gameObject);
            }
            property.enabled = actor.enabled;
            return property;
        }
    }
}
