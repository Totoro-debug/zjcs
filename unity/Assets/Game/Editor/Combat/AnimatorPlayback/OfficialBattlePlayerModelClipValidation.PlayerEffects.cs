using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        public static void RunPlayerEffects()
        {
            RunPlayerActors();
            SessionState.SetBool(Key + ".player-effects", true);
        }

        [Serializable] private sealed class EffectCases { public EffectCase[] cases; }
        [Serializable] private sealed class EffectCase
        {
            public string name;
            public int skin_type, particles, followers, backers;
            public EffectNode[] nodes;
        }
        [Serializable] private sealed class EffectNode
        {
            public string name;
            public int parent;
            public bool active;
            public EffectTransform transform;
        }
        [Serializable] private sealed class EffectTransform
        {
            public Vector3 m_LocalPosition, m_LocalScale;
            public Quaternion m_LocalRotation;
        }

        private static IEnumerator CheckPlayerEffects(ProfessionNativeBattlePlayerActor actor,
            Sex sex, int profession, ProfessionAppearanceTables tables, AppearanceConfig config,
            ProfessionAppearanceResolver resolver, Action<string> capture)
        {
            var cases = JsonUtility.FromJson<EffectCases>(File.ReadAllText(Path.Combine(
                Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "player-effect-cases.json"))).cases;
            var catalog = Resources.Load<OfflineProfessionImageCatalog>("Combat/PlayerEffects/catalog");
            Require(cases.Length == 32 && catalog.Entries.Length == 32, "all current standalone effect addresses");
            int count = 0, particles = 0, followers = 0, backers = 0;
            foreach (var item in cases)
            {
                string sexToken = sex == Sex.Male ? "_M_" : "_F_";
                if ((item.name.Contains("_M_") || item.name.Contains("_F_")) && !item.name.Contains(sexToken)) continue;
                var template = (GameObject)catalog.Entries.Single(e => e.Path.EndsWith("/" + item.name, StringComparison.Ordinal)).Asset;
                var sourceNodes = template.GetComponentsInChildren<Transform>(true);
                Require(sourceNodes.Length == item.nodes.Length, "complete source effect prefab");
                for (int i = 0; i < sourceNodes.Length; i++)
                {
                    var expected = item.nodes[i]; var node = sourceNodes[i];
                    Require(node.name == expected.name && node.gameObject.activeSelf == expected.active
                        && (i == 0 || node.parent == sourceNodes[expected.parent])
                        && (node.localPosition - expected.transform.m_LocalPosition).sqrMagnitude < 1e-8f
                        && (node.localScale - expected.transform.m_LocalScale).sqrMagnitude < 1e-8f
                        && Quaternion.Angle(node.localRotation, expected.transform.m_LocalRotation) < .01f,
                        "unaltered serialized effect hierarchy: " + item.name + "/" + i);
                }
                var data = resolver.BuildAppearanceRuntimeData(sex, (Profession)profession,
                    resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession), config.GetAppendSkins(AppendSkinType.Game));
                data.SkinMap[(SkinType)item.skin_type] = item.name;
                actor.Appearance.AppearanceCharacter.SetRuntimeData(data);
                actor.Renderer.UpdateAnimationAndMesh();
                var slot = actor.Animator.transform.Find("PartSlot");
                var effect = slot.Cast<Transform>().Single(t => t.name == item.name + "(Clone)");
                var nodes = effect.GetComponentsInChildren<Transform>(true);
                Require(nodes.Length == item.nodes.Length, "complete original effect hierarchy: " + item.name);
                for (int i = 0; i < nodes.Length; i++)
                {
                    var expected = item.nodes[i]; var node = nodes[i];
                    Require(node.name == expected.name + (i == 0 ? "(Clone)" : ""), "effect instance name: " + item.name + "/" + i);
                    Require(i == 0 || node.parent == nodes[expected.parent], "effect source parent: " + item.name + "/" + i);
                    // Followers own their animated transforms after original Init.
                    if (node.GetComponent<BaseSkeletonFollower>()) continue;
                    Require((node.localPosition - expected.transform.m_LocalPosition).sqrMagnitude < 1e-8f
                        && (node.localScale - expected.transform.m_LocalScale).sqrMagnitude < 1e-8f
                        && Quaternion.Angle(node.localRotation, expected.transform.m_LocalRotation) < .01f,
                        "effect original transform: " + item.name + "/" + i);
                }
                Require(effect.GetComponentsInChildren<MonoBehaviour>(true).All(c => c), "effect has no missing scripts: " + item.name);
                Require(effect.GetComponentsInChildren<ParticleSystem>(true).Length == item.particles
                    && effect.GetComponentsInChildren<SkeletonRendererFollower>(true).Length == item.followers
                    && effect.GetComponentsInChildren<SkeletonRendererAttachmentBacker>(true).Length == item.backers,
                    "complete source particle/follower/backer counts: " + item.name);
                for (int frame = 0; frame < 40; frame++) yield return null;
                foreach (var material in effect.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
                {
                    string messages = string.Join("; ", ShaderUtil.GetShaderMessages(material.shader).Select(m => m.message));
                    Debug.Log($"Original effect material: {item.name}; {material.name}; {material.shader.name}; supported={material.shader.isSupported}; {messages}");
                    Require(material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "original effect shader compiles for Windows: " + material.name);
                }
                foreach (var follower in effect.GetComponentsInChildren<SkeletonRendererFollower>(true))
                    Require(follower.transform.parent == effect ? follower.SkeletonRenderer == actor.Renderer : !follower.SkeletonRenderer,
                        "original handler binds direct-child followers and preserves other null references");
                CheckPlayerEffectOrdering(actor, effect, sourceNodes, nodes, sex + "-" + item.name);
                foreach (var backer in effect.GetComponentsInChildren<SkeletonRendererAttachmentBacker>(true))
                {
                    // Each cape has a source-disabled alternate branch. Exercise
                    // that branch explicitly, then restore the actual source pose.
                    var inactive = new System.Collections.Generic.List<GameObject>();
                    for (var node = backer.transform; node != actor.Animator.transform; node = node.parent)
                        if (!node.gameObject.activeSelf) inactive.Add(node.gameObject);
                    int index = actor.Renderer.State.Source.FindSlot(backer.SlotName);
                    Require(index >= 0, "original cape stencil slot exists");
                    var current = actor.Renderer.State.GetAttachment(index);
                    try
                    {
                        for (int i = inactive.Count - 1; i >= 0; i--) inactive[i].SetActive(true);
                        var named = actor.Renderer.State.GetAttachment(index, backer.AttachmentName);
                        Require(named is ProfessionNativeRenderAttachment, "original named cape attachment exists");
                        actor.Renderer.State.SetSlotAttachment(index, named);
                        actor.Renderer.LateUpdateMesh();
                        Require(backer.SkeletonRenderer == actor.Renderer && backer.Slot != null
                            && backer.Attachment != null && backer.MeshFilter.sharedMesh.vertexCount > 0,
                            "original cape stencil mesh follows current attachment: " + item.name + "/" + backer.SlotName);
                        actor.Renderer.State.SetSlotAttachment(index, null);
                        actor.Renderer.LateUpdateMesh();
                        Require(backer.MeshFilter.sharedMesh.vertexCount == 0,
                            "cached named attachment gates current empty slot, without stale stencil geometry");
                        actor.Renderer.State.SetSlotAttachment(index, named);
                        actor.Renderer.LateUpdateMesh();
                        Require(backer.MeshFilter.sharedMesh.vertexCount > 0, "cape stencil returns on current attachment restoration");
                        backer.gameObject.SetActive(false);
                        actor.Renderer.State.SetSlotAttachment(index, null);
                        actor.Renderer.LateUpdateMesh();
                        Require(backer.MeshFilter.sharedMesh.vertexCount > 0, "disabled stencil unsubscribes mesh updates");
                        backer.gameObject.SetActive(true);
                        actor.Renderer.LateUpdateMesh();
                        Require(backer.MeshFilter.sharedMesh.vertexCount == 0, "re-enabled stencil resumes current-slot baking");
                    }
                    finally
                    {
                        actor.Renderer.State.SetSlotAttachment(index, current);
                        foreach (var node in inactive) node.SetActive(false);
                        actor.Renderer.LateUpdateMesh();
                    }
                }
                if (item.backers > 0)
                {
                    var masks = effect.GetComponentsInChildren<SkeletonRendererAttachmentBacker>()
                        .Where(b => b.MeshRenderer.enabled && b.MeshFilter.sharedMesh && b.MeshFilter.sharedMesh.vertexCount > 0).ToArray();
                    Require(masks.Length > 0, "the original visible cape branch has an actual stencil mesh");
                    var renderers = actor.Root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    var before = Capture(bounds, item.name + "-stencil-on");
                    try
                    {
                        foreach (var mask in masks) mask.MeshRenderer.enabled = false;
                        var after = Capture(bounds, item.name + "-stencil-off");
                        Require(!before.SequenceEqual(after), "original live cape stencil changes actual GPU pixels");
                    }
                    finally { foreach (var mask in masks) mask.MeshRenderer.enabled = true; }
                }
                capture(item.name + "-original-effect");
                count++; particles += item.particles; followers += item.followers; backers += item.backers;
            }
            Require(count == cases.Count(item => (!item.name.Contains("_M_") && !item.name.Contains("_F_"))
                || item.name.Contains(sex == Sex.Male ? "_M_" : "_F_")), "all effect skins for this original sex");
            Debug.Log($"Original player effects passed: {sex}; {count} complete prefabs, {particles} particles, {followers} application followers, {backers} live stencil backers; source hierarchy, original handler loading, actual Animator/LateUpdate and GPU capture.");
        }
    }
}
