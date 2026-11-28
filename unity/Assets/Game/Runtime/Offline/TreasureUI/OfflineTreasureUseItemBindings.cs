using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasurePromoteUI;
using Zjcs.Official.TreasureUI;

namespace Zjcs.Offline.TreasureUI
{
    // Reuse the common use-item controller while observing the actual treasure player's inventory events.
    public sealed class OfflineTreasureUseItemBindings:IProfessionUpgradeBindings
    {
        private readonly IProfessionUpgradeBindings other;
        private readonly IProfessionMaterialServices materials;
        private readonly Func<ITreasurePromoteServices> services;
        private readonly ITreasureIconServices icons;
        public IProfessionUpgradePlayer MainPlayer { get; }
        public OfflineTreasureUseItemBindings(IProfessionUpgradeBindings other,IProfessionMaterialServices materials,
            ITreasureIconServices icons,Func<ITreasurePromoteServices> services)
        {this.other=other;this.materials=materials;this.icons=icons;this.services=services;MainPlayer=new Player(other.MainPlayer,icons,services);}
        public bool ManagerAvailable=>icons.ManagerAvailable;
        public SafeAction OnPlayerUpdate=>icons.PlayerUpdated;
        public IProfessionTipDotSystem TipDotSystem=>icons.TipDotSystem;
        public ProfessionDetailDefinition GetProfession(int id)=>other.GetProfession(id);
        public int GetLevelPropId(int id)=>other.GetLevelPropId(id);
        public bool TryGetLevelInfo(int id,int level,out ProfessionUpgradeCostDefinition info)=>other.TryGetLevelInfo(id,level,out info);
        public IReadOnlyDictionary<int,long> GetProps(int id,int level)=>other.GetProps(id,level);
        public Dictionary<int,long> GetProfessionLevelPropDiff(int id,int from,int to)=>other.GetProfessionLevelPropDiff(id,from,to);
        public string GetPropNameText(int type)=>other.GetPropNameText(type);
        public string FormatPropValue(int type,long value)=>other.FormatPropValue(type,value);
        public void InitPropItem(ProfessionPlayerPropItem row,int type,long value,long next)=>other.InitPropItem(row,type,value,next);
        public int GetItemType(IProfessionItemInfo item)=>other.GetItemType(item);
        public long CalcAwardBadge(ProfessionDetailDefinition profession,IProfessionUpgradePlayer player)=>other.CalcAwardBadge(profession,player);
        public ItemIcon GetIcon(int id,Transform parent)=>materials.GetIcon(id,parent);
        public void RecycleIcon(ItemIcon icon)=>materials.RecycleIcon(icon);
        public void Upgrade(int profession,Action completed)=>other.Upgrade(profession,completed);
        public int MoneyId=>other.MoneyId;
        public void ShowLackOfItem(int id,long count)=>other.ShowLackOfItem(id,count);
        public bool IsItemSourceFromMerge(IProfessionUpgradePlayer player,int id)=>services().IsItemSourceFromMerge(id);
        public long CalcCanMergeAmount(IProfessionUpgradePlayer player,int id,bool recursion)=>services().CalcCanMergeAmount(id,recursion);
        private sealed class Player:IProfessionUpgradePlayer
        {
            private readonly IProfessionUpgradePlayer other;private readonly ITreasureIconServices icons;private readonly Func<ITreasurePromoteServices> services;
            public Player(IProfessionUpgradePlayer other,ITreasureIconServices icons,Func<ITreasurePromoteServices> services){this.other=other;this.icons=icons;this.services=services;}
            public int Profession=>other.Profession;public bool IsProfessionFunctionLimit=>other.IsProfessionFunctionLimit;
            public bool IsProfessionUnlocked(int id)=>other.IsProfessionUnlocked(id);
            public int GetProfessionLevel(int id)=>other.GetProfessionLevel(id);
            public long GetItemAmount(int id)=>services().GetItemAmount(id);
            public event Action<IProfessionItemData,long,object> OnItemCountUpdate {add=>icons.OnItemCountUpdate+=value;remove=>icons.OnItemCountUpdate-=value;}
            public event Action<int,int> OnProfessionUpgrade {add=>other.OnProfessionUpgrade+=value;remove=>other.OnProfessionUpgrade-=value;}
            public event Action<int,int> OnSkillRankRewardUpdate {add=>other.OnSkillRankRewardUpdate+=value;remove=>other.OnSkillRankRewardUpdate-=value;}
            public event Action<IProfessionItemData,int,int> OnSkillRankUp {add=>other.OnSkillRankUp+=value;remove=>other.OnSkillRankUp-=value;}
            public event Action<Dictionary<int,int>,Dictionary<int,int>> OnSkillRankUpBatch {add=>other.OnSkillRankUpBatch+=value;remove=>other.OnSkillRankUpBatch-=value;}
        }
    }
}
