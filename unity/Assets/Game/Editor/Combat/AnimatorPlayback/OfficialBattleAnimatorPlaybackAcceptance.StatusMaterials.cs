using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunStatusMaterials()
        {
            Run(); SessionState.SetBool(Key + ".statusMaterials", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneStatusMaterialSteps()
        {
            var steps = ScenePlayerAppearanceSteps(labelSuffix: "-materials");
            while (steps.MoveNext()) yield return steps.Current;
            Debug.Log("Original status materials passed: four actual native sex/profession characters, full appearance and lite part material collection, complete 24059 graph, overlapping leases, last-return restoration, same-pool replay and original curve rendering.");
        }

        private static T Private<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static IEnumerator CheckStatusMaterials(Zjcs.Official.CombatAppearance.PlayerAppearance player)
        {
            var actor = player.gameObject;
            var appearance = actor.GetComponent<Zjcs.Official.CombatAppearance.AppearanceRenderer>();
            var root = new GameObject("Original native status material graph scope");
            var center = new MyPlayableCenter();
            var factory = new StatusMaterialFactory(center);
            var previousFactory = StoryPlayableInstantiation.Resolve;
            StoryPlayableInstantiation.Resolve = _ => factory;
            var manager = new MyPlayableManager { ScopeRoot = root, MyPlayableCenter = center,
                InstanceFactory = factory, LowMemory = new Zjcs.Official.StoryPlayables.GAction() };
            var properties = actor.AddComponent<MeshMaterialProperty>();
            var attachment = actor.AddComponent<AttachAgent>(); attachment.AddBoneNode("body", actor.transform);
            var prefab = Resources.Load<GameObject>("Combat/StatusMaterials/Prefabs/EcEntity/Fight/FightSkill/24059/playable24059");
            Require(prefab, "complete native 24059 graph resource");
            var configuration = prefab.GetComponentInChildren<MyPlayableMaterialProperty>(true);
            Require(configuration && configuration.Keyword == "_DITHER_FADE" && configuration.FloatPropertyName == "_DitherAlpha"
                && configuration.Duration == -1 && configuration.NeedResume, "original material leaf serialized fields");
            properties.SetAppearanceTarget(actor); properties.Init();
            Require(properties.Valid, "real original player material collection is valid");
            var expected = appearance.IndependentMaterialDict.Values.ToList();
            foreach (var entry in appearance.PartHandler.LoadedSkeletonDict.Values)
                if (entry.Part.SkeletonTarget.TryGetComponent<AppearanceRendererLite>(out var lite)) expected.AddRange(lite.GetIndependentMaterials());
            var materials = Private<IEnumerable<Material>>(properties, "mMaterials").ToArray();
            Require(materials.SequenceEqual(expected) && materials.Length > 0, "body then loaded lite part material order");
            Require(properties.HasFloat("_DitherAlpha") && properties.enabled && !properties.HasFloat("_absent_fixture_property") && properties.enabled,
                "every material must support a property, without changing component enabled state");
            var defaults = materials.Select(m => m.GetFloat("_DitherAlpha")).ToArray();
            var keywords = materials.Select(m => m.IsKeywordEnabled("_DITHER_FADE")).ToArray();
            void Draw()
            {
                actor.GetComponent<Zjcs.Offline.ProfessionUI.ProfessionNativeSceneRenderer>().LateUpdateMesh();
                foreach (var entry in appearance.PartHandler.LoadedSkeletonDict.Values)
                {
                    var go = entry.Part.GameObject;
                    go.GetComponent<SkeletonRendererFollower>()?.ForceUpdate();
                    go.GetComponentInChildren<Zjcs.Offline.ProfessionUI.ProfessionNativeSceneWeaponRenderer>(true)?.UpdateAnimationAndMesh();
                }
            }
            Draw();
            var renderers = actor.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            string label = "status-material-" + actor.GetComponent<Zjcs.Offline.ProfessionUI.ProfessionNativeSceneRenderer>().State.Source.Sex
                + "-" + player.AppearanceRuntimeData.SkinMap[SkinType.pWeapon];
            CaptureSceneRendererCamera(bounds, label + "-visibility"); Draw();
            var baseline = CaptureSceneRendererCamera(bounds, label + "-before");
            IEnumerator Pump() { for (int i = 0; i < 4; i++) { yield return null; center.Update(.02f); center.LateUpdate(); } }
            void Restored()
            {
                Require(materials.Select(m => m.GetFloat("_DitherAlpha")).SequenceEqual(defaults)
                    && materials.Select(m => m.IsKeywordEnabled("_DITHER_FADE")).SequenceEqual(keywords), "per-material original defaults restored");
            }
            try
            {
                manager.Initialize();
                var first = manager.Play(prefab, actor, actor); yield return Pump();
                var firstLeaf = first.GetComponentInChildren<MyPlayableMaterialProperty>(true);
                Require(firstLeaf.myApplyComponent == properties, "original ApplyBySource resolves the actor material component");
                float value = configuration.FloatCurve.Evaluate(1);
                Require(materials.All(m => m.IsKeywordEnabled("_DITHER_FADE") && Mathf.Approximately(m.GetFloat("_DitherAlpha"), value)),
                    "negative duration uses original final curve value");
                var changed = CaptureSceneRendererCamera(bounds, label + "-active");
                Require(!baseline.SequenceEqual(changed), "real original character raster responds to material graph");
                var second = manager.Play(prefab, actor, actor); yield return Pump();
                Require(!ReferenceEquals(first, second), "concurrent applications lease distinct graph instances");
                first.Stop(); yield return Pump();
                Require(materials.All(m => m.IsKeywordEnabled("_DITHER_FADE") && Mathf.Approximately(m.GetFloat("_DitherAlpha"), value)),
                    "first return retains other application's property reference");
                second.Stop(); yield return Pump(); Restored();
                var replay = manager.Play(prefab, actor, actor); yield return Pump();
                Require(ReferenceEquals(replay, first) || ReferenceEquals(replay, second), "real native graph pool reuse");
                replay.Stop(); yield return Pump(); Restored();
                var colorName = new[] { "_HitEffectColor", "_BaseColor", "_Color" }.FirstOrDefault(name => materials.All(m => m.HasColor(name)));
                Require(colorName != null && properties.HasColor(colorName), "real native materials expose a shared color property");
                var colorDefaults = materials.Select(m => m.GetColor(colorName)).ToArray();
                properties.UseColor(colorName); properties.UseColor(colorName);
                properties.SetColor(colorName, Color.magenta); properties.ReturnColor(colorName);
                Require(materials.All(m => m.GetColor(colorName) == Color.magenta), "color stays until the last reference returns");
                properties.ReturnColor(colorName);
                Require(materials.Select(m => m.GetColor(colorName)).SequenceEqual(colorDefaults), "per-material color defaults restore");
                var sampleTemplate = Object.Instantiate(prefab, root.transform); sampleTemplate.SetActive(false);
                var sampleConfig = sampleTemplate.GetComponentInChildren<MyPlayableMaterialProperty>(true);
                sampleConfig.Duration = .4f; sampleConfig.FloatCurve = AnimationCurve.Linear(0, .2f, 1, .8f);
                sampleConfig.ColorPropertyName = colorName; sampleConfig.ColorCurve = new Gradient();
                sampleConfig.ColorCurve.SetKeys(new[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                var sample = manager.Play(sampleTemplate, actor, actor);
                var sampleLeaf = sample.GetComponentInChildren<MyPlayableMaterialProperty>(true);
                yield return null;
                sampleLeaf.ProcessFrame(.1f);
                Require(materials.All(m => Mathf.Approximately(m.GetFloat("_DitherAlpha"), .35f)), "positive duration evaluates quarter-time float curve");
                Require(materials.All(m => Mathf.Abs(m.GetColor(colorName).r - .25f) < .001f), "positive duration evaluates quarter-time color gradient");
                sample.Stop(); yield return Pump(); Restored();
                Require(materials.Select(m => m.GetColor(colorName)).SequenceEqual(colorDefaults), "playable stop restores gradient color defaults");
                var heldTemplate = Object.Instantiate(sampleTemplate, root.transform);
                var heldConfig = heldTemplate.GetComponentInChildren<MyPlayableMaterialProperty>(true);
                heldConfig.NeedResume = false; heldConfig.Duration = 0;
                var held = manager.Play(heldTemplate, actor, actor);
                yield return null;
                held.GetComponentInChildren<MyPlayableMaterialProperty>(true).ProcessFrame(.01f);
                held.Stop(); yield return Pump();
                Require(materials.All(m => Mathf.Approximately(m.GetFloat("_DitherAlpha"), .2f)), "zero duration clamps delta to zero and NeedResume false retains the initial curve value");
                // Destroying the original collector clears every remaining lease.
                Object.DestroyImmediate(properties); properties = actor.AddComponent<MeshMaterialProperty>();
                properties.SetAppearanceTarget(actor); properties.Init(); Restored();
                Require(materials.Select(m => m.GetColor(colorName)).SequenceEqual(colorDefaults), "collector destruction restores held colors");
                var perpetualTemplate = Object.Instantiate(heldTemplate, root.transform);
                perpetualTemplate.GetComponentInChildren<MyPlayableMaterialProperty>(true).Duration = -1;
                var perpetual = manager.Play(perpetualTemplate, actor, actor); yield return Pump();
                perpetual.Stop(); yield return Pump();
                Require(materials.All(m => Mathf.Approximately(m.GetFloat("_DitherAlpha"), .8f)), "negative duration and NeedResume false retain the final curve value");
                Object.DestroyImmediate(properties); properties = actor.AddComponent<MeshMaterialProperty>();
                properties.SetAppearanceTarget(actor); properties.Init(); Restored();
                // Exercise the original MeshRenderer branch against the same real
                // native mesh materials after the appearance branch is released.
                var rendererTarget = new GameObject("Original material renderer branch");
                var rendererProperties = rendererTarget.AddComponent<MeshMaterialProperty>();
                rendererProperties.MeshRenderer = rendererTarget.AddComponent<MeshRenderer>();
                rendererProperties.MeshRenderer.sharedMaterials = materials;
                rendererProperties.Init();
                rendererProperties.UseFloat("_DitherAlpha"); rendererProperties.UseFloat("_DitherAlpha");
                rendererProperties.SetFloat("_DitherAlpha", .123f); rendererProperties.ReturnFloat("_DitherAlpha");
                Require(materials.All(m => Mathf.Approximately(m.GetFloat("_DitherAlpha"), .123f)), "renderer branch keeps overlapping lease");
                rendererProperties.ReturnFloat("_DitherAlpha"); Restored();
                Object.DestroyImmediate(rendererTarget);
                var customSteps = CheckCustomMaterialTarget(prefab, root, actor, manager, materials, Pump, bounds, label);
                while (customSteps.MoveNext()) yield return customSteps.Current;
                Restored();
                manager.RemoveAllPlayable(true); center.RemoveAll();
                Object.DestroyImmediate(root);
                Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-restored")), "native character GPU pixels return to exact baseline");
                Debug.Log("[Original status material] " + label + "; materials=" + materials.Length + "; complete graph overlap, pool replay and exact GPU restoration passed.");
            }
            finally
            {
                manager.Dispose(); center.RemoveAll();
                StoryPlayableInstantiation.Resolve = previousFactory;
                Object.DestroyImmediate(properties); Object.DestroyImmediate(attachment);
                if (root) Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator CheckCustomMaterialTarget(GameObject prefab, GameObject scope, GameObject actor,
            MyPlayableManager manager, Material[] materials, Func<IEnumerator> pump, Bounds bounds, string label)
        {
            var firstTarget = new GameObject("Custom apply material target A"); firstTarget.transform.SetParent(scope.transform, false);
            var secondTarget = new GameObject("Custom apply material target B"); secondTarget.transform.SetParent(scope.transform, false);
            MeshMaterialProperty Receiver(GameObject go)
            {
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                var receiver = go.AddComponent<MeshMaterialProperty>(); receiver.MeshRenderer = renderer; receiver.Init(); return receiver;
            }
            var firstReceiver = Receiver(firstTarget); var secondReceiver = Receiver(secondTarget);
            var template = Object.Instantiate(prefab, scope.transform); template.SetActive(false);
            var config = template.GetComponentInChildren<MyPlayableMaterialProperty>(true);
            // A modified fixture template isolates the original custom binding;
            // no official native graph or its serialized configuration is edited.
            Object.DestroyImmediate(config.GetComponent<ApplyBySource>());
            var custom = config.gameObject.AddComponent<ApplyByCustom>(); custom.CustomApplyObj = firstTarget;
            var graph = manager.Play(template, actor, actor);
            var leaf = graph.GetComponentInChildren<MyPlayableMaterialProperty>(true);
            Require(leaf.myApplyComponent == firstReceiver && leaf.mySource == actor && leaf.myTarget == actor,
                "original custom target overrides source/target while retaining them as context");
            yield return pump();
            graph.Stop(true); yield return pump();
            var binding = leaf.GetComponent<ApplyByCustom>();
            int initialized = 0, played = 0;
            Action<MyPlayable> initializedHandler = entry =>
            {
                Require(entry.myApplyComponent == firstReceiver, "custom Init resolves the current configured object");
                initialized++; binding.CustomApplyObj = secondTarget;
            };
            Action<MyPlayable> playedHandler = entry =>
            {
                Require(entry.myApplyComponent == secondReceiver, "custom Play re-evaluates the object after Init"); played++;
            };
            leaf.OnInit += initializedHandler; leaf.OnPostPlay += playedHandler;
            try
            {
                var replay = manager.Play(template, actor, actor);
                Require(ReferenceEquals(replay, graph), "custom binding runs on an actual native material graph pooled instance");
                yield return pump();
                Require(initialized == 1 && played == 1 && leaf.myApplyComponent == secondReceiver,
                    "custom Init and Play each run once after pool reset and retain the final receiver");
                Require(materials.All(material => material.IsKeywordEnabled("_DITHER_FADE")
                    && Mathf.Approximately(material.GetFloat("_DitherAlpha"), config.FloatCurve.Evaluate(1))),
                    "custom selected receiver drives the real native character material effect");
                CaptureSceneRendererCamera(bounds, label + "-custom-target");
                replay.Stop(true); yield return pump();
            }
            finally { leaf.OnInit -= initializedHandler; leaf.OnPostPlay -= playedHandler; }
            Debug.Log("[Original custom target] Native character material graph fixture: source/target retained, configured receiver selected, Init-to-Play retargeting and actual pool replay passed.");
        }

        private sealed class StatusMaterialFactory : IStoryPlayableInstanceFactory
        {
            private readonly MyPlayableCenter center;
            public StatusMaterialFactory(MyPlayableCenter center) { this.center = center; }
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            {
                var instance = Object.Instantiate(prefab, parent, worldPositionStays);
                foreach (var playable in instance.GetComponentsInChildren<MyPlayable>(true)) playable.MyPlayableCenter = center;
                return instance;
            }
        }
    }
}
