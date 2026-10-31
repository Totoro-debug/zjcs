// Ported from active official client; see partner-roster-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerRosterLanguage;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.SkillsUI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NpcPanelNpcList : MonoBehaviour, LoopScrollDataSource
{
    private IPartnerRosterUiServices services;
    public void Configure(IPartnerRosterUiServices value) { services = value; }

	[SerializeField]
	private FilterSortPanel mFilterSortPanel;

	[SerializeField]
	private LoopVerticalScrollRect mLoop;

	[SerializeField]
	private ListItemShowSequence mShowSequence;

	[SerializeField]
	private UIToggle mShowAllToggle;

	private CollectSortType mDefaultSort = CollectSortType.Friendship;

	private bool mShowAllMeet;

	private CollectSortType mSortType;

	private bool mIsReverse;

	private List<NpcRosterItem> mNpcs = new List<NpcRosterItem>();

	protected void Awake()
	{
		mFilterSortPanel.OnSortOptionChanged += OnSortOptionChanged;
	}

	protected void OnDestroy()
	{
		mFilterSortPanel.OnSortOptionChanged -= OnSortOptionChanged;
	}

	protected void OnEnable()
	{
		mSortType = CollectSortType.Friendship;
		mFilterSortPanel.SwitchBtnText.SetKey(LanguageUtils.GetEnumKey(mDefaultSort));
		mShowAllToggle.isOn = false;
		mShowAllMeet = false;
		mShowAllToggle.OnValueChanged += OnShowAllToggle;
		services.NpcCollectedChanged += OnNpcCollectChanged;
		services.NpcRemoved += OnRemoveNpc;
		InitNpcList();
		((MonoBehaviour)this).StartCoroutine(DelayShow());
	}

	private IEnumerator DelayShow()
	{
		yield return null;
		RefreshLoop();
	}

	protected void OnDisable()
	{
		mNpcs.Clear();
		mLoop.ClearCells();
		if (services.MainPlayerAvailable)
		{
			services.NpcCollectedChanged -= OnNpcCollectChanged;
			services.NpcRemoved -= OnRemoveNpc;
		}
		mShowAllToggle.OnValueChanged -= OnShowAllToggle;
	}

	private void OnSortOptionChanged(object ob, bool isOn, bool isReverse)
	{
		if (isOn && ob is CollectSortType collectSortType)
		{
			mSortType = collectSortType;
			mIsReverse = isReverse;
			mFilterSortPanel.SwitchBtnText.SetKey(LanguageUtils.GetEnumKey(collectSortType));
			mLoop.ClearCells();
			((MonoBehaviour)this).StartCoroutine(DelayShow());
		}
	}

	private void InitNpcList()
    {
        mNpcs.Clear();
        mNpcs.AddRange(services.NpcRoster(mShowAllMeet));
    }

	private void OnNpcCollectChanged(int npcClassId, bool isCollected)
	{
		float curPosition = mLoop.GetCurPosition();
		Refresh();
		mLoop.RefillCellsByPosition(curPosition);
	}

	private void OnRemoveNpc(int npcClassId)
	{
		float curPosition = mLoop.GetCurPosition();
		Refresh();
		mLoop.RefillCellsByPosition(curPosition);
	}

	private void OnShowAllToggle(GameObject obj, bool isOn)
	{
		mShowAllMeet = isOn;
		Refresh();
	}

	private void Refresh()
	{
		InitNpcList();
		RefreshLoop();
	}

	private void RefreshLoop()
    {
        switch (mSortType)
        {
            case CollectSortType.Friendship:
                mNpcs.Sort((x, y) => NpcRosterRules.Compare(x, y, NpcRosterSort.Friendship, mIsReverse, services.CanPinNpc));
                break;
            case CollectSortType.ActivateTime:
                mNpcs.Sort((x, y) => NpcRosterRules.Compare(x, y, NpcRosterSort.ActivateTime, mIsReverse, services.CanPinNpc));
                break;
            default: Debug.LogError("NpcPanelNpcList unsupported sort: " + mSortType); break;
        }
        mLoop.dataSource = this;
        mLoop.totalCount = mNpcs.Count;
        mLoop.RefillCells();
    }

	public void ProvideData(Transform transform, int idx)
	{
		if (idx >= 0 && idx < mNpcs.Count)
		{
			NpcPanelNpcItem component = ((Component)transform).GetComponent<NpcPanelNpcItem>();
			_ = mNpcs[idx];
			component.Init(mNpcs, idx);
			mShowSequence.AddItem(component);
		}
	}
}

}
