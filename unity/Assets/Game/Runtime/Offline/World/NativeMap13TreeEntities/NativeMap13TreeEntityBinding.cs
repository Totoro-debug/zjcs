using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Offline.World.NativeMap13EntityViews;
using Zjcs.Offline.World.NativeMap13Skeletons;

namespace Zjcs.Offline.World.NativeMap13TreeEntities
{
    // Concrete SerializeReference identities for the original shared classes.
    // Every field and lifecycle method is inherited from the official ports.
    [Serializable] public sealed class TreeWorldStageView : ECWorldStageView { }
    [Serializable] public sealed class TreeWorldStageConfigProvider : ECWorldStageCfgNormalProvider { }
    [Serializable] public sealed class TreeWorldStagePlayableHandler : ECPlayableWorldStageHandler { }
    [Serializable] public sealed class TreePlayableSupporterView : ECPlayableSupporterView { }

    public sealed class NativeMap13TreeEntityBinding : MonoBehaviour
    {
        [Serializable] public sealed class OriginalObject
        {
            public string Key;
            public UnityEngine.Object Value;
        }
        public string SourceKey;
        public ECViewDriver Driver;
        public NativeMap13EntityModelBinding Model;
        public NativeMap13EntitySpriteBinding[] Sprites;
        public NativeMap13SlicedSpriteBinding[] SlicedSprites;
        public OriginalObject[] OriginalObjects;
        private bool prepared;

        private void Awake()
        {
            if (!prepared) throw new InvalidOperationException("Prepare the original complete tree before activation.");
        }

        public void Prepare(NativeMap13AnimatorLibrary animations, NativeMap13EntitySpriteLibrary sprites,
            IReadOnlyDictionary<string, RuntimeAnimatorController> additionalControllers,
            IServiceProvider services, IClientVisibilitySupporterFactory visibilityFactory)
        {
            if (prepared || gameObject.activeInHierarchy)
                throw new InvalidOperationException("Prepare the original complete tree once while inactive.");
            if (!Driver || !Model || services == null || visibilityFactory == null)
                throw new InvalidOperationException("Original tree resource/entity services are incomplete.");
            var originals = OriginalObjects.ToDictionary(value => value.Key, value => value.Value);
            var targets = Model.ExternalFadeTargets.ToDictionary(value => value.GameObjectKey,
                value => (GameObject)originals[value.GameObjectKey]);
            Model.Prepare(animations, sprites, targets, additionalControllers);
            foreach (var sprite in Sprites) sprite.Bind(sprites);
            foreach (var slice in SlicedSprites) slice.Bind(sprites);
            Driver.ServiceProvider = services;
            Driver.ClientVisibilitySupporterFactory = visibilityFactory;
            prepared = true;
        }

        public void InitializeActive()
        {
            if (!prepared || !gameObject.activeInHierarchy)
                throw new InvalidOperationException("Initialize the prepared original complete tree after activation.");
            Model.InitializeActive();
        }
    }
}
