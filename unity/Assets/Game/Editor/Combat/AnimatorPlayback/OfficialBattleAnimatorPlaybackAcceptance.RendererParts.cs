using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunRendererParts()
        {
            Run();
            SessionState.SetBool(Key + ".rendererParts", true);
        }

        private static IEnumerator RendererPartSteps()
        {
            var root = new GameObject("Isolated original scene part application contracts");
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var events = new List<string>();
            var endpoint = root.AddComponent<PartRendererProbe>(); endpoint.Events = events;
            var slots = new GameObject("Original part slot contract").transform; slots.SetParent(root.transform, false);
            var after = new GameObject("Existing following sibling"); after.transform.SetParent(root.transform, false);
            var bindings = new PartResourceProbe(config, events, root.transform);
            var handler = root.AddComponent<AppearanceMecanimPartHandler>();
            handler.ConfigureParts(bindings);
            handler.ConfigureRenderer(endpoint, slots, bindings, "Default", 27);
            handler.OnGetSharedMaterialDelegate = (renderer, material) => material;
            int changed = 0;
            handler.OnSkinChanged.AddListener(() => changed++);
            try
            {
                var skins = new Dictionary<SkinType, string> { { SkinType.pWeapon, "weapon-a" },
                    { SkinType.sWeapon, "secondary" }, { SkinType.backDecoration, "cape" }, { SkinType.eye, "effect-a" },
                    { SkinType.body, "emptySkin" }, { SkinType.hair, "ordinary-core-skin" } };
                handler.SetSkins(skins);
                Require(changed == 1 && bindings.Rents == 3 && bindings.Loads == 1 && bindings.Instances == 1, "part/effect classification and one skin callback");
                Require(bindings.Pools[SkinType.pWeapon] && bindings.Pools[SkinType.backDecoration] && !bindings.Pools[SkinType.sWeapon], "only original primary weapon/back decoration use pool");
                Require(endpoint.Subscribers == 1 && endpoint.Adds == 1 && endpoint.Reinits == 0 && endpoint.MaterialSubscribers == 1, "create separator/material callback and one action subscription");
                Require(slots.GetSiblingIndex() == root.transform.childCount - 1, "part slot moved after existing siblings");
                Require(endpoint.SeparatorSlots.Count == 2 && ReferenceEquals(endpoint.SeparatorSlots[0], endpoint.Slots["weapon"]) && ReferenceEquals(endpoint.SeparatorSlots[1], endpoint.Slots["effect"]), "part/effect slots deduplicate while preserving insertion order");
                Require(bindings.EffectFollower.Target == root && bindings.EffectFollower.Targets == 1 && bindings.NestedFollower.Targets == 0, "only direct effect children receive original follow initialization");
                var weapon = bindings.Parts[SkinType.pWeapon];
                Require(weapon.Target == root && weapon.Initializations == 1 && weapon.transform.localScale == new Vector3(2, 3, 1), "rent scale restored before original part initialization");
                Require(events.IndexOf("mesh") < events.IndexOf("follow:weapon-a") && events.IndexOf("follow:weapon-a") < events.IndexOf("hasAnim:weapon-a"), "mesh then followers then animation subscription scan");
                var clipA = new object();
                endpoint.Apply(clipA, "100/skill_06", 1, 0, .7f, .8f, true);
                endpoint.Apply(clipA, "100/skill_06", 0, 1, .9f, .7f, false);
                Require(weapon.Played.Count == 1 && weapon.Played[0] == "100/skill_06", "zero weight/backward notification forwards once by animation identity");
                endpoint.Apply(new object(), "100/skill_06");
                Require(weapon.Played.Count == 2 && bindings.Parts[SkinType.sWeapon].Played.Count == 0, "same name distinct identity forwards; nonanimated parts excluded");

                int targetCalls = bindings.EffectFollower.Targets;
                handler.SetSkins(new Dictionary<SkinType, string>(skins));
                Require(changed == 2 && bindings.Rents == 3 && bindings.Loads == 1 && bindings.Instances == 1 && bindings.Returns == 0, "same skins retain exact parts and effect asset/instance");
                Require(endpoint.Subscribers == 1 && endpoint.Adds == 1 && endpoint.Reinits == 1 && endpoint.MaterialSubscribers == 1 && bindings.EffectFollower.Targets == targetCalls,
                    "retained effect reuses follower without target reinit or duplicate subscriptions");
                Require(handler.AddSeparatorSlot("weapon") && !handler.AddSeparatorSlot("weapon") && handler.AddSeparatorSlot("dynamic") && !handler.AddSeparatorSlot(""), "dynamic separator membership semantics");
                Require(endpoint.SeparatorSlots.Count == 3 && ReferenceEquals(endpoint.SeparatorSlots[2], endpoint.Slots["dynamic"]), "dynamic slot merged after part slots");
                Require(handler.RemoveSeparatorSlot("weapon") && !handler.RemoveSeparatorSlot("weapon") && endpoint.SeparatorSlots.Count == 3, "removing dynamic membership preserves required weapon separator");
                handler.ClearDynamicSeparatorSlots();
                Require(endpoint.SeparatorSlots.Count == 2, "clearing dynamic separators retains part/effect separators");

                handler.enabled = false;
                Require(endpoint.Subscribers == 0, "disable unsubscribes");
                var clipB = new object(); endpoint.Apply(clipB, "200/skill_13");
                Require(weapon.Played.Count == 2, "disabled handler receives no actions");
                handler.enabled = true;
                Require(endpoint.Subscribers == 1, "enable restores exactly one subscription");
                endpoint.Apply(clipB, "200/skill_13");
                Require(weapon.Played.Count == 3, "re-enabled handler forwards next animation");
                skins[SkinType.pWeapon] = "weapon-b";
                handler.SetSkins(skins);
                var replacement = bindings.Parts[SkinType.pWeapon];
                Require(!ReferenceEquals(weapon, replacement) && bindings.Rents == 4 && bindings.Returns == 1 && !bindings.LastReturnAll, "changed part returned before replacement rent");
                endpoint.Apply(clipB, "200/skill_13");
                Require(replacement.Played.Count == 0, "last animation identity persists through skin replacement as in original");
                endpoint.Apply(new object(), "100/skill_50");
                Require(replacement.Played.Count == 1 && weapon.Played.Count == 3, "only current animated part receives new action");
                skins.Remove(SkinType.eye); handler.SetSkins(skins);
                Require(bindings.Releases == 1 && !bindings.LastReleaseAll && endpoint.SeparatorSlots.Count == 1, "removed effect releases once and removes its separator");
                yield return null;
                Require(bindings.EffectInstance == null, "removed effect GameObject destroyed in Play Mode");

                handler.SetSkins(new Dictionary<SkinType, string>());
                Require(endpoint.Subscribers == 0 && handler.LoadedSkeletonDict.Count == 0 && bindings.Returns == 4, "empty skins return all parts and unsubscribe animated list");
                handler.SetSkins(new Dictionary<SkinType, string> { { SkinType.pWeapon, "weapon-final" }, { SkinType.eye, "effect-b" } });
                var final = bindings.Parts[SkinType.pWeapon];
                handler.Release(true);
                Require(final.Unloads == 1 && bindings.LastReturnAll && bindings.LastReleaseAll && endpoint.Clears == 1, "full release marks native texture unload before return, releases effects and clears separator materials");
                handler.enabled = false;
                Require(endpoint.Subscribers == 0 && handler.LoadedSkeletonDict.Count == 0, "final disable detaches original subscription");
                handler.Release();
                Require(endpoint.Clears == 1, "ordinary repeat release does not clear materials again");

                // Check the already migrated real application part's source-name behavior.
                var partGo = new GameObject("Original part animation mapping contract"); partGo.transform.SetParent(root.transform, false);
                var animated = partGo.AddComponent<PartAnimatedProbe>();
                var part = partGo.AddComponent<AppearanceSkeletonPart>();
                part.Skeleton = partGo; part.SkinType = SkinType.pWeapon; part.ConfigurePart(bindings);
                Require(part.HasAnim && part.AnimDict.ContainsKey("map/idle_M_01"), "actual original config builds source-name whitelist from target animation names");
                part.TryPlayAnim("map/idle_M_01"); part.TryPlayAnim("100/skill_06");
                Require(animated.Played == "map/idle_M_01" && animated.Looped && animated.Count == 1, "original part forwards source name with looping, without inventing target-name rewrite");
                Debug.Log("Original scene part application contracts passed: resource classification, pooling/reuse/replace/release, direct effect followers, separators, ordered mesh/follower/animation callbacks, identity deduplication and lifecycle. No scene weapon prefab or renderer backend is substituted by this probe.");
            }
            finally
            {
                handler.enabled = false;
                handler.Release();
                Object.DestroyImmediate(root);
            }
            yield return null;
        }

        private sealed class PartSlotProbe : IProfessionSkinClipSlot { public object Attachment => null; }

        private sealed class PartRendererProbe : MonoBehaviour, IProfessionAppearancePartRenderer, IProfessionAppearancePartMecanim,
            IProfessionAppearancePartSeparator, IProfessionSkinClipSkeleton
        {
            internal readonly Dictionary<string, IProfessionSkinClipSlot> Slots = new Dictionary<string, IProfessionSkinClipSlot>
                { { "weapon", new PartSlotProbe() }, { "effect", new PartSlotProbe() }, { "dynamic", new PartSlotProbe() } };
            internal List<string> Events;
            internal int Adds, Reinits, Clears, Subscribers, MaterialSubscribers;
            private ProfessionPartClipApplied applied;
            public GameObject GameObject => gameObject;
            public IProfessionSkinClipSkeleton Skeleton => this;
            public List<IProfessionSkinClipSlot> SeparatorSlots { get; } = new List<IProfessionSkinClipSlot>();
            public IProfessionAppearancePartSeparator Separator => Adds == 0 ? null : this;
            public event ProfessionPartClipApplied OnClipApplied { add { applied += value; Subscribers++; } remove { applied -= value; Subscribers--; } }
            public event ProfessionPartSharedMaterial OnGetSharedMaterial { add { MaterialSubscribers++; } remove { MaterialSubscribers--; } }
            public IProfessionSkinClipSlot FindSlot(string name) => Slots.TryGetValue(name, out var slot) ? slot : null;
            public IProfessionAppearancePartSeparator AddSeparator(int layer, int extra, int increment, int order)
            { CheckSort(layer, extra, increment, order); Adds++; Events.Add("separator:add"); return this; }
            public void ReInitPartRenderers(int layer, int extra, int increment, int order)
            { CheckSort(layer, extra, increment, order); Reinits++; Events.Add("separator:reinit"); }
            private static void CheckSort(int layer, int extra, int increment, int order)
            { Require(layer == SortingLayer.NameToID("Default") && extra == 0 && increment == 0 && order == 27, "exact original separator arguments"); }
            public void ClearSharedMaterials() { Clears++; }
            public void LateUpdateMesh() { Events.Add("mesh"); }
            internal void Apply(object identity, string name, int layer = 0, float weight = 1, float time = 0, float last = 0, bool backward = false)
            { applied?.Invoke(identity, name, layer, weight, time, last, backward); }
        }

        private sealed class SkeletonPartProbe : MonoBehaviour, IAppearanceSkeletonPart, IProfessionAppearancePartFollower, IProfessionAppearanceUnloadable
        {
            internal List<string> Events;
            internal string SkinName;
            internal bool Animated;
            internal GameObject Target;
            internal int Initializations, Unloads;
            internal readonly List<string> Played = new List<string>();
            public GameObject GameObject => gameObject;
            public GameObject SkeletonTarget => gameObject;
            public IProfessionAppearancePartFollower SkeletonFollower => this;
            public bool HasAnim { get { Events.Add("hasAnim:" + SkinName); return Animated; } }
            public string SlotName => "weapon";
            public bool ZFront => true;
            public void Init(GameObject target) { Target = target; }
            public void Initialize() { Initializations++; Events.Add("follow:" + SkinName); }
            public void TryPlayAnim(string name) { Played.Add(name); }
            public void MarkUnloadTextureImmediately() { Unloads++; Events.Add("unload:" + SkinName); }
        }

        private sealed class EffectFollowerProbe : BaseSkeletonFollower
        {
            internal GameObject Target;
            internal int Targets;
            public override void InitFollowTarget(GameObject target) { Target = target; Targets++; }
            protected override bool UpdateVisible() => false;
            protected override bool InitComponent(ref IProfessionFollowerAnimation animation, ref Transform skeletonTransform, ref bool customZ) => false;
        }

        private sealed class PartAnimatedProbe : MonoBehaviour, IProfessionPartAnimatedSkeleton
        {
            internal string Played; internal bool Looped; internal int Count;
            public string[] AnimationNames => new[] { "idle_01", "loop_L" };
            public void PlayAnimation(string name, bool loop) { Played = name; Looped = loop; Count++; }
        }

        private sealed class PartAssetProbe : IProfessionAppearancePartAsset
        {
            private readonly PartResourceProbe owner;
            public bool IsValid => Result != null;
            public int Status => 1;
            public event Action<IProfessionAssetOperation<GameObject>> Completed { add { } remove { } }
            public GameObject Result { get; }
            internal PartAssetProbe(PartResourceProbe owner, GameObject result) { this.owner = owner; Result = result; }
            public void Release(bool all) { owner.Releases++; owner.LastReleaseAll = all; }
            public void Dispose() { Release(false); }
        }

        private sealed class PartResourceProbe : IProfessionAppearancePartBindings, ICharacterPartEntryProvider
        {
            public AppearanceConfig Config { get; }
            private readonly List<string> events;
            private readonly Transform root;
            internal int Rents, Returns, Loads, Instances, Releases;
            internal bool LastReturnAll, LastReleaseAll;
            internal GameObject EffectInstance;
            internal EffectFollowerProbe EffectFollower, NestedFollower;
            internal readonly Dictionary<SkinType, bool> Pools = new Dictionary<SkinType, bool>();
            internal readonly Dictionary<SkinType, SkeletonPartProbe> Parts = new Dictionary<SkinType, SkeletonPartProbe>();
            internal PartResourceProbe(AppearanceConfig config, List<string> events, Transform root)
            { Config = config; this.events = events; this.root = root; }
            public bool HasEffect(string name) => name == "effect-a" || name == "effect-b";
            public IProfessionAppearancePartAsset LoadPrefab(string path)
            {
                Require(path == "Appearance/PlayerEffect/Renderer/effect-a" || path == "Appearance/PlayerEffect/Renderer/effect-b", "original renderer effect address");
                Loads++; return new PartAssetProbe(this, root.gameObject);
            }
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            {
                Require(prefab == root.gameObject && !worldPositionStays, "original effect instantiate contract");
                Instances++; EffectInstance = new GameObject("Isolated effect lifecycle probe"); EffectInstance.transform.SetParent(parent, false);
                var direct = new GameObject("Direct follower"); direct.transform.SetParent(EffectInstance.transform, false);
                EffectFollower = direct.AddComponent<EffectFollowerProbe>(); EffectFollower.SlotName = "effect";
                var nested = new GameObject("Nested follower"); nested.transform.SetParent(direct.transform, false);
                NestedFollower = nested.AddComponent<EffectFollowerProbe>(); NestedFollower.SlotName = "dynamic";
                return EffectInstance;
            }
            public CharacterPartPoolEntry RentIntegratedPart(SkinType type, string name, Transform parent, bool usePool, Action<GameObject> before)
            {
                Require(before == null, "original scene integrated rent has no before-parent callback");
                Rents++; Pools[type] = usePool;
                var go = new GameObject("Isolated part lifecycle probe " + name); go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(2, 3, 1);
                var handle = new PartAssetProbe(this, go);
                var entry = new CharacterPartPoolEntry(go, name, handle, false);
                go.transform.localScale = new Vector3(8, 9, 1);
                var part = go.AddComponent<SkeletonPartProbe>(); part.Events = events; part.SkinName = name; part.Animated = type == SkinType.pWeapon;
                Parts[type] = part;
                return entry;
            }
            public CharacterPartPoolEntry RentStandalonePart(SkinType type, string name, Transform parent, bool ui, bool pool)
            { throw new InvalidOperationException("Original scene handler must rent integrated parts."); }
            public void ReturnPart(CharacterPartPoolEntry entry, bool all)
            { Returns++; LastReturnAll = all; events.Add("return:" + entry.AssetPath); }
        }
    }
}
