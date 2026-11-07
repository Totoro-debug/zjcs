using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ItemDetailUI
{
    // Values projected from ItemInfo/LanguageUtils and the selected item-source
    // agents. Keeping these inputs together preserves the original UI branches.
    public sealed class ItemDetailDefinition
    {
        public IProfessionItemInfo Info;
        public string NameKey, TypeKey, DescriptionKey, FunctionDescriptionKey;
        public object[] DescriptionArguments = Array.Empty<object>();
        public object[] FunctionDescriptionArguments = Array.Empty<object>();
        public bool ShowPlayerItemCount, ShowUseDetail, IsCookingMaterial;
        public bool IsCountLimit;
        public long MaxCount;
        public bool HasOutputAdditionGroup;
    }

    public sealed class ItemDetailSource
    {
        public ItemSourceType Type;
        public bool Hidden, Limited;
        public string DescriptionKey;
        public object[] DescriptionArguments = Array.Empty<object>();
        public Action<IWindowOperator, long> Navigate;
        public Action BroadcastLimit;
    }

    public interface IItemDetailBindings
    {
        IProfessionItemIconContext Items { get; }
        ItemDetailDefinition Detail(int itemId);
        bool ContainsText(string key);
        Sprite LoadSprite(string address);
        Sprite QualityBackground(Quality quality, bool small);
        Color QualityTextColor(Quality quality);
        IReadOnlyList<ItemDetailSource> Sources(int itemId);
        GameObject InstantiateUi(GameObject prefab, Transform parent);
        void Close(GameObject window);
    }
}
