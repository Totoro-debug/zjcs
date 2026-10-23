using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coffee.UIExtensions;
using UnityEngine;
using Zjcs.Presentation.Opening;

namespace Zjcs.Presentation.Home
{
    public sealed class PetGachaRuntimeEffectRenderer : MonoBehaviour
    {
        private const string ParticleShaderPathId = "-1464726073327024352";
        private readonly Dictionary<string, object> prefabs = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> instances = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> useVersions = new Dictionary<string, int>(StringComparer.Ordinal);
        private HomeAssetCatalog catalog;
        private Transform parent;

        public void Configure(HomeAssetCatalog assets, Transform targetParent,
            IDictionary<string, object> officialPrefabs)
        {
            catalog = assets ?? throw new ArgumentNullException(nameof(assets));
            parent = targetParent ?? throw new ArgumentNullException(nameof(targetParent));
            prefabs.Clear();
            if (officialPrefabs != null)
                foreach (KeyValuePair<string, object> prefab in officialPrefabs)
                    prefabs[prefab.Key] = prefab.Value;
        }

        public void Play(string resourceId, Vector2 position)
        {
            if (string.IsNullOrEmpty(resourceId) || catalog == null || parent == null) return;
            if (!prefabs.TryGetValue(resourceId, out object rawPrefab))
            {
                Debug.LogWarning("Official Pet Gacha effect is not bound: " + resourceId);
                return;
            }

            GameObject root;
            if (!instances.TryGetValue(resourceId, out root) || root == null)
            {
                root = CreateEffect(resourceId, rawPrefab);
                instances[resourceId] = root;
            }
            RectTransform rootRect = root.transform as RectTransform;
            if (rootRect != null) rootRect.anchoredPosition = position;
            root.SetActive(true);
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Clear(true);
                system.Play(true);
            }
            UIParticle uiParticle = root.GetComponent<UIParticle>();
            if (uiParticle != null) uiParticle.Play();
            int version = useVersions.TryGetValue(resourceId, out int previous) ? previous + 1 : 1;
            useVersions[resourceId] = version;
            StartCoroutine(DisableAfter(resourceId, root, version));
        }

        public void Stop()
        {
            foreach (GameObject instance in instances.Values)
                if (instance != null) Destroy(instance);
            instances.Clear();
            useVersions.Clear();
        }

        private GameObject CreateEffect(string resourceId, object rawPrefab)
        {
            Dictionary<string, object> prefab = OpeningJson.Object(rawPrefab, "Pet Gacha effect root");
            List<object> effectRows = OpeningJson.Array(prefab["effects"], "Pet Gacha effect list");
            if (effectRows.Count == 0) throw new FormatException("Pet Gacha effect has no particle data: " + resourceId);
            Dictionary<string, object> effect = OpeningJson.Object(effectRows[0], "Pet Gacha particle effect");
            var root = new GameObject("PetGachaEffect_" + resourceId,
                typeof(RectTransform));
            root.SetActive(false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(100f, 100f);
            rootRect.anchoredPosition = Vector2.zero;

            List<object> emitterRows = OpeningJson.Array(effect["emitters"], "Pet Gacha effect emitters");
            List<object> trailRows = OpeningJson.Array(prefab.TryGetValue("trails", out object rawTrails)
                ? rawTrails : null, "Pet Gacha effect trails");
            foreach (object emitterRow in emitterRows)
            {
                Dictionary<string, object> emitter = OpeningJson.Object(emitterRow, "Pet Gacha effect emitter");
                CreateEmitter(resourceId, emitter, trailRows, rootRect);
            }

            Coffee.UIExtensions.UIParticle particle = root.AddComponent<Coffee.UIExtensions.UIParticle>();
            particle.positionMode = Coffee.UIExtensions.UIParticle.PositionMode.Relative;
            particle.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.Transform;
            particle.raycastTarget = false;
            List<object> scale = OpeningJson.Array(effect.TryGetValue("scale", out object rawScale)
                ? rawScale : null, "Pet Gacha effect scale");
            if (scale.Count >= 3)
                particle.scale3D = new Vector3(Number(scale[0]), Number(scale[1]), Number(scale[2]));
            return root;
        }

        private void CreateEmitter(string resourceId, Dictionary<string, object> source,
            List<object> trailRows, RectTransform root)
        {
            string path = OpeningJson.String(source, "path");
            string name = path.Split('/').LastOrDefault();
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Particle" : name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            ApplyEmitterTransform(source, rect);

            ParticleSystem system = go.AddComponent<ParticleSystem>();
            ConfigureSystem(system, source);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = (ParticleSystemRenderMode)Integer(source, "render_mode");
            renderer.alignment = (ParticleSystemRenderSpace)Integer(source, "alignment");
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sharedMaterial = CreateMaterial(Object(source, "material"),
                OpeningJson.String(source, "shader_path_id"));
            ConfigureTrail(system, renderer, path, trailRows);
        }

        private static void ApplyEmitterTransform(Dictionary<string, object> source, RectTransform rect)
        {
            List<object> rows = OpeningJson.Array(source.TryGetValue("matrix", out object matrix)
                ? matrix : null, "Pet Gacha effect transform");
            if (rows.Count < 3) return;
            float[,] value = new float[4, 4];
            for (int row = 0; row < Mathf.Min(4, rows.Count); row++)
            {
                List<object> cells = OpeningJson.Array(rows[row], "Pet Gacha effect matrix row");
                for (int column = 0; column < Mathf.Min(4, cells.Count); column++) value[row, column] = Number(cells[column]);
            }
            Vector3 x = new Vector3(value[0, 0], value[1, 0], value[2, 0]);
            Vector3 y = new Vector3(value[0, 1], value[1, 1], value[2, 1]);
            Vector3 z = new Vector3(value[0, 2], value[1, 2], value[2, 2]);
            Vector3 scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f) scale.z = -scale.z;
            Matrix4x4 rotation = Matrix4x4.identity;
            rotation.SetColumn(0, new Vector4(x.normalized.x, x.normalized.y, x.normalized.z, 0f));
            rotation.SetColumn(1, new Vector4(y.normalized.x, y.normalized.y, y.normalized.z, 0f));
            rotation.SetColumn(2, new Vector4(z.normalized.x, z.normalized.y, z.normalized.z, 0f));
            rect.anchoredPosition3D = new Vector3(value[0, 3], value[1, 3], value[2, 3]);
            rect.localRotation = rotation.rotation;
            rect.localScale = scale;
        }

        private static void ConfigureSystem(ParticleSystem system, Dictionary<string, object> source)
        {
            var main = system.main;
            main.duration = Mathf.Max(0.01f, Float(source, "duration", 1f));
            main.loop = Bool(source, "loop");
            main.prewarm = Bool(source, "prewarm");
            main.playOnAwake = false;
            main.simulationSpeed = Float(source, "speed", 1f);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, Integer(source, "max_particles", 100));
            main.simulationSpace = (ParticleSystemSimulationSpace)Integer(source, "simulation_space", 1);
            main.startDelay = Curve(Object(source, "delay"), 0f);
            main.startLifetime = Curve(Object(source, "life"), 1f);
            main.startSpeed = Curve(Object(source, "velocity"), 0f);
            main.startSize3D = Bool(source, "size3d");
            if (main.startSize3D)
            {
                main.startSizeX = Curve(Object(source, "size"), 1f);
                main.startSizeY = Curve(Object(source, "size_y"), 1f);
            }
            else main.startSize = Curve(Object(source, "size"), 1f);
            main.startRotation = Curve(Object(source, "rotation"), 0f);
            main.startColor = MinMaxGradient(Object(source, "color"));

            Dictionary<string, object> shapeData = Object(source, "shape");
            var shape = system.shape;
            shape.enabled = Bool(shapeData, "enabled");
            shape.shapeType = (ParticleSystemShapeType)Integer(shapeData, "type");
            shape.angle = Float(shapeData, "angle");
            shape.length = Float(shapeData, "length");
            shape.radius = Float(Object(shapeData, "radius"), "value", 1f);
            shape.radiusThickness = Float(shapeData, "radiusThickness", 1f);
            shape.donutRadius = Float(shapeData, "donutRadius");
            shape.arc = Float(Object(shapeData, "arc"), "value", 360f);
            shape.position = Vector3Value(Object(shapeData, "m_Position"));
            shape.rotation = Vector3Value(Object(shapeData, "m_Rotation"));
            shape.scale = Vector3Value(Object(shapeData, "m_Scale"), Vector3.one);
            shape.alignToDirection = Bool(shapeData, "alignToDirection");

            Dictionary<string, object> rate = Object(source, "rate");
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = Curve(rate, 0f);
            List<object> bursts = OpeningJson.Array(source.TryGetValue("bursts", out object rawBursts)
                ? rawBursts : null, "Pet Gacha effect bursts");
            if (bursts.Count > 0)
            {
                var values = new ParticleSystem.Burst[bursts.Count];
                for (int index = 0; index < bursts.Count; index++)
                {
                    Dictionary<string, object> burst = OpeningJson.Object(bursts[index], "Pet Gacha particle burst");
                    Dictionary<string, object> count = Object(burst, "count");
                    GetCurveRange(count, out short minCount, out short maxCount);
                    var value = new ParticleSystem.Burst(Float(burst, "time"), minCount, maxCount,
                        Mathf.Max(1, Integer(burst, "cycles", 1)), Float(burst, "interval"));
                    value.probability = Mathf.Clamp01(Float(burst, "probability", 1f));
                    values[index] = value;
                }
                emission.SetBursts(values);
            }

            Dictionary<string, object> modules = Object(source, "modules");
            if (modules.TryGetValue("SizeModule", out object sizeRaw))
            {
                Dictionary<string, object> sizeData = OpeningJson.Object(sizeRaw, "Pet Gacha size module");
                var size = system.sizeOverLifetime;
                size.enabled = true;
                Dictionary<string, object> curve = Object(sizeData, "curve");
                size.separateAxes = Bool(sizeData, "separateAxes");
                if (size.separateAxes)
                {
                    size.x = Curve(curve, 1f);
                    size.y = Curve(Object(sizeData, "y"), 1f);
                }
                else size.size = Curve(curve, 1f);
            }
            if (modules.TryGetValue("ColorModule", out object colorRaw))
            {
                var color = system.colorOverLifetime;
                color.enabled = true;
                color.color = MinMaxGradient(OpeningJson.Object(colorRaw, "Pet Gacha color module"));
            }
            if (modules.TryGetValue("RotationModule", out object rotationRaw))
            {
                Dictionary<string, object> rotationData = OpeningJson.Object(rotationRaw, "Pet Gacha rotation module");
                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.separateAxes = Bool(rotationData, "separateAxes");
                if (rotation.separateAxes) rotation.z = Curve(Object(rotationData, "curve"), 0f);
                else rotation.z = Curve(Object(rotationData, "curve"), 0f);
            }
            if (modules.TryGetValue("ClampVelocityModule", out object clampRaw))
            {
                Dictionary<string, object> clampData = OpeningJson.Object(clampRaw, "Pet Gacha clamp module");
                var clamp = system.limitVelocityOverLifetime;
                clamp.enabled = true;
                clamp.limit = Curve(Object(clampData, "magnitude"), 0f);
                clamp.dampen = Float(clampData, "dampen");
            }
            if (modules.TryGetValue("UVModule", out object uvRaw))
            {
                Dictionary<string, object> uvData = OpeningJson.Object(uvRaw, "Pet Gacha UV module");
                var sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = Mathf.Max(1, Integer(uvData, "tilesX", 1));
                sheet.numTilesY = Mathf.Max(1, Integer(uvData, "tilesY", 1));
                sheet.frameOverTime = Curve(Object(uvData, "frame"), 0f);
                sheet.startFrame = Curve(Object(uvData, "start"), 0f);
                sheet.cycleCount = Mathf.Max(1, Integer(uvData, "cycles", 1));
            }
        }

        private void ConfigureTrail(ParticleSystem system, ParticleSystemRenderer renderer,
            string emitterPath, List<object> trailRows)
        {
            object raw = trailRows.FirstOrDefault(value =>
            {
                Dictionary<string, object> row = OpeningJson.Object(value, "Pet Gacha trail");
                return OpeningJson.String(row, "path").StartsWith(emitterPath + "/", StringComparison.Ordinal);
            });
            if (raw == null) return;
            Dictionary<string, object> source = OpeningJson.Object(raw, "Pet Gacha trail");
            var trail = system.trails;
            trail.enabled = Bool(source, "emitting", true);
            trail.mode = ParticleSystemTrailMode.PerParticle;
            trail.lifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, Float(source, "duration", 0.1f)));
            trail.minVertexDistance = Mathf.Max(0.001f, Float(source, "minimum", 0.1f));
            trail.widthOverTrail = new ParticleSystem.MinMaxCurve(Float(source, "width", 0.5f),
                UnityCurve(Object(source, "width_curve")));
            trail.colorOverLifetime = MinMaxGradient(Object(source, "gradient"));
            trail.textureMode = (ParticleSystemTrailTextureMode)Integer(source, "texture_mode");
            renderer.trailMaterial = CreateMaterial(Object(source, "material"),
                OpeningJson.String(source, "shader_path_id"));
        }

        private Material CreateMaterial(Dictionary<string, object> source, string shaderPathId)
        {
            if (shaderPathId != ParticleShaderPathId)
                throw new InvalidOperationException("Official Pet Gacha effect shader is not supported: " + shaderPathId);
            Shader shader = Shader.Find("Zjcs/Opening/UIParticle");
            if (shader == null) throw new InvalidOperationException("The official Windows Pet Gacha particle shader is missing.");
            var material = new Material(shader) { name = OpeningJson.String(source, "name") };
            if (source.TryGetValue("floats", out object rawFloats))
                foreach (KeyValuePair<string, object> value in OpeningJson.Object(rawFloats, "particle material floats"))
                    if (material.HasProperty(value.Key)) material.SetFloat(value.Key, (float)OpeningJson.ToNumber(value.Value));
            if (source.TryGetValue("colors", out object rawColors))
                foreach (KeyValuePair<string, object> value in OpeningJson.Object(rawColors, "particle material colors"))
                    if (material.HasProperty(value.Key)) material.SetVector(value.Key, Vector4Value(value.Value));
            if (source.TryGetValue("textures", out object rawTextures))
                foreach (KeyValuePair<string, object> value in OpeningJson.Object(rawTextures, "particle material textures"))
                {
                    Dictionary<string, object> textureData = OpeningJson.Object(value.Value, "particle texture");
                    string file = OpeningJson.String(textureData, "file");
                    Texture texture = file == "builtin/white" ? Texture2D.whiteTexture : catalog.FindTexture(file);
                    if (texture == null)
                    {
                        Destroy(material);
                        throw new FileNotFoundException("Official Pet Gacha effect texture is missing: " + file);
                    }
                    material.SetTexture(value.Key, texture);
                    material.SetTextureScale(value.Key, Vector2Value(textureData, "scale", Vector2.one));
                    material.SetTextureOffset(value.Key, Vector2Value(textureData, "offset", Vector2.zero));
                }
            return material;
        }

        private static ParticleSystem.MinMaxCurve Curve(Dictionary<string, object> source, float fallback)
        {
            if (source == null || source.Count == 0) return new ParticleSystem.MinMaxCurve(fallback);
            int mode = Integer(source, "mode");
            float min = Float(source, "min", fallback), max = Float(source, "max", min);
            switch ((ParticleSystemCurveMode)mode)
            {
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(min, DataCurve(OpeningJson.Array(source["high"], "particle curve"), max));
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(1f,
                        DataCurve(OpeningJson.Array(source["low"], "particle min curve"), min),
                        DataCurve(OpeningJson.Array(source["high"], "particle max curve"), max));
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(min, max);
                default:
                    return new ParticleSystem.MinMaxCurve(min);
            }
        }

        private static void GetCurveRange(Dictionary<string, object> source, out short minimum, out short maximum)
        {
            float min = Float(source, "min"), max = Float(source, "max", min);
            minimum = (short)Mathf.Clamp(Mathf.RoundToInt(source.Count == 0 ? 1 : min), 0, short.MaxValue);
            maximum = (short)Mathf.Clamp(Mathf.RoundToInt(max), minimum, short.MaxValue);
        }

        private static AnimationCurve DataCurve(List<object> rows, float fallback)
        {
            if (rows == null || rows.Count == 0) return AnimationCurve.Linear(0f, fallback, 1f, fallback);
            var keys = new Keyframe[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                List<object> values = OpeningJson.Array(rows[index], "particle curve key");
                float time = values.Count > 0 ? Number(values[0]) : 0f;
                float value = values.Count > 1 ? Number(values[1]) : fallback;
                float incoming = values.Count > 2 ? Number(values[2]) : 0f;
                float outgoing = values.Count > 3 ? Number(values[3]) : incoming;
                keys[index] = new Keyframe(time, value, incoming, outgoing);
            }
            return new AnimationCurve(keys);
        }

        private static AnimationCurve UnityCurve(Dictionary<string, object> source)
        {
            List<object> rows = OpeningJson.Array(source.TryGetValue("m_Curve", out object raw)
                ? raw : null, "particle trail curve");
            var keys = new Keyframe[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, object> row = OpeningJson.Object(rows[index], "particle trail curve key");
                keys[index] = new Keyframe(Float(row, "time"), Float(row, "value"),
                    Float(row, "inSlope"), Float(row, "outSlope"));
            }
            return keys.Length == 0 ? AnimationCurve.Linear(0f, 1f, 1f, 0f) : new AnimationCurve(keys);
        }

        private static ParticleSystem.MinMaxGradient MinMaxGradient(Dictionary<string, object> source)
        {
            if (source == null || source.Count == 0) return new ParticleSystem.MinMaxGradient(Color.white);
            int mode = Integer(source, "mode");
            Color min = ColorValue(OpeningJson.Array(source["min"], "particle min color"), Color.white);
            Color max = ColorValue(OpeningJson.Array(source["max"], "particle max color"), min);
            switch ((ParticleSystemGradientMode)mode)
            {
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Gradient(Object(source, "high")));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(min, max);
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Gradient(Object(source, "low")),
                        Gradient(Object(source, "high")));
                case ParticleSystemGradientMode.RandomColor:
                    var random = new ParticleSystem.MinMaxGradient(Gradient(Object(source, "high")));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;
                default:
                    return new ParticleSystem.MinMaxGradient(max);
            }
        }

        private static Gradient Gradient(Dictionary<string, object> source)
        {
            List<object> colorRows = OpeningJson.Array(source.TryGetValue("rgb", out object rawRgb)
                ? rawRgb : null, "particle gradient colors");
            List<object> alphaRows = OpeningJson.Array(source.TryGetValue("alpha", out object rawAlpha)
                ? rawAlpha : null, "particle gradient alpha");
            var colors = new GradientColorKey[Mathf.Max(1, colorRows.Count)];
            var alphas = new GradientAlphaKey[Mathf.Max(1, alphaRows.Count)];
            if (colorRows.Count == 0) colors[0] = new GradientColorKey(Color.white, 0f);
            for (int index = 0; index < colorRows.Count; index++)
            {
                List<object> row = OpeningJson.Array(colorRows[index], "particle gradient color");
                colors[index] = new GradientColorKey(ColorValue(row, Color.white), row.Count > 0 ? Number(row[0]) : 0f);
            }
            if (alphaRows.Count == 0) alphas[0] = new GradientAlphaKey(1f, 0f);
            for (int index = 0; index < alphaRows.Count; index++)
            {
                List<object> row = OpeningJson.Array(alphaRows[index], "particle gradient alpha");
                alphas[index] = new GradientAlphaKey(row.Count > 1 ? Number(row[1]) : 1f,
                    row.Count > 0 ? Number(row[0]) : 0f);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> source, string key)
        {
            return source != null && source.TryGetValue(key, out object value)
                ? OpeningJson.Object(value, key) : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static float Float(Dictionary<string, object> source, string key, float fallback = 0f)
            => (float)OpeningJson.Number(source, key, fallback);
        private static int Integer(Dictionary<string, object> source, string key, int fallback = 0)
            => OpeningJson.Int(source, key, fallback);
        private static bool Bool(Dictionary<string, object> source, string key, bool fallback = false)
            => OpeningJson.Bool(source, key, fallback);
        private static float Number(object value) => (float)OpeningJson.ToNumber(value);

        private static Vector3 Vector3Value(Dictionary<string, object> source, Vector3 fallback = default)
        {
            return source == null || source.Count == 0 ? fallback
                : new Vector3(Float(source, "x"), Float(source, "y"), Float(source, "z"));
        }

        private static Vector2 Vector2Value(Dictionary<string, object> source, string key, Vector2 fallback)
        {
            List<object> values = OpeningJson.Array(source.TryGetValue(key, out object raw) ? raw : null,
                "particle texture vector");
            return values.Count < 2 ? fallback : new Vector2(Number(values[0]), Number(values[1]));
        }

        private static Vector4 Vector4Value(object value)
        {
            List<object> values = OpeningJson.Array(value, "particle material color");
            return values.Count < 4 ? Vector4.one
                : new Vector4(Number(values[0]), Number(values[1]), Number(values[2]), Number(values[3]));
        }

        private static Color ColorValue(List<object> values, Color fallback)
        {
            return values == null || values.Count < 4 ? fallback
                : new Color(Number(values[0]), Number(values[1]), Number(values[2]), Number(values[3]));
        }

        private System.Collections.IEnumerator DisableAfter(string key, GameObject root, int version)
        {
            yield return new WaitForSecondsRealtime(1.25f);
            if (root != null && useVersions.TryGetValue(key, out int current) && current == version)
            {
                foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                root.SetActive(false);
            }
        }

        private void OnDestroy() => Stop();
    }
}
