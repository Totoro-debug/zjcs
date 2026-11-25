using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;

namespace Zjcs.Official.HomeCartWindowsUI
{
    public sealed class HomeCartWindowResourceHandle<T> : IDisposable where T : Object
    {
        public T Result { get; }
        public HomeCartWindowResourceHandle(T result) => Result = result;
        public void Dispose() { }
    }

    public sealed class HomeCartWindowResourceBag : IDisposable
    {
        private readonly List<IDisposable> items = new List<IDisposable>();

        public T Add<T>(T item) where T : IDisposable
        {
            if (item != null) items.Add(item);
            return item;
        }

        public void Dispose()
        {
            for (int i = items.Count - 1; i >= 0; i--) items[i]?.Dispose();
            items.Clear();
        }
    }

    public interface IHomeCartWindowResources
    {
        HomeCartWindowResourceHandle<T> LoadAsset<T>(string path) where T : Object;
    }

    public sealed class HomeCartHangUpInfo
    {
        public int Id;
        public bool CanLevelUp;
        public int SortOrder;
        public int RewardId;
        public IHomeFurnitureCondition UnlockCondition;
        public IHomeFurnitureCondition NoticeCondition;
    }

    public readonly struct HomeCartUnlockData
    {
        public readonly int HangUpId;
        public readonly int Level;
        public readonly double StoredSeconds;

        public HomeCartUnlockData(int hangUpId, int level, double storedSeconds)
        {
            HangUpId = hangUpId;
            Level = level;
            StoredSeconds = storedSeconds;
        }
    }

    public readonly struct HomeCartEquipPreviewItem
    {
        public readonly int ItemId;
        public readonly double Probability;
        public readonly int Amount;

        public HomeCartEquipPreviewItem(int itemId, double probability, int amount)
        {
            ItemId = itemId;
            Probability = probability;
            Amount = amount;
        }
    }

    public sealed class HomeCartStandardSignificantPercentArg
    {
        public readonly double PercentValue;
        private readonly LanguageType language;

        public HomeCartStandardSignificantPercentArg(double val, LanguageType language = LanguageType.zh_CN)
        {
            PercentValue = val;
            this.language = language;
        }

        public override string ToString()
            => ProfessionPropertyFormula.ToStandardSignificantPercent(PercentValue, language);
    }

    public interface IRewardPreviewDataProvider
    {
        int Count { get; }
        void RefreshItem(int index, RewardPreviewDialogItem item);
    }

    public interface IHomeCartWindowPlayer
    {
        int Level { get; }
        int SubRank { get; }
        event Action<int> OnHangUpUnlock;
        event Action<int, int> OnHangUpLevelUp;
        event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        bool TryGetUnlockSingleHangUpData(int hangUpId, out HomeCartUnlockData data);
        int GetHangUpLevel(int hangUpId);
        int GetHangUpMaxLevel();
        int GetBlessingLevel();
        bool HasItems(Dictionary<int, long> items);
        bool IsFunctionLimit(int functionType);
        int CalcLevelUpNum(int hangUpId, int maxLevel);
    }

    public static class HomeCartRectUtils
    {
        public static bool CheckIfClickInsideUI(RectTransform rectTransform, Vector2 clickPosition, Camera uiCamera)
        {
            if (!rectTransform) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, clickPosition, uiCamera, out var localPoint);
            return rectTransform.rect.Contains(localPoint);
        }

        public static Vector2 GetRealCanvasSize(Canvas canvas, CanvasScaler canvasScaler, out float scaleFactor)
        {
            Vector2 pixelSize = canvas.pixelRect.size;
            scaleFactor = GetCanvasScaleFactor(canvas, canvasScaler);
            return scaleFactor > 0f ? pixelSize / scaleFactor : pixelSize;
        }

        public static float GetCanvasScaleFactor(Canvas canvas, CanvasScaler canvasScaler)
        {
            if (!canvas || !canvasScaler) return 1f;
            Vector2 pixelSize = canvas.pixelRect.size;
            float x = pixelSize.x / canvasScaler.referenceResolution.x;
            float y = pixelSize.y / canvasScaler.referenceResolution.y;
            return Mathf.Min(x, y);
        }

        public static Vector2 ClampRectPosInScreenByCorner(RectTransform rectTransform, Vector2 pos, Vector2 screenSize)
        {
            Vector2 size = rectTransform.rect.size;
            Vector2 halfSize = size / 2f;
            Vector2 minScreen = -screenSize / 2f;
            Vector2 maxScreen = screenSize / 2f;
            if (pos.x - size.x >= minScreen.x && pos.y + size.y <= maxScreen.y)
                return new Vector2(pos.x - halfSize.x, pos.y + halfSize.y);
            if (pos.x + size.x <= maxScreen.x && pos.y + size.y <= maxScreen.y)
                return new Vector2(pos.x + halfSize.x, pos.y + halfSize.y);
            if (pos.x - size.x >= minScreen.x && pos.y - size.y >= minScreen.y)
                return new Vector2(pos.x - halfSize.x, pos.y - halfSize.y);
            if (pos.x + size.x <= maxScreen.x && pos.y + size.y >= minScreen.y)
                return new Vector2(pos.x + halfSize.x, pos.y + halfSize.y);
            return pos;
        }
    }

    public interface IHomeCartWindowServices
    {
        public const int FunctionHangUpCalcLevelUp = 175;
        public const int ServerFunctionHangUpUnlock = 1;
        public const int ServerFunctionHangUpLevelUp = 2;
        public const int ErrorHangUpUnlockDisabled = 53852;
        public const int ErrorHangUpLevelUpDisabled = 53853;

        IHomeCartWindowPlayer Player { get; }
        IHomeCartWindowResources Resources { get; }
        IHomeAwardItemServices AwardItemServices { get; }
        IProfessionRuleTipData RuleTips { get; }
        LanguageType CurrentLanguage { get; }
        int HangUpCalcMaxLevelUp { get; }

        string Text(string key);
        string TextFormatted(string key, params object[] args);
        string GetItemNameKey(int classId);
        string GetItemDescKey(int classId);
        string GetHangUpName(int hangUpId);
        string GetCartHangUpIconPath(int hangUpId);
        string GetCartItemBgPath(int hangUpId);

        HomeWindowFurnitureInfo GetFurnitureInfo(EFurniture furniture);
        HomeCartHangUpInfo GetHangUpInfo(int hangUpId);
        bool TryGetHangUpInfos(EFurniture furniture, out IReadOnlyList<HomeCartHangUpInfo> infos);
        HomeWindowItemInfo GetHangUpRewardItem(int hangUpId);
        ItemInfo GetAwardItemInfo(int itemId);
        bool IsIgnoreShowAward(int itemId);
        bool CanUseItem(int itemId);

        bool CanGetHangUpReward(int hangUpId);
        double GetHangUpReward(int hangUpId, double hours, int level);
        double GetHangUpReward(int hangUpId, double hours);
        long GetHangUpMaxRewardStore(int hangUpId);
        long GetHangUpAwardCount(int hangUpId, out double hangUpSeconds);
        int GetUpgradeMaxLevel(int hangUpId);
        Dictionary<int, long> GetUpgradeMaterialCost(int hangUpId, int targetLevel);
        IReadOnlyList<HomeCartEquipPreviewItem> GetEquipPreviewItems(int hangUpId);

        bool IsConditionMet(IHomeFurnitureCondition condition);
        IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action);
        IDisposable SubscribeTips(Action<HangUpTipLevelChangeData> action);
        int GetTipLevel(EFurniture furniture);
        IDisposable SubscribeGlobalPointerDown(Action<Vector2> callback);
        Camera GetUiCamera();

        FurnitureCartHangUpItem InstantiateCartHangUpItem(FurnitureCartHangUpItem prefab, Transform parent);
        HangUpLevelUpSwitchItem InstantiateLevelUpSwitchItem(HangUpLevelUpSwitchItem prefab, Transform parent);

        bool CheckFunctionDisabled(int serverFunctionType);
        void BroadcastErrCode(int errCode);
        void SystemMessage(string message);

        void UnlockHangUp(int hangUpId, Action onSuccess);
        void LevelUpHangUp(int hangUpId, int level);
        void Collect(IReadOnlyList<int> hangUpIds, Action<IHomeHangUpAwardMessage> onSuccess);
        void AwardCollected(IList dataList, object sourceData);
        void MergeAward(IList dataList);
        void MergeAwardData(List<AwardData> awardList);
        void PublishCollected();
        void PopupAwardPanel(IHomeHangUpAwardMessage response);
        void PopupAwardList(IList dataList);
        void OpenCartItemDetail(int hangUpId, Vector3 mousePosition);
        void OpenHangUpLevelUp(int hangUpId, EFurniture furniture);
        void OpenCartRule();
        void EnableAwardItemDetailUseButton(GameObject detailWindow);
        void Close(GameObject window);
    }
}
