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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcPanelNpcItem : UIBase, IShowSequenceItem
{
    private IPartnerRosterUiServices services;
    private CancellationTokenSource mMeetLifetime;
    public void Configure(IPartnerRosterUiServices value)
    {
        services = value;
        mFriendshipLevel.Configure(value);
    }
    private void CancelMeetPresentation()
    {
        if (mMeetLifetime != null) { mMeetLifetime.Cancel(); mMeetLifetime.Dispose(); mMeetLifetime = null; }
    }

	public enum LockStatus
	{
		Unlock,
		WaitLock,
		Locking
	}

	[SerializeField]
	private GameObject mContentRoot;

	[SerializeField]
	private MultiLanguageText mCommonName;

	[SerializeField]
	private MultiLanguageText mSpecialName;

	[SerializeField]
	private NpcMeetProgressItem mNpcMeetProgressItem;

	[SerializeField]
	private Image mIcon;

	[SerializeField]
	private Image mIconDark;

	[SerializeField]
	private Image mBgImage;

	[SerializeField]
	private Image mBgImageDark;

	[SerializeField]
	private Image mCollectionImage;

	[SerializeField]
	private NpcFriendshipLevelShow mFriendshipLevel;

	[SerializeField]
	private Button mDetailBtn;

	[SerializeField]
	private Button mMeetButton;

	[SerializeField]
	private TipDotNodeLinker mTipDotNodeLinker;

	[SerializeField]
	private WorldStateSwitcher<NpcMeetStatus> mNpcMeetStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<NpcRoleType> mNpcRoleStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<LockStatus> mLockEffectSwitcher;

	public AudioClip MeetSound;

	private int mIndex;

	private NpcMeetStatus mMeetStatus;

	private NpcRosterItem mNpcDataWrap;

	private List<NpcRosterItem> mNpcDataWrapList;


	private IProfessionAssetOperation<Sprite> mIconHandle;

	private IProfessionAssetOperation<Sprite> mBgHandle;

	private bool mIsMeeting;



	protected override void OnEnable()
	{
		base.OnEnable();
		((UnityEvent)mDetailBtn.onClick).AddListener((UnityAction)OnDetailBtnClicked);
		((UnityEvent)mMeetButton.onClick).AddListener((UnityAction)OnMeetClick);
		services.NpcAdded += OnAddNewNpc;
		mIsMeeting = false;
	}

	protected override void OnDisable()
	{
		((UnityEvent)mDetailBtn.onClick).RemoveListener((UnityAction)OnDetailBtnClicked);
		((UnityEvent)mMeetButton.onClick).RemoveListener((UnityAction)OnMeetClick);
		if (services.MainPlayerAvailable)
		{
			services.NpcAdded -= OnAddNewNpc;
		}
		base.OnDisable();
	}

	private async void OnAddNewNpc(int npcId, NpcMeetType type)
    {
        if (mNpcDataWrap == null || mNpcDataWrap.NpcClassId != npcId) return;
        CancelMeetPresentation();
        mMeetLifetime = new CancellationTokenSource();
        CancellationToken token = mMeetLifetime.Token;
        mLockEffectSwitcher.SetState(LockStatus.Locking);
        mNpcDataWrap.MeetStatus = NpcMeetStatus.HadMeet;
        if (MeetSound != null) services.PostSound(MeetSound, gameObject);
        try { await UniTask.Delay(700, false, (PlayerLoopTiming)8, token, false); }
        catch (System.OperationCanceledException) { return; }
        if (token.IsCancellationRequested) return;
        mIsMeeting = false;
        Refresh();
        services.ShowFriendshipLevelUp(services.GetNpc(npcId), 0, 1);
    }

	public void Init(List<NpcRosterItem> dataList, int index)
    {
        InitWrapper(() =>
        {
            CancelMeetPresentation();
            mIsMeeting = false;
            mIndex = index;
            mNpcDataWrapList = dataList;
            mNpcDataWrap = dataList[index];
            mContentRoot.SetActive(false);
        });
    }

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
		RefreshFriendShip();
	}

	protected override void OnDisableAndInited()
	{
		ResetInitState();
		ReleaseHandles();
		base.OnDisableAndInited();
	}

	public override void ResetInitState()
	{
		CancelMeetPresentation();
		mNpcDataWrap = null;
		base.ResetInitState();
	}

	private void ReleaseHandles()
	{
		if (mIconHandle != null)
		{
			mIconHandle.Dispose();
			mIconHandle = null;
		}
		if (mBgHandle != null)
		{
			mBgHandle.Dispose();
			mBgHandle = null;
		}
	}

	private void Refresh()
	{
		ReleaseHandles();
		((Component)mCollectionImage).gameObject.SetActive(false);
		if (services.CanPinNpc)
		{
			((Component)mCollectionImage).gameObject.SetActive(mNpcDataWrap.IsCollected);
		}
		PartnerNpcDefinition npcInfo = services.GetDefinition(mNpcDataWrap.NpcClassId);
		UpdateRoleName(npcInfo.NpcRoleType, npcInfo.NameKey);
		mIconHandle = services.LoadSprite(npcInfo.FigureImage);
		mIcon.sprite = mIconHandle.Result;
		mIconDark.sprite = mIcon.sprite;
		mBgHandle = services.LoadSprite(npcInfo.SmallBgImage);
		mBgImage.sprite = mBgHandle.Result;
		mBgImageDark.sprite = mBgImage.sprite;
		mMeetStatus = services.MeetStatusOf(npcInfo.Id);
		services.BindNpcTipDot(mTipDotNodeLinker, npcInfo.Id);
		mNpcRoleStateSwitcher.SetState(npcInfo.NpcRoleType);
		mNpcMeetStateSwitcher.SetState(mMeetStatus);
		mLockEffectSwitcher.SetState((mMeetStatus == NpcMeetStatus.CanMeet) ? LockStatus.WaitLock : LockStatus.Unlock);
		if (mMeetStatus != NpcMeetStatus.HadMeet)
		{
			PartnerMeetProgress? progress = services.MeetProgress(npcInfo.Id);
            if (!progress.HasValue)
            {
                mNpcMeetProgressItem.gameObject.SetActive(false);
                return;
            }
            mNpcMeetProgressItem.SetProgress(progress.Value.Current, progress.Value.Target);
            mNpcMeetProgressItem.gameObject.SetActive(true);
		}
	}

	private void UpdateRoleName(NpcRoleType role, string nameKey)
	{
		if (role == NpcRoleType.Special)
		{
			mSpecialName.SetKey(nameKey);
		}
		else
		{
			mCommonName.SetKey(nameKey);
		}
	}

	private void RefreshFriendShip()
	{
		mFriendshipLevel.Init(mNpcDataWrap.NpcClassId);
	}

	private async void OnMeetClick()
    {
        if (services.IsNormalNpcDisabled) { services.ShowError(53900); return; }
        if (mIsMeeting || mMeetStatus != NpcMeetStatus.CanMeet || mNpcDataWrap == null) return;
        mIsMeeting = true;
        int npcId = mNpcDataWrap.NpcClassId;
        bool applied = await services.MeetNpc(npcId);
        if (!applied && mNpcDataWrap != null && mNpcDataWrap.NpcClassId == npcId)
        {
            mIsMeeting = false;
            Refresh();
        }
    }

	private void OnDetailBtnClicked()
    {
        if (services.IsNormalNpcDisabled) { services.ShowError(53900); return; }
        if (mMeetStatus == NpcMeetStatus.WaitMeet || mMeetStatus == NpcMeetStatus.UnMeet)
        {
            PartnerNpcDefinition npcInfo = services.GetDefinition(mNpcDataWrap.NpcClassId);
            if (services.HasItemDefinition(npcInfo.ExclusiveGiftId))
                services.ShowExclusiveGift(npcInfo.ExclusiveGiftId, this);
        }
        else
        {
            if (services.MetNpcCount == 0) return;
            List<NpcRosterItem> met = mNpcDataWrapList.Where(npc => npc.MeetStatus == NpcMeetStatus.HadMeet).ToList();
            int index = met.IndexOf(mNpcDataWrap);
            if (index != -1) services.ShowNpcDetails(met, index);
        }
    }

	public void Hide()
	{
		mContentRoot.SetActive(false);
	}

	public void Show()
	{
		mContentRoot.SetActive(true);
	}
}

}
