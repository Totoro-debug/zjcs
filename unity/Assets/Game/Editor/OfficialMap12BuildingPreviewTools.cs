using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap12BuildingPreviewTools
    {
        private const string Source = "Assets/Official/World/Map/12/Source/BuildingPreview";
        private const string Destination = "Assets/Official/World/Map/12/BuildingPreview";
        [Serializable] private sealed class Payload { public string manifest; public int mapClassId; public Model[] models; }
        [Serializable] private sealed class Model
        {
            public string path, name;
            public float scale;
            public float[] positions, uv;
            public int[] triangles;
            public TextureData texture;
            public MaterialData material;
            public Color color;
            public bool raycast, maskable;
        }
        [Serializable] private sealed class TextureData
        {
            public string path;
            public int width, height, mips, filter, wrapU, wrapV, aniso;
            public float bias;
            public bool srgb;
        }
        [Serializable] private sealed class MaterialData { public FloatData[] floats; public ColorData[] colors; public string[] keywords; }
        [Serializable] private sealed class FloatData { public string name; public float value; }
        [Serializable] private sealed class ColorData { public string name; public float r, g, b, a; }

        public static void Import()
        {
            AssetDatabase.Refresh();
            Payload data = JsonUtility.FromJson<Payload>(File.ReadAllText(Source + "/preview.json"));
            if (data.manifest != "871_156598" || data.mapClassId != 12 || data.models.Length != 3)
                throw new InvalidDataException("Original lift dialog geometry is incomplete.");
            Shader shader = Shader.Find("Zjcs/Official/World/Map12/BuildingPreview");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException("Original building UI shader did not compile.");
            Directory.CreateDirectory(Destination);
            foreach (Model model in data.models)
            {
                string stem = Destination + "/" + model.name;
                var settings = model.texture;
                var texture = new Texture2D(settings.width, settings.height, TextureFormat.RGBA32, settings.mips, !settings.srgb)
                {
                    name = model.name, filterMode = (FilterMode)settings.filter, wrapModeU = (TextureWrapMode)settings.wrapU,
                    wrapModeV = (TextureWrapMode)settings.wrapV, anisoLevel = settings.aniso, mipMapBias = settings.bias
                };
                texture.LoadRawTextureData(File.ReadAllBytes(Path.GetFullPath("../" + settings.path)));
                texture.Apply(false, false);
                Save(texture, stem + ".texture.asset");
                var mesh = new Mesh { name = model.name + " original setup" };
                mesh.vertices = Enumerable.Range(0, model.positions.Length / 2).Select(i => new Vector3(model.positions[i * 2], model.positions[i * 2 + 1])).ToArray();
                mesh.uv = Enumerable.Range(0, model.uv.Length / 2).Select(i => new Vector2(model.uv[i * 2], model.uv[i * 2 + 1])).ToArray();
                mesh.triangles = model.triangles; mesh.RecalculateBounds(); Save(mesh, stem + ".mesh.asset");
                var material = new Material(shader) { name = model.name + " original UI", shaderKeywords = model.material.keywords };
                foreach (FloatData value in model.material.floats) if (material.HasProperty(value.name)) material.SetFloat(value.name, value.value);
                foreach (ColorData value in model.material.colors) if (material.HasProperty(value.name)) material.SetColor(value.name, new Color(value.r,value.g,value.b,value.a));
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + ".texture.asset");
                Save(material, stem + ".mat");
            }
            AssetDatabase.SaveAssets();
        }

        private static void Save(UnityEngine.Object value, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing == null) AssetDatabase.CreateAsset(value, path);
            else { EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(value); }
        }

        public static void Bind(Func<string, Transform> require)
        {
            Payload data = JsonUtility.FromJson<Payload>(File.ReadAllText(Source + "/preview.json"));
            foreach (Model model in data.models)
            {
                var graphic = require(model.path).gameObject.AddComponent<NativeMap12BuildingGraphic>();
                string stem = Destination + "/" + model.name;
                graphic.setupMesh = AssetDatabase.LoadAssetAtPath<Mesh>(stem + ".mesh.asset");
                graphic.atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(stem + ".texture.asset");
                graphic.material = AssetDatabase.LoadAssetAtPath<Material>(stem + ".mat");
                graphic.skeletonScale = model.scale; graphic.color = model.color;
                graphic.raycastTarget = model.raycast; graphic.maskable = model.maskable;
                if (graphic.setupMesh == null || graphic.atlas == null || graphic.material == null)
                    throw new InvalidDataException("Original lift dialog native assets are missing: " + model.path);
            }
        }
    }
}
