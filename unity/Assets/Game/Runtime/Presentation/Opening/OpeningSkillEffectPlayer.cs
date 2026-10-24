using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Runtime adapter for verified particle and Wwise payloads used by the opening record.
    /// </summary>
    public sealed class OpeningSkillEffectPlayer : IDisposable
    {
        private const string PayloadPath = "Opening/skill-effects.json";
        private const string TexturePath = "OpeningSkillEffects/Textures/";
        private const string AudioPath = "OpeningSkillEffects/Audio/";
        private const string ParticleShaderName = "Zjcs/Opening/NoiseMaskPro";
        private const string SpriteParticleShaderName = "Sprites/Default";
        private readonly Dictionary<string, List<Dictionary<string, object>>> effects =
            new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> meshHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        private readonly List<GameObject> roots = new List<GameObject>();
        private readonly Dictionary<int, List<GameObject>> entityRoots = new Dictionary<int, List<GameObject>>();
        private readonly Dictionary<int, List<GameObject>> statusRoots = new Dictionary<int, List<GameObject>>();
        private readonly List<Material> particleMaterials = new List<Material>();
        private readonly Action<string> log;
        private readonly Shader shader;
        private readonly Shader spriteShader;
        private int spawnedEffects;

        private OpeningSkillEffectPlayer(Action<string> log)
        {
            this.log = log ?? (_ => { });
            shader = Shader.Find(ParticleShaderName);
            spriteShader = Shader.Find(SpriteParticleShaderName);
        }

        public int SpawnedSystems { get; private set; }
        public int SpawnedSounds { get; private set; }
        public int SpawnedEffects => spawnedEffects;
        public int SpawnedStatusEffects => statusRoots.Count;
        public int PlayingSounds
        {
            get
            {
                int count = 0;
                foreach (GameObject root in roots)
                {
                    if (root == null) continue;
                    foreach (AudioSource source in root.GetComponents<AudioSource>())
                        if (source != null && source.isPlaying) count++;
                }
                return count;
            }
        }

        public static OpeningSkillEffectPlayer LoadDefault(Action<string> log)
        {
            string path = Path.Combine(Application.streamingAssetsPath, PayloadPath);
            if (!File.Exists(path))
            {
                log?.Invoke("开场技能特效载荷缺失: " + path);
                return null;
            }
            Dictionary<string, object> root = OpeningJson.Object(
                OpeningJson.Parse(File.ReadAllBytes(path)), "opening skill effects");
            if (OpeningJson.String(root, "manifest") != OpeningAssets.Manifest)
                throw new FormatException("Opening skill effect payload is not pinned.");
            var player = new OpeningSkillEffectPlayer(log);
            if (player.shader == null)
                throw new InvalidOperationException("Windows opening particle shader is unavailable: " + ParticleShaderName);
            foreach (object item in OpeningJson.Array(root["effects"], "effects"))
            {
                Dictionary<string, object> entry = OpeningJson.Object(item, "effect");
                string key = OpeningJson.String(entry, "key");
                if (!player.effects.TryGetValue(key, out List<Dictionary<string, object>> entries))
                {
                    entries = new List<Dictionary<string, object>>();
                    player.effects.Add(key, entries);
                }
                entries.Add(entry);
            }
            Dictionary<string, object> textureEntries = OpeningJson.Object(root["textures"], "effect textures");
            foreach (KeyValuePair<string, object> pair in textureEntries)
            {
                Dictionary<string, object> entry = OpeningJson.Object(pair.Value, "effect texture");
                string name = Path.GetFileNameWithoutExtension(pair.Key);
                Texture2D texture = OpeningJson.String(entry, "builtin") == "white"
                    ? Texture2D.whiteTexture
                    : Resources.Load<Texture2D>(TexturePath + name);
                if (texture == null) throw new InvalidOperationException("Official effect texture is missing: " + name);
                player.textures[name] = texture;
            }
            if (root.TryGetValue("meshes", out object rawMeshes))
            {
                Dictionary<string, object> meshEntries = OpeningJson.Object(rawMeshes, "effect meshes");
                foreach (KeyValuePair<string, object> pair in meshEntries)
                {
                    Dictionary<string, object> entry = OpeningJson.Object(pair.Value, "effect mesh");
                    player.meshHashes[pair.Key] = OpeningJson.String(entry, "sha256");
                }
            }
            if (root.TryGetValue("sounds", out object rawSounds))
            {
                Dictionary<string, object> sounds = OpeningJson.Object(rawSounds, "effect sounds");
                foreach (string file in sounds.Keys)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    AudioClip clip = Resources.Load<AudioClip>(AudioPath + name);
                    if (clip == null) throw new InvalidOperationException("Official effect audio is missing: " + file);
                    player.audioClips[name] = clip;
                }
            }
            return player;
        }

        public IEnumerable<Dictionary<string, object>> GetEvents(string effectType, int classId)
        {
            string key = (effectType ?? string.Empty) + ":" + classId;
            if (effects.TryGetValue(key, out List<Dictionary<string, object>> entries)) return entries;
            return Array.Empty<Dictionary<string, object>>();
        }

        public GameObject CreateSpecialParticle(Dictionary<string, object> data, Transform parent, float eventSpeed)
        {
            if (data == null || parent == null) return null;
            return CreateParticle(data, parent, 0f, eventSpeed, false);
        }

        public bool Play(string effectType, int classId, Vector3 worldPosition, Transform parent, int ownerEntityId = 0,
            Spine.Unity.SkeletonAnimation sourceUnit = null, Spine.Unity.SkeletonAnimation targetUnit = null,
            Camera camera = null, Vector3 sourcePosition = default, Vector3 targetPosition = default,
            Quaternion skillRotation = default)
        {
            bool played = false;
            OpeningSpecialEffectRuntime special = null;
            foreach (Dictionary<string, object> entry in GetEvents(effectType, classId))
            {
                string kind = OpeningJson.String(entry, "kind");
                if (kind == "special")
                {
                    special ??= OpeningSpecialEffectRuntime.GetOrCreate(parent, camera);
                    Material specialMaterial = null;
                    Dictionary<string, object> sourceEvent = SourceEvent(entry);
                    if (sourceEvent.TryGetValue("fields", out object rawFields)
                        && rawFields is Dictionary<string, object> fields
                        && fields.ContainsKey("mStartAttachPos")
                        && sourceEvent.TryGetValue("grappling_line", out object rawLine)
                        && rawLine is Dictionary<string, object> lineData)
                    {
                        Dictionary<string, object> materialData = OpeningJson.Object(lineData["material"], "grapple material");
                        specialMaterial = new Material(shader) { name = OpeningJson.String(materialData, "name") };
                        ConfigureMaterial(specialMaterial, materialData, materialData);
                        particleMaterials.Add(specialMaterial);
                    }
                    played |= special.Play(entry, sourceUnit, targetUnit, sourcePosition, targetPosition,
                        skillRotation, this, specialMaterial,
                        eventSpeed: (float)OpeningJson.Number(entry, "eventSpeed", 1f));
                    continue;
                }
                if (kind != "particles" && kind != "sound") continue;
                Spine.Unity.SkeletonAnimation attachment = ResolveAttachTarget(entry, sourceUnit, targetUnit);
                played |= PlayEntry(entry, worldPosition, parent, attachment, ownerEntityId, false) != null;
            }
            return played;
        }

        public bool PlayStatus(int statusEntityId, int classId, Spine.Unity.SkeletonAnimation unit)
        {
            if (unit == null) return false;
            bool played = false;
            foreach (Dictionary<string, object> entry in GetEvents("status", classId))
            {
                if (OpeningJson.String(entry, "playable_field") != "myPlayablePrefab") continue;
                string kind = OpeningJson.String(entry, "kind");
                if (kind != "particles" && kind != "sound") continue;
                played |= PlayEntry(entry, unit.transform.position, unit.transform, unit,
                    statusEntityId, true) != null;
            }
            return played;
        }

        private GameObject PlayEntry(Dictionary<string, object> entry, Vector3 worldPosition, Transform parent,
            Spine.Unity.SkeletonAnimation followTarget, int ownerEntityId, bool statusEffect)
        {
            string kind = OpeningJson.String(entry, "kind");
            var root = new GameObject("OpeningEffect_" + OpeningJson.Int(entry, "classId") + "_" + kind);
            root.transform.SetParent(parent, false);
            if (followTarget != null)
            {
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                ConfigureBoneFollower(root, entry, followTarget);
            }
            else root.transform.position = worldPosition;

            Transform content = root.transform;
            if (followTarget != null)
            {
                var offset = new GameObject("Attachment");
                offset.transform.SetParent(root.transform, false);
                ApplyAttachment(offset.transform, entry);
                content = offset.transform;
            }

            float lifetime = 0f;
            int systemsBefore = SpawnedSystems;
            if (kind == "particles")
            {
                float speed = Mathf.Max(0.01f, (float)OpeningJson.Number(entry, "eventSpeed", 1f));
                float delay = Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime")) / speed;
                foreach (object item in OpeningJson.Array(entry["particles"], "effect particles"))
                {
                    Dictionary<string, object> data = OpeningJson.Object(item, "effect particle");
                    GameObject child = CreateParticle(data, content, delay, speed);
                    if (child == null) continue;
                    lifetime = Mathf.Max(lifetime, delay + ParticleLifetime(data) / speed);
                }
            }
            else if (kind == "sound")
            {
                lifetime = PlaySound(entry, root);
            }

            bool hasParticles = SpawnedSystems > systemsBefore;
            bool hasSound = kind == "sound" && root.GetComponent<AudioSource>() != null;
            if (!hasParticles && !hasSound)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            if (!statusEffect)
            {
                var cleanup = root.AddComponent<OpeningDelayedDestroy>();
                cleanup.Delay = Mathf.Max(1f, lifetime + 0.25f);
            }
            TrackRoot(root, ownerEntityId, statusEffect);
            return root;
        }

        private void ConfigureBoneFollower(GameObject root, Dictionary<string, object> entry,
            Spine.Unity.SkeletonAnimation target)
        {
            Dictionary<string, object> source = SourceEvent(entry);
            Dictionary<string, object> attach = source.TryGetValue("attach", out object rawAttach)
                && rawAttach is Dictionary<string, object> value ? value : null;
            Dictionary<string, object> follow = source.TryGetValue("follow", out object rawFollow)
                && rawFollow is Dictionary<string, object> flags ? flags : null;
            var follower = root.AddComponent<Spine.Unity.BoneFollower>();
            follower.skeletonRenderer = target;
            follower.boneName = attach != null && attach.TryGetValue("myBoneName", out object rawBone)
                ? Convert.ToString(rawBone) : "body";
            follower.followXYPosition = Flag(follow, "IsFollowPos", true);
            follower.followZPosition = true;
            follower.followBoneRotation = Flag(follow, "IsFollowRot", false);
            follower.followLocalScale = Flag(follow, "IsFollowScale", false);
            follower.Initialize();
        }

        private static void ApplyAttachment(Transform transform, Dictionary<string, object> entry)
        {
            Dictionary<string, object> source = SourceEvent(entry);
            if (!source.TryGetValue("attach", out object rawAttach)
                || !(rawAttach is Dictionary<string, object> attach)) return;
            if (attach.TryGetValue("myOffset", out object rawOffset)
                && rawOffset is Dictionary<string, object> offset)
                transform.localPosition = new Vector3((float)OpeningJson.Number(offset, "x"),
                    (float)OpeningJson.Number(offset, "y"), (float)OpeningJson.Number(offset, "z"));
            if (attach.TryGetValue("myRotation", out object rawRotation)
                && rawRotation is Dictionary<string, object> rotation)
                transform.localRotation = new Quaternion((float)OpeningJson.Number(rotation, "x"),
                    (float)OpeningJson.Number(rotation, "y"), (float)OpeningJson.Number(rotation, "z"),
                    (float)OpeningJson.Number(rotation, "w", 1f));
            transform.localScale = Vector3.one * (float)OpeningJson.Number(attach, "myScale", 1f);
        }

        private float PlaySound(Dictionary<string, object> entry, GameObject root)
        {
            List<object> variants = OpeningJson.Array(entry["variants"], "sound variants");
            if (variants.Count == 0) return 0f;
            Dictionary<string, object> variant = SelectVariant(variants);
            string resource = OpeningJson.String(variant, "resource");
            if (!audioClips.TryGetValue(resource, out AudioClip clip) || clip == null)
            {
                log("开场战斗音频未绑定: " + resource);
                return 0f;
            }
            float speed = Mathf.Clamp((float)OpeningJson.Number(entry, "eventSpeed", 1f), 0.01f, 3f);
            float offset = Mathf.Max(0f, (float)OpeningJson.Number(variant, "loop_offset"));
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clip;
            source.pitch = speed;
            source.loop = OpeningJson.Bool(variant, "loop");
            source.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clip.length - 0.01f));
            float delay = Mathf.Max(0f, (float)OpeningJson.Number(entry, "eventTime")) / speed;
            source.PlayDelayed(delay);
            SpawnedSounds++;
            return delay + Mathf.Max(0f, clip.length - offset) / speed;
        }

        private static Dictionary<string, object> SelectVariant(List<object> variants)
        {
            float total = 0f;
            foreach (object item in variants)
            {
                Dictionary<string, object> variant = OpeningJson.Object(item, "sound variant");
                total += Mathf.Max(0f, (float)OpeningJson.Number(variant, "weight", 1f));
            }
            if (total <= 0f) return OpeningJson.Object(variants[0], "sound variant");
            float selected = UnityEngine.Random.value * total;
            foreach (object item in variants)
            {
                Dictionary<string, object> variant = OpeningJson.Object(item, "sound variant");
                selected -= Mathf.Max(0f, (float)OpeningJson.Number(variant, "weight", 1f));
                if (selected <= 0f) return variant;
            }
            return OpeningJson.Object(variants[variants.Count - 1], "sound variant");
        }

        private static Dictionary<string, object> SourceEvent(Dictionary<string, object> entry)
        {
            return entry.TryGetValue("source_event", out object raw)
                && raw is Dictionary<string, object> source ? source : new Dictionary<string, object>();
        }

        private static Spine.Unity.SkeletonAnimation ResolveAttachTarget(Dictionary<string, object> entry,
            Spine.Unity.SkeletonAnimation source, Spine.Unity.SkeletonAnimation target)
        {
            Dictionary<string, object> sourceEvent = SourceEvent(entry);
            if (!sourceEvent.TryGetValue("attach", out object rawAttach)
                || !(rawAttach is Dictionary<string, object> attach)) return null;
            switch (OpeningJson.Int(attach, "AttachTo", -1))
            {
                case 0: return source;
                case 1: return target;
                default: return null;
            }
        }

        private static bool Flag(Dictionary<string, object> owner, string key, bool fallback)
        {
            if (owner == null || !owner.TryGetValue(key, out object value)) return fallback;
            if (value is bool flag) return flag;
            return OpeningJson.ToNumber(value) != 0;
        }

        private void TrackRoot(GameObject root, int ownerEntityId, bool statusEffect)
        {
            roots.Add(root);
            spawnedEffects++;
            if (ownerEntityId <= 0) return;
            Dictionary<int, List<GameObject>> owner = statusEffect ? statusRoots : entityRoots;
            if (!owner.TryGetValue(ownerEntityId, out List<GameObject> values))
            {
                values = new List<GameObject>();
                owner.Add(ownerEntityId, values);
            }
            values.Add(root);
        }

        public void StopEntity(int entityId) => DestroyOwnedRoots(entityRoots, entityId);
        public void StopStatus(int entityId) => DestroyOwnedRoots(statusRoots, entityId);

        private void DestroyOwnedRoots(Dictionary<int, List<GameObject>> collection, int entityId)
        {
            if (!collection.TryGetValue(entityId, out List<GameObject> values)) return;
            foreach (GameObject root in values)
                if (root != null) UnityEngine.Object.Destroy(root);
            collection.Remove(entityId);
        }

        private GameObject CreateParticle(Dictionary<string, object> data, Transform parent, float eventDelay,
            float eventSpeed, bool play = true, int recursionDepth = 0)
        {
            if (recursionDepth > 4) throw new InvalidDataException("Opening particle sub-emitter nesting is too deep.");
            var item = new GameObject("SkillParticle_" + SpawnedSystems.ToString("D2"));
            item.transform.SetParent(parent, false);
            ApplyMatrix(item.transform, OpeningJson.Array(data["matrix"], "effect matrix"));
            ParticleSystem system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = (float)OpeningJson.Number(data, "duration", 1f);
            main.loop = OpeningJson.Bool(data, "loop");
            main.playOnAwake = false;
            main.prewarm = OpeningJson.Bool(data, "prewarm");
            main.simulationSpeed = (float)OpeningJson.Number(data, "speed", 1f) * eventSpeed;
            main.startDelay = ModuleCurve(data, "delay", 0f, eventDelay);
            main.maxParticles = Mathf.Max(1, OpeningJson.Int(data, "max_particles", 32));
            main.startLifetime = Curve(data, "life", 0.5f);
            main.startSpeed = Curve(data, "velocity", 0f);
            main.startSize = Curve(data, "size", 1f);
            main.startRotation = Curve(data, "rotation", 0f);
            main.startColor = ColorRange(data, "color");
            main.startSize3D = OpeningJson.Bool(data, "size3d");
            if (main.startSize3D)
            {
                main.startSizeX = Curve(data, "size", 1f);
                main.startSizeY = Curve(data, "size_y", 1f);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            }
            main.startRotation3D = OpeningJson.Bool(data, "rotation3d");
            if (main.startRotation3D)
            {
                main.startRotationX = Curve(data, "rotation_x", 0f);
                main.startRotationY = Curve(data, "rotation_y", 0f);
            }
            main.simulationSpace = (ParticleSystemSimulationSpace)OpeningJson.Int(data, "simulation_space");
            main.scalingMode = (ParticleSystemScalingMode)OpeningJson.Int(data, "scaling_mode");

            var emission = system.emission;
            Dictionary<string, object> rate = OpeningJson.Object(data["rate"], "effect rate");
            emission.rateOverTime = Curve(data, "rate", 0f);
            List<object> bursts = OpeningJson.Array(data["bursts"], "effect bursts");
            if (bursts.Count > 0)
            {
                var values = new ParticleSystem.Burst[bursts.Count];
                for (int i = 0; i < bursts.Count; i++)
                {
                    Dictionary<string, object> burst = OpeningJson.Object(bursts[i], "effect burst");
                    values[i] = new ParticleSystem.Burst((float)OpeningJson.Number(burst, "time"),
                        ModuleCurve(burst, "count", 0f),
                        (short)Mathf.Clamp(OpeningJson.Int(burst, "cycles", 1), 1, short.MaxValue),
                        (float)OpeningJson.Number(burst, "interval", 0.01f));
                    values[i].probability = Mathf.Clamp01((float)OpeningJson.Number(burst, "probability", 1f));
                }
                emission.SetBursts(values);
            }

            Dictionary<string, object> shapeData = OpeningJson.Object(data["shape"], "effect shape");
            var shape = system.shape;
            shape.enabled = OpeningJson.Bool(shapeData, "enabled");
            shape.shapeType = (ParticleSystemShapeType)OpeningJson.Int(shapeData, "type");
            shape.angle = (float)OpeningJson.Number(shapeData, "angle");
            shape.length = (float)OpeningJson.Number(shapeData, "length");
            Dictionary<string, object> radius = OpeningJson.Object(shapeData["radius"], "shape radius");
            shape.radius = (float)OpeningJson.Number(radius, "value");
            shape.radiusMode = (ParticleSystemShapeMultiModeValue)OpeningJson.Int(radius, "mode");
            shape.radiusSpread = (float)OpeningJson.Number(radius, "spread");
            shape.radiusSpeed = SerializedCurve(OpeningJson.Object(radius["speed"], "shape radius speed"), 1f);
            shape.radiusThickness = (float)OpeningJson.Number(shapeData, "radiusThickness", 1f);
            shape.donutRadius = (float)OpeningJson.Number(shapeData, "donutRadius", 0.2f);
            Dictionary<string, object> arc = OpeningJson.Object(shapeData["arc"], "shape arc");
            shape.arc = (float)OpeningJson.Number(arc, "value", 360f);
            shape.arcMode = (ParticleSystemShapeMultiModeValue)OpeningJson.Int(arc, "mode");
            shape.arcSpread = (float)OpeningJson.Number(arc, "spread");
            shape.arcSpeed = SerializedCurve(OpeningJson.Object(arc["speed"], "shape arc speed"), 1f);
            shape.randomDirectionAmount = (float)OpeningJson.Number(shapeData, "randomDirectionAmount");
            shape.sphericalDirectionAmount = (float)OpeningJson.Number(shapeData, "sphericalDirectionAmount");
            shape.randomPositionAmount = (float)OpeningJson.Number(shapeData, "randomPositionAmount");
            shape.alignToDirection = OpeningJson.Bool(shapeData, "alignToDirection");
            shape.position = Vector3Value(shapeData, "m_Position");
            shape.rotation = Vector3Value(shapeData, "m_Rotation");
            shape.scale = Vector3Value(shapeData, "m_Scale", Vector3.one);
            shape.boxThickness = Vector3Value(shapeData, "boxThickness");
            shape.meshMaterialIndex = OpeningJson.Int(shapeData, "m_MeshMaterialIndex");
            shape.normalOffset = (float)OpeningJson.Number(shapeData, "m_MeshNormalOffset");
            shape.useMeshMaterialIndex = OpeningJson.Bool(shapeData, "m_UseMeshMaterialIndex");
            shape.useMeshColors = OpeningJson.Bool(shapeData, "m_UseMeshColors", true);
            shape.meshShapeType = (ParticleSystemMeshShapeType)OpeningJson.Int(shapeData, "placementMode");
            Dictionary<string, object> meshSpawn = OpeningJson.Object(shapeData["m_MeshSpawn"], "shape mesh spawn");
            shape.meshSpawnMode = (ParticleSystemShapeMultiModeValue)OpeningJson.Int(meshSpawn, "mode");
            shape.meshSpawnSpread = (float)OpeningJson.Number(meshSpawn, "spread");
            shape.meshSpawnSpeed = SerializedCurve(OpeningJson.Object(meshSpawn["speed"], "shape mesh spawn speed"), 1f);
            if (data.TryGetValue("shapeMeshResource", out object rawShapeMesh) && rawShapeMesh is string shapeMeshName)
                shape.mesh = LoadMesh(shapeMeshName);

            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = (ParticleSystemRenderMode)OpeningJson.Int(data, "render_mode");
            renderer.alignment = (ParticleSystemRenderSpace)OpeningJson.Int(data, "alignment");
            // The source field is a layer index from the Android project, not a Unity
            // SortingLayer unique ID. This project intentionally uses the default layer.
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 25000 + OpeningJson.Int(data, "sorting_order");
            if (data.TryGetValue("meshResources", out object rawMeshResources) && rawMeshResources is List<object> meshResources)
            {
                var rendererMeshes = new List<Mesh>();
                foreach (object rawMeshName in meshResources)
                    if (rawMeshName is string meshName && LoadMesh(meshName) is Mesh mesh) rendererMeshes.Add(mesh);
                if (rendererMeshes.Count > 0) renderer.SetMeshes(rendererMeshes.ToArray(), rendererMeshes.Count);
            }
            Dictionary<string, object> materialData = OpeningJson.Object(data["material"], "effect material");
            Material particleMaterial = new Material(shader) { name = OpeningJson.String(materialData, "name") };
            particleMaterial.shader = ParticleShader(materialData);
            particleMaterials.Add(particleMaterial);
            ConfigureMaterial(particleMaterial, materialData, data);
            renderer.sharedMaterial = particleMaterial;
            ConfigureModules(system, data, item.transform, eventDelay, eventSpeed, recursionDepth);
            // The source value is an Android sorting-layer index, not this project's unique layer ID.
            renderer.sortingLayerID = 0;
            if (play) system.Play();
            SpawnedSystems++;
            return item;
        }

        private void ConfigureMaterial(Material material, Dictionary<string, object> source,
            Dictionary<string, object> particle)
        {
            Dictionary<string, object> materialTextures = OpeningJson.Object(source["textures"], "material textures");
            Dictionary<string, object> resources = particle.TryGetValue("textureResources", out object rawResources)
                && rawResources is Dictionary<string, object> mapped
                ? mapped
                : source.TryGetValue("textureResources", out object materialResources)
                    && materialResources is Dictionary<string, object> materialMap
                    ? materialMap : new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in materialTextures)
            {
                Dictionary<string, object> textureInfo = OpeningJson.Object(pair.Value, "material texture");
                string resource = resources.TryGetValue(pair.Key, out object mappedName)
                    ? Convert.ToString(mappedName)
                    : Path.GetFileNameWithoutExtension(OpeningJson.String(textureInfo, "file"));
                if (!textures.TryGetValue(resource, out Texture2D texture) || texture == null)
                {
                    log("开场粒子材质贴图未绑定: " + pair.Key + " / " + resource);
                    continue;
                }
                if (!material.HasProperty(pair.Key)) continue;
                material.SetTexture(pair.Key, texture);
                if (textureInfo.TryGetValue("scale", out object rawScale) && rawScale is List<object> scale && scale.Count >= 2)
                    material.SetTextureScale(pair.Key, new Vector2((float)OpeningJson.ToNumber(scale[0]),
                        (float)OpeningJson.ToNumber(scale[1])));
                if (textureInfo.TryGetValue("offset", out object rawOffset) && rawOffset is List<object> offset && offset.Count >= 2)
                    material.SetTextureOffset(pair.Key, new Vector2((float)OpeningJson.ToNumber(offset[0]),
                        (float)OpeningJson.ToNumber(offset[1])));
            }
            Dictionary<string, object> floats = OpeningJson.Object(source["floats"], "material floats");
            foreach (KeyValuePair<string, object> pair in floats)
                if (material.HasProperty(pair.Key)) material.SetFloat(pair.Key, (float)OpeningJson.ToNumber(pair.Value));
            Dictionary<string, object> colors = OpeningJson.Object(source["colors"], "material colors");
            foreach (KeyValuePair<string, object> pair in colors)
                if (material.HasProperty(pair.Key) && pair.Value is List<object> values && values.Count >= 4)
                    material.SetColor(pair.Key, new Color((float)OpeningJson.ToNumber(values[0]),
                        (float)OpeningJson.ToNumber(values[1]), (float)OpeningJson.ToNumber(values[2]),
                        (float)OpeningJson.ToNumber(values[3])));
            SetFeature(material, source, "USE_MAINTEX", "_UseMainTex", materialTextures.ContainsKey("_MainTex"));
            SetFeature(material, source, "USE_NOISE", "_UseNoise", materialTextures.ContainsKey("_NoiseTex"));
            SetFeature(material, source, "USE_MASK", "_UseMask", materialTextures.ContainsKey("_MaskTex"));
            SetFeature(material, source, "USE_DISTORT", "_UseDistortion",
                materialTextures.ContainsKey("_DistortionTex"));
            SetFeature(material, source, "USE_DISSOLVE", "_UseDissolve",
                materialTextures.ContainsKey("_DissolveTex"));
            SetFeature(material, source, "USE_DISSOLVE2", "_EnableDissolve2",
                OpeningJson.Number(floats, "_UseDissolve2") > 0.5d);
            SetFeature(material, source, "USE_PIXEL", "_UsePixel",
                OpeningJson.Number(floats, "_PixelSize") > 0.000001d);
        }

        private Shader ParticleShader(Dictionary<string, object> materialData)
        {
            string runtimeShader = materialData.TryGetValue("runtimeShader", out object rawRuntimeShader)
                ? Convert.ToString(rawRuntimeShader) : ParticleShaderName;
            if (string.Equals(runtimeShader, SpriteParticleShaderName, StringComparison.Ordinal))
            {
                if (spriteShader == null)
                    throw new InvalidOperationException("Windows sprite particle shader is unavailable: "
                        + SpriteParticleShaderName);
                return spriteShader;
            }
            if (!string.Equals(runtimeShader, ParticleShaderName, StringComparison.Ordinal))
                throw new InvalidDataException("Opening particle requests an unsupported Windows shader: " + runtimeShader);
            return shader;
        }

        private static void SetFeature(Material material, Dictionary<string, object> source,
            string keyword, string property, bool fallback)
        {
            if (!material.HasProperty(property)) return;
            bool enabled = fallback;
            if (source.TryGetValue("shaderKeywords", out object rawKeywords) && rawKeywords is List<object> keywords)
            {
                enabled = false;
                foreach (object rawKeyword in keywords)
                    if (string.Equals(Convert.ToString(rawKeyword), keyword, StringComparison.Ordinal))
                    {
                        enabled = true;
                        break;
                    }
            }
            material.SetFloat(property, enabled ? 1f : 0f);
        }

        private Mesh LoadMesh(string name)
        {
            if (meshes.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            if (Path.GetFileName(name) != name || !meshHashes.TryGetValue(name, out string expectedHash))
                throw new InvalidDataException("Opening particle references an unmanifested mesh: " + name);
            string path = Path.Combine(Application.streamingAssetsPath, "Opening", "Meshes", name);
            byte[] bytes = File.ReadAllBytes(path);
            using (SHA256 sha = SHA256.Create())
            {
                string actualHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Opening particle mesh hash differs from manifest: " + name);
            }

            Dictionary<string, object> data = OpeningJson.Object(OpeningJson.Parse(bytes), "opening mesh");
            List<object> rawVertices = OpeningJson.Array(data["vertices"], "mesh vertices");
            var vertices = new Vector3[rawVertices.Count];
            for (int index = 0; index < vertices.Length; index++)
            {
                List<object> row = OpeningJson.Array(rawVertices[index], "mesh vertex");
                vertices[index] = new Vector3((float)OpeningJson.ToNumber(row[0]),
                    (float)OpeningJson.ToNumber(row[1]), (float)OpeningJson.ToNumber(row[2]));
            }
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(name)) };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            if (data.TryGetValue("uv", out object rawUv))
            {
                List<object> uvRows = OpeningJson.Array(rawUv, "mesh uv");
                if (uvRows.Count == vertices.Length)
                {
                    var uv = new Vector2[uvRows.Count];
                    for (int index = 0; index < uv.Length; index++)
                    {
                        List<object> row = OpeningJson.Array(uvRows[index], "mesh uv row");
                        uv[index] = new Vector2((float)OpeningJson.ToNumber(row[0]), (float)OpeningJson.ToNumber(row[1]));
                    }
                    mesh.uv = uv;
                }
            }
            if (data.TryGetValue("colors", out object rawColors))
            {
                List<object> colorRows = OpeningJson.Array(rawColors, "mesh colors");
                if (colorRows.Count == vertices.Length)
                {
                    var colors = new Color[colorRows.Count];
                    for (int index = 0; index < colors.Length; index++)
                    {
                        List<object> row = OpeningJson.Array(colorRows[index], "mesh color");
                        colors[index] = new Color((float)OpeningJson.ToNumber(row[0]),
                            (float)OpeningJson.ToNumber(row[1]), (float)OpeningJson.ToNumber(row[2]),
                            row.Count > 3 ? (float)OpeningJson.ToNumber(row[3]) : 1f);
                    }
                    mesh.colors = colors;
                }
            }
            List<object> rawTriangles = OpeningJson.Array(data["triangles"], "mesh triangles");
            if (rawTriangles.Count > 0 && rawTriangles[0] is List<object> first && first.Count > 0
                && first[0] is List<object>)
            {
                for (int submesh = 0; submesh < rawTriangles.Count; submesh++)
                    mesh.SetTriangles(ReadTriangles(OpeningJson.Array(rawTriangles[submesh], "mesh submesh")), submesh);
            }
            else mesh.SetTriangles(ReadTriangles(rawTriangles), 0);
            mesh.RecalculateBounds();
            meshes[name] = mesh;
            return mesh;
        }

        private static int[] ReadTriangles(List<object> rows)
        {
            var indices = new List<int>(rows.Count * 3);
            foreach (object rawRow in rows)
            {
                List<object> row = OpeningJson.Array(rawRow, "mesh triangle");
                foreach (object rawIndex in row) indices.Add(Convert.ToInt32(OpeningJson.ToNumber(rawIndex)));
            }
            return indices.ToArray();
        }

        private void ConfigureModules(ParticleSystem system, Dictionary<string, object> data, Transform parent,
            float eventDelay, float eventSpeed, int recursionDepth)
        {
            Dictionary<string, object> modules = data.TryGetValue("modules", out object rawModules)
                && rawModules is Dictionary<string, object> values
                ? values : new Dictionary<string, object>();
            if (TryModule(modules, "SizeModule", out Dictionary<string, object> sizeData))
            {
                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.separateAxes = OpeningJson.Bool(sizeData, "separateAxes");
                size.size = ModuleCurve(sizeData, "curve", 1f);
                if (size.separateAxes)
                {
                    size.x = ModuleCurve(sizeData, "curve", 1f);
                    size.y = ModuleCurve(sizeData, "y", 1f);
                }
            }
            if (TryModule(modules, "ColorModule", out Dictionary<string, object> colorData))
            {
                var color = system.colorOverLifetime;
                color.enabled = true;
                color.color = ModuleGradient(colorData);
            }
            if (TryModule(modules, "UVModule", out Dictionary<string, object> uvData))
            {
                var uv = system.textureSheetAnimation;
                uv.enabled = true;
                uv.animation = (ParticleSystemAnimationType)OpeningJson.Int(uvData, "animationType");
                uv.numTilesX = OpeningJson.Int(uvData, "tilesX", 1);
                uv.numTilesY = OpeningJson.Int(uvData, "tilesY", 1);
                uv.cycleCount = Mathf.Max(1, (int)OpeningJson.Number(uvData, "cycles", 1f));
                uv.rowIndex = OpeningJson.Int(uvData, "rowIndex");
                uv.rowMode = (ParticleSystemAnimationRowMode)OpeningJson.Int(uvData, "rowMode");
                uv.frameOverTime = ModuleCurve(uvData, "frame", 1f);
                uv.startFrame = ModuleCurve(uvData, "start", 0f);
            }
            if (TryModule(modules, "RotationModule", out Dictionary<string, object> rotationData))
            {
                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.separateAxes = OpeningJson.Bool(rotationData, "separateAxes");
                rotation.z = ModuleCurve(rotationData, "curve", 0f);
                if (rotation.separateAxes)
                {
                    rotation.x = ModuleCurve(rotationData, "x", 0f);
                    rotation.y = ModuleCurve(rotationData, "y", 0f);
                }
            }
            if (TryModule(modules, "VelocityModule", out Dictionary<string, object> velocityData))
            {
                var velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = OpeningJson.Bool(velocityData, "inWorldSpace")
                    ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                velocity.x = ModuleCurve(velocityData, "x", 0f);
                velocity.y = ModuleCurve(velocityData, "y", 0f);
                velocity.z = ModuleCurve(velocityData, "z", 0f);
                velocity.orbitalX = ModuleCurve(velocityData, "orbitalX", 0f);
                velocity.orbitalY = ModuleCurve(velocityData, "orbitalY", 0f);
                velocity.orbitalZ = ModuleCurve(velocityData, "orbitalZ", 0f);
                velocity.orbitalOffsetX = ModuleCurve(velocityData, "orbitalOffsetX", 0f);
                velocity.orbitalOffsetY = ModuleCurve(velocityData, "orbitalOffsetY", 0f);
                velocity.orbitalOffsetZ = ModuleCurve(velocityData, "orbitalOffsetZ", 0f);
                velocity.radial = ModuleCurve(velocityData, "radial", 0f);
                velocity.speedModifier = ModuleCurve(velocityData, "speedModifier", 1f);
            }
            if (TryModule(modules, "ClampVelocityModule", out Dictionary<string, object> clampData))
            {
                var clamp = system.limitVelocityOverLifetime;
                clamp.enabled = true;
                clamp.separateAxes = false;
                clamp.limit = ModuleCurve(clampData, "magnitude", 1f);
                clamp.dampen = (float)OpeningJson.Number(clampData, "dampen");
                clamp.space = OpeningJson.Bool(clampData, "inWorldSpace")
                    ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            }
            if (TryModule(modules, "NoiseModule", out Dictionary<string, object> noiseData))
            {
                var noise = system.noise;
                noise.enabled = true;
                noise.separateAxes = OpeningJson.Bool(noiseData, "separateAxes");
                noise.strength = ModuleCurve(noiseData, "strength", 0f);
                if (noise.separateAxes)
                {
                    noise.strengthX = ModuleCurve(noiseData, "strength", 0f);
                    noise.strengthY = ModuleCurve(noiseData, "strengthY", 0f);
                    noise.strengthZ = ModuleCurve(noiseData, "strengthZ", 0f);
                }
                noise.frequency = (float)OpeningJson.Number(noiseData, "frequency", 0.5f);
                noise.damping = OpeningJson.Bool(noiseData, "damping", true);
                noise.octaveCount = Mathf.Clamp(OpeningJson.Int(noiseData, "octaves", 1), 1, 4);
                noise.octaveMultiplier = (float)OpeningJson.Number(noiseData, "octaveMultiplier", 0.5f);
                noise.octaveScale = (float)OpeningJson.Number(noiseData, "octaveScale", 2f);
                noise.quality = (ParticleSystemNoiseQuality)OpeningJson.Int(noiseData, "quality");
                noise.scrollSpeed = ModuleCurve(noiseData, "scrollSpeed", 0f);
                noise.remapEnabled = OpeningJson.Bool(noiseData, "remapEnabled");
                noise.remap = ModuleCurve(noiseData, "remap", 1f);
                noise.remapY = ModuleCurve(noiseData, "remapY", 1f);
                noise.remapZ = ModuleCurve(noiseData, "remapZ", 1f);
                noise.positionAmount = (float)OpeningJson.Number(noiseData, "positionAmount");
                noise.rotationAmount = (float)OpeningJson.Number(noiseData, "rotationAmount");
                noise.sizeAmount = (float)OpeningJson.Number(noiseData, "sizeAmount");
            }
            if (TryModule(modules, "TrailModule", out Dictionary<string, object> trailData))
            {
                var trails = system.trails;
                trails.enabled = OpeningJson.Bool(trailData, "enabled");
                trails.mode = (ParticleSystemTrailMode)OpeningJson.Int(trailData, "mode");
                trails.ratio = (float)OpeningJson.Number(trailData, "ratio", 1f);
                trails.lifetime = ModuleCurve(trailData, "lifetime", 1f);
                trails.dieWithParticles = OpeningJson.Bool(trailData, "dieWithParticles");
                trails.sizeAffectsWidth = OpeningJson.Bool(trailData, "sizeAffectsWidth");
                trails.sizeAffectsLifetime = OpeningJson.Bool(trailData, "sizeAffectsLifetime");
                trails.inheritParticleColor = OpeningJson.Bool(trailData, "inheritParticleColor");
                trails.colorOverLifetime = ModuleGradient(trailData, "colorOverLifetime");
                trails.colorOverTrail = ModuleGradient(trailData, "colorOverTrail");
                trails.widthOverTrail = ModuleCurve(trailData, "widthOverTrail", 1f);
                trails.minVertexDistance = (float)OpeningJson.Number(trailData, "minVertexDistance", 0.1f);
                trails.textureMode = (ParticleSystemTrailTextureMode)OpeningJson.Int(trailData, "textureMode");
                trails.worldSpace = OpeningJson.Bool(trailData, "worldSpace");
                trails.ribbonCount = Mathf.Clamp(OpeningJson.Int(trailData, "ribbonCount", 1), 1, 8);
                trails.shadowBias = (float)OpeningJson.Number(trailData, "shadowBias", 0.5f);
                trails.generateLightingData = OpeningJson.Bool(trailData, "generateLightingData");
                trails.attachRibbonsToTransform = OpeningJson.Bool(trailData, "attachRibbonsToTransform");
                trails.splitSubEmitterRibbons = OpeningJson.Bool(trailData, "splitSubEmitterRibbons");
                Vector3 textureScale = Vector3Value(trailData, "textureScale", Vector3.one);
                trails.textureScale = new Vector2(textureScale.x, textureScale.y);
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.trailMaterial = renderer.sharedMaterial;
            }
            if (TryModule(modules, "SubModule", out object rawSubEmitters)
                && rawSubEmitters is List<object> subEmitters)
                ConfigureSubEmitters(system, subEmitters, parent, eventDelay, eventSpeed, recursionDepth);
        }

        private void ConfigureSubEmitters(ParticleSystem system, List<object> definitions, Transform parent,
            float eventDelay, float eventSpeed, int recursionDepth)
        {
            var subEmitters = system.subEmitters;
            subEmitters.enabled = definitions.Count > 0;
            foreach (object rawDefinition in definitions)
            {
                Dictionary<string, object> source = OpeningJson.Object(rawDefinition, "sub-emitter");
                if (!source.TryGetValue("definition", out object rawParticle)
                    || !(rawParticle is Dictionary<string, object> particleData)) continue;
                GameObject child = CreateParticle(particleData, parent, 0f, eventSpeed, false, recursionDepth + 1);
                ParticleSystem childSystem = child != null ? child.GetComponent<ParticleSystem>() : null;
                if (childSystem == null) continue;
                subEmitters.AddSubEmitter(childSystem,
                    (ParticleSystemSubEmitterType)OpeningJson.Int(source, "type"),
                    (ParticleSystemSubEmitterProperties)OpeningJson.Int(source, "properties"),
                    Mathf.Clamp01((float)OpeningJson.Number(source, "probability", 1f)));
            }
        }

        private static bool TryModule(Dictionary<string, object> modules, string key, out Dictionary<string, object> value)
        {
            if (modules.TryGetValue(key, out object raw) && raw is Dictionary<string, object> map)
            {
                value = map;
                return true;
            }
            value = null;
            return false;
        }

        private static bool TryModule(Dictionary<string, object> modules, string key, out object value)
        {
            return modules.TryGetValue(key, out value);
        }

        private static ParticleSystem.MinMaxCurve ModuleCurve(Dictionary<string, object> owner, string key,
            float fallback, float offset = 0f)
        {
            if (!owner.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> curve))
                return new ParticleSystem.MinMaxCurve(fallback);
            int mode = OpeningJson.Int(curve, "mode");
            float minimum = (float)OpeningJson.Number(curve, "min", fallback);
            float maximum = (float)OpeningJson.Number(curve, "max", minimum);
            AnimationCurve low = ReadAnimationCurve(curve, "low");
            AnimationCurve high = ReadAnimationCurve(curve, "high");
            switch (mode)
            {
                case 1: return high.length == 0
                    ? new ParticleSystem.MinMaxCurve(maximum + offset)
                    : new ParticleSystem.MinMaxCurve(maximum, ReadAnimationCurve(curve, "high", offset));
                case 2: return low.length == 0 || high.length == 0
                    ? new ParticleSystem.MinMaxCurve(minimum + offset, maximum + offset)
                    : new ParticleSystem.MinMaxCurve(maximum, ReadAnimationCurve(curve, "low", offset),
                        ReadAnimationCurve(curve, "high", offset));
                case 3: return new ParticleSystem.MinMaxCurve(minimum + offset, maximum + offset);
                default: return new ParticleSystem.MinMaxCurve(minimum + offset);
            }
        }

        private static ParticleSystem.MinMaxCurve SerializedCurve(Dictionary<string, object> source, float fallback)
        {
            int mode = OpeningJson.Int(source, "minMaxState");
            float value = (float)OpeningJson.Number(source, "scalar", fallback);
            float minimum = (float)OpeningJson.Number(source, "minScalar", value);
            switch ((ParticleSystemCurveMode)mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(value);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(value, ReadSerializedAnimationCurve(source, "maxCurve"));
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(value,
                        ReadSerializedAnimationCurve(source, "minCurve"),
                        ReadSerializedAnimationCurve(source, "maxCurve"));
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(minimum, value);
                default:
                    throw new InvalidDataException("Unsupported serialized particle curve mode: " + mode);
            }
        }

        private static AnimationCurve ReadSerializedAnimationCurve(Dictionary<string, object> owner, string key)
        {
            Dictionary<string, object> source = OpeningJson.Object(owner[key], "serialized particle curve");
            List<object> values = OpeningJson.Array(source["m_Curve"], "serialized particle curve keys");
            if (values.Count == 0) return AnimationCurve.Linear(0f, 1f, 1f, 1f);
            var keys = new Keyframe[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> item = OpeningJson.Object(values[index], "serialized particle curve key");
                var frame = new Keyframe((float)OpeningJson.Number(item, "time"),
                    (float)OpeningJson.Number(item, "value"),
                    (float)OpeningJson.Number(item, "inSlope"),
                    (float)OpeningJson.Number(item, "outSlope"),
                    (float)OpeningJson.Number(item, "inWeight", 1f / 3f),
                    (float)OpeningJson.Number(item, "outWeight", 1f / 3f));
                frame.weightedMode = (WeightedMode)OpeningJson.Int(item, "weightedMode");
                keys[index] = frame;
            }
            return new AnimationCurve(keys)
            {
                preWrapMode = (WrapMode)OpeningJson.Int(source, "m_PreInfinity", (int)WrapMode.Loop),
                postWrapMode = (WrapMode)OpeningJson.Int(source, "m_PostInfinity", (int)WrapMode.Loop),
            };
        }

        private static AnimationCurve ReadAnimationCurve(Dictionary<string, object> owner, string key, float valueOffset = 0f)
        {
            if (!owner.TryGetValue(key, out object raw) || !(raw is List<object> values) || values.Count == 0)
                return new AnimationCurve();
            var keys = new Keyframe[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                if (!(values[index] is List<object> row) || row.Count < 2) continue;
                float time = (float)OpeningJson.ToNumber(row[0]);
                float value = (float)OpeningJson.ToNumber(row[1]) + valueOffset;
                float inSlope = row.Count > 2 ? (float)OpeningJson.ToNumber(row[2]) : 0f;
                float outSlope = row.Count > 3 ? (float)OpeningJson.ToNumber(row[3]) : inSlope;
                keys[index] = new Keyframe(time, value, inSlope, outSlope);
            }
            return new AnimationCurve(keys);
        }

        private static ParticleSystem.MinMaxGradient ModuleGradient(Dictionary<string, object> source,
            string key = null)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (!source.TryGetValue(key, out object nested) || !(nested is Dictionary<string, object> gradientData))
                    return new ParticleSystem.MinMaxGradient(Color.white);
                source = gradientData;
            }
            int mode = OpeningJson.Int(source, "mode");
            Color minimum = ColorValue(OpeningJson.Array(source["min"], "minimum module color"));
            Color maximum = ColorValue(OpeningJson.Array(source["max"], "maximum module color"));
            Gradient low = source.TryGetValue("low", out object rawLow) && rawLow is Dictionary<string, object> lowData
                ? ReadGradient(lowData) : null;
            Gradient high = source.TryGetValue("high", out object rawHigh) && rawHigh is Dictionary<string, object> highData
                ? ReadGradient(highData) : null;
            if (mode == 1 && high != null) return new ParticleSystem.MinMaxGradient(high);
            if (mode == 3 && low != null && high != null) return new ParticleSystem.MinMaxGradient(low, high);
            if (mode == 4) return new ParticleSystem.MinMaxGradient(minimum, maximum);
            if (mode == 2) return new ParticleSystem.MinMaxGradient(minimum, maximum);
            if (high != null) return new ParticleSystem.MinMaxGradient(high);
            return new ParticleSystem.MinMaxGradient(minimum);
        }

        private static Gradient ReadGradient(Dictionary<string, object> source)
        {
            List<object> rgb = source.TryGetValue("rgb", out object rawRgb)
                ? OpeningJson.Array(rawRgb, "gradient colors") : new List<object>();
            List<object> alpha = source.TryGetValue("alpha", out object rawAlpha)
                ? OpeningJson.Array(rawAlpha, "gradient alpha") : new List<object>();
            var colors = new GradientColorKey[rgb.Count];
            for (int index = 0; index < rgb.Count; index++)
            {
                List<object> row = OpeningJson.Array(rgb[index], "gradient color key");
                if (row.Count < 4) continue;
                colors[index] = new GradientColorKey(new Color((float)OpeningJson.ToNumber(row[1]),
                    (float)OpeningJson.ToNumber(row[2]), (float)OpeningJson.ToNumber(row[3])),
                    (float)OpeningJson.ToNumber(row[0]));
            }
            var alphas = new GradientAlphaKey[alpha.Count];
            for (int index = 0; index < alpha.Count; index++)
            {
                List<object> row = OpeningJson.Array(alpha[index], "gradient alpha key");
                if (row.Count < 2) continue;
                alphas[index] = new GradientAlphaKey((float)OpeningJson.ToNumber(row[1]),
                    (float)OpeningJson.ToNumber(row[0]));
            }
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            gradient.mode = OpeningJson.Int(source, "mode") == 1 ? GradientMode.Fixed : GradientMode.Blend;
            return gradient;
        }

        private static Vector3 Vector3Value(Dictionary<string, object> source, string key, Vector3 fallback = default)
        {
            if (!source.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> value)) return fallback;
            return new Vector3((float)OpeningJson.Number(value, "x", fallback.x),
                (float)OpeningJson.Number(value, "y", fallback.y),
                (float)OpeningJson.Number(value, "z", fallback.z));
        }

        private static float ParticleLifetime(Dictionary<string, object> data)
        {
            float duration = (float)OpeningJson.Number(data, "duration", 1f);
            Dictionary<string, object> life = OpeningJson.Object(data["life"], "life");
            return duration + Mathf.Max((float)OpeningJson.Number(life, "min"), (float)OpeningJson.Number(life, "max"));
        }

        private static ParticleSystem.MinMaxCurve Curve(Dictionary<string, object> owner, string key, float fallback)
        {
            if (!owner.TryGetValue(key, out object value) || !(value is Dictionary<string, object> source))
                return new ParticleSystem.MinMaxCurve(fallback);
            if (source.ContainsKey("mode")) return ModuleCurve(owner, key, fallback);
            float minimum = (float)OpeningJson.Number(source, "min", fallback);
            float maximum = (float)OpeningJson.Number(source, "max", minimum);
            return Mathf.Approximately(minimum, maximum)
                ? new ParticleSystem.MinMaxCurve(minimum)
                : new ParticleSystem.MinMaxCurve(minimum, maximum);
        }

        private static ParticleSystem.MinMaxGradient ColorRange(Dictionary<string, object> owner, string key)
        {
            Dictionary<string, object> source = OpeningJson.Object(owner[key], "effect color");
            if (source.ContainsKey("mode")) return ModuleGradient(owner, key);
            List<object> minimum = OpeningJson.Array(source["min"], "minimum particle color");
            List<object> maximum = OpeningJson.Array(source["max"], "maximum particle color");
            return new ParticleSystem.MinMaxGradient(ColorValue(minimum), ColorValue(maximum));
        }

        private static Color ColorValue(List<object> values)
        {
            return new Color((float)OpeningJson.ToNumber(values[0]), (float)OpeningJson.ToNumber(values[1]),
                (float)OpeningJson.ToNumber(values[2]), (float)OpeningJson.ToNumber(values[3]));
        }

        private static void ApplyMatrix(Transform transform, List<object> raw)
        {
            if (raw.Count < 4 || !(raw[0] is List<object> row0) || !(raw[1] is List<object> row1)
                || !(raw[2] is List<object> row2)) return;
            Vector3 position = new Vector3((float)OpeningJson.ToNumber(row0[3]), (float)OpeningJson.ToNumber(row1[3]),
                (float)OpeningJson.ToNumber(row2[3]));
            float sx = Mathf.Sqrt(Mathf.Pow((float)OpeningJson.ToNumber(row0[0]), 2f)
                                  + Mathf.Pow((float)OpeningJson.ToNumber(row1[0]), 2f));
            float sy = Mathf.Sqrt(Mathf.Pow((float)OpeningJson.ToNumber(row0[1]), 2f)
                                  + Mathf.Pow((float)OpeningJson.ToNumber(row1[1]), 2f));
            float angle = Mathf.Atan2((float)OpeningJson.ToNumber(row1[0]), (float)OpeningJson.ToNumber(row0[0]));
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            transform.localScale = new Vector3(sx, sy, 1f);
        }

        public int ValidateAllParticleResources()
        {
            var validationRoot = new GameObject("OpeningParticlePayloadValidation");
            int particleCount = 0;
            try
            {
                foreach (List<Dictionary<string, object>> entries in effects.Values)
                {
                    foreach (Dictionary<string, object> entry in entries)
                    {
                        foreach (Dictionary<string, object> particle in EnumerateParticleResources(entry))
                        {
                            List<object> unconverted = OpeningJson.Array(particle["unconverted_modules"],
                                "unconverted particle modules");
                            if (unconverted.Count > 0)
                                throw new InvalidDataException("Opening particle has unconverted modules: "
                                    + OpeningJson.String(particle, "path"));
                            GameObject instance = CreateParticle(particle, validationRoot.transform, 0f, 1f, false);
                            ParticleSystemRenderer renderer = instance.GetComponent<ParticleSystemRenderer>();
                            Dictionary<string, object> material = OpeningJson.Object(particle["material"], "effect material");
                            if (renderer == null || renderer.sharedMaterial == null
                                || renderer.sharedMaterial.shader != ParticleShader(material))
                                throw new InvalidDataException("Opening particle did not bind its Windows shader adapter: "
                                    + OpeningJson.String(particle, "path"));
                            string sourceShader = OpeningJson.String(material, "shader");
                            if (sourceShader != "Effect/Noise_Mask_Pro"
                                && sourceShader != "Universal Render Pipeline/2D/Sprite-Lit-Default")
                                throw new InvalidDataException("Opening particle source shader was not preserved: "
                                    + OpeningJson.String(material, "name"));
                            Dictionary<string, object> floats = OpeningJson.Object(material["floats"], "material floats");
                            foreach (KeyValuePair<string, object> pair in floats)
                                if (renderer.sharedMaterial.HasProperty(pair.Key)
                                    && !Mathf.Approximately(renderer.sharedMaterial.GetFloat(pair.Key),
                                        (float)OpeningJson.ToNumber(pair.Value)))
                                    throw new InvalidDataException("Opening particle float property was not applied: "
                                        + OpeningJson.String(material, "name") + "/" + pair.Key);
                            Dictionary<string, object> colors = OpeningJson.Object(material["colors"], "material colors");
                            foreach (KeyValuePair<string, object> pair in colors)
                                if (renderer.sharedMaterial.HasProperty(pair.Key)
                                    && pair.Value is List<object> color && color.Count >= 4)
                                {
                                    Color expected = new Color((float)OpeningJson.ToNumber(color[0]),
                                        (float)OpeningJson.ToNumber(color[1]), (float)OpeningJson.ToNumber(color[2]),
                                        (float)OpeningJson.ToNumber(color[3]));
                                    if (renderer.sharedMaterial.GetColor(pair.Key) != expected)
                                        throw new InvalidDataException("Opening particle color property was not applied: "
                                            + OpeningJson.String(material, "name") + "/" + pair.Key);
                                }
                            if (sourceShader == "Effect/Noise_Mask_Pro")
                            {
                                ValidateFeature(material, renderer.sharedMaterial, "USE_MAINTEX", "_UseMainTex");
                                ValidateFeature(material, renderer.sharedMaterial, "USE_NOISE", "_UseNoise");
                                ValidateFeature(material, renderer.sharedMaterial, "USE_MASK", "_UseMask");
                                ValidateFeature(material, renderer.sharedMaterial, "USE_DISTORT", "_UseDistortion");
                                ValidateFeature(material, renderer.sharedMaterial, "USE_DISSOLVE", "_UseDissolve");
                                ValidateFeature(material, renderer.sharedMaterial, "USE_DISSOLVE2", "_EnableDissolve2");
                                ValidateFeature(material, renderer.sharedMaterial, "USE_PIXEL", "_UsePixel");
                            }
                            Dictionary<string, object> materialTextures = OpeningJson.Object(material["textures"],
                                "material textures");
                            Dictionary<string, object> resourceNames = OpeningJson.Object(particle["textureResources"],
                                "material texture resources");
                            foreach (KeyValuePair<string, object> pair in materialTextures)
                            {
                                if (!resourceNames.TryGetValue(pair.Key, out object rawName)
                                    || !(rawName is string resourceName)
                                    || !textures.TryGetValue(resourceName, out Texture2D texture) || texture == null
                                    || !renderer.sharedMaterial.HasProperty(pair.Key)
                                    || renderer.sharedMaterial.GetTexture(pair.Key) != texture)
                                    throw new InvalidDataException("Opening particle texture was not bound: "
                                        + OpeningJson.String(material, "name") + "/" + pair.Key);
                            }
                            particleCount++;
                        }
                    }
                }
                return particleCount;
            }
            finally
            {
                if (validationRoot != null) UnityEngine.Object.Destroy(validationRoot);
            }
        }

        private static IEnumerable<Dictionary<string, object>> EnumerateParticleResources(
            Dictionary<string, object> entry)
        {
            if (OpeningJson.String(entry, "kind") == "particles")
                foreach (object rawParticle in OpeningJson.Array(entry["particles"], "effect particles"))
                    foreach (Dictionary<string, object> particle in EnumerateParticleHierarchy(
                                 OpeningJson.Object(rawParticle, "effect particle")))
                        yield return particle;

            if (!entry.TryGetValue("source_event", out object rawSource)
                || !(rawSource is Dictionary<string, object> sourceEvent)
                || !sourceEvent.TryGetValue("follower", out object rawFollower)
                || !(rawFollower is Dictionary<string, object> follower)
                || !follower.TryGetValue("prefabs", out object rawPrefabs)
                || !(rawPrefabs is List<object> prefabs)) yield break;

            foreach (object rawPrefab in prefabs)
            {
                Dictionary<string, object> prefab = OpeningJson.Object(rawPrefab, "follow-spine prefab");
                foreach (object rawNode in OpeningJson.Array(prefab["nodes"], "follow-spine nodes"))
                {
                    Dictionary<string, object> node = OpeningJson.Object(rawNode, "follow-spine node");
                    foreach (object rawParticle in OpeningJson.Array(node["particles"], "follow-spine particles"))
                        foreach (Dictionary<string, object> particle in EnumerateParticleHierarchy(
                                     OpeningJson.Object(rawParticle, "follow-spine particle")))
                            yield return particle;
                }
            }

        }

        private static IEnumerable<Dictionary<string, object>> EnumerateParticleHierarchy(
            Dictionary<string, object> particle)
        {
            yield return particle;
            if (!particle.TryGetValue("modules", out object rawModules)
                || !(rawModules is Dictionary<string, object> modules)
                || !modules.TryGetValue("SubModule", out object rawSubEmitters)
                || !(rawSubEmitters is List<object> subEmitters)) yield break;

            foreach (object rawSubEmitter in subEmitters)
            {
                Dictionary<string, object> subEmitter = OpeningJson.Object(rawSubEmitter, "sub-emitter");
                if (subEmitter.TryGetValue("definition", out object rawDefinition)
                    && rawDefinition is Dictionary<string, object> definition)
                    foreach (Dictionary<string, object> nested in EnumerateParticleHierarchy(definition))
                        yield return nested;
            }
        }

        private static void ValidateFeature(Dictionary<string, object> source, Material material,
            string keyword, string property)
        {
            List<object> keywords = OpeningJson.Array(source["shaderKeywords"], "material shader keywords");
            bool expected = false;
            foreach (object rawKeyword in keywords)
                if (string.Equals(Convert.ToString(rawKeyword), keyword, StringComparison.Ordinal))
                {
                    expected = true;
                    break;
                }
            if (!Mathf.Approximately(material.GetFloat(property), expected ? 1f : 0f))
                throw new InvalidDataException("Opening particle shader keyword was not applied: "
                    + OpeningJson.String(source, "name") + "/" + keyword);
        }

        public void Dispose()
        {
            foreach (GameObject root in roots) if (root != null) UnityEngine.Object.Destroy(root);
            roots.Clear();
            entityRoots.Clear();
            statusRoots.Clear();
            foreach (Material material in particleMaterials) if (material != null) UnityEngine.Object.Destroy(material);
            particleMaterials.Clear();
            foreach (Mesh mesh in meshes.Values) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            meshes.Clear();
            meshHashes.Clear();
            textures.Clear();
            audioClips.Clear();
        }
    }
}
