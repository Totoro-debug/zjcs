using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.MapDisplays;
using Zjcs.Offline.World.NativeMap14EntityAssets;

namespace Zjcs.Offline.World.NativeMap14HeroStatues
{
    // The original manager owns parenting/activation. Prepare the Windows Sprite
    // bindings and required dependencies while the clone is still inactive.
    public sealed class NativeMap14DisplayInstantiation : IMapDisplayInstantiation
    {
        private readonly Transform staging;
        private readonly IServiceProvider services;
        private readonly IClientVisibilitySupporterFactory visibility;
        private readonly NativeMap14EntitySpriteLibrary sprites;
        private readonly Action<MonoBehaviour> inject;
        public NativeMap14DisplayInstantiation(Transform staging, IServiceProvider services,
            IClientVisibilitySupporterFactory visibility, NativeMap14EntitySpriteLibrary sprites, Action<MonoBehaviour> inject)
        {
            this.staging = staging ? staging : throw new ArgumentNullException(nameof(staging));
            if (staging.gameObject.activeInHierarchy || staging.position != Vector3.zero || staging.rotation != Quaternion.identity || staging.lossyScale != Vector3.one)
                throw new InvalidOperationException("Original map-display injection requires an inactive staging root at the identity transform.");
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            this.sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
            this.inject = inject ?? throw new ArgumentNullException(nameof(inject));
        }
        public GameObject Instantiate(GameObject prefab)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, staging, false);
            try
            {
                foreach (var binding in instance.GetComponentsInChildren<NativeMap14HeroStatueSprites>(true)) binding.Bind(sprites);
                foreach (var driver in instance.GetComponentsInChildren<ECViewDriver>(true))
                { driver.ServiceProvider = services; driver.ClientVisibilitySupporterFactory = visibility; }
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true)) inject(component);
                return instance;
            }
            catch { UnityEngine.Object.Destroy(instance); throw; }
        }
    }
}
