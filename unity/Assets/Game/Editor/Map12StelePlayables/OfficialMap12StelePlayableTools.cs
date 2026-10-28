using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.StelePlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor.Map12StelePlayables
{
    public static class OfficialMap12StelePlayableTools
    {
        public static void Validate()
        {
            var catalog = OfficialStelePlayableCatalog.Load();
            int nodes = 0, particles = 0, renderers = 0, behaviours = 0;
            foreach (var entry in catalog.Entries)
            {
                Check(entry.mCanInteractionPlayable, entry.entityClassId, false);
                Check(entry.mEndInteractionPlayable, entry.entityClassId, true);
            }
            Require(nodes == 84 && particles == 64 && renderers == 72 && behaviours == 20,
                "Original stele hierarchy and particle coverage differs.");
            ValidateStateBranches(catalog.Find(6650));
            Debug.Log("Map12 stele native asset validation passed: 4 prefabs, 84 nodes, 64 particle systems, "
                + "72 renderers, original script bindings, unbaked renderer defaults and periodic-interaction branches. "
                + "Map interaction, actual playback and rendering remain separate checks.");

            void Check(GameObject prefab, int entityClassId, bool finished)
            {
                string name = (entityClassId == 6650 ? "multistele" : "singlestele")
                    + (finished ? "_dis_playable" : "_loop_playable");
                Require(prefab != null && prefab.name == name, "Original stele prefab identity differs.");
                string path = AssetDatabase.GetAssetPath(prefab);
                Require(path.StartsWith("Assets/Official/World/Map/12/NativeStelePlayables/", StringComparison.Ordinal),
                    "Original stele prefab is not bound to its restored native asset.");
                foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    Require(AssetDatabase.LoadMainAssetAtPath(dependency) != null,
                        "Original stele dependency failed to import: " + dependency);
                var transforms = prefab.GetComponentsInChildren<Transform>(true);
                nodes += transforms.Length;
                foreach (var transform in transforms)
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                        "Original stele prefab has a missing script.");
                var sequence = prefab.GetComponent<MyPlayableSequence>();
                Require(sequence != null && sequence.myPlayables.Count == 1 && sequence.myPlayables[0].myTime == 0f,
                    "Original stele sequence differs.");
                var attachments = prefab.GetComponentsInChildren<MyPlayableAttachToBone>(true);
                Require(attachments.Length == 2 && attachments.Contains(sequence.myPlayables[0].myPlayable),
                    "Original stele sequence child binding differs.");
                foreach (var attachment in attachments)
                {
                    Require(attachment.myDuration == (finished ? 1.85f : -1f)
                        && attachment.myAttach.myBoneName == "CommonEffectSlot"
                        && attachment.GetComponent<ApplyEnableOnPlay>() != null,
                        "Original stele duration, effect slot or activation binding differs.");
                }
                particles += prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
                behaviours += prefab.GetComponentsInChildren<MonoBehaviour>(true).Length;
                var particleRenderers = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
                renderers += particleRenderers.Length;
                foreach (var renderer in particleRenderers)
                {
                    Require((renderer.lightmapIndex & 0xffff) == 0xffff && (renderer.realtimeLightmapIndex & 0xffff) == 0xffff
                        && renderer.lightmapScaleOffset == new Vector4(1, 1, 0, 0)
                        && renderer.realtimeLightmapScaleOffset == new Vector4(1, 1, 0, 0),
                        "Original unbaked renderer state differs after native import.");
                    foreach (var material in renderer.sharedMaterials)
                        Require(material != null && material.shader != null && material.shader.isSupported
                            && material.shader == AssetDatabase.LoadAssetAtPath<Shader>(
                                "Assets/Game/Runtime/Official/World/NativeWater/Shaders/EnvironmentNoise.shader"),
                            "Original stele Windows material shader is unavailable.");
                }
            }
        }

        private static void ValidateStateBranches(OfficialStelePlayableCatalog.Entry entry)
        {
            var state = new StateSource();
            var played = new List<GameObject>();
            var view = new ECPeriodicityInteractionStateView(entry, null, state, (prefab, source, target) =>
            {
                played.Add(prefab);
                return null;
            });
            view.Load();
            Require(played.SequenceEqual(new[] { entry.mCanInteractionPlayable }), "Original initial loop branch differs.");
            state.Notify(111, 1);
            Require(played.Count == 1, "Unrelated periodicity group restarted the original stele.");
            state.Count = 1;
            state.Notify(110, 1);
            Require(played.Last() == entry.mEndInteractionPlayable && played.Count == 2,
                "Original completion transition did not select the end performance.");
            state.Notify(110, 1);
            Require(played.Count == 2, "Repeated completion replayed the original end performance.");
            view.Unload();
            state.Count = 0;
            state.Notify(110, 0);
            Require(played.Count == 2, "Unloaded original view remained subscribed.");
            played.Clear();
            state.Count = 1;
            view = new ECPeriodicityInteractionStateView(entry, null, state, (prefab, source, target) =>
            {
                played.Add(prefab);
                return null;
            });
            view.Load();
            Require(played.Count == 0, "Cold loading a completed stele replayed an original performance.");
            view.Unload();
        }

        private sealed class StateSource : IPeriodicityInteractionStateSource
        {
            public int Count;
            public int GetCurrentPeriodicityGroupId() => 110;
            public int GetWorldPeriodicityInteractionCount(int groupId) => Count;
            public int? GetPeriodicityMaxCount(int groupId) => 1;
            public event Action<int, int> OnPeriodicityInteractionChanged;
            public void Notify(int groupId, int count) => OnPeriodicityInteractionChanged?.Invoke(groupId, count);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
