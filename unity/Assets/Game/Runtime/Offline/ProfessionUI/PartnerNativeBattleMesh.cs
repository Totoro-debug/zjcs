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
    // Shared independent geometry endpoint for an actor and an embedded skill model.
    // The caller supplies the original prefab transform and renderer fields.
    internal sealed class PartnerNativeBattleMesh : IDisposable
    {
        internal Dictionary<string, object> Settings { get; }
        internal ProfessionNativeAppearanceSkeleton Skeleton { get; }
        internal ProfessionNativeMeshBatches Batches { get; }
        internal ProfessionNativeSceneMeshPart Mesh { get; }
        internal Material Material { get; }
        internal ProfessionNativeAnimationChannels IndependentAnimation { get; }
        internal float Elapsed;
        private readonly bool ownsMaterial;
        private PartnerNativeBattleRendererPort appearanceRenderer;

        internal PartnerNativeBattleMesh(PartnerNativeBattleAssetCatalog catalog,
            Dictionary<string, object> row, Transform node, Material originalMaterial = null)
            : this(ProfessionNativeSkeletonData.LoadPartnerBattle(catalog.Resolve<TextAsset>((string)row["package"]).bytes,
                Int(row, "npcId")), Obj(row["renderer"]), Arr(row["skins"]).Select(value => (string)value).ToArray(),
                node, originalMaterial ? originalMaterial : OriginalMaterial(catalog, row), Equals(row["rendererKind"], "SkeletonAnimation"))
        {
            ownsMaterial = !originalMaterial;
            try { ApplyRenderer(Mesh.MeshRenderer, Obj(row["meshRenderer"])); }
            catch { Dispose(); throw; }
        }

        internal PartnerNativeBattleMesh(ProfessionNativeSkeletonData data, Dictionary<string, object> settings,
            string[] skins, Transform node, Material material, bool independentAnimation)
        {
            Settings = settings; Material = material; ownsMaterial = true;
            try
            {
                if (Arr(Settings["separatorSlotNames"]).Count != 0 || Flag(Settings, "initialFlipX")
                    || Flag(Settings, "initialFlipY") || Flag(Settings, "tintBlack")
                    || Flag(Settings, "addNormals") || Flag(Settings, "calculateTangents") || Int(Settings, "maskInteraction") != 0)
                    throw new NotSupportedException("Bind additional original partner rendering branches.");
                Skeleton = new ProfessionNativeAppearanceSkeleton(data);
                Skeleton.SetSkin(data.CreateCombinedNpcSkin(skins));
                Batches = new ProfessionNativeMeshBatches(data.SlotCount);
                Mesh = node.gameObject.AddComponent<ProfessionNativeSceneMeshPart>();
                if (independentAnimation)
                    IndependentAnimation = new ProfessionNativeAnimationChannels(data, (string)Settings["animationName"]);
            }
            catch { Dispose(); throw; }
        }
        private static Material OriginalMaterial(PartnerNativeBattleAssetCatalog catalog, Dictionary<string, object> row)
        {
            var materials = Arr(OfficialEquipmentJson.Parse(catalog.Resolve<TextAsset>((string)row["materials"]).text));
            if (materials.Count != 1) throw new NotSupportedException("Bind original partner multipage materials.");
            return CreateMaterial(catalog, Obj(materials[0]));
        }
        internal void SampleAnimation()
            => ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(Skeleton, IndependentAnimation, Elapsed, Flag(Settings, "loop"));
        internal void Draw()
        {
            Batches.Build(Skeleton.Pose, Skeleton.CreateDrawSlots(), Flag(Settings, "useClipping"),
                Flag(Settings, "pmaVertexColors"), Number(Settings, "zSpacing"), allowConcaveClipping: true);
            Mesh.Assign(Batches.Batches, batch => batch.Blend == 0
                ? appearanceRenderer && appearanceRenderer.CustomMaterialOverride.TryGetValue(Material, out var replacement) ? replacement : Material
                : throw new NotSupportedException("Original partner non-normal material is unbound."));
            if (appearanceRenderer) appearanceRenderer.NotifyMeshDrawn();
        }
        internal void BindAppearanceRenderer(PartnerNativeBattleRendererPort renderer)
        {
            if (!renderer || renderer.transform != Mesh.transform || appearanceRenderer)
                throw new InvalidOperationException("Bind this original partner renderer once.");
            appearanceRenderer = renderer;
        }
        public void Dispose() { Batches?.Dispose(); if (ownsMaterial) Destroy(Material); }
        internal static Material CreateMaterial(PartnerNativeBattleAssetCatalog catalog, Dictionary<string, object> source)
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

        internal static void ApplyRenderer(MeshRenderer renderer, Dictionary<string, object> source)
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

        private static void Destroy(Object value) { if (!value) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector4 Tiling(object value) { var v = Obj(value); return new Vector4(Number(v, "x"), Number(v, "y"), Number(v, "z"), Number(v, "w")); }
    }
}
