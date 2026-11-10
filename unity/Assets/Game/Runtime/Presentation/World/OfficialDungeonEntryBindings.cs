using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public static class OfficialDungeonEntryBindings
    {
        [Serializable] private sealed class MatchingPanelsDocument { public MatchingPanelPrefab[] prefabs; }
        [Serializable] private sealed class MatchingPanelPrefab
        { public string name, layoutJson; public MatchingPanelContent[] contents; public MatchingPanelSound[] sounds; public SafeAreaBinding[] safeAreas; public MatchingPanelContent towerButton; }
        [Serializable] private sealed class SafeAreaBinding
        { public string path; public string[] objects; public bool enabled; public float PaddingTop, PaddingBottom; public bool DisableTopAdapt, DisableBottomAdapt; }
        [Serializable] private sealed class MatchingPanelContent { public string path; public string[] objects; }
        [Serializable] private sealed class MatchingPanelSound
        { public string path; public string[] objects; public bool enabled; public MatchingPanelEvent[] events; }
        [Serializable] private sealed class MatchingPanelEvent { public string name, resource; public uint id; }
        private static MatchingPanelsDocument matchingPanels;

        // GameMatchMiniPanel.OnEnable -> RefreshUI -> HideContent when no match exists.
        // Local dungeon entry pages have no active game-match task at initialization.
        public static void Apply(GameObject root, string prefabName,
            Action<AudioClip, GameObject> postSound)
        {
            if (matchingPanels == null)
            {
                TextAsset data = Resources.Load<TextAsset>("Dungeons/matching-panel-bindings");
                if (data == null) throw new InvalidDataException("Original dungeon matching panel bindings are missing.");
                matchingPanels = JsonUtility.FromJson<MatchingPanelsDocument>(data.text);
            }
            MatchingPanelPrefab binding = matchingPanels.prefabs?.SingleOrDefault(value => value.name == prefabName);
            if (binding == null) return;
            foreach (SafeAreaBinding area in binding.safeAreas)
            {
                GameObject node = Find(root, area.path, area.objects).gameObject;
                WorldSafeArea safe = node.GetComponent<WorldSafeArea>() ?? node.AddComponent<WorldSafeArea>();
                safe.enabled = false;
                safe.PaddingTop = area.PaddingTop; safe.PaddingBottom = area.PaddingBottom;
                safe.DisableTopAdapt = area.DisableTopAdapt; safe.DisableBottomAdapt = area.DisableBottomAdapt;
                safe.enabled = area.enabled;
            }
            OfficialDungeonUiBindingRuntime.RestoreEntryLayouts(root, binding.layoutJson);
            foreach (MatchingPanelSound sound in binding.sounds)
            {
                Transform node = Find(root, sound.path, sound.objects);
                var receiver = node.GetComponent<WorldAnimationSoundPlayer>()
                    ?? node.gameObject.AddComponent<WorldAnimationSoundPlayer>();
                receiver.mEvents = new List<AudioClip>();
                foreach (MatchingPanelEvent entry in sound.events)
                {
                    AudioClip clip = Resources.Load<AudioClip>(entry.resource);
                    if (clip == null || clip.name != entry.name)
                        throw new InvalidDataException("Original dungeon animation sound is missing: " + entry.resource);
                    receiver.mEvents.Add(clip);
                }
                receiver.Post = postSound;
                receiver.enabled = sound.enabled;
            }
            foreach (MatchingPanelContent content in binding.contents)
                Find(root, content.path, content.objects).gameObject.SetActive(false);
        }

        public static void ApplyLayouts(GameObject root, string serialized)
            => OfficialDungeonUiBindingRuntime.RestoreEntryLayouts(root, serialized);

        private static Transform Find(GameObject root, string path, string[] objects)
        {
            var names = (string[])objects.Clone();
            names[0] = root.name;
            return OfficialDungeonUiBindingRuntime.Find(root.transform, path, names);
        }

        public static UnityEngine.UI.Button TowerEntryButton(GameObject root)
        {
            MatchingPanelContent binding = matchingPanels.prefabs.Single(value => value.name == "TravelNotesPanel").towerButton;
            if (binding == null) throw new InvalidDataException("Original OldPlayEntryBtn.mEntryBtn binding is missing.");
            return Find(root, binding.path, binding.objects).GetComponent<UnityEngine.UI.Button>()
                ?? throw new InvalidDataException("Original tower entry Button is missing.");
        }

        public static void RefreshTowerTravelEntry(GameObject root, bool unlocked, bool preview,
            IReadOnlyList<KeyValuePair<string, int>> levels)
        {
            MatchingPanelPrefab binding = matchingPanels.prefabs.Single(value => value.name == "TravelNotesPanel");
            OfficialDungeonUiBindingRuntime.RefreshTowerTravelEntry(root, binding.layoutJson, unlocked, preview, levels);
        }

        public static void RefreshCatalogueDetail(GameObject root, bool unlocked, bool canEnter)
        {
            MatchingPanelPrefab binding = matchingPanels.prefabs.Single(value => value.name == "DailyDungeonDialog");
            OfficialDungeonUiBindingRuntime.RefreshCatalogueDetail(root, binding.layoutJson, unlocked, canEnter);
        }
    }
}
