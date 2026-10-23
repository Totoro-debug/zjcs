using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10EnvironmentView : MonoBehaviour
    {
        private const string WaterPayloadResource = "Map10/Environment/map10-water-runtime";
        private const string ButterflyPayloadResource = "Map10/Environment/map10-butterflies-runtime";
        private const string WaterShaderResource = "World/Map10Water";
        private const string ButterflyShaderResource = "World/Map10Butterfly";

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly List<GameObject> generatedRoots = new List<GameObject>();
        private readonly List<ButterflyMotion> butterflyMotions = new List<ButterflyMotion>();

        public int WaterMeshCount { get; private set; }
        public int ButterflyCount { get; private set; }

        private void Update()
        {
            foreach (ButterflyMotion motion in butterflyMotions)
            {
                motion.elapsed += Time.deltaTime;
                float time = motion.loop ? motion.elapsed % motion.duration : Mathf.Min(motion.elapsed, motion.duration);
                ApplyMotion(motion, time);
            }
        }

        private void OnDestroy()
        {
            DestroyGenerated();
        }

        public void Initialize()
        {
            TextAsset water = Resources.Load<TextAsset>(WaterPayloadResource);
            TextAsset butterflies = Resources.Load<TextAsset>(ButterflyPayloadResource);
            if (water == null) throw new InvalidOperationException("Official Map10 water runtime payload is missing.");
            if (butterflies == null) throw new InvalidOperationException("Official Map10 butterfly runtime payload is missing.");
            Initialize(water, butterflies);
        }

        public void Initialize(TextAsset waterSource, TextAsset butterflySource)
        {
            if (waterSource == null) throw new ArgumentNullException(nameof(waterSource));
            if (butterflySource == null) throw new ArgumentNullException(nameof(butterflySource));
            DestroyGenerated();
            WaterPayload water = JsonUtility.FromJson<WaterPayload>(waterSource.text);
            ButterflyPayload butterflies = JsonUtility.FromJson<ButterflyPayload>(butterflySource.text);
            ValidatePayloads(water, butterflies);
            BuildWater(water);
            BuildButterflies(butterflies);
        }

        public static void ValidatePayloads(TextAsset waterSource, TextAsset butterflySource,
            out int waterCount, out int butterflyCount, out int animationCount)
        {
            if (waterSource == null) throw new ArgumentNullException(nameof(waterSource));
            if (butterflySource == null) throw new ArgumentNullException(nameof(butterflySource));
            WaterPayload water = JsonUtility.FromJson<WaterPayload>(waterSource.text);
            ButterflyPayload butterflies = JsonUtility.FromJson<ButterflyPayload>(butterflySource.text);
            ValidatePayloads(water, butterflies);
            waterCount = water.renderers.Length;
            butterflyCount = butterflies.instances.Length;
            var animations = new HashSet<string>(StringComparer.Ordinal);
            foreach (ButterflyInstance instance in butterflies.instances)
                animations.Add(instance.animationResource);
            animationCount = animations.Count;
        }

        private static void ValidatePayloads(WaterPayload water, ButterflyPayload butterflies)
        {
            if (water == null || water.format != 1 || water.mapClassId != 10 || water.renderers == null
                || water.renderers.Length != 4 || string.IsNullOrEmpty(water.maskResource)
                || water.maskRect == null || water.maskRect.Length != 4)
                throw new InvalidOperationException("Official Map10 water runtime payload is invalid.");
            foreach (WaterRendererPayload renderer in water.renderers)
            {
                if (renderer == null || string.IsNullOrEmpty(renderer.name) || renderer.vertices == null
                    || renderer.vertices.Length < 6 || renderer.vertices.Length % 2 != 0
                    || renderer.indices == null || renderer.indices.Length == 0 || renderer.indices.Length % 3 != 0)
                    throw new InvalidOperationException("Official Map10 water mesh payload is invalid.");
                int vertexCount = renderer.vertices.Length / 2;
                foreach (int index in renderer.indices)
                    if (index < 0 || index >= vertexCount)
                        throw new InvalidOperationException("Official Map10 water mesh index is invalid: " + renderer.name);
            }
            if (butterflies == null || butterflies.format != 1 || butterflies.mapClassId != 10
                || butterflies.instances == null || butterflies.instances.Length != 15)
                throw new InvalidOperationException("Official Map10 butterfly runtime payload is invalid.");
            foreach (ButterflyInstance instance in butterflies.instances)
            {
                if (instance == null || string.IsNullOrEmpty(instance.animationResource)
                    || instance.parentTransform == null || instance.motion == null || instance.motion.duration <= 0
                    || !instance.motion.loop || instance.motion.tracks == null || instance.motion.tracks.Length != 6
                    || instance.position == null || instance.scale == null || instance.tint == null || instance.tint.Length != 4)
                    throw new InvalidOperationException("Official Map10 butterfly instance is invalid: " + instance?.path);
                foreach (ButterflyMotionTrack track in instance.motion.tracks)
                    if (track == null || string.IsNullOrEmpty(track.property) || track.keys == null || track.keys.Length == 0)
                        throw new InvalidOperationException("Official Map10 butterfly motion curve is invalid: " + instance.path);
            }
        }

        private void BuildWater(WaterPayload payload)
        {
            Shader shader = Resources.Load<Shader>(WaterShaderResource);
            Texture2D waterMask = Resources.Load<Texture2D>(payload.maskResource);
            if (shader == null) throw new InvalidOperationException("Map10 Windows water shader is missing: " + WaterShaderResource);
            if (waterMask == null) throw new InvalidOperationException("Official Map10 water mask is missing: " + payload.maskResource);

            for (int i = 0; i < payload.renderers.Length; i++)
            {
                WaterRendererPayload source = payload.renderers[i];
                var root = new GameObject(source.name);
                root.transform.SetParent(transform, false);
                generatedRoots.Add(root);
                var mesh = new Mesh { name = source.name };
                generatedMeshes.Add(mesh);
                int vertexCount = source.vertices.Length / 2;
                var positions = new Vector3[vertexCount];
                var uvs = new Vector2[vertexCount];
                for (int vertex = 0; vertex < vertexCount; vertex++)
                {
                    float x = source.vertices[vertex * 2];
                    float y = source.vertices[vertex * 2 + 1];
                    positions[vertex] = new Vector3(x, y, 0f);
                    uvs[vertex] = new Vector2(x, y);
                }
                mesh.vertices = positions;
                mesh.uv = uvs;
                mesh.triangles = source.indices;
                mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh = mesh;

                var material = new Material(shader) { name = source.material + " " + source.name };
                generatedMaterials.Add(material);
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetTexture("_WaterMask", waterMask);
                material.SetVector("_MaskRect", new Vector4(payload.maskRect[0], payload.maskRect[1],
                    payload.maskRect[2], payload.maskRect[3]));
                foreach (WaterTextureBinding texture in source.textures)
                {
                    Texture2D value = Resources.Load<Texture2D>(texture.resource);
                    if (value == null)
                        throw new InvalidOperationException("Official Map10 water texture is missing: " + texture.resource);
                    material.SetTexture(texture.property, value);
                    if (texture.scale == null || texture.scale.Length != 2 || texture.offset == null || texture.offset.Length != 2)
                        throw new InvalidOperationException("Official Map10 water texture transform is invalid: " + texture.property);
                    material.SetTextureScale(texture.property, new Vector2(texture.scale[0], texture.scale[1]));
                    material.SetTextureOffset(texture.property, new Vector2(texture.offset[0], texture.offset[1]));
                }
                foreach (WaterColorBinding color in source.colors)
                {
                    if (color.value == null || color.value.Length != 4)
                        throw new InvalidOperationException("Official Map10 water color is invalid: " + color.property);
                    material.SetColor(color.property, new Color(color.value[0], color.value[1], color.value[2], color.value[3]));
                }
                foreach (WaterFloatBinding parameter in source.parameters)
                    material.SetFloat(parameter.property, parameter.value);

                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = payload.sortingOrder;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                WaterMeshCount++;
            }
        }

        private void BuildButterflies(ButterflyPayload payload)
        {
            Shader shader = Resources.Load<Shader>(ButterflyShaderResource);
            if (shader == null)
                throw new InvalidOperationException("Map10 Windows butterfly shader is missing: " + ButterflyShaderResource);

            foreach (ButterflyInstance source in payload.instances)
            {
                TextAsset animation = Resources.Load<TextAsset>(source.animationResource);
                if (animation == null)
                    throw new InvalidOperationException("Official Map10 butterfly animation is missing: " + source.animationResource);
                if (source.sortingOrder != 18003)
                    throw new InvalidOperationException("Official Map10 butterfly sorting order differs from its source: " + source.path);
                ValidateButterflyAnimation(animation, source.animationResource);

                var parent = new GameObject("Butterfly origin " + source.path);
                parent.transform.SetParent(transform, false);
                generatedRoots.Add(parent);
                parent.transform.localPosition = new Vector3(source.parentTransform.x, source.parentTransform.y,
                    source.parentTransform.z);
                parent.transform.localRotation = Quaternion.Euler(0f, 0f, source.parentTransform.rotationZ);
                parent.transform.localScale = new Vector3(source.parentTransform.scaleX, source.parentTransform.scaleY,
                    source.parentTransform.scaleZ);

                var animated = new GameObject("Butterfly motion " + source.path);
                animated.transform.SetParent(parent.transform, false);
                animated.transform.localPosition = new Vector3(source.position.x, source.position.y, source.position.z);
                animated.transform.localScale = new Vector3(source.scale.x, source.scale.y, source.scale.z);

                var meshObject = new GameObject("Butterfly Spine " + source.path);
                meshObject.transform.SetParent(animated.transform, false);
                OfficialMap10MonsterView spine = meshObject.AddComponent<OfficialMap10MonsterView>();
                var tint = new Color(source.tint[0], source.tint[1], source.tint[2], source.tint[3]);
                spine.InitializeAnimation(animation, shader, tint, 0,
                    source.intensity, source.alphaIntensity, source.clipHeight);

                var motion = new ButterflyMotion
                {
                    transform = animated.transform,
                    duration = source.motion.duration,
                    loop = source.motion.loop,
                    initialPosition = animated.transform.localPosition,
                    initialRotation = animated.transform.localEulerAngles,
                    tracks = CreateCurves(source.motion.tracks)
                };
                butterflyMotions.Add(motion);
                ApplyMotion(motion, 0f);
                ButterflyCount++;
            }
        }

        private static void ValidateButterflyAnimation(TextAsset source, string resource)
        {
            OfficialMap10MonsterView.ValidatePayload(source, 0, out string textureResource);
            if (Resources.Load<Texture2D>(textureResource) == null)
                throw new InvalidOperationException("Official Map10 butterfly atlas is missing: " + textureResource);
            if (string.IsNullOrWhiteSpace(resource))
                throw new InvalidOperationException("Official Map10 butterfly animation has no resource key.");
        }

        private static MotionCurve[] CreateCurves(ButterflyMotionTrack[] source)
        {
            var result = new MotionCurve[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var keys = new Keyframe[source[i].keys.Length];
                for (int key = 0; key < keys.Length; key++)
                {
                    ButterflyMotionKey value = source[i].keys[key];
                    keys[key] = new Keyframe(value.time, value.value, value.inTangent, value.outTangent);
                }
                var curve = new AnimationCurve(keys)
                {
                    preWrapMode = WrapMode.Loop,
                    postWrapMode = WrapMode.Loop
                };
                result[i] = new MotionCurve { property = source[i].property, curve = curve };
            }
            return result;
        }

        private static void ApplyMotion(ButterflyMotion motion, float time)
        {
            Vector3 position = motion.initialPosition;
            Vector3 rotation = motion.initialRotation;
            foreach (MotionCurve track in motion.tracks)
            {
                float value = track.curve.Evaluate(time);
                switch (track.property)
                {
                    case "position.x": position.x = value; break;
                    case "position.y": position.y = value; break;
                    case "position.z": position.z = value; break;
                    case "rotation.x": rotation.x = value; break;
                    case "rotation.y": rotation.y = value; break;
                    case "rotation.z": rotation.z = value; break;
                    default: throw new InvalidOperationException("Unsupported official Map10 butterfly motion property: " + track.property);
                }
            }
            motion.transform.localPosition = position;
            motion.transform.localEulerAngles = rotation;
        }

        private void DestroyGenerated()
        {
            butterflyMotions.Clear();
            for (int i = 0; i < generatedRoots.Count; i++) DestroyGeneratedObject(generatedRoots[i]);
            generatedRoots.Clear();
            for (int i = 0; i < generatedMeshes.Count; i++) DestroyGeneratedObject(generatedMeshes[i]);
            for (int i = 0; i < generatedMaterials.Count; i++) DestroyGeneratedObject(generatedMaterials[i]);
            generatedMeshes.Clear();
            generatedMaterials.Clear();
            WaterMeshCount = 0;
            ButterflyCount = 0;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        [Serializable]
        private sealed class WaterPayload
        {
            public int format;
            public int mapClassId;
            public int sortingOrder;
            public string maskResource;
            public float[] maskRect;
            public WaterRendererPayload[] renderers;
        }

        [Serializable]
        private sealed class WaterRendererPayload
        {
            public string name;
            public string material;
            public float[] vertices;
            public int[] indices;
            public WaterTextureBinding[] textures;
            public WaterColorBinding[] colors;
            public WaterFloatBinding[] parameters;
        }

        [Serializable]
        private sealed class WaterTextureBinding
        {
            public string property;
            public string resource;
            public float[] scale;
            public float[] offset;
        }

        [Serializable]
        private sealed class WaterColorBinding
        {
            public string property;
            public float[] value;
        }

        [Serializable]
        private sealed class WaterFloatBinding
        {
            public string property;
            public float value;
        }

        [Serializable]
        private sealed class ButterflyPayload
        {
            public int format;
            public int mapClassId;
            public ButterflyInstance[] instances;
        }

        [Serializable]
        private sealed class ButterflyInstance
        {
            public string path;
            public string animationResource;
            public ButterflyTransform parentTransform;
            public ButterflyMotionPayload motion;
            public ButterflyVector position;
            public ButterflyVector scale;
            public float[] tint;
            public float intensity;
            public float alphaIntensity;
            public float clipHeight;
            public int sortingOrder;
        }

        [Serializable]
        private sealed class ButterflyTransform
        {
            public float x;
            public float y;
            public float z;
            public float rotationZ;
            public float scaleX;
            public float scaleY;
            public float scaleZ;
        }

        [Serializable]
        private sealed class ButterflyVector
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private sealed class ButterflyMotionPayload
        {
            public float duration;
            public bool loop;
            public ButterflyMotionTrack[] tracks;
        }

        [Serializable]
        private sealed class ButterflyMotionTrack
        {
            public string property;
            public ButterflyMotionKey[] keys;
        }

        [Serializable]
        private sealed class ButterflyMotionKey
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
        }

        private sealed class ButterflyMotion
        {
            public Transform transform;
            public float elapsed;
            public float duration;
            public bool loop;
            public Vector3 initialPosition;
            public Vector3 initialRotation;
            public MotionCurve[] tracks;
        }

        private sealed class MotionCurve
        {
            public string property;
            public AnimationCurve curve;
        }
    }
}
