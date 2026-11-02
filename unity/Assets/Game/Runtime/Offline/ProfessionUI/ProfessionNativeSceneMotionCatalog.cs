using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Original Unity clips carry the motion name and graph clock. The native
    // skeleton consumer supplies the actual bone/slot/constraint channels.
    public sealed class ProfessionNativeSceneMotionCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string Sex;
            public AnimationClip Clip;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private Dictionary<string, Dictionary<string, AnimationClip>> motions;
        public IReadOnlyList<Entry> Entries => entries;

        public AnimationClip Load(string sex, string nativeMotion)
        {
            if (sex == null) throw new ArgumentNullException(nameof(sex));
            if (nativeMotion == null) throw new ArgumentNullException(nameof(nativeMotion));
            if (motions == null)
            {
                var next = new Dictionary<string, Dictionary<string, AnimationClip>>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Sex) || !entry.Clip)
                        throw new InvalidOperationException("Original scene motion catalog contains an unbound clip.");
                    if (!next.TryGetValue(entry.Sex, out var names))
                        next.Add(entry.Sex, names = new Dictionary<string, AnimationClip>(StringComparer.Ordinal));
                    if (names.ContainsKey(entry.Clip.name))
                        throw new InvalidOperationException("Original scene motion identity is ambiguous: " + entry.Sex + "/" + entry.Clip.name);
                    names.Add(entry.Clip.name, entry.Clip);
                }
                motions = next;
            }
            return motions.TryGetValue(sex, out var clips) && clips.TryGetValue(nativeMotion, out var clip) ? clip : null;
        }
    }
}
