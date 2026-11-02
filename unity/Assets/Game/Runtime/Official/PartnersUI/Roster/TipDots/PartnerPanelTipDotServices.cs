using System;
using System.Collections.Generic;
using Zjcs.Official.Partners;

namespace Zjcs.Official.PartnersUI
{
    // Dependency boundary for the original NpcPanel's furniture/dialogue/pet graph closure.
    // Implementations must supply the real save, parsers and condition subscriptions.
    public interface IPartnerPanelTipDotNode { IPartnerPanelTipDotServices Services { get; set; } }
    public interface IPartnerPanelTipDotServices
    {
        IPartnerPanelTipDotPlayer Player { get; }
        PartnerPanelFurnitureInfo GetFurniture(int id);
        IEnumerable<PartnerPanelNpcInfo> MapExploreNpcs();
        PartnerPanelPetInfo GetPet(int id);
        Dictionary<int, PartnerPanelPetAbilityInfo> GetPetAbilities(int id);
        object PetAbilityPropInfoParser { get; }
        PartnerPanelPetEvolutionInfo GetPetEvolution(int id, PetEvolutionPhase phase);
        IReadOnlyList<(int, long)> SortedFoods { get; }
        bool TryCalcLevelUpExpRequire(PartnerPanelPetRecord pet, int levels, out long required);
        PartnerPanelCondition GetPetPotentialCondition();
        void RegisterCondition(PartnerPanelCondition condition, Action changed);
        void DeregisterCondition(PartnerPanelCondition condition, Action changed);
        string ItemName(int id);
    }
    public interface IPartnerPanelTipDotPlayer
    {
        bool HasFurniture(int id);
        bool HasItems(object items);
        bool HasAnyNpcDialogueRedDot();
        PartnerPanelPetManager PetManager { get; }
        bool PetCanEvolution(long id);
        bool PetCanUpgradePotentialLevel(long id);
        long GetItemAmount(int id);
        event Action<int> OnRepairFurniture;
        event Action<object> OnNpcDialogueAdd;
        event Action<object> OnNpcDialogueUpdate;
        event Action<object> OnNpcDialogueRemove;
        event Action<PartnerPanelPetRecord> OnPetDataUpdate;
        event Action<PartnerPanelPetRecord> OnGetPet;
        event Action<PartnerTipDotItem, long, object> OnItemCountUpdate;
    }
    public abstract class PartnerPanelPetManager
    {
        public abstract PartnerPanelPetRecord this[long id] { get; }
        public abstract bool TryGetValue(long id, out PartnerPanelPetRecord pet);
        public abstract IEnumerable<PartnerPanelPetRecord> GetAllPet();
    }
    public sealed class PartnerPanelCondition
    {
        private readonly Func<IPartnerPanelTipDotPlayer, bool> check;
        public PartnerPanelCondition(Func<IPartnerPanelTipDotPlayer, bool> check)
        { this.check = check ?? throw new ArgumentNullException(nameof(check)); }
        public bool IsMet(IPartnerPanelTipDotPlayer player) => check(player);
    }
    public sealed class PartnerPanelFurnitureInfo
    { public PartnerPanelCondition UnlockCondition; public object RepairCost; }
    public sealed class PartnerPanelNpcInfo { public int Id; public NpcRoleType NpcRoleType; }
    public sealed class PartnerPanelPetRecord { public long Index; public int ClassId; }
    public sealed class PartnerPanelPetInfo { public int AbilityId; public PartnerPanelPetItemInfo ItemInfo; }
    public sealed class PartnerPanelPetItemInfo { public string Name; }
    public sealed class PartnerPanelPetAbilityInfo { public int PageCode; }
    public sealed class PartnerPanelPetEvolutionInfo { public List<int> PromotableSkills; }
}
