using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    internal static class OfficialBattleMonsterAnimatorBindings
    {
        [Serializable] private sealed class Catalog { public string manifest; public Model[] models; }
        [Serializable] internal sealed class State { public string state, animation, clip; }
        [Serializable] private sealed class Model { public int entityId; public State[] states; }
        private static Dictionary<int, State[]> models;

        internal static State[] Find(int entityId, BattleNativeMonsterAssets assets = null)
        {
            string scoped = assets?.PresentationResource("animator-bindings");
            if (scoped != null)
                return Read(scoped, assets.SourceManifest).TryGetValue(entityId, out var selected) ? selected
                    : throw new InvalidOperationException("Original skill actor Animator states are unbound: " + entityId);
            if (models == null)
                models = Read("Combat/monster-animator-bindings", null);
            return models.TryGetValue(entityId, out var states) ? states
                : throw new InvalidOperationException("Original monster Animator states are unbound: " + entityId);
        }

        private static Dictionary<int, State[]> Read(string resource, string expectedManifest)
        {
            var asset = Resources.Load<TextAsset>(resource);
            var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
            if (catalog?.models == null || (expectedManifest != null && catalog.manifest != expectedManifest))
                throw new InvalidOperationException("Original monster Animator state bindings are missing: " + resource);
            return catalog.models.ToDictionary(value => value.entityId, value => value.states);
        }

        internal static State UniqueState(State[] states, string animation)
        {
            if (string.IsNullOrEmpty(animation)) return null;
            State result = null;
            foreach (var state in states)
                if (state.animation == animation)
                {
                    if (result != null) return null;
                    result = state;
                }
            return result;
        }

        internal static AnimationClip ResolveMotion(BattleNativeMonsterAssets assets, State[] states, AnimationClip marker)
        {
            if (!marker) return null;
            AnimationClip result = null;
            foreach (var state in states)
                if (state.animation == marker.name && !string.IsNullOrEmpty(state.clip))
                {
                    var original = assets.Resolve<AnimationClip>(state.clip);
                    if (result && result != original)
                        throw new InvalidOperationException("Original monster skill motion is ambiguous: " + marker.name);
                    if (Mathf.Abs(original.length - marker.length) > .002f)
                        throw new InvalidOperationException("Original monster skill duration differs: " + marker.name);
                    result = original;
                }
            return result;
        }
    }
}
