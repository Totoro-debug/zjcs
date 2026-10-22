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
        private readonly List<HomeSceneLayer> furnitureLayers = new List<HomeSceneLayer>();

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
                if (!clickRegions.TryGetValue(type, out List<HomeQuad> list))
                    clickRegions[type] = list = new List<HomeQuad>();
                list.Add(region);
            }
        }

        private void CreateQuad(HomeSceneLayer layer, int order, Transform parent)
        {
            Texture2D texture = catalog.FindTexture(layer.sprite?.file);
            if (texture == null) return;
            var go = new GameObject(layer.sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = layer.path };
            var vertices = new Vector3[4];
            for (int index = 0; index < 4; index++)
                vertices[index] = new Vector3(layer.vertices[index * 2], layer.vertices[index * 2 + 1], 0);
            mesh.vertices = vertices;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Color color = Color.white;
            if (layer.color != null && layer.color.Length >= 4)
                color = new Color(layer.color[0], layer.color[1], layer.color[2], layer.color[3]);
            mesh.colors = new[] { color, color, color, color };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            filter.mesh = mesh;
            renderer.sharedMaterial = Material(texture);
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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

        /// <summary>Walks the official click quadrilaterals in world space.</summary>
        public int HitTest(Vector2 world)
        {
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
                    if (world.x >= minX && world.x <= maxX && world.y >= minY && world.y <= maxY) return pair.Key;
                }
            return -1;
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
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }
    }

    /// <summary>Mouse drag/click bridge with the official 1px drag threshold.</summary>
    public sealed class HomeInputController
    {
        private const float DragThresholdSquared = 1f;

        private readonly HomeSceneView scene;
        private readonly Func<bool> blocked;
        private Vector2 pressPosition;
        private Vector2 lastPosition;
        private bool dragging;
        private bool pressed;

        public HomeInputController(HomeSceneView scene, Func<bool> blocked = null)
        {
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            this.blocked = blocked;
        }

        public event Action<int> FurnitureClicked;

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
                }
                dragging = false;
            }
        }
    }
}
