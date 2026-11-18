using System;
using UnityEngine;
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

    // The official 10600 skill owns its visible slash; its 10601 hit has no playable.
    internal sealed class OfficialBattleSlashImpact : MonoBehaviour
    {
        private ParticleSystem slash;
        private Action arrival;

        internal static OfficialBattleSlashImpact Attach(MyPlayable playable, Action arrival)
        {
            if (!playable || arrival == null) return null;
            ParticleSystem slash = null;
            foreach (var particle in playable.GetComponentsInChildren<ParticleSystem>(true))
                if (particle.name == "daoguang da (23)")
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
}
