using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class CharacterCreationDyeMapBinding : MonoBehaviour
    {
        [SerializeField] private int skinType;
        [SerializeField] private Texture2D dyeMask;

        public int SkinType => skinType;
        public Texture2D DyeMask => dyeMask;

        public void Bind(int type, Texture2D mask)
        {
            skinType = type;
            dyeMask = mask;
        }

        // Original Spine attachments share the atlas page material, so dyeing
        // one skin also colors other attachments on that same page.
        public static void ApplyPageDyes(IEnumerable<Renderer> source,
            IReadOnlyDictionary<int, Vector4[]> changes)
        {
            // Geometry draw renderers inherit their source attachment's block;
            // unrelated components on the model must keep their own properties.
            Renderer[] renderers = source.Where(r => r && r.sharedMaterial
                && r.name.StartsWith("__skin__", StringComparison.Ordinal)).ToArray();
            var pages = new Dictionary<Material, (Texture2D mask, Vector4[] channels)>();
            if (changes != null)
                foreach (var change in changes)
                    foreach (Renderer renderer in renderers)
                    {
                        var binding = renderer.GetComponent<CharacterCreationDyeMapBinding>();
                        if (!renderer.gameObject.activeSelf || !binding || !binding.DyeMask
                            || binding.SkinType != change.Key) continue;
                        Material page = renderer.sharedMaterial;
                        if (!pages.TryGetValue(page, out var data))
                            data = (binding.DyeMask, new Vector4[3]);
                        else if (data.mask != binding.DyeMask)
                            throw new InvalidOperationException("Conflicting official dye masks for atlas page " + page.name);
                        for (int i = 0; i < data.channels.Length && i < change.Value.Length; i++)
                            if (change.Value[i].w != 0f) data.channels[i] = change.Value[i];
                        pages[page] = data;
                    }
            var properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in renderers)
            {
                if (!pages.TryGetValue(renderer.sharedMaterial, out var data))
                    data = (Texture2D.blackTexture, new Vector4[3]);
                renderer.GetPropertyBlock(properties);
                properties.SetTexture("_DyeTex", data.mask);
                properties.SetVector("_DyeHsv1", data.channels[0]);
                properties.SetVector("_DyeHsv2", data.channels[1]);
                properties.SetVector("_DyeHsv3", data.channels[2]);
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }
    }
}
