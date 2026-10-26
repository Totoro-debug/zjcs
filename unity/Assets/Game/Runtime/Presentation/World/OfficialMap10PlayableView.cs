using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10PlayableView : MonoBehaviour
    {
        private const string ShaderName = "Zjcs/Map10/Official Effect";
        private readonly List<GameObject> activePlayables = new List<GameObject>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();

        public void Play(string assetGuid, Transform target)
        {
            if (string.IsNullOrWhiteSpace(assetGuid)) throw new ArgumentException("A Map10 playable GUID is required.", nameof(assetGuid));
            TextAsset source = Resources.Load<TextAsset>("Map10/Playables/" + assetGuid);
            if (source == null) throw new InvalidDataException("Official Map10 playable resource is missing: " + assetGuid);
            if (target == null) throw new ArgumentNullException(nameof(target));

            PlayablePayload payload = JsonUtility.FromJson<PlayablePayload>(source.text);
            if (payload == null || payload.format != 1 || payload.mapClassId != 10
                || payload.assetGuid != assetGuid || payload.duration <= 0f
                || payload.systems == null || payload.systems.Length == 0
                || payload.materials == null || payload.meshes == null)
                throw new InvalidDataException("Official Map10 playable payload is malformed: " + assetGuid);

            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("Map10 Windows particle shader is unavailable: " + ShaderName);
            var root = new GameObject("Map10Playable_" + assetGuid);
            root.transform.SetParent(target, false);
            try
            {
                var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (PlayableMaterial data in payload.materials)
                    materials.Add(data.id, CreateMaterial(data, shader));
                var meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
                foreach (PlayableMesh data in payload.meshes)
                    meshes.Add(data.name, CreateMesh(data));

                foreach (ParticleRecord data in payload.systems)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.material_id)
                        || !materials.TryGetValue(data.material_id, out Material material))
                        throw new InvalidDataException("Official Map10 particle has no material: " + (data?.path ?? ""));
                    CreateParticle(data, material, meshes, root.transform);
                }

                activePlayables.Add(root);
                StartCoroutine(DestroyAfter(root, payload.duration));
            }
            catch
            {
                DestroyGenerated(root);
                throw;
            }
        }

        public static void Validate(TextAsset source, string assetGuid, out int particleCount, out int materialCount,
            out int meshCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            PlayablePayload payload = JsonUtility.FromJson<PlayablePayload>(source.text);
            if (payload == null || payload.format != 1 || payload.mapClassId != 10
                || payload.assetGuid != assetGuid || payload.duration <= 0f
                || payload.systems == null || payload.materials == null || payload.meshes == null)
                throw new InvalidDataException("Official Map10 playable payload is malformed: " + assetGuid);
            var materialIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableMaterial material in payload.materials)
                if (material == null || string.IsNullOrWhiteSpace(material.id) || !materialIds.Add(material.id)
                    || material.floats == null || material.colors == null || material.textures == null)
                    throw new InvalidDataException("Official Map10 playable material is malformed: " + assetGuid);
            foreach (ParticleRecord particle in payload.systems)
                if (particle == null || particle.matrix == null || particle.matrix.Length != 16
                    || !materialIds.Contains(particle.material_id) || particle.unconverted_modules != null
                    && particle.unconverted_modules.Length != 0)
                    throw new InvalidDataException("Official Map10 playable particle is malformed: " + assetGuid);
            particleCount = payload.systems.Length;
            materialCount = payload.materials.Length;
            meshCount = payload.meshes.Length;
        }

        private Material CreateMaterial(PlayableMaterial data, Shader shader)
        {
            var material = new Material(shader) { name = "Map10 " + data.name + " " + data.id };
            material.renderQueue = (int)RenderQueue.Transparent;
            generatedMaterials.Add(material);
            foreach (FloatProperty property in data.floats)
                if (material.HasProperty(property.name)) material.SetFloat(property.name, property.value);
            foreach (ColorProperty property in data.colors)
                if (material.HasProperty(property.name)) material.SetColor(property.name, ToColor(property.value));
            foreach (TextureBinding binding in data.textures)
            {
                Texture2D texture = Resources.Load<Texture2D>(binding.resource);
                if (texture == null)
                    throw new InvalidDataException("Official Map10 playable texture is missing: " + binding.resource);
                material.SetTexture(binding.property, texture);
                material.SetTextureScale(binding.property, ToVector2(binding.scale, Vector2.one));
                material.SetTextureOffset(binding.property, ToVector2(binding.offset, Vector2.zero));
            }
            return material;
        }

        private Mesh CreateMesh(PlayableMesh data)
        {
            if (data.vertices == null || data.vertices.Length == 0 || data.triangles == null
                || data.triangles.Length == 0 || data.triangles.Length % 3 != 0)
                throw new InvalidDataException("Official Map10 particle mesh is malformed: " + data.name);
            var mesh = new Mesh { name = "Map10 " + data.name };
            if (data.vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            var vertices = new Vector3[data.vertices.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = ToVector3(data.vertices[i]);
            mesh.vertices = vertices;
            if (data.uv != null && data.uv.Length == vertices.Length)
            {
                var uvs = new Vector2[data.uv.Length];
                for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(data.uv[i].x, data.uv[i].y);
                mesh.uv = uvs;
            }
            if (data.colors != null && data.colors.Length == vertices.Length)
            {
                var colors = new Color[data.colors.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = ToColor(data.colors[i]);
                mesh.colors = colors;
            }
            mesh.triangles = data.triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            generatedMeshes.Add(mesh);
            return mesh;
        }

        private void CreateParticle(ParticleRecord data, Material material, Dictionary<string, Mesh> meshes,
            Transform parent)
        {
            var particleObject = new GameObject(ParticleName(data.path));
            particleObject.transform.SetParent(parent, false);
            ApplyLocalMatrix(particleObject.transform, data.matrix);

            ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = Mathf.Max(0.01f, data.duration);
            main.loop = data.loop;
            main.prewarm = data.prewarm;
            main.playOnAwake = false;
            main.simulationSpeed = data.speed;
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
            renderer.sortingOrder = data.sorting_layer * 1000 + data.sorting_order;
            renderer.sharedMaterial = material;
            if (data.meshResources != null && data.meshResources.Length > 0)
            {
                var particleMeshes = new Mesh[data.meshResources.Length];
                for (int i = 0; i < particleMeshes.Length; i++)
                {
                    string key = data.meshResources[i].Substring(data.meshResources[i].LastIndexOf('/') + 1);
                    if (!meshes.TryGetValue(key, out particleMeshes[i]))
                        throw new InvalidDataException("Official Map10 particle mesh binding is missing: " + key);
                }
                renderer.SetMeshes(particleMeshes, particleMeshes.Length);
            }
            system.Play(true);
        }

        private static void ConfigureEmission(ParticleSystem system, ParticleRecord data)
        {
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = ToCurve(data.rate);
            var bursts = new ParticleSystem.Burst[data.bursts == null ? 0 : data.bursts.Length];
            for (int i = 0; i < bursts.Length; i++)
            {
                BurstRecord source = data.bursts[i];
                bursts[i] = new ParticleSystem.Burst(source.time, ToCurve(source.count),
                    Mathf.Max(1, source.cycles), source.interval);
                bursts[i].probability = Mathf.Clamp01(source.probability);
            }
            emission.SetBursts(bursts);
        }

        private static void ConfigureShape(ParticleSystem system, ShapeRecord data)
        {
            var shape = system.shape;
            shape.enabled = data.enabled;
            if (!data.enabled) return;
            shape.shapeType = (ParticleSystemShapeType)data.type;
            shape.angle = data.angle;
            shape.length = data.length;
            shape.radiusThickness = data.radiusThickness;
            shape.donutRadius = data.donutRadius;
            shape.radius = data.radius == null ? 0f : data.radius.value;
            shape.arc = data.arc == null ? 360f : data.arc.value;
            if (data.boxThickness != null) shape.scale = ToVector3(data.boxThickness);
            if (data.m_Position != null) shape.position = ToVector3(data.m_Position);
            if (data.m_Rotation != null) shape.rotation = ToVector3(data.m_Rotation);
            if (data.m_Scale != null) shape.scale = ToVector3(data.m_Scale);
        }

        private static void ConfigureModules(ParticleSystem system, ModuleRecord data)
        {
            if (data == null) return;
            if (data.SizeModule != null)
            {
                var module = system.sizeOverLifetime;
                module.enabled = true;
                module.separateAxes = data.SizeModule.separateAxes;
                module.size = ToCurve(data.SizeModule.curve);
                if (data.SizeModule.separateAxes)
                {
                    module.x = ToCurve(data.SizeModule.x);
                    module.y = ToCurve(data.SizeModule.y);
                    module.z = ToCurve(data.SizeModule.z);
                }
            }
            if (data.ColorModule != null)
            {
                var module = system.colorOverLifetime;
                module.enabled = true;
                module.color = ToMinMaxGradient(data.ColorModule);
            }
            if (data.RotationModule != null)
            {
                var module = system.rotationOverLifetime;
                module.enabled = true;
                module.separateAxes = data.RotationModule.separateAxes;
                if (module.separateAxes)
                {
                    module.x = ToCurve(data.RotationModule.x);
                    module.y = ToCurve(data.RotationModule.y);
                    module.z = ToCurve(data.RotationModule.z ?? data.RotationModule.curve);
                }
                else module.z = ToCurve(data.RotationModule.curve);
            }
            if (data.ClampVelocityModule != null)
            {
                var module = system.limitVelocityOverLifetime;
                module.enabled = true;
                module.limit = ToCurve(data.ClampVelocityModule.magnitude);
                module.dampen = data.ClampVelocityModule.dampen;
            }
            if (data.UVModule != null)
            {
                var module = system.textureSheetAnimation;
                module.enabled = true;
                module.numTilesX = Math.Max(1, data.UVModule.tilesX);
                module.numTilesY = Math.Max(1, data.UVModule.tilesY);
                module.cycleCount = Math.Max(1, Mathf.RoundToInt(data.UVModule.cycles));
                module.animation = (ParticleSystemAnimationType)data.UVModule.animationType;
                module.rowMode = (ParticleSystemAnimationRowMode)data.UVModule.rowMode;
                module.rowIndex = data.UVModule.rowIndex;
                module.frameOverTime = ToCurve(data.UVModule.frame);
                module.startFrame = ToCurve(data.UVModule.start);
            }
            if (data.NoiseModule != null)
            {
                var module = system.noise;
                module.enabled = true;
                module.separateAxes = data.NoiseModule.separateAxes;
                module.strength = ToCurve(data.NoiseModule.strength);
                module.frequency = data.NoiseModule.frequency;
                module.damping = data.NoiseModule.damping;
                module.octaveCount = Mathf.Clamp(data.NoiseModule.octaves, 1, 4);
                module.octaveMultiplier = data.NoiseModule.octaveMultiplier;
                module.octaveScale = data.NoiseModule.octaveScale;
                module.scrollSpeed = ToCurve(data.NoiseModule.scrollSpeed);
            }
        }

        private static ParticleSystem.MinMaxCurve ToCurve(CurveRecord data)
        {
            if (data == null) return new ParticleSystem.MinMaxCurve(0f);
            switch (data.mode)
            {
                case 0: return new ParticleSystem.MinMaxCurve(data.min);
                case 1: return new ParticleSystem.MinMaxCurve(data.max, ToAnimationCurve(data.high));
                case 2: return new ParticleSystem.MinMaxCurve(1f,
                    ToAnimationCurve(data.low), ToAnimationCurve(data.high));
                case 3: return new ParticleSystem.MinMaxCurve(data.min, data.max);
                default: throw new InvalidDataException("Unsupported official Map10 particle curve mode: " + data.mode);
            }
        }

        private static AnimationCurve ToAnimationCurve(CurveKey[] source)
        {
            if (source == null || source.Length == 0) return AnimationCurve.Linear(0f, 1f, 1f, 1f);
            var keys = new Keyframe[source.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                CurveKey key = source[i];
                keys[i] = new Keyframe(key.time, key.value, key.inSlope, key.outSlope);
            }
            return new AnimationCurve(keys);
        }

        private static ParticleSystem.MinMaxGradient ToMinMaxGradient(ColorRecord data)
        {
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
                default: throw new InvalidDataException("Unsupported official Map10 particle color mode: " + data.mode);
            }
        }

        private static ParticleSystem.MinMaxGradient ToMinMaxGradient(ColorModuleRecord data)
        {
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
                default: throw new InvalidDataException("Unsupported official Map10 particle color module mode: " + data.mode);
            }
        }

        private static Gradient ToGradient(GradientRecord data)
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

        private IEnumerator DestroyAfter(GameObject root, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            activePlayables.Remove(root);
            if (root != null) DestroyGenerated(root);
        }

        private void OnDestroy()
        {
            foreach (GameObject root in activePlayables) DestroyGenerated(root);
            activePlayables.Clear();
            foreach (Material material in generatedMaterials) DestroyGenerated(material);
            generatedMaterials.Clear();
            foreach (Mesh mesh in generatedMeshes) DestroyGenerated(mesh);
            generatedMeshes.Clear();
        }

        private static void DestroyGenerated(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private static Vector3 ToVector3(Vector3Record value) => new Vector3(value.x, value.y, value.z);
        private static Vector2 ToVector2(float[] value, Vector2 fallback) => value == null || value.Length < 2
            ? fallback : new Vector2(value[0], value[1]);
        private static Color ToColor(ColorValue value) => new Color(value.r, value.g, value.b, value.a);

        [Serializable] private sealed class PlayablePayload
        {
            public int format;
            public int mapClassId;
            public string assetGuid;
            public float duration;
            public ParticleRecord[] systems;
            public PlayableMaterial[] materials;
            public PlayableMesh[] meshes;
        }
        [Serializable] private sealed class ParticleRecord
        {
            public string path;
            public float[] matrix;
            public float duration;
            public bool loop;
            public bool prewarm;
            public float speed;
            public int max_particles;
            public CurveRecord delay;
            public CurveRecord life;
            public CurveRecord velocity;
            public CurveRecord size;
            public CurveRecord size_y;
            public bool size3d;
            public CurveRecord rotation;
            public bool rotation3d;
            public CurveRecord rotation_x;
            public CurveRecord rotation_y;
            public ColorRecord color;
            public ShapeRecord shape;
            public CurveRecord rate;
            public BurstRecord[] bursts;
            public int render_mode;
            public int alignment;
            public int sorting_layer;
            public int sorting_order;
            public ModuleRecord modules;
            public string material_id;
            public string[] meshResources;
            public string[] unconverted_modules;
        }
        [Serializable] private sealed class CurveRecord
        {
            public int mode;
            public float min;
            public float max;
            public CurveKey[] low;
            public CurveKey[] high;
        }
        [Serializable] private sealed class CurveKey
        {
            public float time;
            public float value;
            public float inSlope;
            public float outSlope;
        }
        [Serializable] private sealed class ColorRecord
        {
            public int mode;
            public ColorValue min;
            public ColorValue max;
            public GradientRecord low;
            public GradientRecord high;
        }
        [Serializable] private sealed class ColorModuleRecord
        {
            public int mode;
            public ColorValue min;
            public ColorValue max;
            public GradientRecord low;
            public GradientRecord high;
        }
        [Serializable] private sealed class GradientRecord
        {
            public GradientColorValue[] rgb;
            public GradientAlphaValue[] alpha;
        }
        [Serializable] private sealed class GradientColorValue
        {
            public float time;
            public float r;
            public float g;
            public float b;
        }
        [Serializable] private sealed class GradientAlphaValue
        {
            public float time;
            public float alpha;
        }
        [Serializable] private sealed class ColorValue
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }
        [Serializable] private sealed class Vector3Record
        {
            public float x;
            public float y;
            public float z;
        }
        [Serializable] private sealed class RadiusRecord { public float value; }
        [Serializable] private sealed class ShapeRecord
        {
            public bool enabled;
            public int type;
            public float angle;
            public float length;
            public float radiusThickness;
            public float donutRadius;
            public RadiusRecord radius;
            public RadiusRecord arc;
            public Vector3Record boxThickness;
            public Vector3Record m_Position;
            public Vector3Record m_Rotation;
            public Vector3Record m_Scale;
        }
        [Serializable] private sealed class BurstRecord
        {
            public float time;
            public CurveRecord count;
            public int cycles;
            public float interval;
            public float probability;
        }
        [Serializable] private sealed class ModuleRecord
        {
            public SizeModuleRecord SizeModule;
            public ColorModuleRecord ColorModule;
            public RotationModuleRecord RotationModule;
            public ClampVelocityModuleRecord ClampVelocityModule;
            public UVModuleRecord UVModule;
            public NoiseModuleRecord NoiseModule;
        }
        [Serializable] private sealed class SizeModuleRecord
        {
            public CurveRecord curve;
            public CurveRecord x;
            public CurveRecord y;
            public CurveRecord z;
            public bool separateAxes;
        }
        [Serializable] private sealed class RotationModuleRecord
        {
            public CurveRecord curve;
            public CurveRecord x;
            public CurveRecord y;
            public CurveRecord z;
            public bool separateAxes;
        }
        [Serializable] private sealed class ClampVelocityModuleRecord
        {
            public CurveRecord magnitude;
            public float dampen;
        }
        [Serializable] private sealed class UVModuleRecord
        {
            public int tilesX;
            public int tilesY;
            public float cycles;
            public int animationType;
            public int rowIndex;
            public int rowMode;
            public CurveRecord frame;
            public CurveRecord start;
        }
        [Serializable] private sealed class NoiseModuleRecord
        {
            public bool separateAxes;
            public float frequency;
            public bool damping;
            public int octaves;
            public float octaveMultiplier;
            public float octaveScale;
            public CurveRecord strength;
            public CurveRecord scrollSpeed;
        }
        [Serializable] private sealed class PlayableMaterial
        {
            public string id;
            public string name;
            public FloatProperty[] floats;
            public ColorProperty[] colors;
            public TextureBinding[] textures;
        }
        [Serializable] private sealed class FloatProperty { public string name; public float value; }
        [Serializable] private sealed class ColorProperty { public string name; public ColorValue value; }
        [Serializable] private sealed class TextureBinding
        {
            public string property;
            public string resource;
            public float[] scale;
            public float[] offset;
        }
        [Serializable] private sealed class PlayableMesh
        {
            public string name;
            public Vector3Record[] vertices;
            public Vector2Record[] uv;
            public ColorValue[] colors;
            public int[] triangles;
        }
        [Serializable] private sealed class Vector2Record { public float x; public float y; }
    }
}
