using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Builds the official game_902 fight stage from the migrated payload: world-space
    /// sprite/tilemap quad batches, the official particle mounts and the stage camera.
    /// </summary>
    public sealed class OpeningBattlefield : IDisposable
    {
        public const string PayloadPath = "Opening/battlefield/game_902.json";
        private const string TextureResourcePath = "OpeningBattlefield/Textures/";

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<GameObject> particles = new List<GameObject>();
        private readonly GameObject root;
        private readonly Dictionary<int, string> npcModels = new Dictionary<int, string>();
        private readonly Vector3 gridOrigin;

        private OpeningBattlefield(GameObject root, Camera camera, Transform unitRoot, Vector3 rootOffset,
            Vector3 gridOrigin, Vector2Int size, Vector2Int entry, List<Vector2Int> blocked)
        {
            this.root = root;
            StageCamera = camera;
            UnitRoot = unitRoot;
            RootOffset = rootOffset;
            this.gridOrigin = gridOrigin;
            Size = size;
            Entry = entry;
            Blocked = blocked;
        }

        public Camera StageCamera { get; }
        public Transform UnitRoot { get; }
        public Vector3 RootOffset { get; }
        public Vector2Int Size { get; }
        public Vector2Int Entry { get; }
        public List<Vector2Int> Blocked { get; }
        public Vector3 GridOrigin { get; private set; }
        public int BatchCount { get; private set; }
        public int ParticleCount => particles.Count;
        public GameObject Root => root;

        public static bool PayloadExists =>
            File.Exists(Path.Combine(Application.streamingAssetsPath, PayloadPath));

        public static OpeningBattlefield Load(Transform parent, Action<string> log)
        {
            string path = Path.Combine(Application.streamingAssetsPath, PayloadPath);
            if (!File.Exists(path))
                throw new FileNotFoundException("Opening battlefield payload is missing.", path);
            Dictionary<string, object> data = OpeningJson.Object(
                OpeningJson.Parse(File.ReadAllBytes(path)), "battlefield");
            if (OpeningJson.String(data, "manifest") != OpeningAssets.Manifest)
                throw new FormatException("Opening battlefield payload is not pinned.");

            var rootObject = new GameObject("OpeningBattlefield");
            rootObject.transform.SetParent(parent, false);
            var unitObject = new GameObject("OpeningBattlefieldUnits");
            unitObject.transform.SetParent(rootObject.transform, false);
            Vector3 rootOffset = ReadVector(OpeningJson.Array(data["root_offset"], "root offset"));
            Dictionary<string, object> grid = OpeningJson.Object(data["grid"], "grid");
            Vector3 gridOrigin = ReadVector(OpeningJson.Array(grid["origin"], "grid origin"));
            unitObject.transform.localPosition = rootOffset + gridOrigin;

            var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (object entry in OpeningJson.Array(data["textures"], "textures"))
            {
                Dictionary<string, object> texture = OpeningJson.Object(entry, "texture");
                string name = OpeningJson.String(texture, "name");
                Texture2D loaded = Resources.Load<Texture2D>(TextureResourcePath + name);
                if (loaded == null)
                    throw new InvalidOperationException("Official battlefield texture is missing: " + name);
                textures[name] = loaded;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Unity's Sprites/Default shader is unavailable.");
            var materialTemplate = new Material(shader) { name = "OpeningBattlefield" };

            var instance = new OpeningBattlefield(rootObject, null, unitObject.transform, rootOffset,
                gridOrigin, Vector2Int.zero, Vector2Int.zero, new List<Vector2Int>());
            instance.materials.Add(materialTemplate);
            foreach (object entry in OpeningJson.Array(data["batches"], "batches"))
                instance.CreateBatch(OpeningJson.Object(entry, "batch"), textures, materialTemplate, log);

            foreach (object entry in OpeningJson.Array(data["particles"], "particles"))
                instance.CreateParticle(OpeningJson.Object(entry, "particle"), textures, log);

            Camera camera = instance.CreateCamera(data);
            var result = new OpeningBattlefield(rootObject, camera, unitObject.transform, rootOffset,
                gridOrigin,
                instance.ReadSize(data), instance.ReadEntry(data), instance.ReadBlocked(data))
            {
                BatchCount = instance.BatchCount,
            };
            if (data.TryGetValue("npc_models", out object npcValue))
            {
                foreach (object item in OpeningJson.Array(npcValue, "npc models"))
                {
                    Dictionary<string, object> record = OpeningJson.Object(item, "npc model");
                    result.npcModels[OpeningJson.Int(record, "class_id")] = OpeningJson.String(record, "skeleton");
                }
            }
            foreach (GameObject particle in instance.particles) result.particles.Add(particle);
            foreach (Mesh mesh in instance.meshes) result.meshes.Add(mesh);
            foreach (Material material in instance.materials) result.materials.Add(material);
            return result;
        }

        private Vector2Int ReadSize(Dictionary<string, object> data)
        {
            Dictionary<string, object> grid = OpeningJson.Object(data["grid"], "grid");
            List<object> size = OpeningJson.Array(grid["size"], "grid size");
            return new Vector2Int(Convert.ToInt32(OpeningJson.ToNumber(size[0])),
                Convert.ToInt32(OpeningJson.ToNumber(size[1])));
        }

        private Vector2Int ReadEntry(Dictionary<string, object> data)
        {
            Dictionary<string, object> grid = OpeningJson.Object(data["grid"], "grid");
            List<object> entry = OpeningJson.Array(grid["entry"], "grid entry");
            return new Vector2Int(Convert.ToInt32(OpeningJson.ToNumber(entry[0])),
                Convert.ToInt32(OpeningJson.ToNumber(entry[1])));
        }

        private Vector3 ReadGridOrigin(Dictionary<string, object> data)
        {
            Dictionary<string, object> grid = OpeningJson.Object(data["grid"], "grid");
            List<object> origin = OpeningJson.Array(grid["origin"], "grid origin");
            return new Vector3((float)OpeningJson.ToNumber(origin[0]), (float)OpeningJson.ToNumber(origin[1]),
                origin.Count > 2 ? (float)OpeningJson.ToNumber(origin[2]) : 0f);
        }

        private List<Vector2Int> ReadBlocked(Dictionary<string, object> data)
        {
            var result = new List<Vector2Int>();
            Dictionary<string, object> grid = OpeningJson.Object(data["grid"], "grid");
            if (!grid.TryGetValue("blocked", out object value) || !(value is List<object> list)) return result;
            foreach (object item in list)
            {
                if (!(item is List<object> cell) || cell.Count < 2) continue;
                result.Add(new Vector2Int(Convert.ToInt32(OpeningJson.ToNumber(cell[0])),
                    Convert.ToInt32(OpeningJson.ToNumber(cell[1]))));
            }
            return result;
        }

        private void CreateBatch(Dictionary<string, object> batch, Dictionary<string, Texture2D> textures,
            Material template, Action<string> log)
        {
            string textureName = OpeningJson.String(batch, "texture");
            if (!textures.TryGetValue(textureName, out Texture2D texture))
            {
                log("官方战场贴图未绑定，跳过批次: " + textureName);
                return;
            }
            List<object> rawPositions = OpeningJson.Array(batch["positions"], "positions");
            List<object> rawUvs = OpeningJson.Array(batch["uvs"], "uvs");
            List<object> rawColors = OpeningJson.Array(batch["colors"], "colors");
            List<object> rawIndices = OpeningJson.Array(batch["indices"], "indices");
            int count = rawPositions.Count / 3;
            if (rawUvs.Count != count * 2 || rawColors.Count != count * 4)
                throw new FormatException("Official battlefield batch is malformed: " + textureName);
            var positions = new Vector3[count];
            var uvs = new Vector2[count];
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3((float)OpeningJson.ToNumber(rawPositions[i * 3]),
                    (float)OpeningJson.ToNumber(rawPositions[i * 3 + 1]),
                    (float)OpeningJson.ToNumber(rawPositions[i * 3 + 2]));
                uvs[i] = new Vector2((float)OpeningJson.ToNumber(rawUvs[i * 2]),
                    (float)OpeningJson.ToNumber(rawUvs[i * 2 + 1]));
                colors[i] = new Color((float)OpeningJson.ToNumber(rawColors[i * 4]),
                    (float)OpeningJson.ToNumber(rawColors[i * 4 + 1]),
                    (float)OpeningJson.ToNumber(rawColors[i * 4 + 2]),
                    (float)OpeningJson.ToNumber(rawColors[i * 4 + 3]));
            }
            var indices = new int[rawIndices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = Convert.ToInt32(OpeningJson.ToNumber(rawIndices[i]));

            var mesh = new Mesh { name = "OpeningBattlefield " + BatchCount.ToString("D3") };
            if (count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            meshes.Add(mesh);

            var batchObject = new GameObject("Batch " + BatchCount.ToString("D3"));
            batchObject.transform.SetParent(root.transform, false);
            batchObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = batchObject.AddComponent<MeshRenderer>();
            var material = new Material(template) { name = "OpeningBattlefield " + textureName };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            renderer.sharedMaterial = material;
            renderer.sortingOrder = OpeningJson.Int(batch, "layer") * 1000 + OpeningJson.Int(batch, "order");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            materials.Add(material);
            BatchCount++;
        }

        private void CreateParticle(Dictionary<string, object> data, Dictionary<string, Texture2D> textures,
            Action<string> log)
        {
            var particleObject = new GameObject("Particle " + (particles.Count + 1).ToString("D2"));
            particleObject.transform.SetParent(root.transform, false);
            particleObject.transform.localPosition = ReadVector(OpeningJson.Array(data["position"], "particle position"));
            float rotation = (float)OpeningJson.Number(data, "rotation");
            particleObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            List<object> scale = OpeningJson.Array(data["scale"], "particle scale");
            particleObject.transform.localScale = new Vector3((float)OpeningJson.ToNumber(scale[0]),
                (float)OpeningJson.ToNumber(scale[1]), 1f);

            ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = (float)OpeningJson.Number(data, "duration", 2);
            main.loop = OpeningJson.Bool(data, "looping");
            main.playOnAwake = OpeningJson.Bool(data, "play_on_awake");
            main.simulationSpeed = (float)OpeningJson.Number(data, "simulation_speed", 1);
            main.maxParticles = Mathf.Max(1, OpeningJson.Int(data, "max_particles", 10));
            main.startLifetime = ReadMinMax(data, "start_lifetime", 1f);
            main.startSpeed = ReadMinMax(data, "start_speed", 0f);
            main.startSize = ReadMinMax(data, "start_size", 0.5f);
            main.startRotation = ReadMinMax(data, "start_rotation", 0f);
            main.gravityModifier = ReadMinMax(data, "gravity", 0f);
            main.startColor = ReadColor(OpeningJson.Array(data["start_color"], "particle color"));
            main.scalingMode = (ParticleSystemScalingMode)OpeningJson.Int(data, "scaling_mode", 1);
            main.prewarm = OpeningJson.Bool(data, "prewarm");

            var emission = system.emission;
            emission.enabled = OpeningJson.Bool(data, "emission_enabled");
            emission.rateOverTime = ReadMinMax(data, "rate_over_time", 0f);

            var shape = system.shape;
            shape.enabled = OpeningJson.Bool(data, "shape_enabled");
            shape.shapeType = (ParticleSystemShapeType)OpeningJson.Int(data, "shape_type");
            shape.angle = (float)OpeningJson.Number(data, "shape_angle");
            shape.radius = ReadRadius(data, "shape_radius", 0.5f);

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = (ParticleSystemRenderMode)OpeningJson.Int(data, "render_mode");
            renderer.sortingOrder = OpeningJson.Int(data, "sorting_layer") * 1000
                + OpeningJson.Int(data, "sorting_order");
            Shader shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "OpeningBattlefieldFX" };
            string textureName = OpeningJson.String(data, "texture");
            if (!string.IsNullOrEmpty(textureName) && textures.TryGetValue(textureName, out Texture2D texture))
            {
                material.SetTexture("_MainTex", texture);
            }
            else
            {
                log("官方战场粒子材质贴图缺失: " + textureName);
            }
            renderer.sharedMaterial = material;
            materials.Add(material);
            particles.Add(particleObject);
            system.Play();
        }

        private static ParticleSystem.MinMaxCurve ReadMinMax(Dictionary<string, object> data, string key,
            float fallback)
        {
            if (!data.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 2)
                return new ParticleSystem.MinMaxCurve(fallback);
            float minimum = (float)OpeningJson.ToNumber(list[0]);
            float maximum = (float)OpeningJson.ToNumber(list[1]);
            return Mathf.Approximately(minimum, maximum)
                ? new ParticleSystem.MinMaxCurve(minimum)
                : new ParticleSystem.MinMaxCurve(minimum, maximum);
        }

        private static float ReadRadius(Dictionary<string, object> data, string key, float fallback)
        {
            if (!data.TryGetValue(key, out object value) || value == null) return fallback;
            if (value is List<object> list && list.Count >= 2)
                return Mathf.Max(0f, (float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]));
            return fallback;
        }

        private Camera CreateCamera(Dictionary<string, object> data)
        {
            Dictionary<string, object> selected = null;
            foreach (object entry in OpeningJson.Array(data["cameras"], "cameras"))
            {
                Dictionary<string, object> cameraData = OpeningJson.Object(entry, "camera");
                string name = OpeningJson.String(cameraData, "name");
                if (name == "Main Camera")
                {
                    selected = cameraData;
                    break;
                }
                if (OpeningJson.Bool(cameraData, "active") && (selected == null
                    || OpeningJson.Number(cameraData, "depth") < OpeningJson.Number(selected, "depth")))
                    selected = cameraData;
            }
            if (selected == null) throw new FormatException("Official battlefield has no camera.");
            var cameraObject = new GameObject("OpeningFightCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = ReadVector(OpeningJson.Array(selected["position"], "camera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = OpeningJson.Bool(selected, "orthographic");
            camera.orthographicSize = (float)OpeningJson.Number(selected, "orthographic_size", 7);
            camera.nearClipPlane = (float)OpeningJson.Number(selected, "near", 0.3f);
            camera.farClipPlane = (float)OpeningJson.Number(selected, "far", 100f);
            camera.depth = (float)OpeningJson.Number(selected, "depth", 0f) + 10f;
            camera.clearFlags = (CameraClearFlags)OpeningJson.Int(selected, "clear_flags",
                (int)CameraClearFlags.SolidColor);
            camera.cullingMask = OpeningJson.Int(selected, "culling_mask", -1);
            if (selected.TryGetValue("viewport", out object rawViewport)
                && rawViewport is List<object> viewport && viewport.Count >= 4)
                camera.rect = new Rect((float)OpeningJson.ToNumber(viewport[0]),
                    (float)OpeningJson.ToNumber(viewport[1]), (float)OpeningJson.ToNumber(viewport[2]),
                    (float)OpeningJson.ToNumber(viewport[3]));
            if (OpeningJson.String(selected, "name") == "Main Camera") cameraObject.tag = "MainCamera";
            Color background = ReadColor(OpeningJson.Array(selected["background"], "camera background"));
            if (background.a <= 0.001f) background.a = 1f;
            camera.backgroundColor = background;
            return camera;
        }

        /// <summary>Official battle Spine skeleton bound to an NPC class id (NpcClassId 84xx).</summary>
        public bool TryGetNpcModel(int classId, out string key) => npcModels.TryGetValue(classId, out key);

        /// <summary>Official grid cell to world position inside the stage root.</summary>
        public Vector3 CellToWorld(int x, int y) => GridToWorld(x, y);

        public Vector3 GridToWorld(float x, float y) =>
            new Vector3(x, y, 0f) + RootOffset + gridOrigin;

        public void Dispose()
        {
            foreach (GameObject particle in particles) if (particle != null) UnityEngine.Object.Destroy(particle);
            particles.Clear();
            foreach (Mesh mesh in meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            meshes.Clear();
            foreach (Material material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        private static Vector3 ReadVector(List<object> values)
        {
            return new Vector3((float)OpeningJson.ToNumber(values[0]), (float)OpeningJson.ToNumber(values[1]),
                values.Count > 2 ? (float)OpeningJson.ToNumber(values[2]) : 0f);
        }

        private static Color ReadColor(List<object> values)
        {
            return new Color((float)OpeningJson.ToNumber(values[0]), (float)OpeningJson.ToNumber(values[1]),
                (float)OpeningJson.ToNumber(values[2]), (float)OpeningJson.ToNumber(values[3]));
        }
    }
}
