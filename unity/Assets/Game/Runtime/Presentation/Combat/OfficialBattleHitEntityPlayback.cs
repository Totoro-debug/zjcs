using System;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Original hit views loaded through the shared original entity loader.</summary>
    public sealed class OfficialBattleHitEntityPlayback : IDisposable
    {
        private readonly OfficialBattleNativeEntityLoader loader;
        private readonly OfflineBattleNativePlayableScope scope;

        public OfficialBattleHitEntityPlayback(Transform parent, MyContainer container,
            IFightHitViewServices services, OfflineBattleNativePlayableScope scope)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            loader = new OfficialBattleNativeEntityLoader(parent, container, scope, false, instance =>
            {
                int count = 0;
                foreach (var component in instance.GetComponents<MonoBehaviour>())
                    if (component is IFightHitViewBinding view)
                    { view.ViewServices = services; view.mMyPlayableManager = scope; count++; }
                if (count != 1) throw new InvalidOperationException("Native hit prefab requires one original hit view.");
            });
        }
        public int Count => loader.Count;
        public bool ContainsClass(int classId) => loader.ContainsClass(classId);
        public MyEntity GetEntity(EcEntityIdentifier id) => loader.GetEntity(id);
        public MyEntity Create(ECViewEntityData data)
        {
            var entity = loader.Create(data);
            scope.Dispatch(entity.MyStart);
            return entity;
        }
        public void Remove(EcEntityIdentifier id) => loader.Remove(id);
        public void Dispose() => loader.Dispose();
    }
}
