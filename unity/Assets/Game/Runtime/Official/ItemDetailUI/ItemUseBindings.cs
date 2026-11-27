using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public interface IItemDetailDialog
    {
        void Init(int itemId, long requireCount = 0);
        void Init(IProfessionItemData data, long requireCount = 0);
    }
    public interface IItemUseDescriptionBindings
    {
        void SetFunctionDescription(WorldMultiLanguageText text, ItemDetailDefinition definition, long count);
    }
    public readonly struct ItemUseResultEntry
    {
        public readonly int ItemId, FromId;
        public readonly long Count;
        public readonly double Probability;
        public ItemUseResultEntry(int itemId, long count, int fromId, double probability = 0)
        { ItemId = itemId; Count = count; FromId = fromId; Probability = probability; }
    }
    public sealed class ItemUseResultData
    {
        public bool IsRandom, IsEqualProbability, ShowProbability;
        public int OptionCount;
        public IReadOnlyList<ItemUseResultEntry> Entries;
    }
    public interface IItemUseResultBindings
    {
        ItemUseResultData LoadUseResult(int itemId);
    }
    public interface IItemUseAward { bool IsOpenChest { get; } }
    public interface IItemUseContentServices
    {
        IProfessionItemPlayer Player { get; }
        long ItemBatchUseMinCount { get; }
        long ItemBatchUseMaxCount { get; }
        bool CanUseInBatches(int itemId);
        bool CanOverlap(int itemId);
        bool IsCanUse(int itemId, long count);
        string UseButtonText(int itemId);
        void UseItem(int itemId, long count, IWindowOperator owner, Action<long> success);
        void ItemNotEnough(int itemId);
        void CloseParent(GameObject window);
        IDisposable RegisterAwardHandler(Func<IItemUseAward, bool> handler);
        void MergeAndShowAward(IItemUseAward award, IWindowOperator owner);
    }
    public readonly struct ItemChooseFlags
    {
        public readonly bool ProfessionMismatch, FunctionDisabled, Got, IsTreasure;
        public ItemChooseFlags(bool professionMismatch, bool functionDisabled, bool got, bool isTreasure = false)
        { ProfessionMismatch = professionMismatch; FunctionDisabled = functionDisabled; Got = got; IsTreasure = isTreasure; }
    }
    public interface IItemChooseDialogBindings
    {
        IProfessionItemIconContext Items { get; }
        void SortOptions(List<int> ids);
        GameObject InstantiateUi(GameObject prefab, Transform parent);
        void SetConfirmGray(GameObject button, bool gray);
        ItemChooseFlags ChoiceFlags(int itemId);
        void ApplyTreasureOwnership(ItemIcon icon, int itemId, bool showGained);
        void Close(GameObject window);
    }
}
