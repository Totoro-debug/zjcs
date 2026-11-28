using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using S=Zjcs.Official.TreasureSlots;

namespace Zjcs.Official.TreasurePromoteUI
{
    public interface ITreasurePromoteServices
    {
        ITreasureIconServices Icons { get; }
        ITreasurePropertyTables Tables { get; }
        ITreasurePropertyPlayer Player { get; }
        TreasurePropertyQueries Properties { get; }
        TreasurePropertyCalculator Calculator { get; }
        IProfessionImageResourceLoader Images { get; }
        IProfessionMaterialServices Materials { get; }
        IProfessionUpgradeBindings UseItemBindings { get; }
        int SubRank { get; }
        int SeasonId { get; }
        int MaxBaseLevel { get; }
        int MaxBlessLevel { get; }
        int MoneyId { get; }
        int CommonPieceClassId { get; }
        bool AutoMergeWhenLevelUp { get; }
        bool AwakeAutoMergeEnable { get; }
        bool UpgradeUseCommonMaterailEnable { get; }
        event Action<GameObject> OnWindowClosing;
        event Action<IProfessionItemData> OnTreasureAdd;
        int GetItemType(int itemId);
        int GetTreasureIdByPieceId(int pieceId);
        int CanMergeTreasure(int itemId);
        Dictionary<int,long> GetMergeMaterials(int itemId);
        string QualityBackground(Quality quality);
        bool LimitCommonMaterial(int itemId);
        bool CanUseCommonMaterial(int itemId);
        long GetItemAmount(int itemId);
        int GetTreasureFinalLevel(int itemId);
        int GetTreasureBaseLevel(int itemId);
        int GetTreasureInSlotIndex(int itemId);
        bool IsTreasureMaxLevel(int itemId);
        int GetHitCount(TreasureInfo info, int level);
        bool HasBless(int finalLevel);
        Dictionary<int,long> GetLevelUpMaterials(int nextLevel);
        S.TreasureLevelInfo GetLevelInfo(int level);
        S.TreasureBlessInfo GetBlessInfo(int blessLevel);
        bool IsItemSourceFromMerge(int itemId);
        long CalcCanMergeAmount(int itemId, bool recursion);
        (PlayerUseItemContent.UseItemState,string) GetLevelUseItemState(IProfessionItemData item,bool autoMerge);
        int CanSlotLevelUp(S.ElementType element, int index, bool autoMerge);
        void LevelUp(IProfessionItemData item, bool autoMerge, Action completed);
        int GetUpgradeMaterials(int itemId, bool hasEmptyItem, bool useCommonItem, out Dictionary<int,long> materials);
        int CanUpgrade(int itemId, Dictionary<int,long> materials, bool autoMerge);
        void Upgrade(int itemId, bool useCommonItem, bool autoMerge, Action completed);
        IEnumerable<(IProfessionItemData It,long Count)> GetCommonTreasurePiece(IProfessionItemData item);
        long GetStarUpCommonPiece(int itemId,int currentStar);
        long GetUpgradePieceCount(int pieceClassId);
        bool PieceCanMerge(int pieceClassId);
        void SetAutoMergeWhenLevelUp(bool value);
        void SetAwakeAutoMerge(bool value);
        void SetUseCommonMaterial(bool value,Action completed);
        void PopupAwakeMaterials(Action<TreasureAwakeMaterialPanel> initialize);
        ProfessionUseItemSliderGroup InstantiateMaterialRow(GameObject prefab, Transform parent);
        void SystemMessage(string message);
        void ShowError(int code);
    }
}
