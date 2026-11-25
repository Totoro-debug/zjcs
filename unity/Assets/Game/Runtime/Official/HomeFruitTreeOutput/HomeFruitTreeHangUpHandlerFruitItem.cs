// Original fruit slot, pooling and refresh behaviour; local owners are injected.
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFruitTreeOutput
{
    public interface IHomeFruitTreeOutputItem
    {
        long Count { get; }
        double Decimal { get; }
        double IncreaseSpeed { get; }
        DateTime LastUpdateTime { get; }
    }

    public interface IHomeFruitTreeOutputServices
    {
        bool HasPlayer { get; }
        bool InMyHome { get; }
        int FruitId { get; }
        int RandomRange(int minInclusive, int maxExclusive);
        long GetItemAmount(int itemId);
        IHomeFruitTreeOutputItem GetItemById(int itemId);
        IEnumerable<IHomeFruitTreeOutputItem> FindItemsById(int itemId);
        EFurniture FastHangUpFurniture(int fastHangUpId);
        double CalcElapsedSeconds(DateTime since);
        void ShowFruitNotRipe(TimeSpan remaining);
        IDisposable SubscribePostHomeChange(Action changed);
        event Action<long> OnDebugTimeOffset;
        event Action<int> OnFastHangUp;
        GameObject LoadEffectPrefab(string assetGuid);
        GameObject InstantiateEffect(GameObject prefab, Transform parent);
    }

    public class HomeFruitTreeHangUpHandlerFruitItem : MonoBehaviour, IHomeFruitTreeHangUpHandler
    {
        [Serializable] private class EffectItemSlotConfig
        {
            public Transform Slot;
            public AssetGuidProxy<GameObject> ItemProxy;
        }

        [SerializeField] private HomeFruitTreeHangUpEffectItem mEffectItem;
        [SerializeField] private List<EffectItemSlotConfig> mEffectItemSlotConfigs;
        private const float kEffectItemInterval = .1f;
        private int mFruitId;
        private long mEffectItemCount;
        private CancellationTokenSource mAutoRefreshCancellationTokenSource;
        private IDisposable mHomeChangeDisposable;
        private readonly List<HomeFruitTreeHangUpEffectItem> mEffectItemList = new List<HomeFruitTreeHangUpEffectItem>();
        private readonly List<HomeFruitTreeHangUpEffectItem> mEffectItemCache = new List<HomeFruitTreeHangUpEffectItem>();

        public IHomeFruitTreeOutputServices OutputServices { get; set; }

        private void Awake()
        {
            for (int i = mEffectItemSlotConfigs.Count - 1; i >= 0; i--)
            {
                int index = OutputServices.RandomRange(0, i + 1);
                EffectItemSlotConfig value = mEffectItemSlotConfigs[index];
                mEffectItemSlotConfigs[index] = mEffectItemSlotConfigs[i];
                mEffectItemSlotConfigs[i] = value;
            }
            mHomeChangeDisposable = OutputServices.SubscribePostHomeChange(OnPostHomeChange);
        }

        private void OnEnable()
        {
            OutputServices.OnDebugTimeOffset += OnTimeOffset;
            if (!OutputServices.InMyHome) HideFruitItems(); else AutoRefresh();
        }

        private void OnDisable()
        {
            StopAutoRefresh();
            if (OutputServices.HasPlayer) OutputServices.OnDebugTimeOffset -= OnTimeOffset;
        }

        private void OnDestroy()
        {
            mHomeChangeDisposable?.Dispose();
            mHomeChangeDisposable = null;
        }

        private void OnPostHomeChange()
        {
            if (OutputServices.InMyHome) Init(); else HideFruitItems();
        }

        private void OnTimeOffset(long value) => AutoRefresh();
        public bool HaveReward() => OutputServices.GetItemAmount(OutputServices.FruitId) > 0;

        public void Init()
        {
            mFruitId = OutputServices.FruitId;
            mEffectItemCount = OutputServices.GetItemAmount(mFruitId);
            OutputServices.OnFastHangUp -= OperPlayerFastHangUp_OnSuccess;
            OutputServices.OnFastHangUp += OperPlayerFastHangUp_OnSuccess;
            if (!OutputServices.InMyHome)
            {
                HideFruitItems();
                return;
            }
            long count = Math.Min(mEffectItemSlotConfigs.Count, mEffectItemCount);
            HideEffectItemList(mEffectItemList);
            for (int i = 0; i < count; i++)
            {
                HomeFruitTreeHangUpEffectItem item = GetOrCreateEffectItem(i);
                if (item != null) { item.Init(i); mEffectItemList.Add(item); }
            }
            if (isActiveAndEnabled) AutoRefresh();
        }

        public void Uninit()
        {
            if (OutputServices.HasPlayer) OutputServices.OnFastHangUp -= OperPlayerFastHangUp_OnSuccess;
        }

        private void OperPlayerFastHangUp_OnSuccess(int fastHangUpId)
        {
            if (OutputServices.FastHangUpFurniture(fastHangUpId) == EFurniture.Bed) AutoRefresh();
        }

        public void AutoRefresh()
        {
            StopAutoRefresh();
            if (OutputServices.InMyHome)
            {
                mAutoRefreshCancellationTokenSource = new CancellationTokenSource();
                AutoRefreshImpl(mAutoRefreshCancellationTokenSource.Token).SuppressCancellationThrow().Forget();
            }
        }

        private async UniTask AutoRefreshImpl(CancellationToken cancellationToken)
        {
            IHomeFruitTreeOutputItem item = OutputServices.GetItemById(mFruitId);
            if (item == null) return;
            if (item.Count < mEffectItemCount)
            {
                Debug.LogError($"果树数量与实际数量存在错误对应，首位果子数量：{item.Count}");
                foreach (IHomeFruitTreeOutputItem current in OutputServices.FindItemsById(mFruitId))
                    Debug.LogError($"果树数量与实际数量存在错误对应，分别计算果子数量：{current.Count}");
            }
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => item.Count > mEffectItemCount, PlayerLoopTiming.Update, cancellationToken);
                AddFruit();
                await UniTask.WaitForSeconds(kEffectItemInterval, false, PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private void StopAutoRefresh()
        {
            if (mAutoRefreshCancellationTokenSource != null)
            {
                mAutoRefreshCancellationTokenSource.Cancel();
                mAutoRefreshCancellationTokenSource.Dispose();
                mAutoRefreshCancellationTokenSource = null;
            }
        }

        private void HideFruitItems()
        {
            StopAutoRefresh();
            HideEffectItemList(mEffectItemList);
        }

        private void AddFruit()
        {
            if (mEffectItemCount < mEffectItemSlotConfigs.Count)
            {
                int index = (int)mEffectItemCount;
                HomeFruitTreeHangUpEffectItem item = GetOrCreateEffectItem(index);
                if (item != null) { item.Init(index); mEffectItemList.Add(item); }
                mEffectItemCount++;
            }
        }

        public void ResetData()
        {
            mEffectItemCount = 0;
            HideEffectItemList(mEffectItemList);
        }

        public bool TryInteractiveWithNotReward()
        {
            IHomeFruitTreeOutputItem item = OutputServices.GetItemById(mFruitId);
            double seconds = (1.0 - item.Decimal) / item.IncreaseSpeed * 3600.0
                - OutputServices.CalcElapsedSeconds(item.LastUpdateTime);
            OutputServices.ShowFruitNotRipe(new TimeSpan(0, 0, (int)seconds));
            return true;
        }

        private HomeFruitTreeHangUpEffectItem GetOrCreateEffectItem(int index)
        {
            EnsureEffectItemCacheIndex(index);
            Transform slot = GetEffectItemSlot(index);
            if (slot == null) { Debug.LogError($"果树挂机效果挂点未配置，index：{index}"); return null; }
            HomeFruitTreeHangUpEffectItem item = mEffectItemCache[index];
            if (item == null)
            {
                HomeFruitTreeHangUpEffectItem prefab = GetEffectItemPrefab(index);
                if (prefab == null) { Debug.LogError($"果树挂机效果预制体未配置，index：{index}"); return null; }
                item = OutputServices.InstantiateEffect(prefab.gameObject, slot).GetComponent<HomeFruitTreeHangUpEffectItem>();
                mEffectItemCache[index] = item;
            }
            item.transform.SetParent(slot, false);
            item.gameObject.SetActive(true);
            item.OnGameObjectPoolReset();
            return item;
        }

        private void HideEffectItemList(List<HomeFruitTreeHangUpEffectItem> items)
        {
            foreach (HomeFruitTreeHangUpEffectItem item in items)
                if (item != null)
                {
                    item.OnGameObjectPoolClear();
                    item.gameObject.SetActive(false);
                    item.transform.SetParent(transform, false);
                }
            items.Clear();
        }

        private HomeFruitTreeHangUpEffectItem GetEffectItemPrefab(int index)
        {
            EffectItemSlotConfig config = GetEffectItemSlotConfig(index);
            if (config != null && config.ItemProxy.IsValid)
            {
                GameObject prefab = OutputServices.LoadEffectPrefab(config.ItemProxy.AssetGuid);
                if (prefab != null) return prefab.GetComponent<HomeFruitTreeHangUpEffectItem>();
            }
            return mEffectItem;
        }

        private Transform GetEffectItemSlot(int index) => GetEffectItemSlotConfig(index)?.Slot;
        private EffectItemSlotConfig GetEffectItemSlotConfig(int index)
            => index >= 0 && index < mEffectItemSlotConfigs.Count ? mEffectItemSlotConfigs[index] : null;
        private void EnsureEffectItemCacheIndex(int index)
        {
            while (mEffectItemCache.Count <= index) mEffectItemCache.Add(null);
        }
    }
}
