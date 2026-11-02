using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.World
{
    // The map supplies its original prefab, coordinates and local visibility.
    // All cloud geometry, fading, events and materials remain official code/data.
    public sealed class OfficialMainlineGridFog : IDisposable, IRootFogPlatform
    {
        private readonly GameObject root;
        private readonly LocalGridVisibilityAgent agent;
        private readonly HashSet<Material> materials = new HashSet<Material>();
        private bool disposed;
        public Camera MapCamera { get; }
        public bool IsMapScene => !disposed && root && root.activeInHierarchy;

        public static OfficialMainlineGridFog Create(Transform parent, Camera camera, GameObject originalPrefab,
            OfficialMapNavigation navigation, OfflineMapGridVisibility visibility, Func<Pos2d> characterPosition,
            Func<Pos2d?> transferPosition = null) =>
            new OfficialMainlineGridFog(parent, camera, originalPrefab, navigation, visibility, characterPosition, transferPosition);

        private OfficialMainlineGridFog(Transform parent, Camera camera, GameObject prefab,
            OfficialMapNavigation navigation, OfflineMapGridVisibility visibility, Func<Pos2d> characterPosition,
            Func<Pos2d?> transferPosition)
        {
            if (!parent || !camera || !prefab || navigation == null || visibility == null || characterPosition == null)
                throw new ArgumentNullException("Original map fog bindings are required.");
            if (navigation.Width != visibility.Component.Width || navigation.Height != visibility.Component.Height)
                throw new ArgumentException("Map ground and visibility dimensions differ.");
            MapCamera = camera;
            root = new GameObject("OfficialMainlineGridFog");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            try
            {
                var instance = Object.Instantiate(prefab, root.transform, false);
                var cloud = instance.GetComponent<CloudController2>();
                if (!cloud || !cloud.Cloud || !cloud.CloudTemplate || !cloud.CloudTemplate.sprite
                    || !cloud.CloudMat || !cloud.CloudMaskMat || !cloud.DisappearMat)
                    throw new ArgumentException("Original map fog prefab dependency closure is incomplete.");
                cloud.Platform = this;
                // Awake writes the renderer's mask texture. Give each live map
                // its own materials before Awake, including the template binding.
                var clones = new Dictionary<Material, Material>();
                Material Clone(Material source)
                {
                    if (!source) return null;
                    if (clones.TryGetValue(source, out var found)) return found;
                    var clone = Object.Instantiate(source);
                    clones.Add(source, clone); materials.Add(clone); return clone;
                }
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var bindings = renderer.sharedMaterials;
                    for (int i = 0; i < bindings.Length; i++) bindings[i] = Clone(bindings[i]);
                    renderer.sharedMaterials = bindings;
                }
                cloud.CloudMat = Clone(cloud.CloudMat);
                cloud.CloudMaskMat = Clone(cloud.CloudMaskMat);
                cloud.DisappearMat = Clone(cloud.DisappearMat);
                root.SetActive(true);
                agent = new LocalGridVisibilityAgent(visibility.Component, new VisibilityInput(visibility.Component),
                    new GroundInput(navigation), cloud, transferPosition, characterPosition);
                agent.Start();
                // SetupMat creates one additional original material instance.
                materials.Add(cloud.Cloud.sharedMaterial);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            agent?.Dispose();
            if (root) { root.SetActive(false); Object.Destroy(root); }
            foreach (var material in materials) if (material) Object.Destroy(material);
            materials.Clear();
        }

        private sealed class VisibilityInput : FogVisibilityData
        {
            private readonly GridVisibilityAgentComponent component;
            public VisibilityInput(GridVisibilityAgentComponent component) => this.component = component;
            public override object Original => component;
            public override bool GetPosVisibility(int x, int y) => component.GetPosVisibility(x, y);
        }

        private sealed class GroundInput : FogGroundData
        {
            private readonly OfficialMapNavigation navigation;
            private readonly GroundInfo info;
            public GroundInput(OfficialMapNavigation navigation) { this.navigation = navigation; info = new GroundInfo(navigation); }
            public override object Original => navigation;
            public override FogGroundInfo Info => info;
            public override Vector3 GridToPos(float x, float y) => info.OriginPos + new Vector3(x, y, 0);
            public override Vector3 GridToPos(Pos2d p) => GridToPos(p.X, p.Y);
            public override Pos2d PosToGrid(Vector3 p) => new Pos2d((int)(p.x - info.OriginPos.x + .5f), (int)(p.y - info.OriginPos.y + .5f));
        }

        private sealed class GroundInfo : FogGroundInfo
        {
            private readonly OfficialMapNavigation navigation;
            public GroundInfo(OfficialMapNavigation navigation) => this.navigation = navigation;
            public override Pos2d Size => new Pos2d(navigation.Width, navigation.Height);
            public override Vector3 OriginPos => new Vector3(navigation.Origin.X, navigation.Origin.Y, navigation.Origin.Z);
        }
    }
}
