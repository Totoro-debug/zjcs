using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.Equipment;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Original prefab transforms, original Mecanim graph and original material
    // fields feed the existing independent geometry endpoint.
    public sealed class PartnerNativeBattleActor : IDisposable
    {
        private sealed class Part : IDisposable
        {
            public Dictionary<string, object> Settings;
            public ProfessionNativeAppearanceSkeleton Skeleton;
            public ProfessionNativeMeshBatches Batches;
            public ProfessionNativeSceneMeshPart Mesh;
            public Material Material;
            public ProfessionNativeAnimationChannels IndependentAnimation;
            public float Elapsed;
            public void Dispose() { Batches?.Dispose(); Destroy(Material); }
        }
        private readonly List<Part> parts = new List<Part>();
        private readonly Dictionary<string, Transform> transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly ProfessionNativeAnimatorPose pose;
        private bool disposed;
        public Transform Root { get; }
        public Animator Animator { get; }
        public ProfessionNativeAppearanceSkeleton Skeleton => parts[0].Skeleton;
        public IReadOnlyList<MeshRenderer> Renderers => parts.Select(part => part.Mesh.MeshRenderer).ToArray();
        public Dictionary<string, object> OriginalRow { get; }
        public ProfessionNativeAnimatorPose.AppliedFrame LastFrame { get; private set; }
        public IReadOnlyDictionary<string, Transform> OriginalTransforms => transforms;
        public PartnerNativeBattleAttachments Attachments { get; private set; }

        public PartnerNativeBattleActor(PartnerNativeBattleAssetCatalog catalog, int npcId, Transform parent, string name)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            OriginalRow = catalog.Find(npcId) ?? throw new InvalidDataException("Original partner is unbound: " + npcId);
            Root = new GameObject(name).transform; Root.SetParent(parent, false);
            try
            {
                foreach (var row in new[] { OriginalRow }.Concat(Arr(OriginalRow["parts"]).Select(Obj)))
                    parts.Add(CreatePart(catalog, row, Root, transforms));
                var translator = Obj(parts[0].Settings["translator"]);
                if (!Flag(translator, "autoReset") || !Flag(translator, "useCustomMixMode")
                    || Arr(translator["layerMixModes"]).Count != 1 || Convert.ToInt32(Arr(translator["layerMixModes"])[0]) != 1
                    || Arr(translator["layerBlendModes"]).Count != 1 || Convert.ToInt32(Arr(translator["layerBlendModes"])[0]) != 1)
                    throw new NotSupportedException("Bind the original partner translator mode before drawing.");
                Animator = parts[0].Mesh.gameObject.AddComponent<Animator>();
                Animator.runtimeAnimatorController = catalog.Controllers.Find(npcId);
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; Animator.applyRootMotion = false;
                Animator.enabled = false;
                pose = new ProfessionNativeAnimatorPose(Animator, ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics,
                    (skeleton, mix) => ProfessionNativeOrderedConstraintPose.EvaluateMixNextBeforePhysics(skeleton,
                        mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                        mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight), allowPartnerBlendTrees: true);
                Animator.Rebind(); Animator.Update(0); ApplyPose();
                Attachments = new PartnerNativeBattleAttachments(this, transforms, parts.Select(part =>
                    new PartnerNativeBattleAttachments.RendererBinding(part.Mesh.transform, part.Skeleton,
                        part.Settings, part.Material)).ToArray());
            }
            catch { Dispose(); throw; }
        }

        public void Advance(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PartnerNativeBattleActor));
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Animator.Update(deltaTime);
            foreach (Part part in parts.Skip(1)) part.Elapsed += deltaTime * Number(part.Settings, "timeScale");
            ApplyPose();
        }

        public void ApplyPose()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PartnerNativeBattleActor));
            LastFrame = pose.Apply(Skeleton);
            foreach (Part part in parts)
            {
                if (part.IndependentAnimation != null)
                    ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(part.Skeleton, part.IndependentAnimation,
                        part.Elapsed, Flag(part.Settings, "loop"));
                part.Batches.Build(part.Skeleton.Pose, part.Skeleton.CreateDrawSlots(), Flag(part.Settings, "useClipping"),
                    Flag(part.Settings, "pmaVertexColors"), Number(part.Settings, "zSpacing"), allowConcaveClipping: true);
                part.Mesh.Assign(part.Batches.Batches, batch => batch.Blend == 0 ? part.Material
                    : throw new NotSupportedException("Original partner non-normal material is unbound."));
            }
            Attachments?.UpdateFollowers();
        }

        private static Part CreatePart(PartnerNativeBattleAssetCatalog catalog, Dictionary<string, object> row,
            Transform parent, Dictionary<string, Transform> transforms)
        {
            var part = new Part { Settings = Obj(row["renderer"]) };
            try
            {
                foreach (var source in Arr(row["transforms"]).Select(Obj))
                {
                    string key = (string)source["sourceKey"];
                    if (!transforms.TryGetValue(key, out Transform node))
                    {
                        node = new GameObject((string)source["name"]).transform; node.SetParent(parent, false);
                        node.gameObject.layer = Convert.ToInt32(source["layer"]); node.localPosition = Vector(source["position"]);
                        node.localScale = Vector(source["scale"]); var q = Obj(source["rotation"]);
                        node.localRotation = new Quaternion(Number(q, "x"), Number(q, "y"), Number(q, "z"), Number(q, "w"));
                        node.gameObject.SetActive(Flag(source, "active")); transforms.Add(key, node);
                    }
                    parent = node;
                }
                if (Arr(part.Settings["separatorSlotNames"]).Count != 0 || Flag(part.Settings, "initialFlipX")
                    || Flag(part.Settings, "initialFlipY") || Flag(part.Settings, "tintBlack")
                    || Flag(part.Settings, "addNormals") || Flag(part.Settings, "calculateTangents")
                    || Int(part.Settings, "maskInteraction") != 0)
                    throw new NotSupportedException("Bind additional original partner rendering branches.");
                var data = ProfessionNativeSkeletonData.LoadPartnerBattle(catalog.Resolve<TextAsset>((string)row["package"]).bytes, Int(row, "npcId"));
                part.Skeleton = new ProfessionNativeAppearanceSkeleton(data);
                part.Skeleton.SetSkin(data.CreateCombinedNpcSkin(Arr(row["skins"]).Select(value => (string)value).ToArray()));
                part.Batches = new ProfessionNativeMeshBatches(data.SlotCount);
                part.Mesh = parent.gameObject.AddComponent<ProfessionNativeSceneMeshPart>();
                ApplyRenderer(part.Mesh.MeshRenderer, Obj(row["meshRenderer"]));
                var materials = Arr(OfficialEquipmentJson.Parse(catalog.Resolve<TextAsset>((string)row["materials"]).text));
                if (materials.Count != 1) throw new NotSupportedException("Bind original partner multipage materials.");
                part.Material = CreateMaterial(catalog, Obj(materials[0]));
                if (Equals(row["rendererKind"], "SkeletonAnimation"))
                    part.IndependentAnimation = new ProfessionNativeAnimationChannels(data, (string)part.Settings["animationName"]);
                return part;
            }
            catch { part.Dispose(); throw; }
        }

        private static Material CreateMaterial(PartnerNativeBattleAssetCatalog catalog, Dictionary<string, object> source)
        {
            var fields = Obj(source["fields"]); var saved = Obj(fields["m_SavedProperties"]);
            var material = new Material(catalog.Shader) { name = (string)fields["m_Name"],
                renderQueue = Int(fields, "m_CustomRenderQueue"), enableInstancing = Flag(fields, "m_EnableInstancingVariants"),
                doubleSidedGI = Flag(fields, "m_DoubleSidedGI"), globalIlluminationFlags = (MaterialGlobalIlluminationFlags)Int(fields, "m_LightmapFlags") };
            try
            {
                material.shaderKeywords = Arr(fields["m_ValidKeywords"]).Select(value => (string)value).ToArray();
                foreach (var pair in Arr(saved["m_Floats"]).Select(Arr)) material.SetFloat((string)pair[0], Convert.ToSingle(pair[1]));
                foreach (var pair in Arr(saved["m_Ints"]).Select(Arr)) material.SetInteger((string)pair[0], Convert.ToInt32(pair[1]));
                foreach (var pair in Arr(saved["m_Colors"]).Select(Arr))
                {
                    var color = Obj(pair[1]); material.SetColor((string)pair[0], new Color(Number(color, "r"), Number(color, "g"), Number(color, "b"), Number(color, "a")));
                }
                foreach (var pair in Arr(saved["m_TexEnvs"]).Select(Arr))
                {
                    var environment = Obj(pair[1]); var scale = Obj(environment["m_Scale"]); var offset = Obj(environment["m_Offset"]);
                    material.SetTexture((string)pair[0], null);
                    material.SetTextureScale((string)pair[0], new Vector2(Number(scale, "x"), Number(scale, "y")));
                    material.SetTextureOffset((string)pair[0], new Vector2(Number(offset, "x"), Number(offset, "y")));
                }
                foreach (var texture in Arr(source["runtimeTextures"]).Select(Obj))
                    material.SetTexture((string)texture["property"], catalog.Resolve<Texture2D>((string)texture["file"]));
                return material;
            }
            catch { Destroy(material); throw; }
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

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (Part part in parts) part.Dispose(); parts.Clear();
            if (Root) Destroy(Root.gameObject);
        }
        private static void Destroy(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector3 Vector(object value) { var v = Obj(value); return new Vector3(Number(v, "x"), Number(v, "y"), Number(v, "z")); }
        private static Vector4 Tiling(object value) { var v = Obj(value); return new Vector4(Number(v, "x"), Number(v, "y"), Number(v, "z"), Number(v, "w")); }
    }
}
