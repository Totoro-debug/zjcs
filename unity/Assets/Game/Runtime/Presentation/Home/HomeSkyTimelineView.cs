using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Home
{
    internal sealed class HomeSkyTimelineView : IDisposable
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly HomeSceneView scene;
        private readonly Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> renderers = new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, Component> lights = new Dictionary<string, Component>(StringComparer.Ordinal);
        private readonly Dictionary<string, AnimationCurve> curves = new Dictionary<string, AnimationCurve>(StringComparer.Ordinal);
        private readonly Dictionary<string, HomeSkyTrackData> steppedTracks = new Dictionary<string, HomeSkyTrackData>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<Sprite> sprites = new List<Sprite>();
        private HomeSkyTimelineData data;
        private GameObject root;
        private bool disposed;

        public static HomeSkyTimelineView CreateIfSupported(HomeSceneView scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            string path = Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName,
                "environment-visuals", "day-sky-timeline.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official Home Day_Common timeline is missing.", path);
            HomeSkyTimelineData data = JsonUtility.FromJson<HomeSkyTimelineData>(File.ReadAllText(path));
            if (data == null || data.sourceManifest != "854_156383" || data.duration <= 0
                || string.IsNullOrEmpty(data.rootPath) || data.nodes == null || data.renderers == null
                || data.lights == null || data.tracks == null || data.tracks.Length == 0)
                throw new InvalidDataException("The official Home Day_Common timeline payload is incomplete.");
            string renderPipelineIssue = RenderPipelineIssue(data);
            if (!string.IsNullOrEmpty(renderPipelineIssue))
            {
                Debug.LogError("Official Home daytime sky is disabled because its URP 2D renderer is unavailable: "
                    + renderPipelineIssue);
                return null;
            }
            return new HomeSkyTimelineView(scene, data);
        }

        private HomeSkyTimelineView(HomeSceneView scene, HomeSkyTimelineData data)
        {
            this.scene = scene;
            this.data = data;
            try
            {
                Build();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static string RenderPipelineIssue(HomeSkyTimelineData data)
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return "the project uses the Built-in Render Pipeline";
            if (pipeline.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")
                return "the active pipeline is " + pipeline.GetType().FullName;
            MethodInfo getRenderer = pipeline.GetType().GetMethod("GetRenderer", InstanceFlags,
                null, new[] { typeof(int) }, null);
            if (getRenderer == null) return "the active URP asset does not expose its configured renderer";
            object renderer;
            try
            {
                renderer = getRenderer.Invoke(pipeline, new object[] { 0 });
            }
            catch (TargetInvocationException error)
            {
                return "the active URP renderer could not be loaded: " + (error.InnerException ?? error).Message;
            }
            if (renderer == null || renderer.GetType().Name.IndexOf("Renderer2D", StringComparison.Ordinal) < 0)
                return "the active URP asset does not use a 2D renderer";
            bool hasLight2D = false;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("UnityEngine.Rendering.Universal.Light2D", false);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    hasLight2D = true;
                    break;
                }
            }
            if (!hasLight2D) return "the Universal Render Pipeline runtime is not loaded";
            foreach (HomeSkyMaterialData material in data.materials ?? Array.Empty<HomeSkyMaterialData>())
                if (material == null || HomeRuntimeShaderBindings.Resolve(material.shader) == null)
                    return "the official shader is not loaded: " + (material == null ? "<null>" : material.shader);
            return null;
        }

        private void Build()
        {
            foreach (HomeSkyNodeData node in data.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.path) || nodes.ContainsKey(node.path))
                    throw new InvalidDataException("The official Home sky hierarchy contains an invalid or duplicate path.");
                Transform parent;
                if (node.path == data.rootPath) parent = scene.Root.transform;
                else if (!nodes.TryGetValue(node.parent, out GameObject parentObject))
                    throw new InvalidDataException("The official Home sky hierarchy is missing parent " + node.parent);
                else parent = parentObject.transform;

                var gameObject = new GameObject(node.name);
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localPosition = Vector3Value(node.position, Vector3.zero);
                gameObject.transform.localRotation = QuaternionValue(node.rotation);
                gameObject.transform.localScale = Vector3Value(node.scale, Vector3.one);
                gameObject.SetActive(node.active);
                nodes.Add(node.path, gameObject);
                if (node.path == data.rootPath) root = gameObject;
                if (node.cameraFollower != null)
                {
                    HomeSkyCameraFollower follower = gameObject.AddComponent<HomeSkyCameraFollower>();
                    follower.Initialize(scene.Camera.transform, node.cameraFollower.centerX,
                        node.cameraFollower.threshold);
                }
            }
            if (!nodes.ContainsKey(data.rootPath))
                throw new InvalidDataException("The official Home sky root is missing.");

            Type lightType = data.lights.Length == 0 ? null : ResolveLight2DType();
            foreach (HomeSkyLightData light in data.lights)
            {
                if (light == null || !nodes.TryGetValue(light.path, out GameObject gameObject))
                    throw new InvalidDataException("The official Home sky Light2D target is missing.");
                Component component = gameObject.AddComponent(lightType);
                if (component == null) throw new InvalidOperationException("Could not create an official Home Light2D component.");
                if (!(component is Behaviour behaviour))
                    throw new InvalidOperationException("The official Home Light2D component is not a Behaviour.");
                behaviour.enabled = light.enabled;
                if (lights.ContainsKey(light.path))
                    throw new InvalidDataException("The official Home sky has multiple Light2D components on one node.");
                lights.Add(light.path, component);
            }

            foreach (HomeSkyRendererData rendererData in data.renderers)
            {
                if (rendererData == null || !nodes.TryGetValue(rendererData.path, out GameObject gameObject))
                    throw new InvalidDataException("The official Home sky SpriteRenderer target is missing.");
                Sprite sprite = LoadSprite(rendererData);
                var renderer = gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = ColorValue(rendererData.color, Color.white);
                renderer.flipX = rendererData.flipX;
                renderer.flipY = rendererData.flipY;
                renderer.enabled = rendererData.enabled;
                renderer.sortingOrder = scene.GetNextSortingOrder(rendererData.sortingLayer,
                    rendererData.sortingOrder);
                if (!materials.TryGetValue(rendererData.material, out Material material))
                {
                    HomeSkyMaterialData materialData = FindMaterial(rendererData.material);
                    material = CreateMaterial(materialData);
                    materials.Add(rendererData.material, material);
                }
                renderer.sharedMaterial = material;
                renderers.Add(rendererData.path, renderer);
            }

            foreach (HomeSkyTrackData track in data.tracks)
            {
                if (track == null || !nodes.ContainsKey(track.path) || track.keys == null || track.keys.Length == 0)
                    throw new InvalidDataException("The official Home sky animation track is invalid.");
                string key = TrackKey(track.path, track.property);
                if (track.stepped)
                {
                    steppedTracks.Add(key, track);
                    continue;
                }
                var keys = new Keyframe[track.keys.Length];
                for (int index = 0; index < keys.Length; index++)
                {
                    HomeSkyKeyData source = track.keys[index];
                    keys[index] = new Keyframe(source.time, source.value, source.inTangent, source.outTangent);
                }
                curves.Add(key, new AnimationCurve(keys)
                {
                    preWrapMode = track.loop ? WrapMode.Loop : WrapMode.ClampForever,
                    postWrapMode = track.loop ? WrapMode.Loop : WrapMode.ClampForever,
                });
            }
            Apply(0f);
        }

        public void Apply(float dayProgress)
        {
            if (disposed || data == null) return;
            float timelineTime = Mathf.Clamp01(dayProgress) * data.duration;
            float clipTime = (timelineTime - data.clipStart) * data.clipSpeed + data.clipIn;
            if (data.clipLoop && data.clipDuration > 0f) clipTime = Mathf.Repeat(clipTime, data.clipDuration);
            else clipTime = Mathf.Clamp(clipTime, 0f, data.clipDuration);
            foreach (HomeSkyTrackData track in data.tracks)
                ApplyTrack(track, TrackValue(track, clipTime));
        }

        private void ApplyTrack(HomeSkyTrackData track, float value)
        {
            GameObject gameObject = nodes[track.path];
            Transform target = gameObject.transform;
            switch (track.property)
            {
                case "position.x": target.localPosition = WithX(target.localPosition, value); return;
                case "position.y": target.localPosition = WithY(target.localPosition, value); return;
                case "position.z": target.localPosition = WithZ(target.localPosition, value); return;
                case "scale.x": target.localScale = WithX(target.localScale, value); return;
                case "scale.y": target.localScale = WithY(target.localScale, value); return;
                case "scale.z": target.localScale = WithZ(target.localScale, value); return;
                case "m_IsActive": gameObject.SetActive(value >= 0.5f); return;
                case "m_Enabled":
                    if (renderers.TryGetValue(track.path, out SpriteRenderer enabledRenderer)) enabledRenderer.enabled = value >= 0.5f;
                    else if (lights.TryGetValue(track.path, out Component enabledLight)
                        && enabledLight is Behaviour lightBehaviour) lightBehaviour.enabled = value >= 0.5f;
                    return;
                case "m_Color.r": SetColorChannel(track.path, 0, value); return;
                case "m_Color.g": SetColorChannel(track.path, 1, value); return;
                case "m_Color.b": SetColorChannel(track.path, 2, value); return;
                case "m_Color.a": SetColorChannel(track.path, 3, value); return;
                case "m_Intensity": SetLightFloat(track.path, "intensity", value); return;
                case "m_ShadowIntensity": SetLightFloat(track.path, "shadowIntensity", value); return;
                default: throw new InvalidDataException("Unsupported official Home sky animation property: " + track.property);
            }
        }

        private float TrackValue(HomeSkyTrackData track, float time)
        {
            string key = TrackKey(track.path, track.property);
            if (steppedTracks.TryGetValue(key, out HomeSkyTrackData stepped))
            {
                float value = stepped.keys[0].value;
                foreach (HomeSkyKeyData item in stepped.keys)
                {
                    if (item.time > time) break;
                    value = item.value;
                }
                return value;
            }
            return curves.TryGetValue(key, out AnimationCurve curve) ? curve.Evaluate(time) : 0f;
        }

        private void SetColorChannel(string path, int channel, float value)
        {
            if (lights.TryGetValue(path, out Component light))
            {
                Color color = (Color)GetLightProperty(light, "color");
                SetChannel(ref color, channel, value);
                SetLightProperty(light, "color", color);
                return;
            }
            if (!renderers.TryGetValue(path, out SpriteRenderer renderer))
                throw new InvalidDataException("The official Home sky color target has no renderer or Light2D: " + path);
            Color rendererColor = renderer.color;
            SetChannel(ref rendererColor, channel, value);
            renderer.color = rendererColor;
        }

        private void SetLightFloat(string path, string property, float value)
        {
            if (!lights.TryGetValue(path, out Component component))
                throw new InvalidDataException("The official Home sky light curve target is missing: " + path);
            SetLightProperty(component, property, value);
        }

        private static object GetLightProperty(Component component, string property)
        {
            PropertyInfo info = component.GetType().GetProperty(property, InstanceFlags);
            if (info == null || !info.CanRead)
                throw new MissingMemberException(component.GetType().FullName, property);
            return info.GetValue(component, null);
        }

        private static void SetLightProperty(Component component, string property, object value)
        {
            PropertyInfo info = component.GetType().GetProperty(property, InstanceFlags);
            if (info == null || !info.CanWrite)
                throw new MissingMemberException(component.GetType().FullName, property);
            info.SetValue(component, value, null);
        }

        private Type ResolveLight2DType()
        {
            const string typeName = "UnityEngine.Rendering.Universal.Light2D";
            // Recovered prefab schema assemblies also declare Light2D. They are
            // serialization carriers; the active URP asset owns the rendering type.
            Type type = GraphicsSettings.currentRenderPipeline?.GetType().Assembly.GetType(typeName, false);
            if (type != null && typeof(Component).IsAssignableFrom(type)) return type;
            throw new InvalidOperationException("The pinned Home scene requires URP Light2D, but the Universal Render Pipeline runtime is not loaded.");
        }

        private HomeSkyMaterialData FindMaterial(string name)
        {
            foreach (HomeSkyMaterialData material in data.materials ?? Array.Empty<HomeSkyMaterialData>())
                if (material != null && material.name == name) return material;
            throw new InvalidDataException("The official Home sky material is missing: " + name);
        }

        private static Material CreateMaterial(HomeSkyMaterialData source)
        {
            Shader shader = HomeRuntimeShaderBindings.Resolve(source.shader);
            if (shader == null) throw new InvalidOperationException("The official Home sky shader is not loaded: " + source.shader);
            var material = new Material(shader) { name = source.name };
            foreach (HomeSkyFloatProperty property in source.floats ?? Array.Empty<HomeSkyFloatProperty>())
                if (material.HasProperty(property.name)) material.SetFloat(property.name, property.value);
            foreach (HomeSkyColorProperty property in source.colors ?? Array.Empty<HomeSkyColorProperty>())
                if (material.HasProperty(property.name)) material.SetColor(property.name, ColorValue(property.value, Color.white));
            return material;
        }

        private Sprite LoadSprite(HomeSkyRendererData source)
        {
            if (spriteCache.TryGetValue(source.file, out Sprite cached)) return cached;
            string path = Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName,
                "environment-visuals", "sprites", "day-sky", source.file);
            if (!File.Exists(path)) throw new FileNotFoundException("Official Home daytime sky sprite is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = source.sprite,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException("Could not decode official Home daytime sky sprite: " + path);
            }
            textures.Add(texture);
            Rect rect = new Rect(0, 0, source.rect[0], source.rect[1]);
            Vector2 pivot = new Vector2(source.pivot[0], source.pivot[1]);
            SpriteMeshType meshType = source.meshType == "Tight" ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            Sprite sprite = Sprite.Create(texture, rect, pivot, source.pixelsPerUnit, (uint)Mathf.Max(0, source.extrude), meshType);
            if (sprite == null) throw new InvalidDataException("Could not create official Home daytime sky sprite: " + source.sprite);
            sprite.name = source.sprite;
            sprites.Add(sprite);
            spriteCache.Add(source.file, sprite);
            return sprite;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Sprite sprite in sprites) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (Material material in materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (Texture2D texture in textures) if (texture != null) UnityEngine.Object.Destroy(texture);
            nodes.Clear();
            renderers.Clear();
            lights.Clear();
            curves.Clear();
            steppedTracks.Clear();
            materials.Clear();
            spriteCache.Clear();
            sprites.Clear();
            textures.Clear();
            data = null;
        }

        private static string TrackKey(string path, string property) => path + "|" + property;
        private static Vector3 Vector3Value(float[] values, Vector3 fallback) =>
            values != null && values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : fallback;
        private static Quaternion QuaternionValue(float[] values) => values != null && values.Length >= 4
            ? new Quaternion(values[0], values[1], values[2], values[3]) : Quaternion.identity;
        private static Color ColorValue(float[] values, Color fallback) => values != null && values.Length >= 4
            ? new Color(values[0], values[1], values[2], values[3]) : fallback;
        private static Vector3 WithX(Vector3 value, float x) => new Vector3(x, value.y, value.z);
        private static Vector3 WithY(Vector3 value, float y) => new Vector3(value.x, y, value.z);
        private static Vector3 WithZ(Vector3 value, float z) => new Vector3(value.x, value.y, z);
        private static void SetChannel(ref Color color, int channel, float value)
        {
            switch (channel)
            {
                case 0: color.r = value; break;
                case 1: color.g = value; break;
                case 2: color.b = value; break;
                case 3: color.a = value; break;
            }
        }

        [Serializable] private sealed class HomeSkyTimelineData
        {
            public string sourceManifest;
            public string sourceTimeline;
            public string sourceClip;
            public float duration;
            public float clipStart;
            public float clipDuration;
            public float clipIn;
            public float clipSpeed;
            public bool clipLoop;
            public bool loop;
            public string rootPath;
            public HomeSkyNodeData[] nodes;
            public HomeSkyRendererData[] renderers;
            public HomeSkyLightData[] lights;
            public HomeSkyMaterialData[] materials;
            public HomeSkyTrackData[] tracks;
        }

        [Serializable] private sealed class HomeSkyNodeData
        {
            public string path;
            public string parent;
            public string name;
            public float[] position;
            public float[] rotation;
            public float[] scale;
            public bool active;
            public HomeSkyFollowerData cameraFollower;
        }

        [Serializable] private sealed class HomeSkyFollowerData
        {
            public float centerX;
            public float threshold;
        }

        [Serializable] private sealed class HomeSkyRendererData
        {
            public string path;
            public string sprite;
            public string file;
            public float[] rect;
            public float[] pivot;
            public float pixelsPerUnit;
            public int extrude;
            public string meshType;
            public bool enabled;
            public bool flipX;
            public bool flipY;
            public float[] color;
            public int sortingLayer;
            public int sortingOrder;
            public string material;
        }

        [Serializable] private sealed class HomeSkyLightData
        {
            public string path;
            public bool enabled;
        }

        [Serializable] private sealed class HomeSkyMaterialData
        {
            public string name;
            public string shader;
            public HomeSkyFloatProperty[] floats;
            public HomeSkyColorProperty[] colors;
        }

        [Serializable] private sealed class HomeSkyFloatProperty
        {
            public string name;
            public float value;
        }

        [Serializable] private sealed class HomeSkyColorProperty
        {
            public string name;
            public float[] value;
        }

        [Serializable] private sealed class HomeSkyTrackData
        {
            public string path;
            public string property;
            public bool stepped;
            public bool loop;
            public HomeSkyKeyData[] keys;
        }

        [Serializable] private sealed class HomeSkyKeyData
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
        }
    }
}
