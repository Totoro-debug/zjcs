using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class UIMeshUtils
{
    public sealed class Property
    {
        public string Name;
        public int NameId;
        public ShaderPropertyType Type;
    }

    private static readonly Dictionary<Shader, List<Property>> shaderProperties =
        new Dictionary<Shader, List<Property>>();

    private static List<Property> CollectProperties(Shader shader)
    {
        if (shaderProperties.TryGetValue(shader, out List<Property> cached))
        {
            Debug.LogError("shader:" + shader.name + " Already Exist !!!");
            return cached;
        }

        int propertyCount = shader.GetPropertyCount();
        var properties = new List<Property>(propertyCount);
        for (int index = 0; index < propertyCount; index++)
        {
            properties.Add(new Property
            {
                Name = shader.GetPropertyName(index),
                NameId = shader.GetPropertyNameId(index),
                Type = shader.GetPropertyType(index)
            });
        }
        shaderProperties.Add(shader, properties);
        return properties;
    }

    public static void ApplyMaterialPropertyBlock(Material material, MaterialPropertyBlock propertyBlock)
    {
        if (material == null || propertyBlock == null)
        {
            Debug.LogError("ApplyMaterialPropertyBlock Exception!");
            return;
        }

        Shader shader = material.shader;
        if (!shaderProperties.TryGetValue(shader, out List<Property> properties))
            properties = CollectProperties(shader);

        foreach (Property property in properties)
        {
            int nameId = property.NameId;
            switch ((int)property.Type)
            {
                case 0:
                {
                    Color value = propertyBlock.GetColor(nameId);
                    if (value != default) material.SetColor(nameId, value);
                    break;
                }
                case 1:
                {
                    Vector4 value = propertyBlock.GetVector(nameId);
                    if (value != default) material.SetVector(nameId, value);
                    break;
                }
                case 2:
                case 3:
                {
                    float value = propertyBlock.GetFloat(nameId);
                    if (value != 0f) material.SetFloat(nameId, value);
                    break;
                }
                case 4:
                {
                    Texture value = propertyBlock.GetTexture(nameId);
                    if (value != null) material.SetTexture(nameId, value);
                    break;
                }
                case 5:
                {
                    int value = propertyBlock.GetInt(nameId);
                    if (value != 0) material.SetInt(nameId, value);
                    break;
                }
            }
        }
    }
}
