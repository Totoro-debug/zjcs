using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Full source channels feed the common independent skeleton and mesh backend.
    // The owning battle supplies time; followers read the same completed pose.
    [RequireComponent(typeof(ProfessionNativeSceneMeshPart))]
    public sealed partial class BattleNativeMonsterRenderer : MonoBehaviour, IProfessionFollowerRenderer
    {
        private ProfessionNativeAppearanceSkeleton state;
        private ProfessionNativeMeshBatches batches;
        private ProfessionNativePlayerPhysicsPose physics;
        private ProfessionNativeAnimationChannels motion;
        private Dictionary<string, object> settings;
        private Material material;
        private float elapsed;
        private bool loop;
        public Transform EntityRoot { get; private set; }
        public ProfessionNativeAppearanceSkeleton State => state;
        public bool IsAnimationFinished => Animator
            ? !Animator.IsInTransition(0) && !Animator.GetCurrentAnimatorStateInfo(0).loop
                && Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1
            : motion != null && !loop && elapsed >= motion.Duration;
        public string Animation => Animator ? AnimatorFrame.Current.Animation?.Name : motion?.Name;
        public IEnumerable<string> AnimationNames => state.Source.AnimationNames;
        public bool UseCustomZSpacing => false;
        public int UpdateWhenInvisible => Int(settings, "updateWhenInvisible");
        public int UpdateMode => 3;
        public float ZSpacing => Number(settings, "zSpacing");
        IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => state;
        public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;

        public static BattleNativeMonsterRenderer Create(BattleNativeMonsterAssets assets, int entityId,
            Transform parent, string objectName)
        {
            if (!assets) throw new ArgumentNullException(nameof(assets));
            var row = assets.Find(entityId) ?? throw new InvalidDataException("Original monster is unbound: " + entityId);
            Transform root = new GameObject(objectName).transform; root.SetParent(parent, false);
            try
            {
                Transform node = root; bool first = true;
                foreach (var value in Arr(row["transforms"]))
                {
                    var source = Obj(value);
                    Transform child;
                    if (first) { child = root; first = false; }
                    else { child = new GameObject((string)source["name"]).transform; child.SetParent(node, false); }
                    child.localPosition = Vector(source["position"]); child.localScale = Vector(source["scale"]);
                    var q = Obj(source["rotation"]);
                    child.localRotation = new Quaternion(Number(q, "x"), Number(q, "y"), Number(q, "z"), Number(q, "w"));
                    child.gameObject.layer = Int(source, "layer"); child.gameObject.SetActive(Flag(source, "active")); node = child;
                }
                var result = node.gameObject.AddComponent<BattleNativeMonsterRenderer>();
                result.EntityRoot = root; result.Initialize(assets, entityId); return result;
            }
            catch { DestroyOwned(root.gameObject); throw; }
        }

        public void Release() { if (EntityRoot) DestroyOwned(EntityRoot.gameObject); else DestroyOwned(gameObject); }

        private void Initialize(BattleNativeMonsterAssets assets, int entityId)
        {
            if (state != null) throw new InvalidOperationException("Original monster is already initialized.");
            var row = assets.Find(entityId) ?? throw new InvalidDataException("Original monster is unbound: " + entityId);
            settings = Obj(row["renderer"]);
            if (Flag(settings, "initialFlipX") || Flag(settings, "initialFlipY") || Flag(settings, "tintBlack")
                || Flag(settings, "addNormals") || Flag(settings, "calculateTangents")
                || Int(settings, "maskInteraction") != 0 || Arr(settings["separatorSlotNames"]).Count != 0)
                throw new NotSupportedException("Bind additional original monster renderer branches.");
            var data = ProfessionNativeSkeletonData.LoadBattleMonster(assets.Resolve<TextAsset>((string)row["package"]).bytes, entityId);
            state = new ProfessionNativeAppearanceSkeleton(data);
            state.SetSkin(data.CreateCombinedNpcSkin(Arr(row["skins"]).Select(value => (string)value).ToArray()));
            batches = new ProfessionNativeMeshBatches(data.SlotCount);
            physics = data.Constraints.Any(value => value.Kind == "physics") ? new ProfessionNativePlayerPhysicsPose(data) : null;
            var sources = Arr(row["materials"]);
            if (sources.Count != 1) throw new NotSupportedException("Bind original monster multipage materials.");
            material = CreateMaterial(assets, Obj(sources[0]), Obj(row["textures"]));
            ApplyRenderer(GetComponent<MeshRenderer>(), Obj(row["meshRenderer"]));
            Play("idle_01", true, true);
            OnAnimationRebuild?.Invoke(this);
        }

        public bool Play(string name, bool repeat, bool restart = false)
        {
            if (state == null) throw new InvalidOperationException("Initialize the original monster first.");
            if (Animator) throw new InvalidOperationException("The bound original Animator owns monster motion selection.");
            if (!state.Source.AnimationNames.Contains(name)) return false;
            if (!restart && motion?.Name == name && loop == repeat && !IsAnimationFinished) return true;
            motion = new ProfessionNativeAnimationChannels(state.Source, name, originalBezierSegments: true);
            elapsed = 0; loop = repeat; Draw(0); return true;
        }
        public void Advance(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Animator) { AdvanceAnimator(deltaTime); return; }
            if (motion == null) return;
            elapsed += deltaTime;
            Draw(deltaTime);
        }
        private void Draw(float deltaTime)
        {
            float time = loop ? elapsed : Mathf.Min(elapsed, motion.Duration);
            if (physics != null) physics.Apply(state, motion, time, loop, deltaTime);
            else ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(state, motion, time, loop);
            DrawCompletedPose();
        }
        private void DrawCompletedPose()
        {
            batches.Build(state.Pose, state.CreateDrawSlots(), Flag(settings, "useClipping"),
                Flag(settings, "pmaVertexColors"), ZSpacing, allowConcaveClipping: true);
            GetComponent<ProfessionNativeSceneMeshPart>().Assign(batches.Batches, batch => batch.Blend == 0
                ? material : throw new NotSupportedException("Bind original monster non-normal material."));
            Attachments?.UpdateFollowers();
        }
        private static Material CreateMaterial(BattleNativeMonsterAssets assets, Dictionary<string, object> source,
            Dictionary<string, object> textures)
        {
            var fields = Obj(source["fields"]); var saved = Obj(fields["m_SavedProperties"]);
            if (Arr(fields["stringTagMap"]).Count != 0 || Arr(fields["disabledShaderPasses"]).Count != 0
                || Arr(fields["m_BuildTextureStacks"]).Count != 0)
                throw new NotSupportedException("Bind additional original monster material branches.");
            var result = new Material(assets.Shader) { name = (string)fields["m_Name"],
                renderQueue = Int(fields, "m_CustomRenderQueue"), enableInstancing = Flag(fields, "m_EnableInstancingVariants"),
                doubleSidedGI = Flag(fields, "m_DoubleSidedGI"), globalIlluminationFlags = (MaterialGlobalIlluminationFlags)Int(fields, "m_LightmapFlags") };
            try
            {
                result.shaderKeywords = Arr(fields["m_ValidKeywords"]).Select(value => (string)value).ToArray();
                foreach (var pair in Arr(saved["m_Floats"]).Select(Arr)) result.SetFloat((string)pair[0], Convert.ToSingle(pair[1]));
                foreach (var pair in Arr(saved["m_Ints"]).Select(Arr)) result.SetInteger((string)pair[0], Convert.ToInt32(pair[1]));
                foreach (var pair in Arr(saved["m_Colors"]).Select(Arr))
                { var c = Obj(pair[1]); result.SetColor((string)pair[0], new Color(Number(c, "r"), Number(c, "g"), Number(c, "b"), Number(c, "a"))); }
                foreach (var pair in Arr(saved["m_TexEnvs"]).Select(Arr))
                {
                    var env = Obj(pair[1]); var scale = Obj(env["m_Scale"]); var offset = Obj(env["m_Offset"]);
                    result.SetTexture((string)pair[0], null);
                    result.SetTextureScale((string)pair[0], new Vector2(Number(scale, "x"), Number(scale, "y")));
                    result.SetTextureOffset((string)pair[0], new Vector2(Number(offset, "x"), Number(offset, "y")));
                }
                foreach (var texture in Arr(source["textures"]).Select(Obj))
                    result.SetTexture((string)texture["property"], assets.Resolve<Texture2D>((string)Obj(textures[(string)texture["texture"]])["file"]));
                return result;
            }
            catch { DestroyOwned(result); throw; }
        }
        private static void ApplyRenderer(MeshRenderer renderer, Dictionary<string, object> source)
        {
            renderer.enabled = Flag(source, "m_Enabled"); renderer.shadowCastingMode = (ShadowCastingMode)Int(source, "m_CastShadows");
            renderer.receiveShadows = Flag(source, "m_ReceiveShadows"); renderer.allowOcclusionWhenDynamic = Flag(source, "m_DynamicOccludee");
            renderer.motionVectorGenerationMode = (MotionVectorGenerationMode)Int(source, "m_MotionVectors");
            renderer.lightProbeUsage = (LightProbeUsage)Int(source, "m_LightProbeUsage");
            renderer.reflectionProbeUsage = (ReflectionProbeUsage)Int(source, "m_ReflectionProbeUsage");
            renderer.renderingLayerMask = Convert.ToUInt32(source["m_RenderingLayerMask"]); renderer.rendererPriority = Int(source, "m_RendererPriority");
            renderer.lightmapIndex = unchecked((short)Int(source, "m_LightmapIndex"));
            renderer.realtimeLightmapIndex = unchecked((short)Int(source, "m_LightmapIndexDynamic"));
            renderer.lightmapScaleOffset = Tiling(source["m_LightmapTilingOffset"]);
            renderer.realtimeLightmapScaleOffset = Tiling(source["m_LightmapTilingOffsetDynamic"]);
            renderer.sortingLayerID = Int(source, "m_SortingLayerID"); renderer.sortingOrder = Int(source, "m_SortingOrder");
        }
        private void OnDestroy() { batches?.Dispose(); DestroyOwned(material); batches = null; material = null; }
        private static void DestroyOwned(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector3 Vector(object value) { var v = Obj(value); return new Vector3(Number(v, "x"), Number(v, "y"), Number(v, "z")); }
        private static Vector4 Tiling(object value) { var v = Obj(value); return new Vector4(Number(v, "x"), Number(v, "y"), Number(v, "z"), Number(v, "w")); }
    }
}
