using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionItemInfo
    {
        int ClassId { get; }
        string Icon { get; }
        string IconEffect { get; }
        Quality Quality { get; }
        bool CanOverlap { get; }
        object GetFakeIcon();
    }
    public interface IProfessionItemData
    {
        int Id { get; }
        long Count { get; }
        IProfessionItemInfo Info { get; }
        Quality GetQuality();
        bool CanExpire();
    }
    public interface IProfessionItemPlayer
    {
        long GetItemAmount(int itemId);
        event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        bool CanGetItemFromMapLocation(object location);
    }
    public interface IProfessionItemColors
    {
        Color Green { get; }
        Color White { get; }
        Color LightRed { get; }
        Color GetTextColor(ThemeColorType theme, ColorType type);
    }
    public interface IProfessionItemSource
    {
        ItemSourceType SourceType { get; }
        int ItemId { get; }
    }
    public interface IProfessionItemTimeLimitAttachment
    {
        void Init(IProfessionItemData data);
        void Stop();
    }
    public struct ProfessionItemText
    {
        public string Key;
        public object[] Args;
    }
    [Serializable]
    public sealed class ProfessionItemSoundSwitch
    {
        public uint idInternal;
        public byte[] valueGuidInternal;
        public ScriptableObject WwiseObjectReference;
    }
    // Original scene-container/configuration/player/parser/UI services consumed by
    // ItemIcon. The local host must bind these explicitly before initialization.
    public interface IProfessionItemIconContext
    {
        ItemIconConfig Config { get; }
        UnityEngine.Object GrayConfig { get; }
        IProfessionItemColors Colors { get; }
        IProfessionItemPlayer Player { get; }
        IProfessionItemInfo GetItemInfo(int itemId);
        object Compact(long count);
        object CultureText(string key);
        string GetItemNameKey(int itemId);
        bool TryGetSourceData(int itemId, out IReadOnlyList<IProfessionItemSource> sources);
        bool CanMerge(int itemId, bool recursion);
        object GetLocationInfos(int itemId);
        void SetUIGray(UnityEngine.Object config, GameObject target, bool gray);
        void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter);
        GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All);
        void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback);
        void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long required, Action<GameObject> callback);
        void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback);
    }
}
