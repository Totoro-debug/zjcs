using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // A source prefab can keep its particles, original application components
    // and follower references while sharing the battle pose/mesh implementation.
    [DefaultExecutionOrder(-1000)]
    public sealed partial class BattleNativeMonsterRenderer : IProfessionAppearanceRendererLite
    {
        [SerializeField] private BattleNativeMonsterAssets nativeAssets;
        [SerializeField] private int nativeEntityId;
        private Material[] prefabMaterials;
        public Component Component => this;
        public string SkeletonDataName { get; private set; }
        public IProfessionAppearanceSkeleton Skeleton => state;
        public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
        public int SeparatorSlotCount => 0;
        public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;

        public static BattleNativeMonsterRenderer CreatePrefab(GameObject prefab, Transform parent,
            IProfessionAppearanceUILiteBindings appearance)
        {
            if (!prefab || appearance == null) throw new ArgumentNullException();
            var source = prefab.GetComponent<BattleNativeMonsterRenderer>();
            if (!source || !source.nativeAssets || !prefab.GetComponent<AppearanceRendererLite>())
                throw new ArgumentException("Bind the complete original native battle prefab and appearance resources.");
            var staging = new GameObject("Inactive original battle prefab binding");
            staging.SetActive(false);
            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab, staging.transform, false);
                instance.SetActive(false);
                instance.transform.SetParent(parent, false);
                foreach (var renderer in instance.GetComponentsInChildren<AppearanceRendererLite>(true)) renderer.Configure(appearance);
                instance.SetActive(true);
                var result = instance.GetComponent<BattleNativeMonsterRenderer>();
                if (instance.activeInHierarchy && result.state == null)
                    throw new InvalidOperationException("The original native prefab did not initialize.");
                return result;
            }
            catch { if (instance) DestroyOwned(instance); throw; }
            finally { DestroyOwned(staging); }
        }

        private void Awake()
        {
            if (!nativeAssets) return; // Factory-created battle models bind explicitly.
            var row = nativeAssets.Find(nativeEntityId);
            if (row == null || !row.ContainsKey("rtPrefab"))
                throw new InvalidOperationException("Bind the verified original RT prefab variant.");
            if (Int(Obj(row["renderer"]), "updateTiming") != 1)
                throw new NotSupportedException("Bind the original prefab animation update timing.");
            var originals = GetComponent<MeshRenderer>().sharedMaterials.Distinct().ToArray();
            prefabMaterials = Arr(row["materials"]).Select(value => {
                string name = (string)Obj(Obj(value)["fields"])["m_Name"];
                return originals.Single(material => material && material.name == name);
            }).ToArray();
            EntityRoot = transform;
            Initialize(nativeAssets, nativeEntityId);
            SkeletonDataName = (string)row["skeletonName"];
            BindOriginalAnimator(nativeAssets, nativeEntityId);
        }
        private void Update()
        {
            if (nativeAssets && state != null) Advance(Time.deltaTime);
        }
        public void Initialize(bool overwrite, bool quiet)
        {
            if (state == null) throw new InvalidOperationException("Initialize the original native prefab before its appearance or followers.");
            if (overwrite) throw new NotSupportedException("Recreate the native prefab to replace its skeleton.");
        }
        public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
        {
            if (!ReferenceEquals(skin, state.Skin)) throw new ArgumentException("Bind this original native prefab skin.");
            return sourceMaterials;
        }
        public void UpdateAnimationAndMesh() => DrawCompletedPose();
    }
}
