using System;
using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Generation-only methods from ECFightRoleCharacterView. Exact entity roots
    // and initialization flags replace MyEntity / fight-root global lookups.
    public sealed class OfficialBattleCharacterGenerationPlayback : IDisposable
    {
        public sealed class Binding
        {
            public GameObject Generate;
            public float DelayTime;
            public bool NeedsDelay;
            public GameObject[] DelayVisibilityParts;
        }
        private readonly GameObject source;
        private readonly IMyPlayableManager manager;
        private readonly Binding binding;
        private MyPlayable generation;
        private float visibilityDelay;
        private bool delayed, disposed;

        public OfficialBattleCharacterGenerationPlayback(GameObject source, IMyPlayableManager manager,
            Binding binding, bool isNew, bool hasDeathPlayable = false, bool fastforward = false, bool suppressGenerate = false)
        {
            this.source = source ? source : throw new ArgumentNullException(nameof(source));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.binding = binding ?? throw new ArgumentNullException(nameof(binding));
            SetVisibilityPartsActive(true);
            if (!isNew) return;
            if (!hasDeathPlayable && !fastforward && !suppressGenerate && binding.Generate && !binding.NeedsDelay)
                PlayGenerate();
            if (binding.DelayTime > 0)
            {
                SetVisibilityPartsActive(false);
                visibilityDelay = binding.DelayTime;
                delayed = true;
            }
        }
        public void Tick(float deltaTime)
        {
            if (disposed || !delayed) return;
            visibilityDelay -= deltaTime;
            if (visibilityDelay > 0) return;
            delayed = false;
            SetVisibilityPartsActive(true);
            // Original delayed generation has no death/fast-forward/tag gate.
            if (binding.Generate && binding.NeedsDelay) PlayGenerate();
        }
        private void PlayGenerate() => generation = manager.Play(binding.Generate, source, source);
        private void SetVisibilityPartsActive(bool active)
        {
            if (binding.DelayVisibilityParts == null) return;
            foreach (GameObject part in binding.DelayVisibilityParts)
                if (part) part.SetActive(active);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (generation) { generation.Stop(); generation = null; }
            delayed = false;
        }
    }
}
