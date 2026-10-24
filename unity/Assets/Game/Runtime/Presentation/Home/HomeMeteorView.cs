using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed class HomeMeteorView : IDisposable
    {
        private const string EffectShaderName = "Zjcs/Map10/Official Effect";
        private readonly HomeSceneView scene;
        private readonly HomeMeteorRuntimePayload payload;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private GameObject currentRoot;
        private int currentTemplateIndex = -1;

        public HomeMeteorView(HomeSceneView scene, HomeMeteorRuntimePayload payload)
        {
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (payload.format != 1 || payload.templates == null || payload.templates.Length == 0)
                throw new InvalidDataException("Official Home meteor templates are missing or malformed.");
            foreach (HomeMeteorTemplate template in payload.templates)
                if (template == null || string.IsNullOrEmpty(template.assetGuid)
                    || template.systems == null || template.systems.Length == 0)
                    throw new InvalidDataException("Official Home meteor template is incomplete.");
            if (Shader.Find(EffectShaderName) == null)
                throw new InvalidOperationException("Windows Home effect shader is unavailable: " + EffectShaderName);
        }

        public void Play(int templateIndex)
        {
            if (templateIndex < 0 || templateIndex >= payload.templates.Length)
                throw new ArgumentOutOfRangeException(nameof(templateIndex));

            if (templateIndex != currentTemplateIndex || currentRoot == null)
                BuildTemplate(templateIndex);

            foreach (ParticleSystem system in currentRoot.GetComponentsInChildren<ParticleSystem>(true))
                system.Play(true);
        }

        private void BuildTemplate(int templateIndex)
        {
            DestroyObject(currentRoot);
            currentRoot = null;
            currentTemplateIndex = -1;

            var root = new GameObject("HomeMeteor_" + payload.templates[templateIndex].assetGuid);
            root.transform.SetParent(scene.Root.transform, false);
            try
            {
                foreach (HomeMeteorParticle data in payload.templates[templateIndex].systems)
                    BuildParticle(data, root.transform);
                currentRoot = root;
                currentTemplateIndex = templateIndex;
            }
            catch
            {
                DestroyObject(root);
                throw;
            }
        }

        private void BuildParticle(HomeMeteorParticle data, Transform parent)
        {
            if (data == null || data.matrix == null || data.matrix.Length != 16)
                throw new InvalidDataException("Official Home meteor particle has an invalid transform.");
            if (data.unconverted_modules != null && data.unconverted_modules.Length != 0)
                throw new InvalidDataException("Official Home meteor contains unconverted particle modules.");

            var particleObject = new GameObject(ParticleName(data.path));
            particleObject.transform.SetParent(parent, false);
            ApplyLocalMatrix(particleObject.transform, data.matrix);

            ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.01f, data.duration);
            main.loop = data.loop;
            main.prewarm = data.prewarm;
            main.playOnAwake = false;
            main.simulationSpeed = data.speed;
            main.stopAction = (ParticleSystemStopAction)data.stop_action;
            main.cullingMode = (ParticleSystemCullingMode)data.culling_mode;
            main.ringBufferMode = (ParticleSystemRingBufferMode)data.ring_buffer_mode;
            main.emitterVelocityMode = (ParticleSystemEmitterVelocityMode)data.emitter_velocity_mode;
            main.scalingMode = (ParticleSystemScalingMode)data.scaling_mode;
            main.useUnscaledTime = data.use_unscaled_time;
            main.simulationSpace = (ParticleSystemSimulationSpace)data.simulation_space;
            main.maxParticles = Mathf.Max(1, data.max_particles);
            main.startDelay = ToCurve(data.delay);
            main.startLifetime = ToCurve(data.life);
            main.startSpeed = ToCurve(data.velocity);
            main.startSize3D = data.size3d;
            main.startSize = ToCurve(data.size);
            if (data.size3d) main.startSizeY = ToCurve(data.size_y);
            main.startRotation3D = data.rotation3d;
            main.startRotation = ToCurve(data.rotation);
            if (data.rotation3d)
            {
                main.startRotationX = ToCurve(data.rotation_x);
                main.startRotationY = ToCurve(data.rotation_y);
            }
            main.startColor = ToMinMaxGradient(data.color);

            ConfigureEmission(system, data);
            ConfigureShape(system, data.shape);
            ConfigureModules(system, data.modules);

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = (ParticleSystemRenderMode)data.render_mode;
            renderer.alignment = (ParticleSystemRenderSpace)data.alignment;
            renderer.sortingOrder = scene.GetNextSortingOrder(data.sorting_layer, data.sorting_order);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (data.stretch != null)
            {
                renderer.lengthScale = data.stretch.m_LengthScale;
                renderer.velocityScale = data.stretch.m_VelocityScale;
                renderer.cameraVelocityScale = data.stretch.m_CameraVelocityScale;
            }
            renderer.sharedMaterial = CreateMaterial(data.material);
            HomeMeteorTrailModule trail = data.modules == null ? null : data.modules.TrailModule;
            if (trail != null && trail.enabled)
                renderer.trailMaterial = CreateMaterial(trail.material);
        }

        private static void ConfigureEmission(ParticleSystem system, HomeMeteorParticle data)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = data.emission_enabled;
            emission.rateOverTime = ToCurve(data.rate);
            var bursts = new ParticleSystem.Burst[data.bursts == null ? 0 : data.bursts.Length];
            for (int i = 0; i < bursts.Length; i++)
            {
                HomeMeteorBurst source = data.bursts[i];
                bursts[i] = new ParticleSystem.Burst(source.time, ToCurve(source.count),
                    Mathf.Max(1, source.cycles), source.interval);
                bursts[i].probability = Mathf.Clamp01(source.probability);
            }
            emission.SetBursts(bursts);
        }

        private static void ConfigureShape(ParticleSystem system, HomeMeteorShape data)
        {
            if (data == null) return;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = data.enabled;
            if (!data.enabled) return;
            shape.shapeType = (ParticleSystemShapeType)data.type;
            shape.angle = data.angle;
            shape.length = data.length;
            shape.radiusThickness = data.radiusThickness;
            shape.donutRadius = data.donutRadius;
            shape.radius = data.radius;
            shape.arc = data.arc;
            if (data.boxThickness != null) shape.scale = ToVector3(data.boxThickness);
            if (data.m_Position != null) shape.position = ToVector3(data.m_Position);
            if (data.m_Rotation != null) shape.rotation = ToVector3(data.m_Rotation);
            if (data.m_Scale != null) shape.scale = ToVector3(data.m_Scale);
        }

        private static void ConfigureModules(ParticleSystem system, HomeMeteorModules data)
        {
            if (data == null) return;
            if (data.SizeModule != null)
            {
                ParticleSystem.SizeOverLifetimeModule module = system.sizeOverLifetime;
                module.enabled = data.SizeModule.enabled;
                module.separateAxes = data.SizeModule.separateAxes;
                module.size = ToCurve(data.SizeModule.curve);
                if (data.SizeModule.separateAxes)
                {
                    module.x = ToCurve(data.SizeModule.x);
                    module.y = ToCurve(data.SizeModule.y);
                    module.z = ToCurve(data.SizeModule.z);
                }
            }
            if (data.VelocityModule != null)
            {
                ParticleSystem.VelocityOverLifetimeModule module = system.velocityOverLifetime;
                module.enabled = data.VelocityModule.enabled;
                module.space = data.VelocityModule.inWorldSpace
                    ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                module.x = ToCurve(data.VelocityModule.x);
                module.y = ToCurve(data.VelocityModule.y);
                module.z = ToCurve(data.VelocityModule.z);
                module.orbitalZ = ToCurve(data.VelocityModule.orbitalZ);
                module.radial = ToCurve(data.VelocityModule.radial);
                module.speedModifier = ToCurve(data.VelocityModule.speedModifier);
            }
            if (data.ClampVelocityModule != null)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule module = system.limitVelocityOverLifetime;
                module.enabled = data.ClampVelocityModule.enabled;
                module.separateAxes = data.ClampVelocityModule.separateAxes;
                module.space = data.ClampVelocityModule.inWorldSpace
                    ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                module.limit = ToCurve(data.ClampVelocityModule.magnitude);
                module.dampen = data.ClampVelocityModule.dampen;
                module.drag = ToCurve(data.ClampVelocityModule.drag);
                module.multiplyDragByParticleSize = data.ClampVelocityModule.multiplyDragByParticleSize;
                module.multiplyDragByParticleVelocity = data.ClampVelocityModule.multiplyDragByParticleVelocity;
            }
            if (data.ColorModule != null)
            {
                ParticleSystem.ColorOverLifetimeModule module = system.colorOverLifetime;
                module.enabled = data.ColorModule.enabled;
                module.color = ToMinMaxGradient(data.ColorModule);
            }
            if (data.TrailModule == null) return;
            ParticleSystem.TrailModule trail = system.trails;
            trail.enabled = data.TrailModule.enabled;
            if (!data.TrailModule.enabled) return;
            trail.mode = (ParticleSystemTrailMode)data.TrailModule.mode;
            trail.ratio = data.TrailModule.ratio;
            trail.minVertexDistance = data.TrailModule.minVertexDistance;
            trail.textureMode = (ParticleSystemTrailTextureMode)data.TrailModule.textureMode;
            trail.ribbonCount = Mathf.Max(1, data.TrailModule.ribbonCount);
            trail.shadowBias = data.TrailModule.shadowBias;
            trail.worldSpace = data.TrailModule.worldSpace;
            trail.dieWithParticles = data.TrailModule.dieWithParticles;
            trail.sizeAffectsWidth = data.TrailModule.sizeAffectsWidth;
            trail.sizeAffectsLifetime = data.TrailModule.sizeAffectsLifetime;
            trail.inheritParticleColor = data.TrailModule.inheritParticleColor;
            trail.generateLightingData = data.TrailModule.generateLightingData;
            trail.splitSubEmitterRibbons = data.TrailModule.splitSubEmitterRibbons;
            trail.attachRibbonsToTransform = data.TrailModule.attachRibbonsToTransform;
            if (data.TrailModule.textureScale != null)
                trail.textureScale = new Vector2(data.TrailModule.textureScale.x, data.TrailModule.textureScale.y);
            trail.lifetime = ToCurve(data.TrailModule.lifetime);
            trail.widthOverTrail = ToCurve(data.TrailModule.widthOverTrail);
            trail.colorOverLifetime = ToMinMaxGradient(data.TrailModule.colorOverLifetime);
            trail.colorOverTrail = ToMinMaxGradient(data.TrailModule.colorOverTrail);
        }

        private Material CreateMaterial(HomeMeteorMaterial data)
        {
            if (data == null || string.IsNullOrEmpty(data.name))
                throw new InvalidDataException("Official Home meteor particle material is missing.");
            if (materials.TryGetValue(data.name, out Material cached) && cached != null) return cached;

            Shader shader = Shader.Find(EffectShaderName);
            if (shader == null) throw new InvalidOperationException("Windows Home effect shader is unavailable: " + EffectShaderName);
            var material = new Material(shader) { name = "HomeMeteor_" + data.name };
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            foreach (HomeMeteorFloatProperty property in data.floats ?? Array.Empty<HomeMeteorFloatProperty>())
                if (material.HasProperty(property.name)) material.SetFloat(property.name, property.value);
            foreach (HomeMeteorColorProperty property in data.colors ?? Array.Empty<HomeMeteorColorProperty>())
                if (material.HasProperty(property.name)) material.SetColor(property.name, ToColor(property.value));
            foreach (HomeMeteorTextureBinding binding in data.textures ?? Array.Empty<HomeMeteorTextureBinding>())
            {
                Texture2D texture = LoadTexture(binding.file);
                material.SetTexture(binding.property, texture);
                material.SetTextureScale(binding.property, ToVector2(binding.scale, Vector2.one));
                material.SetTextureOffset(binding.property, ToVector2(binding.offset, Vector2.zero));
            }
            materials.Add(data.name, material);
            return material;
        }

        private Texture2D LoadTexture(string file)
        {
            string name = Path.GetFileName((file ?? string.Empty).Replace('\\', '/'));
            if (string.IsNullOrEmpty(name)) throw new InvalidDataException("Official Home meteor texture name is missing.");
            if (textures.TryGetValue(name, out Texture2D cached) && cached != null) return cached;

            string path = Path.Combine(Application.streamingAssetsPath, "Home", "sprites", name);
            if (!File.Exists(path)) throw new FileNotFoundException("Official Home meteor texture is missing.", path);
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { name = name };
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException("Official Home meteor texture could not be decoded: " + name);
            }
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            textures.Add(name, texture);
            return texture;
        }

        private static ParticleSystem.MinMaxCurve ToCurve(HomeMeteorCurve data)
        {
            if (data == null) return new ParticleSystem.MinMaxCurve(0f);
            switch (data.mode)
            {
                case 0: return new ParticleSystem.MinMaxCurve(data.min);
                case 1: return new ParticleSystem.MinMaxCurve(data.max, ToAnimationCurve(data.high));
                case 2: return new ParticleSystem.MinMaxCurve(1f,
                    ToAnimationCurve(data.low), ToAnimationCurve(data.high));
                case 3: return new ParticleSystem.MinMaxCurve(data.min, data.max);
                default: throw new InvalidDataException("Unsupported official Home meteor curve mode: " + data.mode);
            }
        }

        private static AnimationCurve ToAnimationCurve(HomeMeteorCurveKey[] source)
        {
            if (source == null || source.Length == 0) return AnimationCurve.Linear(0f, 1f, 1f, 1f);
            var keys = new Keyframe[source.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                HomeMeteorCurveKey key = source[i];
                keys[i] = new Keyframe(key.time, key.value, key.inSlope, key.outSlope);
            }
            return new AnimationCurve(keys);
        }

        private static ParticleSystem.MinMaxGradient ToMinMaxGradient(HomeMeteorColor data)
        {
            if (data == null) return new ParticleSystem.MinMaxGradient(Color.white);
            switch (data.mode)
            {
                case 0: return new ParticleSystem.MinMaxGradient(ToColor(data.min));
                case 1: return new ParticleSystem.MinMaxGradient(ToGradient(data.high));
                case 2: return new ParticleSystem.MinMaxGradient(ToColor(data.min), ToColor(data.max));
                case 3: return new ParticleSystem.MinMaxGradient(ToGradient(data.low), ToGradient(data.high));
                case 4:
                    var random = new ParticleSystem.MinMaxGradient(ToGradient(data.high));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;
                default: throw new InvalidDataException("Unsupported official Home meteor color mode: " + data.mode);
            }
        }

        private static Gradient ToGradient(HomeMeteorGradientData data)
        {
            var gradient = new Gradient();
            GradientColorKey[] colors = data?.rgb == null || data.rgb.Length == 0
                ? new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }
                : Array.ConvertAll(data.rgb, value => new GradientColorKey(
                    new Color(value.r, value.g, value.b), Mathf.Clamp01(value.time)));
            GradientAlphaKey[] alpha = data?.alpha == null || data.alpha.Length == 0
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                : Array.ConvertAll(data.alpha, value => new GradientAlphaKey(
                    Mathf.Clamp01(value.alpha), Mathf.Clamp01(value.time)));
            gradient.SetKeys(colors, alpha);
            return gradient;
        }

        private static void ApplyLocalMatrix(Transform target, float[] data)
        {
            var matrix = new Matrix4x4();
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    matrix[row, column] = data[row * 4 + column];
            Vector3 x = matrix.GetColumn(0);
            Vector3 y = matrix.GetColumn(1);
            Vector3 z = matrix.GetColumn(2);
            Vector3 scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f) scale.z = -scale.z;
            target.localPosition = matrix.GetColumn(3);
            target.localRotation = Quaternion.LookRotation(z.normalized, y.normalized);
            target.localScale = scale;
        }

        private static string ParticleName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Particle";
            int separator = path.LastIndexOf('/');
            return separator < 0 ? path : path.Substring(separator + 1);
        }

        private static Vector3 ToVector3(HomeMeteorVector3 value) => new Vector3(value.x, value.y, value.z);
        private static Vector2 ToVector2(float[] value, Vector2 fallback) => value == null || value.Length < 2
            ? fallback : new Vector2(value[0], value[1]);
        private static Color ToColor(HomeMeteorColorValue value) => value == null
            ? Color.white : new Color(value.r, value.g, value.b, value.a);

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            DestroyObject(currentRoot);
            currentRoot = null;
            foreach (Material material in materials.Values) DestroyObject(material);
            materials.Clear();
            foreach (Texture2D texture in textures.Values) DestroyObject(texture);
            textures.Clear();
        }
    }

    [Serializable]
    public sealed class HomeMeteorRuntimePayload
    {
        public int format;
        public string sourceManifest;
        public HomeMeteorTemplate[] templates;
    }

    [Serializable]
    public sealed class HomeMeteorTemplate
    {
        public string assetGuid;
        public HomeMeteorParticle[] systems;
    }

    [Serializable]
    public sealed class HomeMeteorParticle
    {
        public string path;
        public float[] matrix;
        public float duration;
        public bool loop;
        public bool prewarm;
        public float speed;
        public int max_particles;
        public bool emission_enabled;
        public int stop_action;
        public int culling_mode;
        public int ring_buffer_mode;
        public int emitter_velocity_mode;
        public int scaling_mode;
        public bool use_unscaled_time;
        public HomeMeteorCurve delay;
        public HomeMeteorCurve life;
        public HomeMeteorCurve velocity;
        public HomeMeteorCurve size;
        public HomeMeteorCurve size_y;
        public bool size3d;
        public HomeMeteorCurve rotation;
        public bool rotation3d;
        public HomeMeteorCurve rotation_x;
        public HomeMeteorCurve rotation_y;
        public HomeMeteorColor color;
        public HomeMeteorShape shape;
        public HomeMeteorCurve rate;
        public HomeMeteorBurst[] bursts;
        public int render_mode;
        public int alignment;
        public int sorting_layer;
        public int sorting_order;
        public int simulation_space;
        public HomeMeteorStretch stretch;
        public HomeMeteorMaterial material;
        public HomeMeteorModules modules;
        public string[] unconverted_modules;
    }

    [Serializable]
    public sealed class HomeMeteorCurve
    {
        public int mode;
        public float min;
        public float max;
        public HomeMeteorCurveKey[] low;
        public HomeMeteorCurveKey[] high;
    }

    [Serializable]
    public sealed class HomeMeteorCurveKey
    {
        public float time;
        public float value;
        public float inSlope;
        public float outSlope;
    }

    [Serializable]
    public sealed class HomeMeteorColor
    {
        public bool enabled;
        public int mode;
        public HomeMeteorColorValue min;
        public HomeMeteorColorValue max;
        public HomeMeteorGradientData low;
        public HomeMeteorGradientData high;
    }

    [Serializable]
    public sealed class HomeMeteorGradientData
    {
        public HomeMeteorGradientColorKey[] rgb;
        public HomeMeteorGradientAlphaKey[] alpha;
    }

    [Serializable]
    public sealed class HomeMeteorGradientColorKey
    {
        public float time;
        public float r;
        public float g;
        public float b;
    }

    [Serializable]
    public sealed class HomeMeteorGradientAlphaKey
    {
        public float time;
        public float alpha;
    }

    [Serializable]
    public sealed class HomeMeteorColorValue
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    [Serializable]
    public sealed class HomeMeteorVector3
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class HomeMeteorShape
    {
        public bool enabled;
        public int type;
        public float angle;
        public float length;
        public float radiusThickness;
        public float donutRadius;
        public float radius;
        public float arc;
        public HomeMeteorVector3 boxThickness;
        public HomeMeteorVector3 m_Position;
        public HomeMeteorVector3 m_Rotation;
        public HomeMeteorVector3 m_Scale;
    }

    [Serializable]
    public sealed class HomeMeteorBurst
    {
        public float time;
        public HomeMeteorCurve count;
        public int cycles;
        public float interval;
        public float probability;
    }

    [Serializable]
    public sealed class HomeMeteorModules
    {
        public HomeMeteorSizeModule SizeModule;
        public HomeMeteorVelocityModule VelocityModule;
        public HomeMeteorClampVelocityModule ClampVelocityModule;
        public HomeMeteorColor ColorModule;
        public HomeMeteorTrailModule TrailModule;
    }

    [Serializable]
    public sealed class HomeMeteorSizeModule
    {
        public bool enabled;
        public HomeMeteorCurve curve;
        public HomeMeteorCurve x;
        public HomeMeteorCurve y;
        public HomeMeteorCurve z;
        public bool separateAxes;
    }

    [Serializable]
    public sealed class HomeMeteorVelocityModule
    {
        public bool enabled;
        public bool inWorldSpace;
        public HomeMeteorCurve x;
        public HomeMeteorCurve y;
        public HomeMeteorCurve z;
        public HomeMeteorCurve orbitalZ;
        public HomeMeteorCurve radial;
        public HomeMeteorCurve speedModifier;
    }

    [Serializable]
    public sealed class HomeMeteorClampVelocityModule
    {
        public bool enabled;
        public bool inWorldSpace;
        public bool separateAxes;
        public bool multiplyDragByParticleSize;
        public bool multiplyDragByParticleVelocity;
        public HomeMeteorCurve magnitude;
        public HomeMeteorCurve drag;
        public float dampen;
    }

    [Serializable]
    public sealed class HomeMeteorTrailModule
    {
        public bool enabled;
        public int mode;
        public float ratio;
        public float minVertexDistance;
        public int textureMode;
        public int ribbonCount;
        public float shadowBias;
        public bool worldSpace;
        public bool dieWithParticles;
        public bool sizeAffectsWidth;
        public bool sizeAffectsLifetime;
        public bool inheritParticleColor;
        public bool generateLightingData;
        public bool splitSubEmitterRibbons;
        public bool attachRibbonsToTransform;
        public HomeMeteorVector3 textureScale;
        public HomeMeteorCurve lifetime;
        public HomeMeteorCurve widthOverTrail;
        public HomeMeteorColor colorOverLifetime;
        public HomeMeteorColor colorOverTrail;
        public HomeMeteorMaterial material;
    }

    [Serializable]
    public sealed class HomeMeteorStretch
    {
        public float m_LengthScale;
        public float m_VelocityScale;
        public float m_CameraVelocityScale;
    }

    [Serializable]
    public sealed class HomeMeteorMaterial
    {
        public string name;
        public HomeMeteorTextureBinding[] textures;
        public HomeMeteorFloatProperty[] floats;
        public HomeMeteorColorProperty[] colors;
    }

    [Serializable]
    public sealed class HomeMeteorTextureBinding
    {
        public string property;
        public string file;
        public float[] scale;
        public float[] offset;
    }

    [Serializable]
    public sealed class HomeMeteorFloatProperty
    {
        public string name;
        public float value;
    }

    [Serializable]
    public sealed class HomeMeteorColorProperty
    {
        public string name;
        public HomeMeteorColorValue value;
    }
}
