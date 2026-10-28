using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Official.WorldUI
{
    // launch-source/UIMeshUtils.cs; preserve the original nonzero-only property writes.
    public static class WorldUiMeshUtils
    {
        public sealed class Property
        {
            public string Name;
            public int NameId;
            public ShaderPropertyType Type;
        }

        private static readonly Dictionary<Shader, List<Property>> mShader2Properties
            = new Dictionary<Shader, List<Property>>();

        private static List<Property> CollectProperties(Shader shader)
        {
            if (mShader2Properties.TryGetValue(shader, out List<Property> value))
            {
                Debug.LogError("shader:" + shader.name + " Already Exist !!!");
                return value;
            }
            int propertyCount = shader.GetPropertyCount();
            var list = new List<Property>(propertyCount);
            for (int i = 0; i < propertyCount; i++)
                list.Add(new Property { Name = shader.GetPropertyName(i), NameId = shader.GetPropertyNameId(i), Type = shader.GetPropertyType(i) });
            mShader2Properties.Add(shader, list);
            return list;
        }

        public static void ApplyMaterialPropertyBlock(Material material, MaterialPropertyBlock mpb)
        {
            if (material == null || mpb == null)
            {
                Debug.LogError("ApplyMaterialPropertyBlock Exception!");
                return;
            }
            Shader shader = material.shader;
            if (!mShader2Properties.TryGetValue(shader, out List<Property> value)) value = CollectProperties(shader);
            foreach (Property item in value)
            {
                int nameId = item.NameId;
                switch (item.Type)
                {
                    case ShaderPropertyType.Color:
                        Color color = mpb.GetColor(nameId);
                        if (color != default(Color)) material.SetColor(nameId, color);
                        break;
                    case ShaderPropertyType.Vector:
                        Vector4 vector = mpb.GetVector(nameId);
                        if (vector != default(Vector4)) material.SetVector(nameId, vector);
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        float number = mpb.GetFloat(nameId);
                        if (number != 0f) material.SetFloat(nameId, number);
                        break;
                    case ShaderPropertyType.Texture:
                        Texture texture = mpb.GetTexture(nameId);
                        if (texture != null) material.SetTexture(nameId, texture);
                        break;
                    case ShaderPropertyType.Int:
                        int integer = mpb.GetInt(nameId);
                        if (integer != 0) material.SetInt(nameId, integer);
                        break;
                }
            }
        }
    }
}
