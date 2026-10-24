using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.CharacterCreation
{
    internal static class CharacterCreationUIMeshUtils
    {
        private sealed class Property
        {
            public int nameId;
            public ShaderPropertyType type;
        }

        private static readonly Dictionary<Shader, List<Property>> PropertiesByShader =
            new Dictionary<Shader, List<Property>>();

        public static void ApplyMaterialPropertyBlock(Material material, MaterialPropertyBlock block)
        {
            if (material == null || block == null) return;
            Shader shader = material.shader;
            if (!PropertiesByShader.TryGetValue(shader, out List<Property> properties))
            {
                properties = CollectProperties(shader);
                PropertiesByShader.Add(shader, properties);
            }

            foreach (Property property in properties)
            {
                switch (property.type)
                {
                    case ShaderPropertyType.Color:
                        Color color = block.GetColor(property.nameId);
                        if (color != default) material.SetColor(property.nameId, color);
                        break;
                    case ShaderPropertyType.Vector:
                        Vector4 vector = block.GetVector(property.nameId);
                        if (vector != default) material.SetVector(property.nameId, vector);
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        float value = block.GetFloat(property.nameId);
                        if (value != 0f) material.SetFloat(property.nameId, value);
                        break;
                    case ShaderPropertyType.Texture:
                        Texture texture = block.GetTexture(property.nameId);
                        if (texture != null) material.SetTexture(property.nameId, texture);
                        break;
                    case ShaderPropertyType.Int:
                        int integer = block.GetInt(property.nameId);
                        if (integer != 0) material.SetInt(property.nameId, integer);
                        break;
                }
            }
        }

        private static List<Property> CollectProperties(Shader shader)
        {
            var properties = new List<Property>(shader.GetPropertyCount());
            for (int i = 0; i < shader.GetPropertyCount(); i++)
                properties.Add(new Property
                {
                    nameId = shader.GetPropertyNameId(i),
                    type = shader.GetPropertyType(i)
                });
            return properties;
        }
    }
}
