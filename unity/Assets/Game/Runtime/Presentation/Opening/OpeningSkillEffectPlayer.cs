using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Small runtime reader for verified battle-presentation particle payloads. The first
    /// migration slice is cast:11150, the fire-momentum effect used repeatedly by the
    /// pinned opening record. Material shader-special playables remain separate work.
    /// </summary>
    public sealed class OpeningSkillEffectPlayer : IDisposable
    {
        private const string PayloadPath = "Opening/skill-effects.json";
        private const string TexturePath = "OpeningSkillEffects/Textures/";
        private readonly Dictionary<string, Dictionary<string, object>> effects =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly List<GameObject> roots = new List<GameObject>();
        private readonly List<Material> particleMaterials = new List<Material>();
        private readonly Action<string> log;
        private readonly Shader shader;

        private OpeningSkillEffectPlayer(Action<string> log)
        {
            this.log = log ?? (_ => { });
            shader = Shader.Find("Sprites/Default");
        }

        public int SpawnedSystems { get; private set; }
        public int SpawnedEffects => roots.Count;

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
            foreach (object item in OpeningJson.Array(root["effects"], "effects"))
            {
                Dictionary<string, object> entry = OpeningJson.Object(item, "effect");
                player.effects[OpeningJson.String(entry, "key")] = entry;
            }
            Dictionary<string, object> textureEntries = OpeningJson.Object(root["textures"], "effect textures");
            foreach (KeyValuePair<string, object> pair in textureEntries)
            {
                Dictionary<string, object> entry = OpeningJson.Object(pair.Value, "effect texture");
                string name = Path.GetFileNameWithoutExtension(pair.Key);
                Texture2D texture = Resources.Load<Texture2D>(TexturePath + name);
                if (texture == null) throw new InvalidOperationException("Official effect texture is missing: " + name);
                player.textures[name] = texture;
            }
            return player;
        }

        public bool Play(string effectType, int classId, Vector3 worldPosition, Transform parent)
        {
            string key = (effectType ?? string.Empty) + ":" + classId;
            if (!effects.TryGetValue(key, out Dictionary<string, object> effect)) return false;
            var root = new GameObject("OpeningSkillEffect_" + classId);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            float lifetime = 0f;
            foreach (object item in OpeningJson.Array(effect["particles"], "effect particles"))
            {
                Dictionary<string, object> data = OpeningJson.Object(item, "effect particle");
                GameObject child = CreateParticle(data, root.transform);
                if (child == null) continue;
                lifetime = Mathf.Max(lifetime, ParticleLifetime(data));
                SpawnedSystems++;
            }
            if (SpawnedSystems == 0)
            {
                UnityEngine.Object.Destroy(root);
                return false;
            }
            var cleanup = root.AddComponent<OpeningDelayedDestroy>();
            cleanup.Delay = Mathf.Max(1.0f, lifetime + 0.25f);
            roots.Add(root);
            return true;
        }

        private GameObject CreateParticle(Dictionary<string, object> data, Transform parent)
        {
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
            main.simulationSpeed = (float)OpeningJson.Number(data, "speed", 1f);
            main.maxParticles = Mathf.Max(1, OpeningJson.Int(data, "max_particles", 32));
            main.startLifetime = Curve(data, "life", 0.5f);
            main.startSpeed = Curve(data, "velocity", 0f);
            main.startSize = Curve(data, "size", 1f);
            main.startRotation = Curve(data, "rotation", 0f);
            main.startColor = ColorRange(data, "color");

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
                    Dictionary<string, object> count = OpeningJson.Object(burst["count"], "burst count");
                    short minimum = (short)Mathf.Clamp(OpeningJson.Int(count, "min"), 0, short.MaxValue);
                    short maximum = (short)Mathf.Clamp(OpeningJson.Int(count, "max", minimum), 0, short.MaxValue);
                    values[i] = new ParticleSystem.Burst((float)OpeningJson.Number(burst, "time"),
                        minimum, maximum, (short)Mathf.Clamp(OpeningJson.Int(burst, "cycles", 1), 1, short.MaxValue),
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
            shape.radius = (float)OpeningJson.Number(shapeData, "radius");

            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = (ParticleSystemRenderMode)OpeningJson.Int(data, "render_mode");
            // The source field is a layer index from the Android project, not a Unity
            // SortingLayer unique ID. This project intentionally uses the default layer.
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 25000 + OpeningJson.Int(data, "sorting_order");
            Dictionary<string, object> materialData = OpeningJson.Object(data["material"], "effect material");
            Dictionary<string, object> textureData = OpeningJson.Object(
                OpeningJson.Object(materialData["textures"], "material textures")["_MainTex"], "main texture");
            string textureResource = OpeningJson.String(data, "textureResource");
            Texture2D texture = textures.TryGetValue(textureResource, out Texture2D found) ? found : null;
            if (texture == null)
            {
                log("开场技能粒子贴图未绑定: " + textureResource);
                UnityEngine.Object.Destroy(item);
                return null;
            }
            Material particleMaterial = new Material(shader) { name = OpeningJson.String(materialData, "name") };
            particleMaterial.mainTexture = texture;
            renderer.sharedMaterial = particleMaterial;
            particleMaterials.Add(particleMaterial);
            // The source value is an Android sorting-layer index, not this project's unique layer ID.
            renderer.sortingLayerID = 0;
            system.Play();
            return item;
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
            float minimum = (float)OpeningJson.Number(source, "min", fallback);
            float maximum = (float)OpeningJson.Number(source, "max", minimum);
            return Mathf.Approximately(minimum, maximum)
                ? new ParticleSystem.MinMaxCurve(minimum)
                : new ParticleSystem.MinMaxCurve(minimum, maximum);
        }

        private static ParticleSystem.MinMaxGradient ColorRange(Dictionary<string, object> owner, string key)
        {
            Dictionary<string, object> source = OpeningJson.Object(owner[key], "effect color");
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

        public void Dispose()
        {
            foreach (GameObject root in roots) if (root != null) UnityEngine.Object.Destroy(root);
            roots.Clear();
            foreach (Material material in particleMaterials) if (material != null) UnityEngine.Object.Destroy(material);
            particleMaterials.Clear();
            textures.Clear();
        }
    }
}
