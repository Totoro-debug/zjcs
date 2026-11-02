using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.ProfessionUI
{
    // Read the exported application prefab, rather than creating alternative
    // scene defaults. Transform, Animator and appearance ownership stay outside.
    internal sealed class ProfessionNativeSceneRendererSettings
    {
        private readonly Dictionary<string, object> mesh, skeleton;
        private ProfessionNativeSceneRendererSettings(Dictionary<string, object> mesh, Dictionary<string, object> skeleton)
        { this.mesh = mesh; this.skeleton = skeleton; }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector4 Vector(Dictionary<string, object> value) => new Vector4(Convert.ToSingle(value["x"]), Convert.ToSingle(value["y"]), Convert.ToSingle(value["z"]), Convert.ToSingle(value["w"]));
        public string InitialSkin => (string)skeleton["initialSkinName"];
        public bool UseClipping => Flag(skeleton, "useClipping");
        public bool PmaVertexColors => Flag(skeleton, "pmaVertexColors");
        public bool DisableRenderingOnOverride => Flag(skeleton, "disableRenderingOnOverride");
        public bool Enabled => Flag(mesh, "m_Enabled");
        public float ZSpacing => Convert.ToSingle(skeleton["zSpacing"]);
        public int UpdateWhenInvisible => Int(skeleton, "updateWhenInvisible");

        public static ProfessionNativeSceneRendererSettings Load(TextAsset source)
        {
            var root = Obj(Arr(Obj(OfficialEquipmentJson.Parse(source.text))["nodes"])[0]);
            var components = Arr(root["components"]).Select(Obj).ToArray();
            var mesh = Obj(components.Single(c => (string)c["type"] == "MeshRenderer")["fields"]);
            var skeleton = Obj(components.Single(c => (string)c["type"] == "Spine.Unity.SkeletonMecanim")["fields"]);
            // Fail explicitly if a future selected prefab needs an unbound drawing feature.
            foreach (string field in new[] { "initialFlipX", "initialFlipY", "zSpacing", "tintBlack", "singleSubmesh", "fixDrawOrder", "addNormals", "calculateTangents", "maskInteraction", "immutableTriangles", "clearStateOnDisable" })
                if (Convert.ToSingle(skeleton[field]) != 0) throw new NotSupportedException("Bind original scene renderer field: " + field);
            if (Arr(skeleton["separatorSlotNames"]).Count != 0 || Arr(mesh["m_Materials"]).Count != 0)
                throw new NotSupportedException("Bind original scene initial separators and materials.");
            foreach (string field in new[] { "m_StaticBatchRoot", "m_ProbeAnchor", "m_LightProbeVolumeOverride", "m_AdditionalVertexStreams", "m_EnlightenVertexStream" })
                if (Convert.ToInt64(Obj(mesh[field])["m_PathID"]) != 0 || Convert.ToInt32(Obj(mesh[field])["m_FileID"]) != 0)
                    throw new NotSupportedException("Bind original scene renderer reference: " + field);
            var batch = Obj(mesh["m_StaticBatchInfo"]);
            if (Int(batch, "firstSubMesh") != 0 || Int(batch, "subMeshCount") != 0 || Flag(mesh, "m_RayTraceProcedural"))
                throw new NotSupportedException("Bind original scene static batch or procedural ray trace data.");
            return new ProfessionNativeSceneRendererSettings(mesh, skeleton);
        }

        public void Apply(MeshRenderer renderer)
        {
            renderer.enabled = Enabled;
            renderer.shadowCastingMode = (ShadowCastingMode)Int(mesh, "m_CastShadows");
            renderer.receiveShadows = Flag(mesh, "m_ReceiveShadows");
            renderer.allowOcclusionWhenDynamic = Flag(mesh, "m_DynamicOccludee");
            renderer.staticShadowCaster = Flag(mesh, "m_StaticShadowCaster");
            renderer.motionVectorGenerationMode = (MotionVectorGenerationMode)Int(mesh, "m_MotionVectors");
            renderer.lightProbeUsage = (LightProbeUsage)Int(mesh, "m_LightProbeUsage");
            renderer.reflectionProbeUsage = (ReflectionProbeUsage)Int(mesh, "m_ReflectionProbeUsage");
            renderer.rayTracingMode = (UnityEngine.Experimental.Rendering.RayTracingMode)Int(mesh, "m_RayTracingMode");
            renderer.renderingLayerMask = Convert.ToUInt32(mesh["m_RenderingLayerMask"]);
            renderer.rendererPriority = Int(mesh, "m_RendererPriority");
            renderer.lightmapIndex = Int(mesh, "m_LightmapIndex") == 65535 ? -1 : Int(mesh, "m_LightmapIndex");
            renderer.realtimeLightmapIndex = Int(mesh, "m_LightmapIndexDynamic") == 65535 ? -1 : Int(mesh, "m_LightmapIndexDynamic");
            renderer.lightmapScaleOffset = Vector(Obj(mesh["m_LightmapTilingOffset"]));
            renderer.realtimeLightmapScaleOffset = Vector(Obj(mesh["m_LightmapTilingOffsetDynamic"]));
            renderer.probeAnchor = null; renderer.lightProbeProxyVolumeOverride = null; renderer.additionalVertexStreams = null;
            renderer.sortingLayerID = Int(mesh, "m_SortingLayerID");
            renderer.sortingOrder = Int(mesh, "m_SortingOrder");
        }
    }
}
