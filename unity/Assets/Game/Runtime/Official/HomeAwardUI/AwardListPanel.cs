// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
public class AwardListPanel : UIBase
{
	public struct AwardListPanelItemData : IMergeableItem, IComparable<AwardListPanelItemData>
	{
		public AwardData AwardData;

		public AwardListPanelItemType ItemType;

		public int CompareTo(AwardListPanelItemData other)
		{
			return AwardData.CompareTo(other.AwardData);
		}

		public bool IsCanMerge()
		{
			return AwardData.IsCanMerge();
		}

		public bool TryMerge(IMergeableItem mergeableItem)
		{
			if (!(mergeableItem is AwardListPanelItemData awardListPanelItemData))
			{
				return false;
			}
			if (awardListPanelItemData.ItemType != ItemType)
			{
				return false;
			}
			return AwardData.TryMerge(awardListPanelItemData.AwardData);
		}
	}

	[Serializable]
	public class AwardListPanelItemConfig
	{
		[SerializeField]
		public AssetGuidProxy<GameObject> AssetGuidProxy;

		[SerializeField]
		public Event AwardSound;

		[SerializeField]
		public AwardListPanelBasePath Path;

		[SerializeField]
		public float DelayCloseTime = 2f;
	}

	[SerializeField]
	private RectTransform mExtraContentSlot;

	[SerializeField]
	private MonoDictionary<AwardListPanelItemType, AwardListPanelItemConfig> mAwardListPanelItemConfigDict;

	[SerializeField]
	private float mIntervalTime = 0.1f;

	private HomeAwardResourceBag mPrefabDisposableBag;

	private Dictionary<AwardListPanelItemType, GameObjectPool<AwardListPanelItem>> mPoolDic = new Dictionary<AwardListPanelItemType, GameObjectPool<AwardListPanelItem>>();

	private Queue<AwardListPanelItemData> mAwardQueue = new Queue<AwardListPanelItemData>();

	private List<AwardListPanelItem> mAwardListPanelItem = new List<AwardListPanelItem>();

	private List<AwardListPanelItemData> mAwardDataListBuffer = new List<AwardListPanelItemData>();

	private float mDelayCloseTime;

	private float mTime;

	private static List<AwardListPanelItemData> sAwardListBuffer = new List<AwardListPanelItemData>();

	private static IHomeAwardPopupOperation sPopupOperation;

	public IHomeAwardQueueServices Services { get; set; }

	public void BindSoundHost(IProfessionSoundHost host)
    {
        foreach (var config in mAwardListPanelItemConfigDict.Values)
            if (config.AwardSound != null) config.AwardSound.InstanceHost = host;
    }

    private void Awake()
	{

		mPrefabDisposableBag = new HomeAwardResourceBag();
		mDelayCloseTime = 1f;
	}

	protected override void OnDestroy()
	{
		mPoolDic.Clear();
		mPrefabDisposableBag.Dispose();
		base.OnDestroy();
	}

	private void Init(List<AwardListPanelItemData> awardList, bool notMergeAward = false)
	{
		InitWrapper(() =>
		{
			EnqueueItem(awardList, notMergeAward);
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.OnWindowOpened += new Action<GameObject>(OnWindowOpened);
	}

	private void OnWindowOpened(GameObject window)
	{
		if (Services.IsFullScreen(window))
		{
			Close();
		}
	}

	protected override void OnDisableAndInited()
	{
		foreach (AwardListPanelItem item in mAwardListPanelItem)
		{
			GetGameObjectPool(item.ItemType).Return(item);
		}
		mAwardListPanelItem.Clear();
		Services.OnWindowOpened -= new Action<GameObject>(OnWindowOpened);
		base.OnDisableAndInited();
	}

	private void EnqueueItem(List<AwardListPanelItemData> awardList, bool notMergeAward = false)
	{
		if (awardList == null)
		{
			return;
		}
		mAwardDataListBuffer.Clear();
		mAwardDataListBuffer.AddRange(mAwardQueue);
		mAwardQueue.Clear();
		if (notMergeAward)
		{
			mAwardDataListBuffer.AddRange(awardList);
		}
		else
		{
			HomeAwardFormula.MergeItemAndSort(mAwardDataListBuffer, awardList);
		}
		foreach (AwardListPanelItemData item in mAwardDataListBuffer)
		{
			if (!Services.IsIgnoreShowAward((item.AwardData.AgiotageId > 0) ? item.AwardData.AgiotageId : item.AwardData.AwardId))
			{
				mAwardQueue.Enqueue(item);
			}
		}
	}

	private void ReturnAwardItem(AwardListPanelItem item)
	{
		mAwardListPanelItem.Remove(item);
		AwardListPanelItemType itemType = item.ItemType;
		GetGameObjectPool(itemType).Return(item);
		GetAwardListPanelPath(itemType).RemoveItem(item);
	}

	public GameObjectPool<AwardListPanelItem> GetGameObjectPool(AwardListPanelItemType awardListPanelItemType)
	{
		if (!mPoolDic.TryGetValue(awardListPanelItemType, out var value))
		{
			AwardListPanelItem awardListPanelItemPrefab = GetAwardListPanelItemPrefab(awardListPanelItemType);
			value = Services.CreatePool(this.transform, awardListPanelItemPrefab);
			mPoolDic.Add(awardListPanelItemType, value);
		}
		return value;
	}

	private void Update()
	{
		mTime += Time.deltaTime;
		if (mAwardQueue.Count != 0)
		{
			if (mTime >= mIntervalTime)
			{
				ShowNextItem();
				mTime = 0f;
			}
		}
		else if (mTime >= mDelayCloseTime)
		{
			Close();
		}
	}

	private void Close()
	{
		Services.Close(this.gameObject);
		mAwardQueue.Clear();
		mTime = 0f;
	}

	private void ShowNextItem()
	{
		if (!Services.HasPlayer || mAwardQueue.Count == 0)
		{
			return;
		}
		AwardListPanelItemData item = mAwardQueue.Dequeue();
		AwardListPanelItemType itemType = item.ItemType;
		AwardListPanelBasePath awardListPanelPath = GetAwardListPanelPath(itemType);
		if (!awardListPanelPath.CanSetItem())
		{
			mAwardQueue.Enqueue(item);
			return;
		}
		Event awardSound = GetAwardSound(itemType);
		if (awardSound != null)
		{
			awardSound.Post(this.gameObject);
		}
		AwardListPanelItem awardListPanelItem = GetGameObjectPool(itemType).Borrow((Transform)(object)awardListPanelPath.Slot);
		awardListPanelItem.Init(item.AwardData, ReturnAwardItem);
		awardListPanelPath.SetItem(awardListPanelItem);
		mAwardListPanelItem.Add(awardListPanelItem);
		mDelayCloseTime = Math.Max(mDelayCloseTime, GetCloseDelayTime(itemType));
	}

	private AwardListPanelBasePath GetAwardListPanelPath(AwardListPanelItemType awardListPanelItemType)
	{
		if (mAwardListPanelItemConfigDict.TryGetValue(awardListPanelItemType, out var value))
		{
			return value.Path;
		}
		Debug.LogError("Original award UI: missing configured dependency.");
		return null;
	}

	private AwardListPanelItem GetAwardListPanelItemPrefab(AwardListPanelItemType awardListPanelItemType)
	{
		if (mAwardListPanelItemConfigDict.TryGetValue(awardListPanelItemType, out var value))
		{
			IHomeAwardAsset<GameObject> assetOperationHandle = mPrefabDisposableBag.Add(Services.LoadAssetByGuid(value.AssetGuidProxy.AssetGuid));
			if (assetOperationHandle != null)
			{
				return assetOperationHandle.Result.GetComponent<AwardListPanelItem>();
			}
		}
		Debug.LogError("Original award UI: missing configured dependency.");
		return null;
	}

	private Event GetAwardSound(AwardListPanelItemType awardListPanelItemType)
	{
		if (mAwardListPanelItemConfigDict.TryGetValue(awardListPanelItemType, out var value))
		{
			return value.AwardSound;
		}
		Debug.LogError("Original award UI: missing configured dependency.");
		return null;
	}

	private float GetCloseDelayTime(AwardListPanelItemType awardListPanelItemType)
	{
		if (mAwardListPanelItemConfigDict.TryGetValue(awardListPanelItemType, out var value))
		{
			return value.DelayCloseTime;
		}
		Debug.LogError("Original award UI: missing configured dependency.");
		return 0f;
	}

	public static void PopupAwardListPanel(IHomeAwardQueueServices services, IHomeAwardMessage msg, bool notMergeAward = false, Action onComplete = null, AwardListPanelItemType awardListPanelItemType = AwardListPanelItemType.Normal)
	{
		PopupAwardListPanel(services, null, msg.DataList, notMergeAward, onComplete, awardListPanelItemType);
	}

	public static void PopupAwardListPanel(IHomeAwardQueueServices services, List<AwardData> awardList, bool notMergeAward = false, Action onComplete = null, AwardListPanelItemType awardListPanelItemType = AwardListPanelItemType.Normal)
	{
		PopupAwardListPanel(services, null, awardList, notMergeAward, onComplete, awardListPanelItemType);
	}

	public static void PopupAwardListPanel(IHomeAwardQueueServices services, IWindowOperator windowOperator, List<AwardData> awardList, bool notMergeAward = false, Action onComplete = null, AwardListPanelItemType awardListPanelItemType = AwardListPanelItemType.Normal)
	{
		if (awardList == null)
		{
			onComplete?.Invoke();
			return;
		}
		foreach (AwardData award in awardList)
		{
			sAwardListBuffer.Add(new AwardListPanelItemData
			{
				AwardData = award,
				ItemType = awardListPanelItemType
			});
		}
		GameObject window = services.GetWindow();
		if ((Object)window != (Object)null)
		{
			window.GetComponent<AwardListPanel>().EnqueueItem(sAwardListBuffer, notMergeAward);
			sAwardListBuffer.Clear();
			onComplete?.Invoke();
			return;
		}
		if (sPopupOperation != null && !sPopupOperation.IsDone)
		{
			onComplete?.Invoke();
			return;
		}
		sPopupOperation = services.Popup(windowOperator, (AwardListPanel panel) =>
		{
			panel.Services = services;
			panel.Init(sAwardListBuffer, notMergeAward);
			sAwardListBuffer.Clear();
			onComplete?.Invoke();
			sPopupOperation = null;
		}, instant: false, local: true, autoAdjustByWindowContext: true);
	}
}
}
