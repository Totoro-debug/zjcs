using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunPartnerMaterials()
        {
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            RunActorMaterials();
            SessionState.SetBool(Key + ".partnerMaterials", true);
        }

        private static IEnumerator PartnerMaterialSteps()
        {
            int[] npcs = { 10608, 10708, 11001, 11002, 11003, 11004, 11005, 11006, 11007, 11008,
                11009, 11010, 11011, 11012, 11013, 11014, 11024, 11025, 11026, 11027,
                11033, 11038, 11039, 11040, 11041, 11042 };
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            var damage = Resources.Load<GameObject>("Combat/SkillGraphs/GameObject/playable_damage_wuli_bigger");
            Require(damage, "original partner damage graph");
            var configuration = damage.GetComponentInChildren<MyPlayableMaterialProperty>(true);
            Require(configuration && configuration.Keyword == "_HITEFFECT" && configuration.Duration == .15f
                && configuration.NeedResume, "original partner damage material fields");
            foreach (int npc in npcs)
            {
                var world = new GameObject("Original partner material validation " + npc);
                using var scope = new OfflineBattleNativePlayableScope(world.transform, null);
                using var actor = new PartnerNativeBattleActor(catalog, npc, world.transform, "Partner material actor");
                using var peer = new PartnerNativeBattleActor(catalog, npc, world.transform, "Partner material peer");
                try
                {
                    peer.Root.position = new Vector3(1000, 1000, 0);
                    var source = actor.Attachments.Agent.gameObject;
                    var property = source.GetComponent<MeshMaterialProperty>();
                    var peerProperty = peer.Attachments.Agent.GetComponent<MeshMaterialProperty>();
                    var appearances = actor.Root.GetComponentsInChildren<AppearanceRendererLite>(true);
                    var peerAppearances = peer.Root.GetComponentsInChildren<AppearanceRendererLite>(true);
                    var binding = PartnerNativeBattleAppearanceCatalog.Find(npc);
                    Require(property && peerProperty && property.MeshRenderer == null && peerProperty.MeshRenderer == null,
                        "original partner null MeshRenderer reference " + npc);
                    Require(appearances.Length == binding.Renderers.Length && appearances.Length == (npc == 11005 ? 2 : 1),
                        "all original partner appearance renderers " + npc);
                    property.Init(); peerProperty.Init(); actor.Advance(0); peer.Advance(0);
                    var materials = appearances.SelectMany(appearance => appearance.GetIndependentMaterials()).ToArray();
                    var peerMaterials = peerAppearances.SelectMany(appearance => appearance.GetIndependentMaterials()).ToArray();
                    actor.Advance(0); peer.Advance(0);
                    var primary = appearances.Single(appearance => appearance.transform == source.transform.Find(binding.AppearanceTargetPath))
                        .GetIndependentMaterials();
                    Require(property.Valid && !materials.Intersect(peerMaterials).Any()
                        && primary.SequenceEqual(Private<IEnumerable<Material>>(property, "mMaterials")),
                        "original partner collector order and instance isolation " + npc);
                    var parts = new[] { actor.OriginalRow }.Concat(((List<object>)actor.OriginalRow["parts"])
                        .Cast<Dictionary<string, object>>()).ToArray();
                    for (int index = 0; index < binding.Renderers.Length; index++)
                    {
                        var appearance = source.transform.Find(binding.Renderers[index].Path).GetComponent<AppearanceRendererLite>();
                        var originalMaterials = (List<object>)OfficialEquipmentJson.Parse(catalog.Resolve<TextAsset>((string)parts[index]["materials"]).text);
                        var material = (Dictionary<string, object>)originalMaterials.Single();
                        var texture = ((List<object>)material["runtimeTextures"]).Cast<Dictionary<string, object>>()
                            .Single(row => object.Equals(row["property"], "_MainTex"));
                        Require(appearance.LevelOfResolution == LevelOfResolution.Low && appearance.TextureLoader
                            && appearance.GetIndependentMaterials().Single().mainTexture == catalog.Resolve<Texture2D>((string)texture["file"]),
                            "exact original partner Low texture " + npc + "/" + index);
                    }
                    foreach (var animator in world.GetComponentsInChildren<Animator>(true)) animator.speed = 0;
                    var defaults = materials.Select(material => material.GetFloat(configuration.FloatPropertyName)).ToArray();
                    var colors = materials.Select(material => material.GetColor(configuration.ColorPropertyName)).ToArray();
                    var keywords = materials.Select(material => material.IsKeywordEnabled(configuration.Keyword)).ToArray();
                    var peerDefaults = peerMaterials.Select(material => material.GetFloat(configuration.FloatPropertyName)).ToArray();
                    var secondary = materials.Except(primary).ToArray();
                    var secondaryDefaults = secondary.Select(material => material.GetFloat(configuration.FloatPropertyName)).ToArray();
                    var bounds = actor.Renderers[0].bounds;
                    foreach (var renderer in actor.Renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    string label = "partner-material-" + npc;
                    CaptureSceneRendererCamera(bounds, label + "-visibility");
                    var baseline = CaptureSceneRendererCamera(bounds, label + "-before");
                    var first = scope.Play(configuration.gameObject, source, source);
                    for (int frame = 0; frame < 4; frame++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
                    actor.Advance(0);
                    var leaf = first.GetComponentInChildren<MyPlayableMaterialProperty>(true);
                    Require(leaf.myApplyComponent == property && primary.All(material => material.IsKeywordEnabled(leaf.Keyword)
                        && Mathf.Approximately(material.GetFloat(leaf.FloatPropertyName), leaf.FloatCurve.Evaluate(.04f / leaf.Duration))),
                        "original partner damage node selects the exact collector " + npc);
                    Require(peerMaterials.Select(material => material.GetFloat(leaf.FloatPropertyName)).SequenceEqual(peerDefaults)
                        && secondary.Select(material => material.GetFloat(leaf.FloatPropertyName)).SequenceEqual(secondaryDefaults),
                        "peer and original unreferenced secondary renderer stay independent " + npc);
                    Require(!baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-active")),
                        "actual partner damage raster changes " + npc);
                    var second = scope.Play(configuration.gameObject, source, source);
                    for (int frame = 0; frame < 4; frame++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
                    first.Stop(true);
                    for (int frame = 0; frame < 4; frame++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
                    Require(primary.All(material => material.IsKeywordEnabled(leaf.Keyword)
                        && Mathf.Approximately(material.GetFloat(leaf.FloatPropertyName), leaf.FloatCurve.Evaluate(.08f / leaf.Duration))),
                        "overlapping original partner material leases " + npc);
                    second.Stop(true);
                    for (int frame = 0; frame < 4; frame++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
                    actor.Advance(0);
                    Require(materials.Select(material => material.GetFloat(leaf.FloatPropertyName)).SequenceEqual(defaults)
                        && materials.Select(material => material.GetColor(leaf.ColorPropertyName)).SequenceEqual(colors)
                        && materials.Select(material => material.IsKeywordEnabled(leaf.Keyword)).SequenceEqual(keywords),
                        "last original partner material lease restores all fields " + npc);
                    Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-restored")),
                        "actual partner raster restoration " + npc);
                    Debug.Log("Original partner material passed: " + npc + "; original nullable reference, Low loader, overlap, peer/secondary isolation and GPU restoration.");
                }
                finally
                {
                    scope.Dispose(); actor.Dispose(); peer.Dispose();
                    Object.DestroyImmediate(world);
                }
                yield return null;
            }
        }
    }
}
