using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Official.BattleSpinePlayables;

namespace Zjcs.Offline.ProfessionUI
{
    // Pre-Awake adapter for the embedded 4.2 NPC in the original 58550 skill.
    // Other skill effects keep their existing runtime. Only serialized metadata
    // and the already decoded native partner package cross this boundary.
    public sealed class PartnerNativeSkillSkeleton : MonoBehaviour
    {
        private PartnerNativeBattleMesh mesh;
        private AttachmentFollower[] followers;
        private bool unscaled;
        private float speed;

        public static void BindOriginalPrefab(GameObject instance)
        {
            var asset = Resources.Load<TextAsset>("Combat/NativePartnerAssets/skill-skeleton-bindings");
            if (!asset) throw new FileNotFoundException("Original embedded partner skeleton bindings are not packaged.");
            var rows = Arr(Obj(OfficialEquipmentJson.Parse(asset.text))["graphs"]).Select(Obj);
            foreach (var row in rows)
            {
                string name = (string)row["prefab"];
                if (instance.name != name && instance.name != name + "(Clone)") continue;
                if (instance.activeInHierarchy) throw new InvalidOperationException("Bind the embedded partner before Awake.");
                var node = instance.transform.Find((string)row["path"])
                    ?? throw new InvalidDataException("Original embedded partner transform is unbound.");
                var child = instance.transform.Find((string)row["parts_path"])
                    ?? throw new InvalidDataException("Original embedded separator child is unbound.");
                var originalAnimation = RequireComponent(node, "Spine.Unity.SkeletonAnimation");
                var followerRows = Arr(row["followers"]).Select(Obj).ToArray();
                var followers = followerRows.Select(value => instance.transform.Find((string)value["path"])
                    .GetComponent<AttachmentFollower>()).ToArray();
                if (followers.Any(value => !value || value.SkeletonRenderer != originalAnimation))
                    throw new InvalidDataException("Original embedded follower references differ.");
                // Remove the obsolete component on this inactive clone, never the prefab
                // asset. Disabled SDK components would still receive Awake on activation.
                Remove(node, "SkeletonRendererHighTexturePreview");
                Remove(node, "AppearanceSystem.SpineExt.DynamicSkeletonRenderSeparator");
                Remove(child, "Spine.Unity.SkeletonPartsRenderer");
                Remove(node, "AppearanceCombineSkin"); // Empty original combined-skin list.
                UnityEngine.Object.DestroyImmediate(originalAnimation);
                var native = node.gameObject.AddComponent<PartnerNativeSkillSkeleton>();
                native.Initialize(row, child, followers);
            }
        }

        private void Initialize(Dictionary<string, object> row, Transform child, AttachmentFollower[] originalFollowers)
        {
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            var actor = catalog.Find(Convert.ToInt32(row["npc_id"]));
            var data = ProfessionNativeSkeletonData.LoadPartnerBattle(catalog.Resolve<TextAsset>((string)actor["package"]).bytes,
                Convert.ToInt32(row["npc_id"]));
            var source = Arr(OfficialEquipmentJson.Parse(catalog.Resolve<TextAsset>((string)actor["materials"]).text));
            if (source.Count != 1) throw new NotSupportedException("Bind original embedded multipage materials.");
            var material = PartnerNativeBattleMesh.CreateMaterial(catalog, Obj(source[0]));
            var high = Resources.Load<Texture2D>((string)row["high_texture"])
                ?? throw new FileNotFoundException("Original embedded high atlas is not packaged.");
            material.SetTexture("_MainTex", high);
            var settings = Obj(row["renderer"]);
            mesh = new PartnerNativeBattleMesh(data, settings, Arr(row["skins"]).Select(value => (string)value).ToArray(),
                child, material, true);
            PartnerNativeBattleMesh.ApplyRenderer(mesh.Mesh.MeshRenderer, Obj(row["mesh_renderer"]));
            // The original separator redirects the one page to its existing child.
            var rootRenderer = GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock(); rootRenderer.GetPropertyBlock(block);
            mesh.Mesh.MeshRenderer.SetPropertyBlock(block); rootRenderer.enabled = false;
            unscaled = Convert.ToBoolean(settings["unscaledTime"]); speed = Convert.ToSingle(settings["timeScale"]);
            var port = gameObject.AddComponent<PartnerNativeBattleRendererPort>();
            port.Bind(mesh.Skeleton, material, Convert.ToInt32(settings["updateWhenInvisible"]),
                Convert.ToSingle(settings["zSpacing"]), (string)row["skeleton_data_name"], Draw, mesh.Mesh.MeshRenderer);
            followers = originalFollowers;
            foreach (var follower in followers) follower.SkeletonRenderer = port;
            mesh.SampleAnimation(); mesh.Draw();
            enabled = Convert.ToBoolean(settings["m_Enabled"]);
        }

        // Original UpdateTiming.InUpdate = 1. Geometry/followers are refreshed
        // after the pose; pooling preserves the original clearStateOnDisable=false.
        private void Update()
        {
            if (mesh == null) return;
            mesh.Elapsed += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime) * speed;
            mesh.SampleAnimation();
        }
        private void LateUpdate() { if (mesh != null) Draw(); }
        private void Draw()
        {
            mesh.Draw();
            foreach (var follower in followers)
                if (follower && follower.isActiveAndEnabled) follower.ForceUpdate();
        }
        private void OnDestroy() { mesh?.Dispose(); mesh = null; }
        private static MonoBehaviour RequireComponent(Transform node, string type)
            => node.GetComponents<MonoBehaviour>().Single(component => component && component.GetType().FullName == type);
        private static void Remove(Transform node, string type) => UnityEngine.Object.DestroyImmediate(RequireComponent(node, type));
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
    }
}
