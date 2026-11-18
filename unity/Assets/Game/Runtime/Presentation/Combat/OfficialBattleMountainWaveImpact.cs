using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Observes the original effect's two visible particle bursts, not a separate timer.
    internal sealed class OfficialBattleMountainWaveImpact : MonoBehaviour
    {
        private ParticleSystem first, second;
        private Action<int> arrival;
        private bool sentFirst, sentSecond;

        internal static OfficialBattleMountainWaveImpact Attach(MyPlayable playable, Action<int> arrival)
        {
            if (!playable || arrival == null) return null;
            ParticleSystem first = null, second = null;
            foreach (var particle in playable.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.name == "diyichon") { if (first) return null; first = particle; }
                else if (particle.name == "dierchong") { if (second) return null; second = particle; }
            }
            if (!first || !second) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleMountainWaveImpact>();
            observer.first = first; observer.second = second; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null) return;
            if (!sentFirst && first && first.particleCount > 0) { sentFirst = true; arrival(1); }
            if (arrival != null && !sentSecond && second && second.particleCount > 0) { sentSecond = true; arrival(2); }
        }

        internal void Detach() { arrival = null; }
    }

    // These official skill effects own the visible impact; their hit entities have no playable.
    internal sealed class OfficialBattleSlashImpact : MonoBehaviour
    {
        private ParticleSystem slash;
        private Action arrival;

        internal static OfficialBattleSlashImpact Attach(MyPlayable playable, string particleName, Action arrival)
        {
            if (!playable || string.IsNullOrEmpty(particleName) || arrival == null) return null;
            ParticleSystem slash = null;
            foreach (var particle in playable.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == particleName)
                {
                    if (slash) return null;
                    slash = particle;
                }
            if (!slash) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleSlashImpact>();
            observer.slash = slash; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !slash || slash.particleCount <= 0) return;
            var callback = arrival; arrival = null; callback();
        }

        internal void Detach() { arrival = null; }
    }

    // Maps the original scope timings onto the original moving blade's animation
    // curve, then waits for that actual animated position rather than a timer.
    internal sealed class OfficialBattleWindBladeImpact : MonoBehaviour
    {
        private Animator blade;
        private ParticleSystem visibleBlade;
        private float[] positions;
        private int next;
        private Action<int> arrival;

        internal static OfficialBattleWindBladeImpact Attach(GameObject prefab, MyPlayable playable,
            FightBattleHitEntity hit, Action<int> arrival)
        {
            if (!prefab || !playable || hit == null || hit.ScopeEntries == null || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effectCount = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect (2)")
                { effectStart = entry.myTime; effectCount++; }
            if (effectCount != 1 || effectStart < 0f || hit.ScopeEntries.Count != 4) return null;
            Animator blade = null; ParticleSystem visible = null;
            foreach (var value in playable.GetComponentsInChildren<Animator>(true))
                if (value.name == "ani") { if (blade) return null; blade = value; }
            foreach (var value in playable.GetComponentsInChildren<ParticleSystem>(true))
                if (value.name == "fengren liang") { if (visible) return null; visible = value; }
            if (!blade || !visible || !blade.runtimeAnimatorController) return null;
            AnimationClip clip = null;
            foreach (var value in blade.runtimeAnimatorController.animationClips)
                if (value && value.name == "Eff_Skill_Windknife") { if (clip) return null; clip = value; }
            if (!clip) return null;
            var positions = new float[hit.ScopeEntries.Count];
            var sample = new GameObject("Wind blade animation sample");
            try
            {
                for (int index = 0; index < positions.Length; index++)
                {
                    var entry = hit.ScopeEntries[index];
                    if (entry == null || entry.Position.X != 0 || entry.Position.Y != index + 1) return null;
                    float time = hit.Delay + entry.Delay - effectStart;
                    if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f || time > clip.length) return null;
                    clip.SampleAnimation(sample, time);
                    positions[index] = sample.transform.localPosition.y;
                    if (index > 0 && positions[index] <= positions[index - 1]) return null;
                }
            }
            finally { Destroy(sample); }
            var observer = playable.gameObject.AddComponent<OfficialBattleWindBladeImpact>();
            observer.blade = blade; observer.visibleBlade = visible; observer.positions = positions; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !blade || !visibleBlade || visibleBlade.particleCount <= 0) return;
            float position = blade.transform.localPosition.y;
            while (arrival != null && next < positions.Length && position >= positions[next]) arrival(next++);
        }

        internal void Detach() { arrival = null; }
    }
}
