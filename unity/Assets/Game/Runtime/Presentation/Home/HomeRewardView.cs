using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class HomeRewardSlotVisual
    {
        public int index;
        public float[] position;
        public int order;
        public int direction;
        public int prefabIndex;
    }

    [Serializable]
    public sealed class HomeLeafVisual
    {
        public string name;
        public string sprite;
        public int order;
        public float[] vertices;
    }

    [Serializable]
    public sealed class HomeAnimalLayer
    {
        public string page;
        public int order;
        public float[] vertices;
        public float[] uv;
        public int[] triangles;
    }

    [Serializable]
    public sealed class HomeAnimalModel
    {
        public float scale;
        public HomeAnimalLayer[] layers;
    }

    [Serializable]
    public sealed class HomeAnimalVisual
    {
        public int itemId;
        public HomeAnimalModel[] models;
    }

    [Serializable]
    public sealed class HomeRewardVisualData
    {
        public string version;
        public HomeRewardSlotVisual[] slots;
        public HomeLeafVisual[] leaves;
        public HomeAnimalVisual[] animals;
    }

    /// <summary>
    /// Instantiates the official 29 random-reward slots with the leaf sprite or the
    /// animal's first spine pose, and routes a click on the slot to the local
    /// collection request.  Animal animations are not included in the extracted
    /// payload, so only the first official pose is drawn.
    /// </summary>
    public sealed class HomeRewardView
    {
        private readonly HomeFeatureRuntime host;
        private readonly HomeAssetCatalog catalog;
        private readonly HomeRewardVisualData data;
        private readonly Dictionary<int, GameObject> items = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Bounds> bounds = new Dictionary<int, Bounds>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Transform root;

        public HomeRewardView(HomeFeatureRuntime host, HomeAssetCatalog catalog, Transform parent)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            string path = Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName,
                "reward-visuals.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Home reward visuals are missing.", path);
            data = JsonUtility.FromJson<HomeRewardVisualData>(File.ReadAllText(path));
            if (data == null || data.slots == null || data.slots.Length != 29)
                throw new InvalidDataException("The official random reward slot list is incomplete.");
            var go = new GameObject("HomeRandomRewards");
            go.transform.SetParent(parent, false);
            root = go.transform;
            Refresh();
        }

        public void Refresh()
        {
            foreach (GameObject item in items.Values) if (item != null) UnityEngine.Object.Destroy(item);
            items.Clear();
            bounds.Clear();
            foreach (OfflineHomeRandomRewardSlot reward in host.Session.RandomRewards)
            {
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0) continue;
                HomeRewardSlotVisual slot = data.slots.FirstOrDefault(value => value.index == reward.Slot);
                if (slot == null) continue;
                GameObject item = CreateItem(reward, slot);
                if (item == null) continue;
                items[reward.Slot] = item;
            }
        }

        private GameObject CreateItem(OfflineHomeRandomRewardSlot reward, HomeRewardSlotVisual slot)
        {
            var item = new GameObject("Reward_" + slot.index);
            item.transform.SetParent(root, false);
            Vector3 position = slot.position != null && slot.position.Length >= 2
                ? new Vector3(slot.position[0], slot.position[1], 0) : Vector3.zero;
            item.transform.position = position;
            bool flip = slot.direction == 2 || (slot.direction == 0 && (slot.index & 1) == 1);
            if (flip) item.transform.localScale = new Vector3(-1, 1, 1);
            bool created;
            if (reward.ItemId == 6013) created = BuildLeaf(item.transform, slot.order);
            else created = BuildAnimal(item.transform, reward.ItemId, slot.order);
            if (!created)
            {
                UnityEngine.Object.Destroy(item);
                return null;
            }
            bounds[slot.index] = new Bounds(position, new Vector3(1.6f, 1.6f, 0.1f));
            return item;
        }

        private bool BuildLeaf(Transform parent, int order)
        {
            HomeLeafVisual leaf = data.leaves != null && data.leaves.Length > 0
                ? data.leaves[Mathf.Abs(parent.name.GetHashCode()) % data.leaves.Length] : null;
            if (leaf == null || leaf.sprite == null || leaf.vertices == null || leaf.vertices.Length < 8) return false;
            Texture2D texture = catalog.FindTexture(leaf.sprite);
            if (texture == null) return false;
            CreateQuad(parent, texture, leaf.vertices, order, new Vector3(0.5f, 0.5f, 1));
            return true;
        }

        private bool BuildAnimal(Transform parent, int itemId, int order)
        {
            HomeAnimalVisual animal = data.animals?.FirstOrDefault(value => value.itemId == itemId);
            if (animal == null || animal.models == null || animal.models.Length == 0) return false;
            HomeAnimalModel model = animal.models[Mathf.Abs(parent.name.GetHashCode()) % animal.models.Length];
            bool any = false;
            foreach (HomeAnimalLayer layer in model.layers ?? Array.Empty<HomeAnimalLayer>())
            {
                Texture2D texture = catalog.FindTexture(layer.page);
                if (texture == null || layer.vertices == null || layer.uv == null) continue;
                int points = layer.vertices.Length / 2;
                if (points < 3 || layer.uv.Length < points * 2) continue;
                var go = new GameObject("Layer_" + layer.order);
                go.transform.SetParent(parent, false);
                Mesh mesh = new Mesh { name = "animal_" + itemId + "_" + layer.order };
                var vertices = new Vector3[points];
                for (int index = 0; index < points; index++)
                    vertices[index] = new Vector3(layer.vertices[index * 2], layer.vertices[index * 2 + 1], 0)
                                      * Mathf.Max(0.0001f, model.scale);
                var uv = new Vector2[points];
                for (int index = 0; index < points; index++)
                    uv[index] = new Vector2(layer.uv[index * 2], layer.uv[index * 2 + 1]);
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = layer.triangles;
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().mesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Material(texture);
                renderer.sortingOrder = order + layer.order;
                any = true;
            }
            return any;
        }

        private void CreateQuad(Transform parent, Texture2D texture, float[] quad, int order, Vector3 scale)
        {
            var go = new GameObject("Sprite");
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            Mesh mesh = new Mesh { name = "reward_leaf" };
            var vertices = new Vector3[4];
            for (int index = 0; index < 4; index++)
                vertices[index] = new Vector3(quad[index * 2], quad[index * 2 + 1], 0);
            mesh.vertices = vertices;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(texture);
            renderer.sortingOrder = order;
        }

        private Material Material(Texture2D texture)
        {
            if (materials.TryGetValue(texture.name, out Material cached) && cached != null) return cached;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            var material = new Material(shader) { name = "Reward_" + texture.name, mainTexture = texture };
            materials[texture.name] = material;
            return material;
        }

        public int HitTest(Vector2 world)
        {
            int best = -1;
            float nearest = float.MaxValue;
            foreach (KeyValuePair<int, Bounds> pair in bounds)
            {
                Bounds box = pair.Value;
                if (world.x < box.min.x || world.x > box.max.x || world.y < box.min.y || world.y > box.max.y) continue;
                float distance = Mathf.Abs(world.x - box.center.x) + Mathf.Abs(world.y - box.center.y);
                if (distance >= nearest) continue;
                nearest = distance;
                best = pair.Key;
            }
            return best;
        }

        public void Dispose()
        {
            foreach (Material material in materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
