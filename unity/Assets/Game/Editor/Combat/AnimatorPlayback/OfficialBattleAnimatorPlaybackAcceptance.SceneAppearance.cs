using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using SceneMecanim = Zjcs.Official.CombatAppearance.AppearanceMecanim;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneAppearance()
        {
            Run();
            SessionState.SetBool(Key + ".sceneAppearance", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneAppearanceSteps()
        {
            // These endpoints inspect original application behavior. They do not
            // replace production rendering, Mecanim translation or bone caching.
            var root = new GameObject("Original current scene appearance lifecycle contracts");
            var events = new List<string>();
            var endpoint = root.AddComponent<SceneAppearanceEndpoint>();
            endpoint.Events = events;
            endpoint.Animator = root.AddComponent<Animator>();
            var meshRenderer = root.AddComponent<MeshRenderer>();
            var source = ProfessionNativeSceneCharacterAsset.LoadResource("male");
            var secondSource = Object.Instantiate(source);
            var original = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources")
                .Find("pWeapon_100_G_007").OriginalMaterial;
            var originals = new[] { new Material(original), new Material(original), new Material(original) };
            endpoint.Materials = new[] { originals[0], null, originals[0], originals[1] };
            string skinName = source.Data.SkinNames.First(name => name.StartsWith("body_", StringComparison.Ordinal));
            var loader = ScriptableObject.CreateInstance<YooAssetTextureLoader>();
            JsonUtility.FromJsonOverwrite("{\"placeholderMap\":[]}", loader);
            var skin = ScriptableObject.CreateInstance<SkinTextureAsset>();
            typeof(SkinTextureAsset).GetField("mSkinTextureConfigList", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(skin, new List<SkinTextureConfig> { new SkinTextureConfig { SkinName = skinName, PlaceHolderTexture = originals[0].mainTexture } });
            skin.OnAfterDeserialize();
            var bindings = new SceneAppearanceBindings(endpoint, loader, skin, events);
            var handler = root.AddComponent<AppearanceRendererPartHandler>();
            var appearance = root.AddComponent<SceneMecanim>();
            appearance.Configure(bindings, SceneAppearanceGuid<SkinTextureAsset>("original-skin-guid"),
                SceneAppearanceGuid<YooAssetTextureLoader>("original-loader-guid"), handler);
            try
            {
                appearance.Init(source, null);
                Require(appearance.Valid && appearance.SkeletonDataAsset == source && appearance.SkinTextureAsset == skin
                    && appearance.TextureLoader == loader && appearance.SkeletonMecanim == endpoint
                    && appearance.Animator == endpoint.Animator, "scene resources and component graph");
                Require(bindings.Loaded.SequenceEqual(new[] { "original-loader-guid", "original-skin-guid" })
                    && bindings.AddedRefs == 1 && bindings.CacheCreates == 1 && endpoint.Initializations == 1
                    && endpoint.MaterialSubscribers == 1 && endpoint.DemandSubscribers == 1,
                    "original loader/skin order and single subscriptions/cache creation");
                Require(events.IndexOf("initialize") < events.IndexOf("loader:add")
                    && events.IndexOf("loader:add") < events.IndexOf("cache:create"), "original initialization order");
                appearance.Init(source, null);
                Require(bindings.Loaded.Count == 2 && endpoint.Initializations == 1 && bindings.CacheCreates == 1,
                    "same source initialization is ignored");
                int materialEvents = 0, skinEvents = 0;
                appearance.OnGetUpdatedSharedMaterialsEvent += values =>
                { materialEvents++; Require(values.Count() == appearance.IndependentMaterialDict.Count, "current independent material collection"); };
                appearance.OnSkinUpdate += () => { skinEvents++; events.Add("skin:event"); };
                events.Clear();
                appearance.SetSkins(new List<string> { skinName });
                Require(bindings.Created == 2 && appearance.IndependentMaterialDict.Count == 2 && materialEvents == 1
                    && skinEvents == 1 && endpoint.LastMaterials[1] == null
                    && endpoint.LastMaterials[0] == endpoint.LastMaterials[2]
                    && endpoint.LastMaterials[0] != originals[0], "array callback deduplication, null preservation and original isolation");
                Require(events.IndexOf("mecanim:0") < events.IndexOf("cache:skin")
                    && events.IndexOf("cache:skin") < events.IndexOf("mesh")
                    && events.IndexOf("mesh") < events.IndexOf("skin:event"), "Mecanim then cache then mesh then skin notification");
                Require(appearance.TryGetMaterial(skinName, out var dyeMaterial) && dyeMaterial == endpoint.LastMaterials[0],
                    "skin placeholder resolves its independent material");
                var segment = new[] { originals[1], originals[0], null };
                handler.OnGetSharedMaterialDelegate(segment);
                Require(segment[0] == endpoint.LastMaterials[3] && segment[1] == endpoint.LastMaterials[0]
                    && segment[2] == null && bindings.Created == 2 && materialEvents == 1,
                    "original segment array shares main renderer material instances");
                var extra = new[] { originals[2], originals[0] };
                handler.OnGetSharedMaterialDelegate(extra);
                Require(bindings.Created == 3 && appearance.IndependentMaterialDict.Count == 3 && materialEvents == 2,
                    "only new original materials allocate and notify");
                endpoint.Demand(); endpoint.Demand();
                Require(bindings.AddedRefs == 1, "texture demand retains one loader reference");
                events.Clear();
                appearance.SetSkins(new List<string> { skinName });
                Require(bindings.Returned == 3 && bindings.Created == 5 && materialEvents == 3
                    && appearance.IndependentMaterialDict.Count == 2 && skinEvents == 2,
                    "skin change returns all prior materials before rebuilding");
                Require(events.IndexOf("material:return") < events.IndexOf("mesh"), "material return precedes source mesh rebuild");
                meshRenderer.sharedMaterials = endpoint.LastMaterials;
                events.Clear();
                appearance.Release(true);
                Require(appearance.IndependentMaterialDict.Count == 0 && !appearance.Skins.Any()
                    && meshRenderer.sharedMaterials.Length == 0 && bindings.Returned == 5
                    && bindings.Cache.Clears == 1 && bindings.RemovedRefs == 0,
                    "full release clears skin, materials and cache while retaining asset lifetime");
                Require(events.IndexOf("material:return") < events.IndexOf("cache:clear"), "release material return precedes cache clear");
                appearance.Init(source, null);
                Require(endpoint.Initializations == 1 && bindings.Loaded.Count == 2, "release preserves original source identity");
                appearance.SetSkins(new List<string> { skinName });
                appearance.Init(secondSource, null);
                Require(endpoint.Initializations == 2 && bindings.Loaded.Count == 2 && bindings.CacheCreates == 1
                    && endpoint.MaterialSubscribers == 1 && endpoint.DemandSubscribers == 1 && bindings.Returned == 7,
                    "changed source reinitializes skeleton while retaining loaded common assets and original subscriptions");
                Debug.Log("[Original scene appearance] Changed source reinitialization and retained common handles/subscriptions passed.");
                appearance.SetSkins(new List<string> { skinName });
                events.Clear();
                Object.Destroy(appearance);
                yield return null;
                Require(bindings.Returned == 9 && bindings.RemovedRefs == 1 && bindings.Disposed == 2
                    && bindings.Cache.Destroyed == 1 && bindings.Cache.Clears == 3, "destruction releases renderer, loader, resource handles and cache");
                Require(events.IndexOf("loader:remove") < events.IndexOf("handle:dispose")
                    && events.LastIndexOf("handle:dispose") < events.IndexOf("cache:destroy"), "original renderer-before-assets-before-cache destruction order");
                Debug.Log("[Original scene appearance] current source graph, GUID handle order, cache lifecycle, array material callbacks, deduplication, segment sharing, rebuild, release and destruction passed. Renderer/Mecanim/cache endpoints are contract probes.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var material in bindings.AllMaterials) Object.DestroyImmediate(material);
                foreach (var material in originals) Object.DestroyImmediate(material);
                Object.DestroyImmediate(loader); Object.DestroyImmediate(skin);
                Object.DestroyImmediate(secondSource);
            }
            // Repeat the original part lifecycle checks with the corrected array callback port.
            var parts = RendererPartSteps();
            while (parts.MoveNext()) yield return parts.Current;
        }

        private static AssetGuidProxy<T> SceneAppearanceGuid<T>(string guid) where T : Object
        { return JsonUtility.FromJson<AssetGuidProxy<T>>("{\"mAssetGuid\":\"" + guid + "\"}"); }

        private sealed class SceneAppearanceEndpoint : MonoBehaviour, IProfessionAppearanceSceneMecanim
        {
            internal List<string> Events;
            internal Material[] Materials, LastMaterials;
            internal int Initializations, MaterialSubscribers, DemandSubscribers;
            private Action<Material[]> materialUpdate;
            private Action<IProfessionAppearanceRendererLite> demand;
            internal ProfessionNativeAppearanceSkeleton State;
            public Animator Animator { get; set; }
            public Component Component => this;
            public string SkeletonDataName => "character_Male_1";
            public ScriptableObject skeletonDataAsset { get; set; }
            public IProfessionAppearanceSkeleton Skeleton => State;
            public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
            public int SeparatorSlotCount => 0;
            public event Action<Material[]> OnGetUpdatedSharedMaterials
            { add { materialUpdate += value; MaterialSubscribers++; } remove { materialUpdate -= value; MaterialSubscribers--; } }
            public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading
            { add { demand += value; DemandSubscribers++; } remove { demand -= value; DemandSubscribers--; } }
            public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin value) => Materials;
            public void Initialize(bool overwrite, bool quiet)
            {
                Require(overwrite && !quiet, "original scene Initialize arguments");
                Initializations++; Events.Add("initialize");
                State = new ProfessionNativeAppearanceSkeleton(((ProfessionNativeSceneCharacterAsset)skeletonDataAsset).Data);
            }
            public void LateUpdateMesh()
            {
                Events.Add("mesh"); LastMaterials = (Material[])Materials.Clone();
                materialUpdate?.Invoke(LastMaterials);
            }
            void IProfessionAppearanceSceneMecanim.Update(float deltaTime)
            { Require(deltaTime == 0, "original skin update delta"); Events.Add("mecanim:0"); }
            public void UpdateAnimationAndMesh() { ((IProfessionAppearanceSceneMecanim)this).Update(0); LateUpdateMesh(); }
            internal void Demand() => demand?.Invoke(this);
        }

        private sealed class SceneAppearanceCache : IProfessionAppearanceSceneCache
        {
            private readonly List<string> events;
            internal int Clears, Destroyed;
            internal SceneAppearanceCache(List<string> events) { this.events = events; }
            public void OnSkinUpdate() { events.Add("cache:skin"); }
            public void Clear() { Clears++; events.Add("cache:clear"); }
            public void OnDestroy() { Destroyed++; events.Add("cache:destroy"); }
        }

        private sealed class SceneAppearanceHandle : IProfessionAppearanceCharacterAsset
        {
            private readonly SceneAppearanceBindings owner;
            public ScriptableObject Result { get; }
            internal SceneAppearanceHandle(SceneAppearanceBindings owner, ScriptableObject result) { this.owner = owner; Result = result; }
            public void Dispose() { owner.Disposed++; owner.Events.Add("handle:dispose"); }
        }

        private sealed class SceneAppearanceBindings : IProfessionAppearanceSceneBindings, IAppearanceAssetManager
        {
            private readonly SceneAppearanceEndpoint endpoint;
            private readonly YooAssetTextureLoader loader;
            private readonly SkinTextureAsset skin;
            internal readonly List<string> Events, Loaded = new List<string>();
            internal readonly List<Material> AllMaterials = new List<Material>();
            internal readonly SceneAppearanceCache Cache;
            internal int AddedRefs, RemovedRefs, CacheCreates, Created, Returned, Disposed;
            internal SceneAppearanceBindings(SceneAppearanceEndpoint endpoint, YooAssetTextureLoader loader, SkinTextureAsset skin, List<string> events)
            { this.endpoint = endpoint; this.loader = loader; this.skin = skin; Events = events; Cache = new SceneAppearanceCache(events); }
            public IAppearanceAssetManager AssetManager => this;
            public IProfessionAppearanceCharacterAsset LoadAssetByGuid(string guid)
            {
                Loaded.Add(guid);
                return new SceneAppearanceHandle(this, guid == "original-loader-guid" ? (ScriptableObject)loader
                    : guid == "original-skin-guid" ? skin : throw new InvalidOperationException("Unexpected source GUID."));
            }
            public IProfessionAppearanceSceneCache CreateMecanimCache(IProfessionAppearanceSceneMecanim value)
            { Require(value == endpoint, "original cache endpoint"); CacheCreates++; Events.Add("cache:create"); return Cache; }
            public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner) => endpoint;
            public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet) => ((ProfessionNativeSceneCharacterAsset)asset).Data.SkinCatalog;
            public IProfessionAppearanceSkin CreateSkin(string name) => endpoint.State.CreateSkin(name);
            public int MaxRendererMaterialCount => 8;
            public IReadOnlyDictionary<Material, Stack<Material>> RendererMaterials { get; } = new Dictionary<Material, Stack<Material>>();
            public Material GetRendererMaterial(Material original)
            { Created++; var result = new Material(original); AllMaterials.Add(result); Events.Add("material:create"); return result; }
            public void ReturnRendererMaterial(Material material, Material original)
            { Require(AllMaterials.Contains(material) && material != original, "return original renderer material pair"); Returned++; Events.Add("material:return"); }
            public void AddTextureLoaderRef(YooAssetTextureLoader value, Object owner)
            { Require(value == loader && owner == endpoint, "original loader owner"); AddedRefs++; Events.Add("loader:add"); }
            public void RemoveTextureLoaderRef(YooAssetTextureLoader value, Object owner)
            { Require(value == loader && owner == endpoint, "original loader removal owner"); RemovedRefs++; Events.Add("loader:remove"); }
            public Material GetUIMaterial() => throw new InvalidOperationException("Scene renderer must not request UI material.");
            public void GetLoaderSharedMaterials(YooAssetTextureLoader value, Material[] source, ref Material[] shared)
                => throw new InvalidOperationException("Full scene renderer uses independent materials.");
            public void ReturnLoaderSharedMaterials(YooAssetTextureLoader value, Material[] source, bool all)
                => throw new InvalidOperationException("Full scene renderer uses independent materials.");
        }
    }
}
