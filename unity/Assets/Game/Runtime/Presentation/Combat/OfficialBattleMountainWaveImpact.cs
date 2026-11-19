using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleTrackPlayables;
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

    // The original sword lands on the centre; its expanding ground particles reach the outer cells.
    internal sealed class OfficialBattleFallingWindImpact : MonoBehaviour
    {
        private Transform sword;
        private Animator animator;
        private ParticleSystem swordParticles, centreParticles, outerParticles;
        private ParticleSystem.Particle[] particles;
        private float landingY, landingTime, outerParticleAge, outerSize;
        private Action<int> arrival;
        private bool sawSwordAbove, sentCentre, sentOuter, hasOuterParticle;
        private uint outerSeed;

        internal static OfficialBattleFallingWindImpact Attach(GameObject prefab, MyPlayable playable,
            FightBattleHitRoot root, FightBattleHitEntity hit, Action<int> arrival)
        {
            if (!prefab || !playable || !playable.myApplyComponent || root == null || hit?.ScopeEntries == null
                || root.EntityId != 10591 || hit.ScopeEntries.Count != 5 || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effects = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect") { effectStart = entry.myTime; effects++; }
            if (effects != 1 || effectStart < 0f) return null;
            var offsets = new System.Collections.Generic.HashSet<FightGridPoint>();
            float outerDelay = -1f; int centres = 0;
            foreach (var scope in hit.ScopeEntries)
            {
                if (scope == null || !offsets.Add(scope.Position)) return null;
                long distance = Math.Abs((long)scope.Position.X) + Math.Abs((long)scope.Position.Y);
                if (distance == 0 && scope.Delay == 0f) centres++;
                else if (distance == 1 && scope.Delay > 0f && (outerDelay < 0f || Math.Abs(scope.Delay - outerDelay) < .00001f))
                    outerDelay = scope.Delay;
                else return null;
            }
            if (centres != 1 || outerDelay <= 0f) return null;
            var effect = playable.myApplyComponent.transform;
            var animator = effect.GetComponent<Animator>();
            var sword = effect.Find("jian");
            if (!animator || !animator.runtimeAnimatorController || !sword) return null;
            AnimationClip clip = null;
            foreach (var candidate in animator.runtimeAnimatorController.animationClips)
                if (candidate && candidate.name == "Eff_Skill_Warrior_Sword") { if (clip) return null; clip = candidate; }
            float landingTime = root.Delay - effectStart;
            if (!clip || landingTime <= 0f || landingTime > clip.length + .00001f) return null;
            float landingY, beforeY;
            var sample = new GameObject("Falling sword animation sample");
            try
            {
                var sampleSword = new GameObject("jian").transform; sampleSword.SetParent(sample.transform, false);
                clip.SampleAnimation(sample, Mathf.Max(0f, landingTime - 1f / 60f)); beforeY = sampleSword.localPosition.y;
                clip.SampleAnimation(sample, Mathf.Min(clip.length, landingTime)); landingY = sampleSword.localPosition.y;
            }
            finally { Destroy(sample); }
            if (!(beforeY > landingY + .001f)) return null;
            ParticleSystem swordParticles = null, centreParticles = null, outerParticles = null;
            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.name == "jian") { if (swordParticles) return null; swordParticles = particle; }
                else if (particle.name == "baodian") { if (centreParticles) return null; centreParticles = particle; }
                else if (particle.name == "tuci") { if (outerParticles) return null; outerParticles = particle; }
            }
            if (!swordParticles || !centreParticles || !outerParticles) return null;
            var main = outerParticles.main; var size = outerParticles.sizeOverLifetime;
            if (main.startDelay.mode != ParticleSystemCurveMode.Constant || main.useUnscaledTime
                || Math.Abs(main.simulationSpeed - 1f) > .00001f || !size.enabled || size.separateAxes
                || size.size.mode != ParticleSystemCurveMode.Curve || main.maxParticles <= 0) return null;
            float outerParticleAge = landingTime + outerDelay - main.startDelay.constant;
            if (outerParticleAge <= 0f) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleFallingWindImpact>();
            observer.sword = sword; observer.animator = animator; observer.swordParticles = swordParticles; observer.centreParticles = centreParticles;
            observer.outerParticles = outerParticles; observer.particles = new ParticleSystem.Particle[main.maxParticles];
            observer.landingY = landingY; observer.landingTime = landingTime;
            observer.outerParticleAge = outerParticleAge; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !sword || !animator || !swordParticles || !centreParticles || !outerParticles) return;
            if (swordParticles.particleCount > 0 && sword.localPosition.y > landingY + .001f) sawSwordAbove = true;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool landedAnimation = state.IsName("Eff_Skill_Warrior_Sword") && state.length > 0f
                && state.normalizedTime * state.length >= landingTime;
            if (!sentCentre && (sawSwordAbove || landedAnimation) && swordParticles.particleCount > 0 && centreParticles.particleCount > 0
                && sword.localPosition.y <= landingY + .001f)
            { sentCentre = true; arrival(0); }
            if (arrival == null || !sentCentre || sentOuter || outerParticles.particleCount == 0) return;
            int count = outerParticles.GetParticles(particles);
            for (int index = 0; index < count; index++)
            {
                var particle = particles[index];
                if (!hasOuterParticle)
                {
                    if (particle.startLifetime <= outerParticleAge) continue;
                    var curve = outerParticles.sizeOverLifetime.size;
                    float normalized = outerParticleAge / particle.startLifetime, previous = curve.Evaluate(0f);
                    for (int sample = 1; sample <= 16; sample++)
                    {
                        float value = curve.Evaluate(normalized * sample / 16f);
                        if (value + .00001f < previous) return;
                        previous = value;
                    }
                    outerSize = particle.startSize * previous;
                    if (outerSize <= 0f) return;
                    outerSeed = particle.randomSeed; hasOuterParticle = true;
                }
                if (particle.randomSeed != outerSeed) continue;
                if (particle.GetCurrentSize(outerParticles) < outerSize) return;
                sentOuter = true; arrival(1); return;
            }
        }

        internal void Detach() { arrival = null; }
    }

    // The passive's original target effect emits this ring at its configured hit boundary.
    internal sealed class OfficialBattleSwordWindImpact : MonoBehaviour
    {
        private ParticleSystem ring;
        private float hitDelay;
        private Action<float> arrival;

        internal static OfficialBattleSwordWindImpact Attach(GameObject prefab, MyPlayable playable, Action<float> arrival)
        {
            if (!prefab || !playable || !playable.myApplyComponent || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effects = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect") { effectStart = entry.myTime; effects++; }
            if (effects != 1 || effectStart < 0f) return null;
            ParticleSystem ring = null;
            foreach (var particle in playable.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == "yuanquan (1)") { if (ring) return null; ring = particle; }
            if (!ring) return null;
            var main = ring.main;
            if (main.startDelay.mode != ParticleSystemCurveMode.Constant || main.useUnscaledTime
                || Math.Abs(main.simulationSpeed - 1f) > .00001f || main.startDelay.constant < 0f) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleSwordWindImpact>();
            observer.ring = ring; observer.hitDelay = effectStart + main.startDelay.constant; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !ring || ring.particleCount <= 0) return;
            var callback = arrival; arrival = null; callback(hitDelay);
        }

        internal void Detach() { arrival = null; }
    }

    // The ice passive's target effect starts at the original child hit's zero-time boundary.
    internal sealed class OfficialBattleIceHitImpact : MonoBehaviour
    {
        private ParticleSystem ice;
        private float hitDelay;
        private float emissionStart;
        private Action<float> arrival;

        internal static OfficialBattleIceHitImpact Attach(GameObject prefab, MyPlayable playable, Action<float> arrival)
        {
            if (!prefab || !playable || !playable.myApplyComponent || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effects = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect") { effectStart = entry.myTime; effects++; }
            if (effects != 1 || effectStart < 0f) return null;
            ParticleSystem ice = null;
            foreach (var particle in playable.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == "hit_da") { if (ice) return null; ice = particle; }
            if (!ice) return null;
            var main = ice.main;
            if (main.startDelay.mode != ParticleSystemCurveMode.Constant || main.useUnscaledTime
                || Math.Abs(main.simulationSpeed - 1f) > .00001f || main.startDelay.constant < 0f) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleIceHitImpact>();
            observer.ice = ice; observer.emissionStart = main.startDelay.constant;
            observer.hitDelay = effectStart + observer.emissionStart; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !ice || ice.particleCount <= 0 && (ice.isPaused || ice.time <= emissionStart)) return;
            var callback = arrival; arrival = null; callback(hitDelay);
        }

        internal void Detach() { arrival = null; Destroy(this); }
    }

    // The original track calls its trigger after moving the projectile to its endpoint.
    internal sealed class OfficialBattleWaterProjectileImpact : MyPlayableTrigger
    {
        private MyPlayableTrack track;
        private MyPlayableTriggerTimeCfg trigger;
        private Action arrival;

        internal static OfficialBattleWaterProjectileImpact Attach(MyPlayable playable, float delayDistFactor, Action arrival)
        {
            if (!(playable is MyPlayableTrack track) || !track.myApplyComponent || !track.myTarget
                || track.myFixDuration || track.myTrackEndStopTime != 0f || track.myTrackTriggerList == null
                || track.myTrackTriggerList.Count != 0 || arrival == null) return null;
            track.CalcInitialDurationSpd(out _, out float speed);
            if (speed <= 0f || float.IsNaN(delayDistFactor) || float.IsInfinity(delayDistFactor)
                || Math.Abs(1f / speed - delayDistFactor) > .00001f) return null;
            var observer = track.gameObject.AddComponent<OfficialBattleWaterProjectileImpact>();
            observer.track = track; observer.arrival = arrival;
            observer.trigger = new MyPlayableTriggerTimeCfg { myTime = 1f, myTrigger = observer };
            track.myTrackTriggerList.Add(observer.trigger);
            return observer;
        }

        public override void Play(MyPlayable playable)
        {
            if (arrival == null || !track || !ReferenceEquals(playable, track)) return;
            float duration = track.GetDuration();
            if (duration <= 0f || track.Time < duration || !track.myApplyComponent || !track.myTarget
                || Vector3.Distance(track.myApplyComponent.transform.position, track.myTarget.transform.position) > .01f) return;
            var callback = arrival; arrival = null; callback();
        }

        internal void Detach()
        {
            arrival = null;
            if (track && track.myTrackTriggerList != null) track.myTrackTriggerList.Remove(trigger);
            track = null; trigger = null;
            Destroy(this);
        }
    }
}
