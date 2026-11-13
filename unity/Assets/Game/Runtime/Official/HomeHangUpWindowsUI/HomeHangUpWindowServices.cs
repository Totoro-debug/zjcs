using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

namespace Zjcs.Official.HomeHangUpWindowsUI
{
    // Data consumed by the original windows. Configuration, response records,
    // property chunks and presentation are supplied by their actual owners.
    public sealed class HomeWindowHangUpInfo
    {
        public int Id, FastHangUpId;
        public EFurniture BindFurniture;
        public PropType EffectFixedPropType, EffectPercentPropType, MaxStorePropType;
    }
    public sealed class HomeWindowFastHangUpInfo
    {
        public int FastHangUpId;
        public bool AutoReceive;
        public Dictionary<int, int> FastHangUpItemDic;
        public List<int> HangUpIdList;
    }
    public sealed class HomeWindowItemInfo { public string Icon; }
    public sealed class HomeWindowFunctionLimitInfo { public object Condition; }
    public sealed class HomeWindowFurnitureInfo { public int ClassId; }
    public sealed class HomeWindowMapRegionInfo { public int ClassId, Region; }
    public sealed class HomeWindowOutputAdditionInfo { public int OutputAdditionPropType; }
    public interface IHomeWindowAwardMessage
    {
        IList DataList { get; }
        object SourceData { get; }
    }
    public readonly struct HomeWindowText
    {
        public readonly string Key;
        public readonly object[] Args;
        public HomeWindowText(string key, params object[] args) { Key = key; Args = args; }
    }
    public interface IHomeWindowPropertyCenter
    {
        void GetProps(List<EPropHandleType> types, Dictionary<PropType, long> result);
        Dictionary<PropType, long> GetChunckProps(EPropHandleType type);
    }
    public interface IHomeHangUpWindowPlayer
    {
        int Level { get; }
        int SubRank { get; }
        IReadOnlyDictionary<PropType, long> NonBattleProps { get; }
        IHomeWindowPropertyCenter PlayerPropCenter { get; }
        int GetHangUpLevel(int id);
        int GetBlessingLevel();
        int GetLostGoddessActiveCount();
        bool CanFreeFastHangUp(int id);
        bool CanGetExp();
        bool IsFunctionLimit(int id);
        bool GetHangUpQuickReceiveState(EFurniture type);
        int GetOutputAdditionPropValue(int type);
        bool IsMapRegionPublic(int classId, int region);
        bool TryGetFastHangUpTip(int id, long hours, out int itemId,
            out HomeWindowText title, out HomeWindowText content);
        void Award(IList data, object source);
        event Action<int> OnFastHangUp;
        event Action<object> OnAddOutputAdditionData;
    }
    public interface IHomeHangUpWindowServices
    {
        IHomeHangUpWindowPlayer Player { get; }
        IProfessionImageResourceLoader Resources { get; }
        int HomeBedSpeedConfirmFunctionId { get; }
        int PlayerExpItemId { get; }
        HomeWindowHangUpInfo GetHangUpInfo(int id);
        bool TryGetMainHangUpInfo(EFurniture type, out HomeWindowHangUpInfo info);
        HomeWindowFastHangUpInfo GetFastHangUpInfo(int id);
        HomeWindowFurnitureInfo GetFurnitureInfo(EFurniture type);
        HomeWindowFunctionLimitInfo GetFunctionLimitInfo(int id);
        string GetFurnitureTypeNameKey(EFurniture type);
        string GetFurnitureIconPrefabPath(EFurniture type);
        string GetItemNameKey(int id);
        string GetItemDescKey(int id);
        string GetHangUpBaseOutput(int id);
        string Text(string key);
        string TextFormatted(string key, params object[] args);
        string FormatCount(long count);
        double GetHangUpReward(int id, double hours, int level = -1, bool isBase = false);
        long GetHangUpMaxRewardStore(int id, int level = -1);
        long GetHangUpAwardCount(int id, out double seconds);
        long CalcOutputAdditionExtStore(int id, long capacity);
        long CalcOutputAdditionUnsettleExtRewardCount(int id);
        double CalcOutputAdditionExtCount(int id, double count);
        bool CanGetHangUpReward(int id);
        HomeWindowItemInfo GetHangUpRewardItem(int id);
        double GetProp(IReadOnlyDictionary<PropType, long> props, PropType type);
        void GetHangUpProp(HomeWindowHangUpInfo info, IReadOnlyDictionary<PropType, long> props,
            out double addPercent, out double addFixed);
        int GetRankHighSubRank(int subRank);
        bool IsRankLimited(int subRank);
        void GetMaxPlayerLevel(int subRank, out int baseLevel, out int blessLevel);
        bool TryGetFirstMainMapRegion(int subRank, out HomeWindowMapRegionInfo region);
        int GetTipLevel(EFurniture type);
        IDisposable SubscribeTips(Action<HangUpTipLevelChangeData> callback);
        void RegisterCondition(object condition, Action callback);
        void DeregisterCondition(object condition, Action callback);
        void SwitchQuickReceive(EFurniture type, bool enabled, Action<object, int> onError);
        void Collect(IReadOnlyList<int> ids, Action<IHomeWindowAwardMessage> onSuccess);
        void MergeAward(IList data);
        void PopupAwardList(IList data);
        void PublishCollected();
        void SystemMessage(string text);
        void Confirm(string key, Action confirmed);
        void Confirm(HomeWindowText key, HomeWindowText title, Action confirmed);
        void OpenFastHangUp(int id, GameObject parent);
        void Close(GameObject window);
        void OperFastHangUp(int id, bool autoReceive, Dictionary<int, long> itemCost, Action onComplete);
        IDisposable SubscribeOutputAdditionFunctionState(Action callback);
        IDisposable SubscribeNewDay(Action<object> callback);
        bool IsOutputAdditionDisabled { get; }
        List<(List<HomeWindowOutputAdditionInfo>, DateTime)> GetCurrOutputGroupPropList(OutputGroupType type);
        void OpenOutputAddition(OutputGroupType type);
        void OpenBlessingRule();
    }
    // Same ownership as the original R3 bag: disposal releases every handle;
    // replacing the bag starts a fresh set of operations.
    public sealed class HomeWindowResourceBag : IDisposable
    {
        List<IDisposable> values;
        int count;
        bool disposed;
        public T Add<T>(T value) where T : IDisposable
        {
            if (disposed) value.Dispose();
            else
            {
                if (values == null) values = new List<IDisposable>();
                values.Add(value); count++;
            }
            return value;
        }
        public void Dispose()
        {
            // Original R3 bag consumes additions made during disposal too.
            if (values != null)
            {
                for (int index = 0; index < count; index++) values[index]?.Dispose();
                values = null; count = 0;
            }
            disposed = true;
        }
    }
}
