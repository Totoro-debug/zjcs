using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Original damage-prefab loading, notifications and FIFO pool lifecycle.</summary>
    public sealed class OfficialBattleDamageEntityPlayback : IDisposable
    {
        private readonly OfficialBattleNativeEntityLoader loader;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Dictionary<EcEntityIdentifier, IFightPlayableOverrideStatusServices> statusContexts =
            new Dictionary<EcEntityIdentifier, IFightPlayableOverrideStatusServices>();

        public OfficialBattleDamageEntityPlayback(Transform parent, MyContainer container,
            IFightDamageViewServices services, OfflineBattleNativePlayableScope scope)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            loader = new OfficialBattleNativeEntityLoader(parent, container, scope,
                OfficialBattleNativeEntityKind.Damage, instance =>
                {
                    var damage = instance.GetComponent<MyFightDamage>();
                    if (!damage || instance.GetComponents<MyFightDamage>().Length != 1)
                        throw new InvalidOperationException("Original damage prefab requires one damage view.");
                    damage.ViewServices = services;
                    damage.mMyPlayableManager = scope;
                }, entity =>
                {
                    foreach (var view in entity.GetComponents<PlayableOverrideByStatusStack>())
                    {
                        if (!statusContexts.TryGetValue(entity.Id, out var context))
                            throw new InvalidOperationException("Original damage override requires its actual source-status context.");
                        view.StatusServices = context;
                    }
                });
        }
        public int Count => loader.Count;
        public bool ContainsClass(int classId) => loader.ContainsClass(classId);
        public MyEntity GetEntity(EcEntityIdentifier id) => loader.GetEntity(id);
        public MyEntity Create(ECViewEntityData data, IFightPlayableOverrideStatusServices statusContext = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (loader.GetEntity(data.Id) != null)
                throw new InvalidOperationException("Native damage identity is already live.");
            if (statusContext != null) statusContexts.Add(data.Id, statusContext);
            try
            {
                var entity = loader.Create(data);
                scope.Dispatch(entity.MyStart);
                return entity;
            }
            catch
            {
                loader.Remove(data.Id);
                statusContexts.Remove(data.Id);
                throw;
            }
        }
        public void Remove(EcEntityIdentifier id)
        {
            loader.Remove(id);
            statusContexts.Remove(id);
        }
        public void Dispose()
        {
            loader.Dispose();
            statusContexts.Clear();
        }
    }
}
