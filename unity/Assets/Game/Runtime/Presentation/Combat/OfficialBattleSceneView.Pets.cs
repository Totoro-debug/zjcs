using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private OfficialBattlePetFollowerCatalog petFollowerCatalog;
        private readonly Dictionary<int, OfficialBattlePetFollowerView> petFollowers = new Dictionary<int, OfficialBattlePetFollowerView>();

        private void RefreshPetFollowers(FightBattleCheckpoint checkpoint)
        {
            if (nativePlayableScope == null) return;
            foreach (var owner in checkpoint.Units)
            {
                if (owner?.PetEnergy == null || owner.PetEnergy.PetClassId <= 0) continue;
                Transform ownerRoot = Body(owner.Id);
                if (!ownerRoot) continue;
                bool transferred = false;
                foreach (var entity in checkpoint.Units)
                    if (entity != null && entity.MasterId == owner.Id && entity.CharacterType == 7
                        && entity.MonsterId == owner.PetEnergy.PetEntityClassId && !entity.Ended && entity.Hp > 0)
                    { transferred = true; break; }
                if (!petFollowers.TryGetValue(owner.Id, out var view))
                {
                    petFollowerCatalog ??= OfficialBattlePetFollowerCatalog.Load();
                    var binding = petFollowerCatalog.Find(owner.PetEnergy.PetClassId);
                    if (binding == null) throw new System.InvalidOperationException("Original follower is unbound for pet " + owner.PetEnergy.PetClassId);
                    Transform facing = attachedModels.TryGetValue(owner.Id, out var attached) ? attached.Model : ownerRoot;
                    view = new OfficialBattlePetFollowerView(binding, owner, ownerRoot, facing, root, gridToWorld, nativePlayableScope,
                        initialTransfer: transferred);
                    petFollowers.Add(owner.Id, view);
                }
                view.Refresh(owner, transferred);
            }
        }

        public Transform TryGetPetTransform(int ownerId) => petFollowers.TryGetValue(ownerId, out var pet) ? pet.Transform : null;
        private void TickPetFollowers(float deltaTime)
        { foreach (var pet in petFollowers.Values) pet.Tick(deltaTime); }
        private void ReleasePetFollowers()
        { foreach (var pet in petFollowers.Values) pet.Dispose(); petFollowers.Clear(); }
    }
}
