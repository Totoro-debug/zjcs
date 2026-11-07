using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Replaces only the obsolete renderer endpoint on an inactive original graph.
    // The original playables still own its activation, transforms and lifetime.
    public sealed class EmbeddedMonsterNativeSkeleton : MonoBehaviour,
        IProfessionFollowerRenderer, IProfessionAppearanceRendererLite
    {
        private ProfessionNativeAppearanceSkeleton skeleton;
        private ProfessionNativeAnimationChannels animation;
        private ProfessionNativeOrderedPhysicsPose physics;
        private readonly ProfessionNativeScenePhysicsMovement movement = new ProfessionNativeScenePhysicsMovement();
        private ProfessionNativeMeshBatches batches;
        private ProfessionNativeSceneMeshPart mesh;
        private Dictionary<string, object> settings;
        private Material material;
        private float elapsed;
        private Vector2 positionInheritance;
        private float rotationInheritance;

        public Component Component => this;
        public ProfessionNativeAppearanceSkeleton State => skeleton;
        public float Elapsed => elapsed;
        public int SkillEntityId { get; private set; }
        public string AnimationName => animation?.Name;
        public MeshRenderer VisibleRenderer => mesh ? mesh.MeshRenderer : null;
        public IProfessionAppearanceSkeleton Skeleton => skeleton;
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => skeleton;
        public string SkeletonDataName { get; private set; }
        public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
        public int SeparatorSlotCount => 0;
        public bool UseCustomZSpacing => false;
        public float ZSpacing => Number(settings, "zSpacing");
        public int UpdateWhenInvisible => Int(settings, "updateWhenInvisible");
        public int UpdateMode => gameObject.activeInHierarchy && VisibleRenderer && VisibleRenderer.enabled
            && VisibleRenderer.isVisible ? 3 : UpdateWhenInvisible;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;
        public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;

        public static int BindOriginalPrefab(GameObject instance)
        {
            if (!instance) throw new ArgumentNullException(nameof(instance));
            var asset = Resources.Load<TextAsset>("Combat/EmbeddedMonsterSkeletons/bindings")
                ?? throw new FileNotFoundException("Original embedded monster skeleton bindings are not packaged.");
            var data = Obj(OfficialEquipmentJson.Parse(asset.text));
            if (!Equals(data["manifest"], "884_156692")) throw new InvalidDataException("Embedded monster source differs.");
            var bound = new Dictionary<Component, EmbeddedMonsterNativeSkeleton>();
            foreach (var row in Arr(data["graphs"]).Select(Obj))
            {
                string name = (string)row["prefab"];
                if (instance.name != name && instance.name != name + "(Clone)") continue;
                if (instance.activeInHierarchy) throw new InvalidOperationException("Bind embedded monsters before Awake.");
                Transform node = instance.transform.Find((string)row["path"])
                    ?? throw new InvalidDataException("Original embedded monster transform is unbound.");
                var original = RequireComponent(node, "Spine.Unity.SkeletonAnimation");
                if (node.GetComponent<EmbeddedMonsterNativeSkeleton>()) throw new InvalidOperationException("Embedded monster is already bound.");
                Transform visible = instance.transform.Find((string)row["visible_path"])
                    ?? throw new InvalidDataException("Original embedded monster renderer child is unbound.");
                var native = node.gameObject.AddComponent<EmbeddedMonsterNativeSkeleton>();
                native.Configure(row, visible);
                bound.Add(original, native);
                if (visible != node)
                {
                    Remove(node, "SkeletonRendererHighTexturePreview");
                    Remove(node, "AppearanceSystem.SpineExt.DynamicSkeletonRenderSeparator");
                    Remove(visible, "Spine.Unity.SkeletonPartsRenderer");
                    Remove(node, "AppearanceCombineSkin"); // Verified empty combined-skin list.
                    var sourceRenderer = node.GetComponent<MeshRenderer>();
                    var block = new MaterialPropertyBlock(); sourceRenderer.GetPropertyBlock(block);
                    native.VisibleRenderer.SetPropertyBlock(block);
                    sourceRenderer.enabled = false;
                }
            }
            foreach (var follower in instance.GetComponentsInChildren<AttachmentFollower>(true))
                if (follower.SkeletonRenderer && bound.TryGetValue(follower.SkeletonRenderer, out var native))
                    follower.SkeletonRenderer = native;
            foreach (var follower in instance.GetComponentsInChildren<SkeletonRendererFollower>(true))
                if (follower.SkeletonRenderer && bound.TryGetValue(follower.SkeletonRenderer, out var native))
                    follower.SkeletonRenderer = native;
            foreach (var original in bound.Keys) UnityEngine.Object.DestroyImmediate(original);
            return bound.Count;
        }

        private void Configure(Dictionary<string, object> row, Transform visible)
        {
            if (skeleton != null) throw new InvalidOperationException("Embedded skeleton is already initialized.");
            settings = Obj(row["renderer"]);
            SkillEntityId = Int(row, "skill_id");
            if (Arr(settings["separatorSlotNames"]).Count != 0 || Flag(settings, "initialFlipX")
                || Flag(settings, "initialFlipY") || Flag(settings, "tintBlack") || Flag(settings, "addNormals")
                || Flag(settings, "calculateTangents") || Int(settings, "maskInteraction") != 0
                || Int(settings, "updateTiming") != 1 || Flag(settings, "clearStateOnDisable")
                || Convert.ToInt64(Obj(settings["physicsMovementRelativeTo"])["m_PathID"]) != 0)
                throw new NotSupportedException("Bind additional original embedded renderer branches.");
            var package = Resources.Load<TextAsset>((string)row["package"])
                ?? throw new FileNotFoundException("Original embedded skeleton package is not bound.");
            var source = ProfessionNativeSkeletonData.LoadBattleMonster(package.bytes, Int(row, "skill_id"));
            skeleton = new ProfessionNativeAppearanceSkeleton(source);
            skeleton.SetSkin(source.CreateCombinedNpcSkin(Arr(row["skins"]).Select(value => (string)value).ToArray()));
            animation = new ProfessionNativeAnimationChannels(source, (string)settings["animationName"], originalBezierSegments: true);
            if (source.Constraints.Any(value => value.Kind == "physics")) physics = new ProfessionNativeOrderedPhysicsPose(source);
            var inheritance = Arr(settings["physicsPositionInheritanceFactor"]);
            positionInheritance = new Vector2(Convert.ToSingle(inheritance[0]), Convert.ToSingle(inheritance[1]));
            rotationInheritance = Number(settings, "physicsRotationInheritanceFactor");
            movement.Reset(transform);
            var originalMaterial = Resources.Load<Material>((string)row["material"])
                ?? throw new FileNotFoundException("Original embedded material is not bound.");
            material = new Material(originalMaterial);
            string high = (string)row["high_texture"];
            if (!string.IsNullOrEmpty(high)) material.mainTexture = Resources.Load<Texture2D>(high)
                ?? throw new FileNotFoundException("Original explicit embedded high atlas is not bound.");
            batches = new ProfessionNativeMeshBatches(source.SlotCount);
            mesh = visible.gameObject.AddComponent<ProfessionNativeSceneMeshPart>();
            PartnerNativeBattleMesh.ApplyRenderer(mesh.MeshRenderer, Obj(row["mesh_renderer"]));
            SkeletonDataName = (string)row["skeleton_data_name"];
            enabled = Flag(settings, "m_Enabled");
            Apply(0);
            OnAnimationRebuild?.Invoke(this);
        }

        private void Update() => Advance((Flag(settings, "unscaledTime") ? Time.unscaledDeltaTime : Time.deltaTime));

        public void Advance(float deltaTime)
        {
            if (skeleton == null || !float.IsFinite(deltaTime) || deltaTime < 0)
                throw new ArgumentException("Bind an original embedded skeleton and a finite forward time step.");
            float scaled = deltaTime * Number(settings, "timeScale");
            if (physics != null) movement.Apply(transform, skeleton.Source.DataScale, positionInheritance,
                rotationInheritance, physics.Translate, physics.Rotate);
            elapsed += scaled;
            Apply(scaled);
        }

        private void Apply(float deltaTime)
        {
            if (physics != null) physics.Apply(skeleton, animation, elapsed, Flag(settings, "loop"), deltaTime);
            else ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(skeleton, animation, elapsed, Flag(settings, "loop"));
            Draw();
        }

        private void Draw()
        {
            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), Flag(settings, "useClipping"),
                Flag(settings, "pmaVertexColors"), ZSpacing, allowConcaveClipping: true);
            var visibleMaterial = CustomMaterialOverride.TryGetValue(material, out var replacement) ? replacement : material;
            mesh.Assign(batches.Batches, batch => batch.Blend == 0 ? visibleMaterial
                : throw new NotSupportedException("Bind original embedded non-normal materials."));
        }

        public void Initialize(bool overwrite, bool quiet)
        {
            if (skeleton == null) throw new InvalidOperationException("Bind the original embedded pose before following it.");
            if (overwrite) throw new NotSupportedException("Recreate the original graph to replace its embedded skeleton.");
        }

        public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
        {
            if (!ReferenceEquals(skin, skeleton.Skin)) throw new ArgumentException("Use this original embedded skin.");
            yield return material;
        }

        // Material/follower demand only redraws the completed pose; it never advances physics.
        public void UpdateAnimationAndMesh() { Draw(); OnCustomDemandLoading?.Invoke(this); }
        private void OnDestroy()
        {
            batches?.Dispose(); batches = null;
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            material = null;
        }

        private static MonoBehaviour RequireComponent(Transform node, string name)
            => node.GetComponents<MonoBehaviour>().Single(value => value && value.GetType().FullName == name);
        private static void Remove(Transform node, string name) => UnityEngine.Object.DestroyImmediate(RequireComponent(node, name));
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
    }
}
