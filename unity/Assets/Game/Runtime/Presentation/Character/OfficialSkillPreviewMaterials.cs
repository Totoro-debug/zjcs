using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Character
{
    internal sealed class OfficialSkillPreviewMaterials : MonoBehaviour
    {
        private readonly List<Material> owned = new List<Material>();

        internal static void Bind(GameObject model, Shader shader)
        {
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("The original character preview shader is not available.");
            if (model.GetComponent<OfficialSkillPreviewMaterials>() != null) return;
            var owner = model.AddComponent<OfficialSkillPreviewMaterials>();
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader.name != "Zjcs/CharacterCreation/OfficialSpineDye") continue;
                    if (!copies.TryGetValue(source, out Material material))
                    {
                        material = new Material(shader) { name = source.name + " original preview" };
                        material.CopyPropertiesFromMaterial(source);
                        copies.Add(source, material);
                        owner.owned.Add(material);
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in owned)
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                }
        }
    }
}
