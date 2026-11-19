using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Observe the original continuous flame; each authored hit wave follows a
    // real emitted particle through the three native scope cells.
    internal sealed class OfficialBattleFlameSprayImpact : MonoBehaviour
    {
        private ParticleSystem fire;
        private ParticleSystem.Particle[] particles;
        private Vector3 origin, direction;
        private float cellSize;
        private float[] phases;
        private readonly bool[] chosen = new bool[3];
        private readonly uint[] seeds = new uint[3];
        private readonly int[] reached = new int[3];
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private Mesh mesh;
        private Action<int, int> arrival;

        internal static OfficialBattleFlameSprayImpact Attach(GameObject prefab, MyPlayable playable,
            FightBattleHitRoot[] roots, FightBattleHitEntity hit, Vector3 gridOrigin, Vector3 gridStep,
            Action<int, int> onArrival)
        {
            if (!prefab || !playable || !playable.mySource || !playable.myApplyComponent
                || roots == null || roots.Length != 3 || hit?.ScopeEntries == null || hit.ScopeEntries.Count != 3
                || float.IsNaN(gridStep.sqrMagnitude) || float.IsInfinity(gridStep.sqrMagnitude)
                || gridStep.sqrMagnitude <= .000001f || onArrival == null) return null;
            var sequence = prefab.GetComponent<MyPlayableSequence>();
            if (!sequence || sequence.myPlayables == null) return null;
            float effectStart = -1; int count = 0;
            foreach (var entry in sequence.myPlayables)
                if (entry?.myPlayable && entry.myPlayable.name == "effect (2)") { effectStart = entry.myTime; count++; }
            if (count != 1 || float.IsNaN(effectStart) || float.IsInfinity(effectStart) || effectStart < 0f) return null;
            var phases = new float[3];
            for (int index = 0; index < 3; index++)
            {
                var scope = hit.ScopeEntries[index]; var root = roots[index];
                if (scope == null || scope.Position.X != 0 || scope.Position.Y != index + 1
                    || float.IsNaN(scope.Delay) || float.IsInfinity(scope.Delay) || Math.Abs(scope.Delay - index * .1f) > .00001f
                    || root == null || root.EntityId != 11691 || float.IsNaN(root.Delay) || float.IsInfinity(root.Delay)
                    || Math.Abs(root.Delay - (.34f + index * .5f)) > .00001f) return null;
                phases[index] = root.Delay - effectStart;
                if (phases[index] < 0 || index > 0 && phases[index] <= phases[index - 1]) return null;
            }
            ParticleSystem fire = null;
            foreach (var particle in playable.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == "firean") { if (fire) return null; fire = particle; }
            if (!fire || fire.main.simulationSpace != ParticleSystemSimulationSpace.World || fire.main.useUnscaledTime
                || fire.main.loop || fire.main.maxParticles <= 0 || phases[2] > fire.main.duration + .0001f
                || !fire.GetComponent<ParticleSystemRenderer>()) return null;
            var observer = playable.gameObject.AddComponent<OfficialBattleFlameSprayImpact>();
            observer.fire = fire; observer.origin = gridOrigin; observer.direction = gridStep.normalized;
            observer.cellSize = gridStep.magnitude; observer.phases = phases; observer.arrival = onArrival;
            observer.particles = new ParticleSystem.Particle[fire.main.maxParticles];
            return observer;
        }

        private static float Cross(Vector3 a, Vector3 b, Vector3 p)
            => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private bool CoversCell(int scope)
        {
            Camera camera = Camera.main;
            if (!camera && Camera.allCamerasCount == 1) camera = Camera.allCameras[0];
            var renderer = fire.GetComponent<ParticleSystemRenderer>();
            if (!camera || !renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            if (!mesh) mesh = new Mesh();
            renderer.BakeMesh(mesh, camera, true);
            vertices.Clear(); mesh.GetVertices(vertices);
            if (vertices.Count == 0) return false;
            triangles.Clear(); mesh.GetTriangles(triangles, 0);
            Vector3 point = camera.WorldToScreenPoint(origin + direction * (cellSize * (scope + 1)));
            if (point.z <= 0) return false;
            for (int index = 0; index + 2 < triangles.Count; index += 3)
            {
                Vector3 a = camera.WorldToScreenPoint(vertices[triangles[index]]);
                Vector3 b = camera.WorldToScreenPoint(vertices[triangles[index + 1]]);
                Vector3 c = camera.WorldToScreenPoint(vertices[triangles[index + 2]]);
                if (a.z <= 0 || b.z <= 0 || c.z <= 0 || Mathf.Abs(Cross(a, b, c)) < .00001f) continue;
                float ab = Cross(a, b, point), bc = Cross(b, c, point), ca = Cross(c, a, point);
                if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            if (!fire || arrival == null || fire.isPaused || Time.timeScale == 0f) return;
            int count = fire.GetParticles(particles);
            for (int wave = 0; wave < 3; wave++)
            {
                if (!chosen[wave] && fire.time >= phases[wave] && count > 0)
                {
                    float best = float.PositiveInfinity;
                    for (int index = 0; index < count; index++)
                    {
                        var particle = particles[index];
                        float birth = fire.time - (particle.startLifetime - particle.remainingLifetime);
                        float distance = Mathf.Abs(birth - phases[wave]);
                        if (distance < best) { best = distance; seeds[wave] = particle.randomSeed; }
                    }
                    chosen[wave] = true;
                }
                if (!chosen[wave]) continue;
                for (int index = 0; index < count; index++)
                {
                    var particle = particles[index];
                    if (particle.randomSeed != seeds[wave]) continue;
                    float extent = Vector3.Dot(particle.position - origin, direction) + particle.GetCurrentSize(fire) / 2;
                    while (arrival != null && reached[wave] < 3
                        && extent >= cellSize * (reached[wave] + 1) && CoversCell(reached[wave]))
                        arrival(wave, reached[wave]++);
                    break;
                }
            }
        }

        internal void Detach() { arrival = null; if (mesh) Destroy(mesh); mesh = null; Destroy(this); }
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
