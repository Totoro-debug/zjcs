using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
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

    // The ice cone's original animation travels through five independently timed scopes.
    internal sealed class OfficialBattleIceConeImpact : MonoBehaviour
    {
        private Animator cone;
        private ParticleSystem visibleCone;
        private ParticleSystemRenderer coneRenderer;
        private Transform source;
        private Mesh renderedCone;
        private readonly System.Collections.Generic.List<Vector3> renderedVertices = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector3> previousVertices = new System.Collections.Generic.List<Vector3>();
        private int scopeCount, next;
        private Action<int> arrival;

        internal static OfficialBattleIceConeImpact Attach(GameObject prefab, MyPlayable playable,
            FightBattleHitEntity hit, Action<int> arrival)
        {
            if (!prefab || !playable || !playable.mySource || hit?.ScopeEntries == null
                || hit.ScopeEntries.Count != 5 || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effects = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect (3)") { effectStart = entry.myTime; effects++; }
            if (effects != 1 || effectStart < 0f) return null;
            Animator cone = null; ParticleSystem visible = null;
            foreach (var value in playable.GetComponentsInChildren<Animator>(true))
                if (value.name == "ani") { if (cone) return null; cone = value; }
            foreach (var value in playable.GetComponentsInChildren<ParticleSystem>(true))
                if (value.name == "da") { if (visible) return null; visible = value; }
            var renderer = visible ? visible.GetComponent<ParticleSystemRenderer>() : null;
            if (!cone || !cone.transform.parent || !renderer || !cone.runtimeAnimatorController) return null;
            AnimationClip clip = null;
            foreach (var value in cone.runtimeAnimatorController.animationClips)
                if (value && value.name == "Fx_Ani_Ice") { if (clip) return null; clip = value; }
            if (!clip) return null;
            var sample = new GameObject("Ice cone animation sample");
            try
            {
                float previous = float.NegativeInfinity;
                for (int index = 0; index < hit.ScopeEntries.Count; index++)
                {
                    var entry = hit.ScopeEntries[index];
                    if (entry == null || entry.Position.X != 0 || entry.Position.Y != index + 1) return null;
                    float time = hit.Delay + entry.Delay - effectStart;
                    if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f || time > clip.length) return null;
                    clip.SampleAnimation(sample, time);
                    float position = sample.transform.localPosition.y;
                    if (position <= previous) return null;
                    previous = position;
                }
            }
            finally { Destroy(sample); }
            var observer = playable.gameObject.AddComponent<OfficialBattleIceConeImpact>();
            observer.cone = cone; observer.visibleCone = visible; observer.scopeCount = hit.ScopeEntries.Count; observer.arrival = arrival;
            observer.source = playable.mySource.transform; observer.coneRenderer = renderer;
            observer.renderedCone = new Mesh { name = "Original ice cone impact geometry" };
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !cone || !visibleCone || visibleCone.particleCount <= 0) return;
            RouteVisibleScopes();
        }

        private void RouteVisibleScopes()
        {
            if (!source || !source.parent || !coneRenderer || !renderedCone) return;
            Camera camera = Camera.main;
            if (!camera && Camera.allCamerasCount == 1) camera = Camera.allCameras[0];
            if (!camera) return;
            Vector3 forward = cone.transform.parent.TransformVector(Vector3.up);
            float cellSize = source.parent.TransformVector(Vector3.right).magnitude;
            if (forward.sqrMagnitude <= .000001f || cellSize <= 0f) return;
            coneRenderer.BakeMesh(renderedCone, camera, true);
            renderedVertices.Clear(); renderedCone.GetVertices(renderedVertices);
            if (renderedVertices.Count == 0) return;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            void Include(Vector3 world)
            {
                Vector3 screen = camera.WorldToScreenPoint(world);
                minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
            }
            foreach (var world in previousVertices) Include(world);
            previousVertices.Clear();
            foreach (var vertex in renderedVertices)
            {
                Vector3 world = coneRenderer.transform.position + vertex;
                previousVertices.Add(world); Include(world);
            }
            while (arrival != null && next < scopeCount)
            {
                Vector3 target = camera.WorldToScreenPoint(source.position + forward.normalized * (cellSize * (next + 1)));
                if (target.z <= 0f || target.x < minX || target.x > maxX || target.y < minY || target.y > maxY) return;
                arrival(next++);
            }
        }

        internal void Detach() { arrival = null; if (renderedCone) Destroy(renderedCone); renderedCone = null; }
        private void OnDestroy() { if (renderedCone) Destroy(renderedCone); }
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

    // Observe the expanding original fire ring at the positions of its configured scopes.
    internal sealed class OfficialBattleFireRingImpact : MonoBehaviour
    {
        private ParticleSystem ring;
        private ParticleSystem.Particle[] particles;
        private float[] ages, sizes;
        private uint seed;
        private bool hasParticle;
        private int next;
        private Action<int> arrival;

        internal static OfficialBattleFireRingImpact Attach(GameObject prefab, MyPlayable playable,
            FightBattleHitRoot root, FightBattleHitEntity hit, Action<int> arrival)
        {
            if (!prefab || !playable || !playable.myApplyComponent || root == null || root.EntityId != 10831
                || hit?.ScopeEntries == null || hit.ScopeEntries.Count != 13 || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effects = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect") { effectStart = entry.myTime; effects++; }
            if (effects != 1 || effectStart < 0f) return null;
            var offsets = new System.Collections.Generic.HashSet<FightGridPoint>();
            var delays = new[] { -1f, -1f, -1f }; var counts = new int[3];
            foreach (var scope in hit.ScopeEntries)
            {
                if (scope == null || !offsets.Add(scope.Position)) return null;
                long distance = Math.Abs((long)scope.Position.X) + Math.Abs((long)scope.Position.Y);
                if (distance > 2 || float.IsNaN(scope.Delay) || float.IsInfinity(scope.Delay)) return null;
                int index = (int)distance;
                if (delays[index] >= 0f && Math.Abs(delays[index] - scope.Delay) > .00001f) return null;
                delays[index] = scope.Delay; counts[index]++;
            }
            if (counts[0] != 1 || counts[1] != 4 || counts[2] != 8) return null;
            ParticleSystem ring = null;
            foreach (var particle in playable.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == "difeng (3)") { if (ring) return null; ring = particle; }
            if (!ring) return null;
            var main = ring.main; var size = ring.sizeOverLifetime;
            if (main.startDelay.mode != ParticleSystemCurveMode.Constant || main.useUnscaledTime
                || Math.Abs(main.simulationSpeed - 1f) > .00001f || !size.enabled || size.separateAxes
                || size.size.mode != ParticleSystemCurveMode.Curve || main.maxParticles <= 0) return null;
            var ages = new float[3];
            for (int index = 0; index < ages.Length; index++)
            {
                ages[index] = root.Delay + delays[index] - effectStart - main.startDelay.constant;
                if (float.IsNaN(ages[index]) || float.IsInfinity(ages[index]) || ages[index] < -.00001f
                    || index > 0 && ages[index] <= ages[index - 1]) return null;
                ages[index] = Mathf.Max(0f, ages[index]);
            }
            var observer = playable.gameObject.AddComponent<OfficialBattleFireRingImpact>();
            observer.ring = ring; observer.particles = new ParticleSystem.Particle[main.maxParticles];
            observer.ages = ages; observer.sizes = new float[ages.Length]; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !ring || ring.particleCount <= 0) return;
            int count = ring.GetParticles(particles);
            for (int index = 0; index < count; index++)
            {
                var particle = particles[index];
                if (!hasParticle)
                {
                    if (particle.startLifetime <= ages[ages.Length - 1]) continue;
                    var curve = ring.sizeOverLifetime.size;
                    float previous = curve.Evaluate(0f);
                    for (int sample = 1; sample <= 32; sample++)
                    {
                        float value = curve.Evaluate(ages[ages.Length - 1] / particle.startLifetime * sample / 32f);
                        if (value + .00001f < previous) return;
                        previous = value;
                    }
                    for (int phase = 0; phase < ages.Length; phase++)
                        sizes[phase] = particle.startSize * curve.Evaluate(ages[phase] / particle.startLifetime);
                    if (sizes[1] <= sizes[0] || sizes[2] <= sizes[1]) return;
                    seed = particle.randomSeed; hasParticle = true;
                }
                if (particle.randomSeed != seed) continue;
                float currentSize = particle.GetCurrentSize(ring);
                while (arrival != null && next < sizes.Length && currentSize + .00001f >= sizes[next]) arrival(next++);
                return;
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

    // Observe an original target particle at the child hit's configured boundary.
    internal sealed class OfficialBattleTargetParticleImpact : MonoBehaviour
    {
        private ParticleSystem targetParticle;
        private float hitDelay;
        private float emissionStart;
        private Action<float> arrival;

        internal static OfficialBattleTargetParticleImpact Attach(GameObject prefab, MyPlayable playable, string particleName, Action<float> arrival)
            => AttachNamed(prefab, playable, "effect", particleName, arrival);

        internal static OfficialBattleTargetParticleImpact AttachWaterDragon(GameObject prefab, MyPlayable playable,
            int wave, float configuredHitDelay, Action<float> arrival)
        {
            if (wave < 0 || wave >= 4 || !(playable is MyPlayableFollowBone follow) || follow.myAttach == null
                || follow.myAttach.AttachTo != EAttachTo.Skill || follow.myAttach.myBoneName != "body"
                || Vector3.Distance(follow.myAttach.myOffset, new Vector3(0f, wave + 1, 0f)) > .00001f)
                return null;
            string leaf = "effect (" + (wave + 2) + ")";
            if (playable.name != leaf || !prefab) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            int count = 0; float time = 0f;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == leaf) { count++; time = entry.myTime; }
            if (count != 1 || Math.Abs(time + .1f - configuredHitDelay) > .00001f) return null;
            return AttachNamed(prefab, playable, leaf, "hit (3)", arrival);
        }

        private static OfficialBattleTargetParticleImpact AttachNamed(GameObject prefab, MyPlayable playable,
            string leafName, string particleName, Action<float> arrival)
        {
            if (!prefab || !playable || !playable.myApplyComponent || string.IsNullOrEmpty(particleName) || arrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1f; int effects = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == leafName) { effectStart = entry.myTime; effects++; }
            if (effects != 1 || effectStart < 0f) return null;
            ParticleSystem targetParticle = null;
            foreach (var particle in playable.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == particleName) { if (targetParticle) return null; targetParticle = particle; }
            if (!targetParticle) return null;
            var main = targetParticle.main;
            if (main.startDelay.mode != ParticleSystemCurveMode.Constant || main.useUnscaledTime
                || Math.Abs(main.simulationSpeed - 1f) > .00001f || main.startDelay.constant < 0f) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleTargetParticleImpact>();
            observer.targetParticle = targetParticle; observer.emissionStart = main.startDelay.constant;
            observer.hitDelay = effectStart + observer.emissionStart; observer.arrival = arrival;
            return observer;
        }

        private void LateUpdate()
        {
            if (arrival == null || !targetParticle || targetParticle.particleCount <= 0 && (targetParticle.isPaused || targetParticle.time <= emissionStart)) return;
            var callback = arrival; arrival = null; callback(hitDelay);
        }

        internal void Detach() { arrival = null; Destroy(this); }
    }

    // The original track calls its trigger after moving the projectile to its endpoint.
    internal sealed class OfficialBattleProjectileImpact : MyPlayableTrigger
    {
        private MyPlayableTrack track;
        private MyPlayableTriggerTimeCfg trigger;
        private Action arrival;
        private bool finishBeforeArrival, reached;

        internal static OfficialBattleProjectileImpact Attach(MyPlayable playable, float delayDistFactor, Action arrival,
            bool finishBeforeArrival = false)
        {
            if (!(playable is MyPlayableTrack track) || !track.myApplyComponent || !track.myTarget
                || track.myFixDuration || track.myTrackEndStopTime != 0f || track.myTrackTriggerList == null
                || track.myTrackTriggerList.Count != 0 || arrival == null) return null;
            track.CalcInitialDurationSpd(out _, out float speed);
            if (speed <= 0f || float.IsNaN(delayDistFactor) || float.IsInfinity(delayDistFactor)
                || Math.Abs(1f / speed - delayDistFactor) > .00001f) return null;
            var observer = track.gameObject.AddComponent<OfficialBattleProjectileImpact>();
            observer.track = track; observer.arrival = arrival; observer.finishBeforeArrival = finishBeforeArrival;
            observer.trigger = new MyPlayableTriggerTimeCfg { myTime = 1f, myTrigger = observer };
            track.myTrackTriggerList.Add(observer.trigger);
            if (finishBeforeArrival) track.OnStop += new Action<MyPlayable>(observer.OnTrackStopped);
            return observer;
        }

        public override void Play(MyPlayable playable)
        {
            if (arrival == null || !track || !ReferenceEquals(playable, track)) return;
            float duration = track.GetDuration();
            if (duration <= 0f || track.Time < duration || !track.myApplyComponent || !track.myTarget
                || Vector3.Distance(track.myApplyComponent.transform.position, track.myTarget.transform.position) > .01f) return;
            if (finishBeforeArrival) { reached = true; return; }
            Deliver();
        }

        private void OnTrackStopped(MyPlayable playable)
        {
            if (reached && ReferenceEquals(playable, track)) Deliver();
        }

        private void Deliver()
        {
            if (arrival == null || !track) return;
            var callback = arrival; arrival = null;
            if (finishBeforeArrival)
            {
                // Chained projectiles can reuse the same pooled track in this step.
                if (track.myTrackTriggerList != null) track.myTrackTriggerList.Remove(trigger);
                trigger = null;
                track.OnStop -= new Action<MyPlayable>(OnTrackStopped);
            }
            callback();
        }

        internal void Detach()
        {
            arrival = null;
            if (track && finishBeforeArrival) track.OnStop -= new Action<MyPlayable>(OnTrackStopped);
            if (track && track.myTrackTriggerList != null) track.myTrackTriggerList.Remove(trigger);
            track = null; trigger = null;
            Destroy(this);
        }
    }
}
