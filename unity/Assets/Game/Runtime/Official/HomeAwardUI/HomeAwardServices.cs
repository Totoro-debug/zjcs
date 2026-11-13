using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeAwardUI
{
    // Catalog/receipt ownership stays outside the original presentation code.
    public interface IHomeAwardCatalog
    {
        ItemInfo GetItemInfo(int id);
    }
    public sealed class GemPartInfo { public int GemType; }
    public abstract class ItemInfo : IProfessionItemInfo
    {
        public abstract int ClassId { get; }
        public abstract string Icon { get; }
        public abstract string IconEffect { get; }
        public abstract Quality Quality { get; }
        public abstract bool CanOverlap { get; }
        public abstract object GetFakeIcon();
        public abstract ItemType ItemType { get; }
        public abstract int Rank { get; }
        public abstract object PartInfo { get; }
        public abstract bool IsSkillPiece { get; }
        public abstract bool IsSkill { get; }
        public abstract bool IsEquip { get; }
    }
    public interface IHomeAwardAsset<out T> : IDisposable { T Result { get; } }
    public interface IHomeAwardPopupOperation { bool IsDone { get; } }
    public interface IHomeAwardMessage { List<AwardData> DataList { get; } }
    public interface IHomeAwardQueueServices
    {
        bool HasPlayer { get; }
        event Action<GameObject> OnWindowOpened;
        bool IsFullScreen(GameObject window);
        void Close(GameObject window);
        bool IsIgnoreShowAward(int id);
        IHomeAwardAsset<GameObject> LoadAssetByGuid(string guid);
        GameObjectPool<AwardListPanelItem> CreatePool(Transform root, AwardListPanelItem prefab);
        GameObject GetWindow();
        IHomeAwardPopupOperation Popup(IWindowOperator owner, Action<AwardListPanel> initialized,
            bool instant, bool local, bool autoAdjustByWindowContext);
    }
    public interface IHomeAwardIconServices : IHomeAwardCatalog
    {
        ItemIcon GetIcon(ItemInfo info, Transform parent);
        ItemIcon GetIcon(IProfessionItemData data, Transform parent);
        void RecycleIcon(ItemIcon icon);
        IProfessionItemData GetItem(ulong index);
        object Compact(long count);
        string Text(string key);
        IHomeAwardOutputAddition GetOutputAddition(ItemIcon icon);
    }
    public interface IHomeAwardItemServices : IHomeAwardCatalog
    {
        HomeAwardIcon InstantiateAward(Transform slot, AwardData data, Action<HomeAwardIcon, AwardData> init,
            bool autoTransition, bool isSelf, bool showOutputAddition);
        string GetItemNameKey(int id);
        object CultureText(string key);
        object Compact(long count);
        object DecorateExtra(object text, Color color);
        int MoneyId { get; }
        int PlayerExpItemId { get; }
        double GetMoneyAdditionPercent();
        double GetExperienceAdditionPercent();
        IHomeAwardAsset<Sprite> LoadSprite(string path);
    }
    // These ports retain the original specialized icon branches. The real icon
    // manager must provide the corresponding implementation when loading them.
    public interface IHomeAwardAppearanceIcon { void ShowRemainTime(long amount); }
    public interface IHomeAwardTreasureIcon { void SetAttachment(ItemIconAttachmentType type, bool show); }
    public interface IHomeAwardOutputAddition { void Format(object text); }
    public interface IHomeAwardGemIcon { ItemIcon SetCount(); }
    public sealed class HomeAwardCancellation : IDisposable
    {
        readonly System.Threading.CancellationTokenSource source = new System.Threading.CancellationTokenSource();
        public System.Threading.CancellationToken Token => source.Token;
        public void Dispose() { source.Cancel(); source.Dispose(); }
    }

    // Original resource-bag iteration also consumes additions made while disposing.
    public sealed class HomeAwardResourceBag : IDisposable
    {
        List<IDisposable> values;
        int count;
        bool disposed;
        public T Add<T>(T value) where T : IDisposable
        {
            if (disposed) value.Dispose();
            else { if (values == null) values = new List<IDisposable>(); values.Add(value); count++; }
            return value;
        }
        public void Dispose()
        {
            if (values != null)
            {
                for (int index = 0; index < count; index++) values[index]?.Dispose();
                values = null; count = 0;
            }
            disposed = true;
        }
    }
}
