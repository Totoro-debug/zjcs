using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Rebuilds the official home scene from ``Home/scene.json``: every sprite
    /// layer keeps its serialized quad, sorting order and colour, and furniture
    /// switches between the official ``Repaired``/``Unrepair`` subtrees.
    /// </summary>
    public sealed class HomeSceneView
    {
        private static readonly Dictionary<string, int> FurnitureRoots = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Bed", 0 }, { "Desk", 1 }, { "DiningTable", 2 }, { "Danlu", 3 }, { "FruitTree", 4 },
            { "StatueOfGod", 5 }, { "PostBox", 6 }, { "StorageBox", 7 }, { "VendingMachine", 8 },
            { "GachaChest", 9 }, { "Cart", 10 }, { "AstrologicalDestiny", 12 }, { "Airship", 13 }
        };

        private readonly HomeAssetCatalog catalog;
        private readonly HomeCameraData camera;
        private readonly Dictionary<int, GameObject> repaired = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> unrepaired = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, List<HomeQuad>> clickRegions = new Dictionary<int, List<HomeQuad>>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly List<Mesh> createdMeshes = new List<Mesh>();
        private readonly List<HomeSceneLayer> furnitureLayers = new List<HomeSceneLayer>();
        private readonly List<KeyValuePair<string, GameObject>> layerObjects = new List<KeyValuePair<string, GameObject>>();
        private readonly Dictionary<string, GameObject> fruitTreeLayers = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> fruitTreeNativeObjects = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<ParticleSystem, FruitTreeParticleState> fruitTreeParticles =
            new Dictionary<ParticleSystem, FruitTreeParticleState>();
        private readonly List<GameObject> fruitObjects = new List<GameObject>();
        private readonly List<TreeMeshView> treeMeshViews = new List<TreeMeshView>();
        private readonly HashSet<string> transientFruitTreeEffects = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> activeFruitTreePaths = new HashSet<string>(StringComparer.Ordinal);
        private HomeTreeAnimation treeAnimation;
        private HomeFruitTreeStateAnimation[] treeStateAnimations;
        private GameObject treeRoot;
        private GameObject fruitTreeParticleRoot;
        private GameObject fruitTreeNightLight;
        private int treeFrameIndex;
        private float treeFrameTimer;
        private HomeFruitTreeScene fruitTreeData;

        public GameObject Root { get; }
        public Camera Camera { get; private set; }
        public Vector3 Position => Camera == null ? Vector3.zero : Camera.transform.position;
        public float OrthographicSize => camera.orthographic_size <= 0 ? 7.111f : camera.orthographic_size;
        public float DragSensitivity => camera.drag_sensitivity <= 0 ? 1.3f : camera.drag_sensitivity;

        public HomeSceneView(HomeAssetCatalog catalog, Transform parent)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            camera = catalog.Scene.camera;
            Root = new GameObject("HomeScene");
            Root.transform.SetParent(parent, false);
            BuildCamera();
            BuildBackground();
            BuildFurniture();
            BuildFruitTree();
            BuildClickRegions();
        }

        private void BuildCamera()
        {
            var go = new GameObject("Home Camera", typeof(Camera));
            go.transform.SetParent(Root.transform, false);
            Camera = go.GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = OrthographicSize;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.07f, 0.08f, 0.12f, 1f);
            Camera.nearClipPlane = -100f;
            Camera.farClipPlane = 100f;
            Camera.transparencySortMode = TransparencySortMode.CustomAxis;
            Camera.transparencySortAxis = new Vector3(0, 0, 1);
            float[] position = camera.position ?? new[] { 0.49f, -1.92f, -10f };
            Vector3 start = new Vector3(position[0], position[1], position.Length > 2 ? position[2] : -10f);
            Camera.transform.position = Clamp(start);
        }

        private void BuildBackground()
        {
            int order = 0;
            foreach (HomeSceneLayer layer in catalog.Scene.background ?? Array.Empty<HomeSceneLayer>())
            {
                if (layer == null || layer.vertices == null || layer.vertices.Length < 8) continue;
                string group = RootGroup(layer.path);
                if (FurnitureRoots.ContainsKey(group) && group != "FruitTree") continue;
                CreateQuad(layer, order++, Root.transform);
            }
        }

        private void BuildFurniture()
        {
            foreach (HomeFurnitureScene furniture in catalog.Scene.furniture ?? Array.Empty<HomeFurnitureScene>())
            {
                if (furniture == null) continue;
                var repairedRoot = new GameObject("Furniture_" + furniture.type + "_Repaired");
                var unrepairedRoot = new GameObject("Furniture_" + furniture.type + "_Unrepair");
                repairedRoot.transform.SetParent(Root.transform, false);
                unrepairedRoot.transform.SetParent(Root.transform, false);
                repairedRoot.SetActive(false);
                unrepairedRoot.SetActive(false);
                int repairedOrder = 0;
                int unrepairedOrder = 0;
                foreach (HomeSceneLayer layer in furniture.layers ?? Array.Empty<HomeSceneLayer>())
                {
                    if (layer == null || layer.vertices == null || layer.vertices.Length < 8) continue;
                    furnitureLayers.Add(layer);
                    bool isRepaired = IsRepairedLayer(layer.path);
                    CreateQuad(layer, isRepaired ? repairedOrder++ : unrepairedOrder++,
                        isRepaired ? repairedRoot.transform : unrepairedRoot.transform);
                }
                repaired[furniture.type] = repairedRoot;
                unrepaired[furniture.type] = unrepairedRoot;
            }
        }

        private void BuildClickRegions()
        {
            foreach (HomeQuad region in catalog.Scene.click_regions ?? Array.Empty<HomeQuad>())
            {
                if (region == null || region.vertices == null || region.vertices.Length < 8) continue;
                string group = RootGroup(region.path);
                if (!FurnitureRoots.TryGetValue(group, out int type)) continue;
                if (group == "FruitTree" && (region.path ?? string.Empty).Contains("/HomeFruitTreePet/"))
                    type = 11;
                if (!clickRegions.TryGetValue(type, out List<HomeQuad> list))
                    clickRegions[type] = list = new List<HomeQuad>();
                list.Add(region);
            }
        }

        private GameObject CreateQuad(HomeSceneLayer layer, int order, Transform parent,
            Vector3? position = null, bool trackLayer = true)
        {
            Texture2D texture = catalog.FindTexture(layer.sprite?.file);
            if (texture == null) return null;
            var go = new GameObject(layer.sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.position = position ?? Vector3.zero;
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = layer.path };
            createdMeshes.Add(mesh);
            var vertices = new Vector3[4];
            for (int index = 0; index < 4; index++)
                vertices[index] = new Vector3(layer.vertices[index * 2], layer.vertices[index * 2 + 1], 0);
            mesh.vertices = vertices;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Color color = Color.white;
            if (layer.color != null && layer.color.Length >= 4)
                color = new Color(layer.color[0], layer.color[1], layer.color[2], layer.color[3]);
            HomeColorMap palette = layer.materials != null && layer.materials.Length > 0 ? layer.materials[0].colors : null;
            if (palette != null && palette._Color1 != null && palette._Color1.Length >= 4
                && palette._Color2 != null && palette._Color2.Length >= 4)
            {
                // The official sky/background ColorBlend materials author a two-colour
                // gradient; the extracted animation clips that animate them are not
                // available yet, so the serialized base colours are applied per vertex.
                Color bottom = new Color(palette._Color1[0], palette._Color1[1], palette._Color1[2], palette._Color1[3]) * color;
                Color top = new Color(palette._Color2[0], palette._Color2[1], palette._Color2[2], palette._Color2[3]) * color;
                mesh.colors = new[] { bottom, bottom, top, top };
            }
            else
            {
                mesh.colors = new[] { color, color, color, color };
            }
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            filter.mesh = mesh;
            if (trackLayer) layerObjects.Add(new KeyValuePair<string, GameObject>(layer.path ?? string.Empty, go));
            renderer.sharedMaterial = Material(texture);
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void BuildFruitTree()
        {
            fruitTreeData = catalog.Scene.fruit_tree;
            if (fruitTreeData == null) return;
            treeStateAnimations = fruitTreeData.state_animations ?? Array.Empty<HomeFruitTreeStateAnimation>();

            foreach (HomeSceneLayer layer in fruitTreeData.layers ?? Array.Empty<HomeSceneLayer>())
            {
                if (layer == null || string.IsNullOrEmpty(layer.path)) continue;
                bool subFurniture = layer.path.Contains("/SubFurnitures/");
                bool rope = layer.path == "/FruitTree/FruitTree_Rope";
                bool nightLight = layer.path.EndsWith("/Light_Glow", StringComparison.Ordinal);
                if (!subFurniture && !rope && !nightLight) continue;
                GameObject go = CreateQuad(layer, 900 + layer.sorting_order, Root.transform, trackLayer: false);
                if (go == null) continue;
                go.name = layer.path;
                if (subFurniture)
                {
                    fruitTreeLayers[layer.path] = go;
                    go.SetActive(false);
                }
                else if (nightLight)
                {
                    fruitTreeNightLight = go;
                    go.SetActive(false);
                }
            }

            BuildFruitTreeParticles();

            HomeSceneLayer fruitEffect = fruitTreeData.fruit_effect;
            foreach (HomeFruitTreeSlot slot in fruitTreeData.fruit_slots ?? Array.Empty<HomeFruitTreeSlot>())
            {
                if (slot?.position == null || slot.position.Length < 2) continue;
                Vector3 position = new Vector3(slot.position[0], slot.position[1],
                    slot.position.Length > 2 ? slot.position[2] : 0f);
                GameObject fruit = CreateQuad(fruitEffect, 1600, Root.transform, position, trackLayer: false);
                if (fruit == null) continue;
                float[] scale = slot.scale ?? Array.Empty<float>();
                fruit.transform.localScale = new Vector3(scale.Length > 0 ? scale[0] : 1f,
                    scale.Length > 1 ? scale[1] : 1f, 1f);
                fruit.name = slot.path;
                fruit.SetActive(false);
                fruitObjects.Add(fruit);
            }

            treeRoot = new GameObject("FruitTreeSkeleton");
            treeRoot.transform.SetParent(Root.transform, false);
            float[] treePosition = fruitTreeData.tree_position ?? Array.Empty<float>();
            treeRoot.transform.localPosition = new Vector3(treePosition.Length > 0 ? treePosition[0] : 0f,
                treePosition.Length > 1 ? treePosition[1] : 0f,
                treePosition.Length > 2 ? treePosition[2] : 0f);
            treeRoot.transform.localScale = Vector3.one * (fruitTreeData.tree_scale <= 0f ? 1f : fruitTreeData.tree_scale);
            HomeTreeAnimation initial = FindTreeAnimation("loop_3")
                ?? (fruitTreeData.animations ?? Array.Empty<HomeTreeAnimation>()).FirstOrDefault();
            SetTreeAnimation(initial);
        }

        private HomeTreeAnimation FindTreeAnimation(string name)
        {
            return (fruitTreeData?.animations ?? Array.Empty<HomeTreeAnimation>())
                .FirstOrDefault(value => value != null && value.name == name);
        }

        private void SetTreeAnimation(HomeTreeAnimation value)
        {
            if (value == null || treeAnimation == value) return;
            treeAnimation = value;
            treeFrameIndex = 0;
            treeFrameTimer = 0f;
            ApplyTreeFrame(0);
        }

        public void SetFruitTreeState(HomeFurnitureState production, HomeFurnitureState pet, long matureCount)
        {
            if (fruitTreeData == null) return;
            int rootState = 0;
            HomeFurnitureState[] states = { production, pet };
            int[] types = { 4, 11 };
            var activePaths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < states.Length; index++)
            {
                int state = (int)states[index];
                if (state != (int)HomeFurnitureState.Locked)
                {
                    rootState = index * 2 + 1;
                    if (states[index] == HomeFurnitureState.Functional) rootState++;
                }
                AddStatePaths(activePaths, types[index] + ":" + state);
            }
            AddStatePaths(activePaths, "root:" + rootState);

            foreach (KeyValuePair<string, GameObject> pair in fruitTreeLayers)
                pair.Value.SetActive(activePaths.Any(path => IsPathOrParent(path, pair.Key)));

            activeFruitTreePaths.Clear();
            activeFruitTreePaths.UnionWith(activePaths);
            ApplyFruitTreeParticleState();

            if (treeRoot != null) treeRoot.SetActive(activePaths.Contains("/FruitTree/Tree"));
            int visibleFruits = activePaths.Contains("/FruitTree/Fruits")
                ? Mathf.Clamp((int)Math.Min(matureCount, fruitObjects.Count), 0, fruitObjects.Count) : 0;
            for (int index = 0; index < fruitObjects.Count; index++)
                fruitObjects[index].SetActive(index < visibleFruits);

            string animationName = treeStateAnimations.FirstOrDefault(value => value != null
                && value.state == rootState)?.name;
            SetTreeAnimation(FindTreeAnimation(animationName));
        }

        private void AddStatePaths(HashSet<string> activePaths, string stateKey)
        {
            HomeFruitTreeState state = (fruitTreeData.states ?? Array.Empty<HomeFruitTreeState>())
                .FirstOrDefault(value => value != null && value.key == stateKey);
            foreach (string path in state?.paths ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(path)) activePaths.Add(path);
        }

        private void BuildFruitTreeParticles()
        {
            GameObject prefab = Resources.Load<GameObject>("FruitTree/FruitTreeParticles");
            if (prefab == null)
                throw new InvalidOperationException("Official fruit tree particle prefab is unavailable.");
            fruitTreeParticleRoot = UnityEngine.Object.Instantiate(prefab, Root.transform, false);
            fruitTreeParticleRoot.name = "FruitTree";
            float[] position = fruitTreeData.tree_position ?? Array.Empty<float>();
            fruitTreeParticleRoot.transform.localPosition = new Vector3(position.Length > 0 ? position[0] : 0f,
                position.Length > 1 ? position[1] : 0f, position.Length > 2 ? position[2] : 0f);
            float scale = fruitTreeData.tree_scale <= 0f ? 1f : fruitTreeData.tree_scale;
            fruitTreeParticleRoot.transform.localScale = Vector3.one * scale;
            IndexFruitTreeNativeObjects(fruitTreeParticleRoot.transform, "/FruitTree");
            foreach (ParticleSystem system in fruitTreeParticleRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Transform current = system.transform;
                string path = NativePath(current);
                fruitTreeParticles.Add(system, new FruitTreeParticleState { Path = path });
            }
        }

        private void IndexFruitTreeNativeObjects(Transform current, string path)
        {
            fruitTreeNativeObjects[path] = current.gameObject;
            for (int index = 0; index < current.childCount; index++)
            {
                Transform child = current.GetChild(index);
                IndexFruitTreeNativeObjects(child, path + "/" + child.name);
            }
        }

        private string NativePath(Transform target)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null && current != fruitTreeParticleRoot.transform)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return "/FruitTree/" + string.Join("/", names.ToArray());
        }

        private void ApplyFruitTreeParticleState()
        {
            foreach (KeyValuePair<ParticleSystem, FruitTreeParticleState> pair in fruitTreeParticles)
            {
                bool shouldPlay = activeFruitTreePaths.Any(path => IsPathOrParent(path, pair.Value.Path))
                    || transientFruitTreeEffects.Any(path => IsPathOrParent(path, pair.Value.Path));
                if (pair.Value.ShouldPlay == shouldPlay) continue;
                pair.Value.ShouldPlay = shouldPlay;
                if (shouldPlay)
                {
                    pair.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    pair.Key.Play(true);
                }
                else
                {
                    pair.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        public void PlayFruitTreeRepairEffect(int furnitureType)
        {
            string facility = furnitureType == 11 ? "HomeFruitTreePet" : "HomeFruitTreeProduceFruit";
            string path = "/FruitTree/SubFurnitures/" + facility
                + "/Other/Eff_Ui_Home_Appear_Quad";
            if (!fruitTreeParticles.Keys.Any(system => IsPathOrParent(path, NativePath(system.transform)))) return;
            transientFruitTreeEffects.Add(path);
            foreach (KeyValuePair<ParticleSystem, FruitTreeParticleState> pair in fruitTreeParticles)
            {
                if (!IsPathOrParent(path, pair.Value.Path)) continue;
                pair.Value.ShouldPlay = true;
                pair.Key.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                pair.Key.Play(true);
            }
        }

        private static bool IsPathOrParent(string parent, string path)
        {
            return string.Equals(parent, path, StringComparison.Ordinal)
                || path.StartsWith(parent.TrimEnd('/') + "/", StringComparison.Ordinal);
        }

        public void Tick(float deltaTime)
        {
            RefreshTransientFruitTreeEffects();
            if (treeRoot == null || !treeRoot.activeSelf || treeAnimation?.frames == null
                || treeAnimation.frames.Length < 2 || deltaTime <= 0f) return;
            float frameDuration = 1f / Mathf.Max(1f, treeAnimation.fps);
            treeFrameTimer += deltaTime;
            while (treeFrameTimer >= frameDuration)
            {
                treeFrameTimer -= frameDuration;
                treeFrameIndex = (treeFrameIndex + 1) % treeAnimation.frames.Length;
                ApplyTreeFrame(treeFrameIndex);
            }
        }

        private void RefreshTransientFruitTreeEffects()
        {
            foreach (string path in transientFruitTreeEffects.ToArray())
            {
                bool alive = fruitTreeParticles.Any(pair => IsPathOrParent(path, pair.Value.Path)
                    && pair.Key != null && pair.Key.IsAlive(true));
                if (alive) continue;
                transientFruitTreeEffects.Remove(path);
                ApplyFruitTreeParticleState();
            }
        }

        private void ApplyTreeFrame(int frameIndex)
        {
            HomeTreeFrame frame = treeAnimation?.frames != null && frameIndex >= 0
                && frameIndex < treeAnimation.frames.Length ? treeAnimation.frames[frameIndex] : null;
            HomeTreeMesh[] meshes = frame?.meshes ?? Array.Empty<HomeTreeMesh>();
            while (treeMeshViews.Count < meshes.Length)
            {
                var go = new GameObject("FruitTreeMesh_" + treeMeshViews.Count, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(treeRoot.transform, false);
                var view = new TreeMeshView
                {
                    Object = go,
                    Mesh = new Mesh { name = go.name },
                    Filter = go.GetComponent<MeshFilter>(),
                    Renderer = go.GetComponent<MeshRenderer>()
                };
                createdMeshes.Add(view.Mesh);
                view.Filter.sharedMesh = view.Mesh;
                view.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.Renderer.receiveShadows = false;
                treeMeshViews.Add(view);
            }

            for (int index = 0; index < treeMeshViews.Count; index++)
            {
                TreeMeshView view = treeMeshViews[index];
                if (index >= meshes.Length)
                {
                    view.Object.SetActive(false);
                    continue;
                }
                HomeTreeMesh source = meshes[index];
                Texture2D texture = catalog.FindTexture(source.page);
                if (texture == null) throw new InvalidOperationException("Official fruit tree texture is missing: " + source.page);
                view.Object.SetActive(true);
                view.Mesh.Clear();
                view.Mesh.vertices = TreeVertices(source.vertices, treeAnimation.scale);
                view.Mesh.uv = Vector2Values(source.uv);
                view.Mesh.triangles = source.triangles ?? Array.Empty<int>();
                Color color = Color.white;
                if (source.color != null && source.color.Length >= 4)
                    color = new Color(source.color[0], source.color[1], source.color[2], source.color[3]);
                Vector3[] vertices = view.Mesh.vertices;
                var colors = new Color[vertices.Length];
                for (int vertex = 0; vertex < colors.Length; vertex++) colors[vertex] = color;
                view.Mesh.colors = colors;
                view.Mesh.RecalculateBounds();
                view.Renderer.sharedMaterial = TreeMaterial(texture, source.blend);
                view.Renderer.sortingOrder = 900 + source.order;
            }
        }

        private static Vector3[] TreeVertices(float[] values, float scale)
        {
            if (values == null || values.Length < 6) return Array.Empty<Vector3>();
            var result = new Vector3[values.Length / 2];
            for (int index = 0; index < result.Length; index++)
                result[index] = new Vector3(values[index * 2] * scale, values[index * 2 + 1] * scale, 0f);
            return result;
        }

        private static Vector2[] Vector2Values(float[] values)
        {
            if (values == null || values.Length < 4) return Array.Empty<Vector2>();
            var result = new Vector2[values.Length / 2];
            for (int index = 0; index < result.Length; index++)
                result[index] = new Vector2(values[index * 2], values[index * 2 + 1]);
            return result;
        }

        private Material TreeMaterial(Texture2D texture, int blend)
        {
            if (blend == 0) return Material(texture);
            string key = texture.name + "#additive";
            if (materials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader shader = Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "FruitTree_Additive_" + texture.name, mainTexture = texture };
            materials[key] = material;
            return material;
        }

        private sealed class TreeMeshView
        {
            public GameObject Object;
            public Mesh Mesh;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        private Material Material(Texture2D texture)
        {
            string key = texture.name;
            if (materials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            var material = new Material(shader) { name = "Home_" + key, mainTexture = texture };
            materials[key] = material;
            return material;
        }

        /// <summary>Applies the official four-state switch to one furniture type.</summary>
        public void SetFurnitureState(int type, HomeFurnitureState state)
        {
            bool functional = state == HomeFurnitureState.Functional;
            if (repaired.TryGetValue(type, out GameObject on)) on.SetActive(functional);
            if (unrepaired.TryGetValue(type, out GameObject off)) off.SetActive(!functional);
        }

        public void SetAllFurnitureVisible(bool visible) { Root.SetActive(visible); }

        /// <summary>Toggles the official light groups by their serialized node path.</summary>
        public void SetLayerGroupVisible(string pathFragment, bool visible)
        {
            if (string.IsNullOrEmpty(pathFragment)) return;
            foreach (KeyValuePair<string, GameObject> pair in layerObjects)
            {
                if (pair.Value == null || pair.Key.IndexOf(pathFragment, StringComparison.Ordinal) < 0) continue;
                pair.Value.SetActive(visible);
            }
        }

        public void SetNightVisuals(bool night)
        {
            SetLayerGroupVisible("/Background/Back_Ground/FX_Star", night);
            if (fruitTreeNightLight != null) fruitTreeNightLight.SetActive(night);
        }

        /// <summary>Walks the official click quadrilaterals in world space.</summary>
        public int HitTest(Vector2 world)
        {
            int hitType = -1;
            float hitArea = float.MaxValue;
            foreach (KeyValuePair<int, List<HomeQuad>> pair in clickRegions)
                foreach (HomeQuad region in pair.Value)
                {
                    float minX = float.MaxValue;
                    float maxX = float.MinValue;
                    float minY = float.MaxValue;
                    float maxY = float.MinValue;
                    for (int index = 0; index + 1 < region.vertices.Length; index += 2)
                    {
                        minX = Mathf.Min(minX, region.vertices[index]);
                        maxX = Mathf.Max(maxX, region.vertices[index]);
                        minY = Mathf.Min(minY, region.vertices[index + 1]);
                        maxY = Mathf.Max(maxY, region.vertices[index + 1]);
                    }
                    float area = (maxX - minX) * (maxY - minY);
                    if (area < hitArea && world.x >= minX && world.x <= maxX
                        && world.y >= minY && world.y <= maxY)
                    {
                        hitArea = area;
                        hitType = pair.Key;
                    }
                }
            return hitType;
        }

        public bool TryGetFurnitureClickBounds(int type, out Rect bounds)
        {
            bounds = default;
            if (!clickRegions.TryGetValue(type, out List<HomeQuad> regions)) return false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool found = false;
            foreach (HomeQuad region in regions)
            {
                if (region?.vertices == null || region.vertices.Length < 8) continue;
                for (int index = 0; index + 1 < region.vertices.Length; index += 2)
                {
                    minX = Mathf.Min(minX, region.vertices[index]);
                    maxX = Mathf.Max(maxX, region.vertices[index]);
                    minY = Mathf.Min(minY, region.vertices[index + 1]);
                    maxY = Mathf.Max(maxY, region.vertices[index + 1]);
                }
                found = true;
            }
            if (!found) return false;
            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return bounds.width > 0f && bounds.height > 0f;
        }

        public Vector3 Clamp(Vector3 position)
        {
            if (camera.border_center == null || camera.border_extents == null) return position;
            float aspect = Camera == null ? 0.5625f : Camera.aspect;
            float extX = OrthographicSize * aspect;
            float extY = OrthographicSize;
            float minX = camera.border_center[0] - camera.border_extents[0] + extX;
            float maxX = camera.border_center[0] + camera.border_extents[0] - extX;
            float minY = camera.border_center[1] - camera.border_extents[1] + extY;
            float maxY = camera.border_center[1] + camera.border_extents[1] - extY;
            return new Vector3(
                maxX < minX ? camera.border_center[0] : Mathf.Clamp(position.x, minX, maxX),
                maxY < minY ? camera.border_center[1] : Mathf.Clamp(position.y, minY, maxY),
                position.z);
        }

        public Vector2 ScreenToWorld(Vector2 screen) => Camera.ScreenToWorldPoint(screen);

        public void ResetCamera()
        {
            if (Camera == null || camera.position == null) return;
            Camera.transform.position = Clamp(new Vector3(camera.position[0], camera.position[1],
                camera.position.Length > 2 ? camera.position[2] : -10f));
        }

        public void MoveCamera(Vector2 deltaPixels)
        {
            if (Camera == null) return;
            float unitsPerPixel = 2f * OrthographicSize / Mathf.Max(1f, Screen.height);
            Vector3 next = Camera.transform.position
                - new Vector3(deltaPixels.x, deltaPixels.y, 0) * unitsPerPixel * DragSensitivity;
            Camera.transform.position = Clamp(next);
        }

        private static string RootGroup(string path)
        {
            string trimmed = (path ?? string.Empty).TrimStart('/');
            int slash = trimmed.IndexOf('/');
            return slash < 0 ? trimmed : trimmed.Substring(0, slash);
        }

        private static bool IsRepairedLayer(string path)
        {
            string trimmed = (path ?? string.Empty).TrimStart('/');
            string[] parts = trimmed.Split('/');
            if (parts.Length < 2) return true;
            string state = parts[1];
            return state.IndexOf("unrepair", StringComparison.OrdinalIgnoreCase) < 0
                && state.IndexOf("un_repaired", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public void Dispose()
        {
            foreach (Material material in materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear();
            foreach (Mesh mesh in createdMeshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            createdMeshes.Clear();
            fruitTreeParticles.Clear();
            fruitTreeNativeObjects.Clear();
            transientFruitTreeEffects.Clear();
            activeFruitTreePaths.Clear();
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }

        private sealed class FruitTreeParticleState
        {
            public string Path;
            public bool ShouldPlay;
        }
    }

    /// <summary>Mouse drag/click bridge with the official 1px drag threshold.</summary>
    public sealed class HomeInputController
    {
        private const float DragThresholdSquared = 1f;

        private readonly HomeSceneView scene;
        private readonly Func<bool> blocked;
        private Func<Vector2, int> rewardHit;
        private Vector2 pressPosition;
        private Vector2 lastPosition;
        private bool dragging;
        private bool pressed;

        public void SetRewardHit(Func<Vector2, int> hit) => rewardHit = hit;

        public HomeInputController(HomeSceneView scene, Func<bool> blocked = null)
        {
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            this.blocked = blocked;
        }

        public event Action<int> FurnitureClicked;
        public event Action<int> RewardClicked;

        public void Update()
        {
            if (scene?.Camera == null) return;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) && !overUi)
            {
                pressed = true;
                dragging = false;
                pressPosition = Input.mousePosition;
                lastPosition = pressPosition;
            }
            if (pressed && Input.GetMouseButton(0))
            {
                Vector2 current = Input.mousePosition;
                if (!dragging && (current - pressPosition).sqrMagnitude >= DragThresholdSquared) dragging = true;
                if (dragging) scene.MoveCamera(current - lastPosition);
                lastPosition = current;
            }
            if (pressed && Input.GetMouseButtonUp(0))
            {
                pressed = false;
                if (!dragging && !overUi && (blocked == null || !blocked()))
                {
                    Vector2 world = scene.ScreenToWorld(Input.mousePosition);
                    int type = scene.HitTest(world);
                    if (type >= 0) FurnitureClicked?.Invoke(type);
                    else
                    {
                        int reward = rewardHit?.Invoke(world) ?? -1;
                        if (reward >= 0) RewardClicked?.Invoke(reward);
                    }
                }
                dragging = false;
            }
        }
    }
}
