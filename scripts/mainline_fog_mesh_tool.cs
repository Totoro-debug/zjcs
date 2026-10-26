using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    // Only the data container accessed by the unmodified URP LightUtility.
    // Mesh construction, triangulation and falloff extrusion use Unity's source.
    internal sealed class Light2D
    {
        internal enum LightType { Parametric, Freeform, Sprite, Point, Global }
        internal Mesh lightMesh = new Mesh();
        internal LightUtility.LightMeshVertex[] vertices;
        internal ushort[] indices;
    }
}

namespace Zjcs.FogTool
{
    [Serializable] public sealed class Region { public int region; public Vector3[] shape; public float falloff; }
    [Serializable] public sealed class Regions { public Region[] regions; }
    public static class FogMeshGenerator
    {
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-fog-input");
            var input = JsonUtility.FromJson<Regions>(File.ReadAllText(args[index + 1]));
            Directory.CreateDirectory("Assets/FogGenerated");
            foreach (Region region in input.regions)
            {
                var light = new Light2D();
                LightUtility.GenerateShapeMesh(light, region.shape, region.falloff);
                if (light.lightMesh.vertexCount == 0) throw new InvalidDataException("Empty native region " + region.region);
                light.lightMesh.name = "LightRegion" + region.region;
                string path = "Assets/FogGenerated/" + light.lightMesh.name + ".asset";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(light.lightMesh, path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Native region light meshes generated: " + input.regions.Length);
            EditorApplication.Exit(0);
        }
    }
}
