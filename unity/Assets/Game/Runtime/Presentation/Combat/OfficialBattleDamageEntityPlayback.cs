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
        private readonly Func<int, OfficialBattleNativeEntityLoader> createLoader;
        private readonly Dictionary<int, OfficialBattleNativeEntityLoader> scopedLoaders =
            new Dictionary<int, OfficialBattleNativeEntityLoader>();
        private readonly Dictionary<EcEntityIdentifier, OfficialBattleNativeEntityLoader> entityLoaders =
            new Dictionary<EcEntityIdentifier, OfficialBattleNativeEntityLoader>();
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Dictionary<EcEntityIdentifier, IFightPlayableOverrideStatusServices> statusContexts =
            new Dictionary<EcEntityIdentifier, IFightPlayableOverrideStatusServices>();

        public OfficialBattleDamageEntityPlayback(Transform parent, MyContainer container,
            IFightSkillViewServices services, OfflineBattleNativePlayableScope scope)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            createLoader = skillItemId => new OfficialBattleNativeEntityLoader(parent, container, scope,
                OfficialBattleNativeEntityKind.Damage, instance =>
                {
                    var damage = instance.GetComponent<MyFightDamage>();
                    if (!damage || instance.GetComponents<MyFightDamage>().Length != 1)
                        throw new InvalidOperationException("Original damage prefab requires one damage view.");
                    damage.ViewServices = services;
                    damage.mMyPlayableManager = scope;
                    OfficialBattleCurrentViewBinding.Prepare(instance, services);
                }, entity =>
                {
                    foreach (var view in entity.GetComponents<PlayableOverrideByStatusStack>())
                    {
                        if (!statusContexts.TryGetValue(entity.Id, out var context))
                            throw new InvalidOperationException("Original damage override requires its actual source-status context.");
                        view.StatusServices = context;
                    }
                }, skillItemId);
            loader = createLoader(0);
        }
        public int Count => entityLoaders.Count;
        public bool ContainsClass(int classId) => loader.ContainsClass(classId);
        public MyEntity GetEntity(EcEntityIdentifier id) =>
            entityLoaders.TryGetValue(id, out var owner) ? owner.GetEntity(id) : null;
        public MyEntity Create(ECViewEntityData data, IFightPlayableOverrideStatusServices statusContext = null,
            int skillItemId = 0)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (entityLoaders.ContainsKey(data.Id))
                throw new InvalidOperationException("Native damage identity is already live.");
            var selected = loader;
            if (loader.SupportsSkillScope(skillItemId)
                && !scopedLoaders.TryGetValue(skillItemId, out selected))
                scopedLoaders.Add(skillItemId, selected = createLoader(skillItemId));
            if (statusContext != null) statusContexts.Add(data.Id, statusContext);
            try
            {
                var entity = selected.Create(data);
                entityLoaders.Add(data.Id, selected);
                scope.Dispatch(entity.MyStart);
                return entity;
            }
            catch
            {
                selected.Remove(data.Id);
                entityLoaders.Remove(data.Id);
                statusContexts.Remove(data.Id);
                throw;
            }
        }
        public void Remove(EcEntityIdentifier id)
        {
            if (entityLoaders.TryGetValue(id, out var owner))
            {
                owner.Remove(id);
                entityLoaders.Remove(id);
            }
            statusContexts.Remove(id);
        }
        public void Dispose()
        {
            loader.Dispose();
            foreach (var scoped in scopedLoaders.Values) scoped.Dispose();
            scopedLoaders.Clear();
            entityLoaders.Clear();
            statusContexts.Clear();
        }
    }
}
