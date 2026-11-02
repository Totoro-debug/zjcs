// Ported original controller; see partner-dialogue-list-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcPanelDialogueList : UIBase
{
    private IPartnerDialogueUiServices services;
    public void Configure(IPartnerDialogueUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
    }

	private class DialogueNpcCompare
	{
		private enum ComparerType
		{
			LastDateTime,
			ClassId
		}

		private List<(ComparerType comparerType, ComparerOrderType orderType)> mDefaultPriorityList = new List<(ComparerType, ComparerOrderType)>
		{
			(ComparerType.LastDateTime, ComparerOrderType.Desc),
			(ComparerType.ClassId, ComparerOrderType.Asc)
		};

		private NpcPanelDialogueList mNpcPanelDialogueList;

		public DialogueNpcCompare(NpcPanelDialogueList npcPanelDialogueList)
		{
			mNpcPanelDialogueList = npcPanelDialogueList;
		}

		public int DefaultComparer(long x, long y)
		{
			return Comparer(x, y, mDefaultPriorityList);
		}

		private int Comparer(long x, long y, List<(ComparerType comparerType, ComparerOrderType orderType)> priorityList)
		{
			mNpcPanelDialogueList.services.Npcs.TryGetValue(x, out var obj);
			mNpcPanelDialogueList.services.Npcs.TryGetValue(y, out var obj2);
			if (obj == null || obj2 == null)
			{
				Debug.LogError($"没有找到 NPC 数据！NpcId: {x}/{y}");
				return 0;
			}
			PartnerDialogueData latestDialogue = GetLatestDialogue(x);
			PartnerDialogueData latestDialogue2 = GetLatestDialogue(y);
			foreach (var priority in priorityList)
			{
				long score = GetScore(obj.NpcClassId, latestDialogue, priority.comparerType);
				long score2 = GetScore(obj2.NpcClassId, latestDialogue2, priority.comparerType);
				int num = PartnerDialogueListRules.Compare(score, score2, priority.orderType);
				if (num != 0)
				{
					return num;
				}
			}
			return 0;
		}

		private long GetScore(int npcClassId, PartnerDialogueData data, ComparerType comparerType)
		{
			return comparerType switch
			{
				ComparerType.LastDateTime => data.DateTime.Ticks,
				ComparerType.ClassId => npcClassId,
				_ => 0L,
			};
		}

		private PartnerDialogueData GetLatestDialogue(long npcId)
		{
			if (!mNpcPanelDialogueList.mNpcDialogues.TryGetValue(npcId, out var value) || value.Count <= 0)
			{
				return null;
			}
			PartnerDialogueData playerNpcDialogueDataWrap = null;
			foreach (PartnerDialogueData item in value)
			{
				if (item.DateTime.Ticks > (playerNpcDialogueDataWrap?.DateTime.Ticks ?? 0))
				{
					playerNpcDialogueDataWrap = item;
				}
			}
			return playerNpcDialogueDataWrap;
		}
	}

	[SerializeField]
	private Transform mRoot;

	[SerializeField]
	private GameObject mItemPrefab;

	[SerializeField]
	private GameObject mEmptyTip;

	[SerializeField]
	private float mFadeInIntervalS = 0.1f;

	private WaitForSeconds mFadeInWaiter;

	private Dictionary<long, List<PartnerDialogueData>> mNpcDialogues = new Dictionary<long, List<PartnerDialogueData>>();

	private DialogueNpcCompare mNpcComparer;

	private List<long> mNpcs = new List<long>();

	private Coroutine mRefreshLoopCo;

	private Dictionary<long, NpcPanelDialogueListNpcItem> mNpcItemDict = new Dictionary<long, NpcPanelDialogueListNpcItem>();

	private List<NpcPanelDialogueListNpcItem> mTempNpcItemList = new List<NpcPanelDialogueListNpcItem>();

	private WaitForSeconds FadeInIntervalWaiter
	{
		get
		{
			if (mFadeInWaiter == null)
			{
				mFadeInWaiter = new WaitForSeconds(mFadeInIntervalS);
			}
			return mFadeInWaiter;
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		services.DialogueAdded += MainPlayer_OnNpcDialogueAdd;
		services.DialogueRemoved += MainPlayer_OnNpcDialogueRemove;
		foreach (NpcPanelDialogueListNpcItem npcItem in GetNpcItemList())
		{
			((Component)npcItem).gameObject.SetActive(false);
		}
		TryRefreshLoop(instant: false);
	}

	protected override void OnDisableAndInited()
	{
		mNpcs.Clear();
		mNpcDialogues.Clear();
		if (services.MainPlayerAvailable)
		{
			services.DialogueAdded -= MainPlayer_OnNpcDialogueAdd;
			services.DialogueRemoved -= MainPlayer_OnNpcDialogueRemove;
		}
		base.OnDisableAndInited();
	}

	public void Init()
	{
		InitWrapper(() =>
		{
			InitNpcDialogues();
		});
	}

	private void InitNpcDialogues()
	{
		mNpcs.Clear();
		mNpcDialogues.Clear();
		mNpcComparer = new DialogueNpcCompare(this);
		foreach (KeyValuePair<long, PartnerDialogueData> item in services.Dialogues)
		{
			PartnerDialogueData value = item.Value;
			long npcId = value.NpcId;
			if (!mNpcs.Contains(npcId))
			{
				mNpcs.Add(npcId);
			}
			if (!mNpcDialogues.TryGetValue(npcId, out var value2))
			{
				value2 = new List<PartnerDialogueData>();
				mNpcDialogues.Add(npcId, value2);
			}
			value2.Add(value);
		}
		mNpcs.Sort(mNpcComparer.DefaultComparer);
		foreach (KeyValuePair<long, List<PartnerDialogueData>> mNpcDialogue in mNpcDialogues)
		{
			mNpcDialogue.Value.Sort(PartnerDialogueListRules.CompareDialogues);
		}
		mEmptyTip.SetActive(mNpcs.Count <= 0);
	}

	private void TryRefreshLoop(bool instant)
	{
		if (mRefreshLoopCo != null)
		{
			((MonoBehaviour)this).StopCoroutine(mRefreshLoopCo);
			mRefreshLoopCo = null;
		}
		mRefreshLoopCo = ((MonoBehaviour)this).StartCoroutine(RefreshLoop(instant));
	}

	private List<NpcPanelDialogueListNpcItem> GetNpcItemList()
	{
		mTempNpcItemList.Clear();
		((Component)mRoot).GetComponentsInChildren<NpcPanelDialogueListNpcItem>(true, mTempNpcItemList);
		return mTempNpcItemList;
	}

	private IEnumerator RefreshLoop(bool instant)
	{
		EnsureNpcListItem();
		mNpcItemDict.Clear();
		int npcCount = mNpcs.Count;
		List<NpcPanelDialogueListNpcItem> npcItemList = GetNpcItemList();
		int i = 0;
		while (i < npcItemList.Count)
		{
			NpcPanelDialogueListNpcItem npcPanelDialogueListNpcItem = npcItemList[i];
			if (i >= npcCount)
			{
				((Component)npcPanelDialogueListNpcItem).gameObject.SetActive(false);
			}
			else
			{
				long key = mNpcs[i];
				PartnerDialogueNpcData npcData = services.Npcs[key];
				npcPanelDialogueListNpcItem.Init(npcData, mNpcDialogues[key], instant);
				((Component)npcPanelDialogueListNpcItem).gameObject.SetActive(true);
				mNpcItemDict.Add(key, npcPanelDialogueListNpcItem);
				if (!instant)
				{
					yield return FadeInIntervalWaiter;
				}
			}
			int num = i + 1;
			i = num;
		}
		mRefreshLoopCo = null;
	}

	private void EnsureNpcListItem()
	{
		int count = mNpcs.Count;
		List<NpcPanelDialogueListNpcItem> npcItemList = GetNpcItemList();
		int num = count - npcItemList.Count;
		if (num >= 0)
		{
			for (int i = 0; i <= num; i++)
			{
				services.Instantiate(mItemPrefab, mRoot, true).SetActive(false);
			}
		}
	}

	private void MainPlayer_OnNpcDialogueAdd(PartnerDialogueData obj)
	{
		long npcId = obj.NpcId;
		bool instant = true;
		List<PartnerDialogueData> value;
		if (!mNpcs.Contains(npcId))
		{
			InitNpcDialogues();
			TryRefreshLoop(instant);
		}
		else if (!mNpcDialogues.TryGetValue(npcId, out value))
		{
			Debug.LogError($"NpcPanelDialogueListNpcItem 数据有误！有 NPC:{npcId} 没有对话信息！");
		}
		else
		{
			value.Add(obj);
			value.Sort(PartnerDialogueListRules.CompareDialogues);
			services.Npcs.TryGetValue(npcId, out var obj2);
			mNpcItemDict[npcId].Init(obj2, value, instant);
		}
	}

	private void MainPlayer_OnNpcDialogueRemove(PartnerDialogueData obj)
	{
		if (obj != null)
		{
			InitNpcDialogues();
			TryRefreshLoop(instant: true);
		}
	}
}

}
