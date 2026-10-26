using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Zjcs.Presentation.World
{
    /// <summary>Native MapRegionLight hierarchy, Freeform mesh and animation rendered
    /// into the original Multiply light texture for the built-in Windows renderer.</summary>
    public sealed class OfficialMainlineRegionFog : MonoBehaviour
    {
        [Serializable] private sealed class Data
        {
            public string manifest;
            public Region[] regions;
            public Rendering rendering;
            public Light globalLight;
        }
        [Serializable] private sealed class Rendering { public float hdr, textureScale; }
        [Serializable] private sealed class Node
        {
            public string name;
            public int parent;
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        [Serializable] private sealed class Key
        {
            public float time, value, inSlope, outSlope, inWeight, outWeight;
            public int weightedMode;
        }
        [Serializable] private sealed class Light
        {
            public int node, order;
            public int[] layers;
            public float[] color;
            public float intensity, falloff, falloffIntensity, recessionBlurEdge;
        }
        [Serializable] private sealed class Region
        {
            public int region;
            public Node[] nodes;
            public Light light;
            public Key[] activationRadius;
            public float duration;
        }
        private sealed class View
        {
            public Region Source;
            public Transform[] Nodes;
            public Mesh Mesh;
            public Material Material;
            public AnimationCurve Radius;
            public bool Public;
            public float UnlockAt = -1;
        }
        private readonly List<View> views = new List<View>();
        private readonly Dictionary<Camera, RenderTexture> targets = new Dictionary<Camera, RenderTexture>();
        private Func<int, bool> isPublic;
        private Data data;
        private CommandBuffer buffer;
        public int ShadowCount => views.Count;
        public int VisibleShadowCount => views.Count(v => !v.Public || v.UnlockAt >= 0 && Time.time - v.UnlockAt < v.Source.duration);

        public void Initialize(Camera camera, Transform terrain, Func<int, bool> publicRegion,
            OfficialMainlineMapProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            isPublic = publicRegion ?? throw new ArgumentNullException(nameof(publicRegion));
            var source = Resources.Load<TextAsset>(profile.Resource("Fog/region-lights"));
            data = source == null ? null : JsonUtility.FromJson<Data>(source.text);
            if (data?.manifest != "854_156383" || !profile.HasRegionFog || data.regions?.Length != profile.RegionShadowCount)
                throw new InvalidDataException("Native region shadows are unbound for Map" + profile.MapClassId);
            var template = Resources.Load<Material>("World/Mainline/RegionLight2D");
            var lit = Resources.Load<Material>("World/Mainline/RegionSpriteLit");
            var falloff = Resources.Load<Texture2D>(profile.Resource("Fog/FalloffLookupTexture"));
            if (template == null || lit == null || falloff == null)
                throw new InvalidDataException("Native 2D light materials or falloff lookup are unbound.");
            foreach (var sourceRegion in data.regions)
            {
                var view = new View { Source = sourceRegion, Public = isPublic(sourceRegion.region) };
                view.Nodes = new Transform[sourceRegion.nodes.Length];
                for (int i = 0; i < view.Nodes.Length; i++)
                {
                    Node node = sourceRegion.nodes[i];
                    var target = new GameObject(node.name).transform;
                    target.SetParent(node.parent < 0 ? transform : view.Nodes[node.parent], false);
                    target.localPosition = node.position;
                    target.localRotation = node.rotation;
                    target.localScale = node.scale;
                    view.Nodes[i] = target;
                }
                view.Mesh = Resources.Load<Mesh>(profile.Resource("Fog/Meshes/LightRegion" + sourceRegion.region));
                if (view.Mesh == null) throw new InvalidDataException("Native light mesh is unbound: " + sourceRegion.region);
                view.Radius = new AnimationCurve(sourceRegion.activationRadius.Select(k => new Keyframe(k.time, k.value, k.inSlope, k.outSlope,
                    k.inWeight, k.outWeight) { weightedMode = (WeightedMode)k.weightedMode }).ToArray());
                view.Material = new Material(template);
                Light light = sourceRegion.light;
                view.Material.SetTexture("_FalloffLookup", falloff);
                view.Material.SetFloat("_FalloffDistance", light.falloff);
                view.Material.SetFloat("_FalloffIntensity", light.falloffIntensity);
                view.Material.SetFloat("_InverseHDREmulationScale", 1 / data.rendering.hdr);
                view.Material.SetFloat("_ShadowIntensity", 1); // Original region shadow has shadowEnabled=false.
                view.Material.SetFloat("_RecessionBlurEdge", light.recessionBlurEdge);
                view.Material.SetColor("_LightColor", new Color(light.color[0], light.color[1], light.color[2], light.color[3]) * light.intensity);
                views.Add(view);
            }
            foreach (var batch in terrain.GetComponentsInChildren<OfficialMap10TerrainView>()) batch.BindRegionLighting(lit);
            BindCamera(camera);
            buffer = new CommandBuffer { name = "Official region Light2D" };
            Camera.onPreCull += Render;
        }

        public void BindCamera(Camera camera)
        {
            if (camera != null && !targets.ContainsKey(camera)) targets.Add(camera, null);
        }

        public void Refresh(Vector3 unlockPosition)
        {
            foreach (View view in views)
            {
                bool value = isPublic(view.Source.region);
                if (value == view.Public) continue;
                view.Public = value;
                view.UnlockAt = value ? Time.time : -1;
                view.Material.SetVector("_RecessionCenter", unlockPosition);
                if (value) view.Material.EnableKeyword("USE_RECESSION");
                else view.Material.DisableKeyword("USE_RECESSION");
            }
        }

        private void Render(Camera camera)
        {
            if (buffer == null || !targets.TryGetValue(camera, out RenderTexture texture)) return;
            int width = Mathf.Max(1, (int)(camera.pixelWidth * data.rendering.textureScale));
            int height = Mathf.Max(1, (int)(camera.pixelHeight * data.rendering.textureScale));
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) { texture.Release(); Destroy(texture); }
                GraphicsFormat format = GraphicsFormat.R8G8B8A8_UNorm;
                if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render))
                    format = GraphicsFormat.B10G11R11_UFloatPack32;
                else if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Linear | FormatUsage.Render))
                    format = GraphicsFormat.R16G16B16A16_SFloat;
                var descriptor = new RenderTextureDescriptor(width, height) { graphicsFormat = format,
                    useMipMap = false, autoGenerateMips = false, depthBufferBits = 0, msaaSamples = 1, dimension = TextureDimension.Tex2D };
                texture = new RenderTexture(descriptor)
                { name = "Official Light2D " + camera.name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                texture.Create();
                targets[camera] = texture;
            }
            buffer.Clear();
            buffer.SetRenderTarget(texture);
            Light global = data.globalLight;
            var clear = new Color(global.color[0], global.color[1], global.color[2], global.color[3]) * (global.intensity / data.rendering.hdr);
            buffer.ClearRenderTarget(false, true, clear);
            buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
            foreach (View view in views.OrderBy(v => v.Source.light.order))
            {
                float elapsed = view.UnlockAt < 0 ? 0 : Time.time - view.UnlockAt;
                if (view.Public && (view.UnlockAt < 0 || elapsed >= view.Source.duration)) continue;
                if (view.UnlockAt >= 0) view.Material.SetFloat("_RecessionRadius", view.Radius.Evaluate(elapsed));
                buffer.DrawMesh(view.Mesh, view.Nodes[view.Source.light.node].localToWorldMatrix, view.Material);
            }
            buffer.SetGlobalTexture("_ShapeLightTexture0", texture);
            buffer.SetGlobalFloat("_HDREmulationScale", data.rendering.hdr);
            buffer.SetGlobalFloat("_UseSceneLighting", 1);
            buffer.SetGlobalVector("_ShapeLightBlendFactors0", new Vector4(1, 0, 0, 0));
            buffer.SetGlobalVector("_ShapeLightMaskFilter0", Vector4.zero);
            buffer.SetGlobalVector("_ShapeLightInvertedFilter0", Vector4.zero);
            Graphics.ExecuteCommandBuffer(buffer);
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= Render;
            buffer?.Release();
            foreach (View view in views) if (view.Material != null) Destroy(view.Material);
            foreach (var texture in targets.Values) if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
