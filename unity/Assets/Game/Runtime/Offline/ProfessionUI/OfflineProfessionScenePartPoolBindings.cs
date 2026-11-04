using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class OfflineProfessionScenePartPoolBindings : IProfessionCharacterPartPoolBindings
    {
        private sealed class Prefab : IProfessionCharacterPartPrefab
        {
            internal readonly ProfessionNativeSceneWeaponCatalog.Entry Entry;
            private TextAsset source;
            internal Prefab(ProfessionNativeSceneWeaponCatalog.Entry entry) { Entry = entry; source = entry.Hierarchy; }
            public TextAsset Result => source;
            public void Release(bool tryUnloadAllAssets) => source = null;
            public void Dispose() => Release(false);
        }
        private readonly ProfessionNativeSceneWeaponCatalog catalog;
        private readonly IProfessionAppearanceUILiteBindings materials;
        private readonly IProfessionAppearancePartBindings parts;
        private readonly IProfessionNativeSceneWeaponPoseProducer poses;
        private readonly Func<bool> usePool;
        private readonly Dictionary<GameObject, ProfessionNativeSceneWeapon> instances = new Dictionary<GameObject, ProfessionNativeSceneWeapon>();
        public bool UsePool => usePool();
        public int LiveInstanceCount => instances.Count;
        public OfflineProfessionScenePartPoolBindings(ProfessionNativeSceneWeaponCatalog catalog,
            IProfessionAppearanceUILiteBindings materials, IProfessionAppearancePartBindings parts, Func<bool> usePool)
            : this(catalog, materials, parts, null, usePool) { }
        public OfflineProfessionScenePartPoolBindings(ProfessionNativeSceneWeaponCatalog catalog,
            IProfessionAppearanceUILiteBindings materials, IProfessionAppearancePartBindings parts,
            IProfessionNativeSceneWeaponPoseProducer poses, Func<bool> usePool)
        {
            this.catalog = catalog ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
            this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
            this.poses = poses;
            this.usePool = usePool ?? throw new ArgumentNullException(nameof(usePool));
        }
        public IProfessionCharacterPartPrefab LoadPrefab(string path) => new Prefab(catalog.FindPath(path));
        public GameObject Instantiate(IProfessionCharacterPartPrefab source, Transform parent)
        {
            if (!(source is Prefab prefab) || !prefab.Result) throw new ArgumentException("Original native prefab definition is unavailable.");
            var target = parent ? parent.GetComponentInParent<ProfessionNativeSceneRenderer>(true) : null;
            if (!target) throw new InvalidOperationException("Bind the original PartSlot under its native scene renderer.");
            var instance = ProfessionNativeSceneWeapon.Create(catalog, prefab.Entry.InitialSkin, parent, target, materials, parts, poses);
            instances.Add(instance.GameObject, instance);
            return instance.GameObject;
        }
        public void PrepareRent(GameObject instance, Transform parent)
        {
            var target = parent ? parent.GetComponentInParent<ProfessionNativeSceneRenderer>(true) : null;
            if (!target) throw new InvalidOperationException("Bind the original PartSlot under its native scene renderer.");
            instances[instance].PrepareTarget(target);
        }
        public void PrepareReturn(GameObject instance) => instances[instance].Detach();
        public void Destroy(GameObject instance, bool tryUnloadAllAssets)
        {
            var value = instances[instance]; instances.Remove(instance); value.Release(tryUnloadAllAssets);
        }
    }
}
