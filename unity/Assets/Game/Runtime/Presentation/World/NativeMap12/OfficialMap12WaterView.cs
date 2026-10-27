using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.World.NativeWater;

namespace Zjcs.Presentation.World.NativeMap12
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap12WaterView : MonoBehaviour
    {
        private const string PayloadResource = "Map12/Water/map12-water-runtime";
        private const string ShaderResource = "World/Map12Water";
        private readonly List<Material> materials = new List<Material>();

        public int RendererCount { get; private set; }

        public void Initialize(GameObject nativeScene)
        {
            if (!nativeScene || nativeScene.activeInHierarchy)
                throw new InvalidOperationException("Bind Map12 Water2 before the original scene is activated.");
            TextAsset source = Resources.Load<TextAsset>(PayloadResource);
            Shader shader = Resources.Load<Shader>(ShaderResource);
            Texture2D mask = Resources.Load<Texture2D>("Map12/Water/map12-water-mask");
            if (!source || !shader || !mask)
                throw new InvalidDataException("Official Map12 Water2 runtime resources are incomplete.");
            WaterPayload payload = JsonUtility.FromJson<WaterPayload>(source.text);
            if (payload == null || payload.format != 1 || payload.mapClassId != 12
                || payload.renderers == null || payload.renderers.Length != 57
                || payload.maskRect == null || payload.maskRect.Length != 4)
                throw new InvalidDataException("Official Map12 Water2 payload is invalid.");
            WaterMaskRenderer maskRenderer = nativeScene.GetComponentsInChildren<WaterMaskRenderer>(true).SingleOrDefault();
            if (!maskRenderer) throw new InvalidDataException("Original Map12 WaterMaskRenderer is missing or ambiguous.");
            var waterMaskMeshes = maskRenderer.LakeRenderers ?? new List<MeshRenderer>();

            foreach (WaterRendererPayload item in payload.renderers)
            {
                Validate(item);
                MeshRenderer renderer = nativeScene.GetComponentsInChildren<MeshRenderer>(true)
                    .SingleOrDefault(value => value.name == item.name);
                MeshFilter filter = renderer ? renderer.GetComponent<MeshFilter>() : null;
                Mesh mesh = filter ? filter.sharedMesh : null;
                long indexCount = 0;
                if (mesh)
                    for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                        indexCount += mesh.GetIndexCount(subMesh);
                if (!renderer || !filter || !filter.sharedMesh
                    || mesh.vertexCount != item.vertices.Length / 2
                    || indexCount != item.indices.Length)
                    throw new InvalidDataException("Original Map12 Water2 renderer is missing or changed: " + item.name);

                var material = new Material(shader) { name = item.material + " " + item.name };
                materials.Add(material);
                foreach (WaterTextureBinding texture in item.textures)
                {
                    Texture2D value = Resources.Load<Texture2D>(texture.resource);
                    if (!value) throw new InvalidDataException("Map12 Water2 texture is missing: " + texture.resource);
                    material.SetTexture(texture.property, value);
                    material.SetTextureScale(texture.property, new Vector2(texture.scale[0], texture.scale[1]));
                    material.SetTextureOffset(texture.property, new Vector2(texture.offset[0], texture.offset[1]));
                }
                foreach (WaterColorBinding color in item.colors)
                    material.SetColor(color.property, new Color(color.value[0], color.value[1], color.value[2], color.value[3]));
                foreach (WaterFloatBinding value in item.parameters)
                    material.SetFloat(value.property, value.value);

                renderer.sharedMaterial = material;
                if (!waterMaskMeshes.Contains(renderer)) waterMaskMeshes.Add(renderer);
                RendererCount++;
            }
            maskRenderer.LakeRenderers = waterMaskMeshes;
        }

        private static void Validate(WaterRendererPayload item)
        {
            if (item == null || string.IsNullOrEmpty(item.name) || item.vertices == null || item.vertices.Length < 6
                || item.vertices.Length % 2 != 0 || item.uv == null
                || item.uv.Length != 0 && item.uv.Length != item.vertices.Length
                || item.indices == null || item.indices.Length == 0 || item.indices.Length % 3 != 0)
                throw new InvalidDataException("Map12 Water2 mesh arrays are invalid.");
            foreach (int index in item.indices)
                if (index < 0 || index >= item.vertices.Length / 2)
                    throw new InvalidDataException("Map12 Water2 mesh index is invalid: " + item.name);
        }

        private void OnDestroy()
        {
            foreach (Material value in materials) DestroyResource(value);
        }

        private static void DestroyResource(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        [Serializable] private sealed class WaterPayload
        {
            public int format, mapClassId, sortingOrder;
            public float[] maskRect;
            public WaterRendererPayload[] renderers;
        }

        [Serializable] private sealed class WaterRendererPayload
        {
            public string name, material;
            public float[] vertices, uv;
            public int[] indices;
            public WaterTextureBinding[] textures;
            public WaterColorBinding[] colors;
            public WaterFloatBinding[] parameters;
        }

        [Serializable] private sealed class WaterTextureBinding
        {
            public string property, resource;
            public float[] scale, offset;
        }

        [Serializable] private sealed class WaterColorBinding { public string property; public float[] value; }
        [Serializable] private sealed class WaterFloatBinding { public string property; public float value; }
    }
}
