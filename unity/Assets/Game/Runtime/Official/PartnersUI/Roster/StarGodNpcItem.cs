// Ported selected official client; see partner-star-god-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class StarGodNpcItem : UIBase
{
    private IPartnerStarGodUiServices services;
    private CancellationTokenSource mMeetLifetime;
    public void Configure(IPartnerStarGodUiServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));
    private void CancelMeetPresentation()
    {
        if (mMeetLifetime != null) { mMeetLifetime.Cancel(); mMeetLifetime.Dispose(); mMeetLifetime = null; }
    }

	[SerializeField]
	private WorldStateSwitcher<NpcMeetStatus> mNpcMeetStatusSwitcher;

	[SerializeField]
	private WorldStateSwitcher<bool> mIsMeetingStateSwitcher;

	[SerializeField]
	private TipDotNodeLinker mNpcTipDotLinker;

	[SerializeField]
	private Image mNpcImage;

	[SerializeField]
	private Image mCollectionImage;

	[SerializeField]
	private MultiLanguageText mNpcNameText;

	[SerializeField]
	private MultiLanguageText mNpcLocationText;

	[SerializeField]
	private MultiLanguageText mNpcLinklLevelText;

	[SerializeField]
	private Button mNpcClickBtn;

	[SerializeField]
	private List<ItemIcon> mWaitMeetItems;

	[SerializeField]
	private List<ItemIcon> mCanMeetItems;

	[SerializeField]
	private Animation mAnimation;

	[SerializeField]
	private StarGodNpcItemContent mStarGodNpcItemContent;

	[SerializeField]
	private float mPopMeetDelayFactor = 0.8f;

	[SerializeField]
	private AnimationClip mUnlockAnimation;

	[SerializeField]
	private ImageLocator mNpcImageLocator;

	public AudioClip MeetSound;

	private NpcRosterItem mNpcData;

	private readonly List<System.IDisposable> mNpcImageDisposableBag = new List<System.IDisposable>();

	private NpcMeetStatus mMeetStatus;


	public void Init(NpcRosterItem npcData)
	{
		if (npcData == null)
		{
			Debug.LogError("StarGodNpcItem init npcData is null!");
		}
		else
		{
			InitWrapper(() =>
			{
				mNpcData = npcData;
				services.BindNpcTipDot(mNpcTipDotLinker, mNpcData.NpcClassId);
			});
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();

		services.NpcAdded += OnAddNewNpc;
		services.NpcFriendshipLevelUpdated += OnNpcFriendshipLevelUpdate;
		mStarGodNpcItemContent.OnUnlockAnimTriggerEvent += ChangeStateToHadMeet;
		((UnityEvent)mNpcClickBtn.onClick).AddListener((UnityAction)OnItemClick);
		mIsMeetingStateSwitcher.SetState(state: false);
		UpdateUI();
	}

	protected override void OnDisableAndInited()
	{
		Clear();
		if (services.MainPlayerAvailable)
		{
			services.NpcAdded -= OnAddNewNpc;
			services.NpcFriendshipLevelUpdated -= OnNpcFriendshipLevelUpdate;
		}
		mStarGodNpcItemContent.OnUnlockAnimTriggerEvent -= ChangeStateToHadMeet;
		((UnityEvent)mNpcClickBtn.onClick).RemoveListener((UnityAction)OnItemClick);
		mNpcData = null;
		base.OnDisableAndInited();
	}

	private void OnNpcFriendshipLevelUpdate(int npcId, int fromLevel, int toLevel)
	{
		if (mNpcData.NpcClassId == npcId)
		{
			RefreshLinkLevel();
		}
	}

	private async void OnAddNewNpc(int npcId, NpcMeetType type)
    {
        if (mNpcData == null || mNpcData.NpcClassId != npcId) return;
        CancelMeetPresentation();
        mMeetLifetime = new CancellationTokenSource();
        CancellationToken token = mMeetLifetime.Token;
        mNpcData.MeetStatus = NpcMeetStatus.HadMeet;
        if (MeetSound != null) services.PostSound(MeetSound, gameObject);
        mIsMeetingStateSwitcher.SetState(true);
        mAnimation.Play(mUnlockAnimation.name);
        try { await UniTask.WaitForSeconds(mUnlockAnimation.length * mPopMeetDelayFactor, false, (PlayerLoopTiming)8, token, false); }
        catch (System.OperationCanceledException) { return; }
        if (token.IsCancellationRequested) return;
        NpcRosterItem byNpcClassId = services.GetNpc(npcId);
        if (mNpcData != byNpcClassId) mNpcData = byNpcClassId;
        services.ShowFriendshipLevelUp(byNpcClassId, 0, 1);
        mIsMeetingStateSwitcher.SetState(false);
    }

	public void ChangeStateToHadMeet()
	{
		UpdateUI();
	}

	private void Clear()
	{
		CancelMeetPresentation();
		foreach (var lease in mNpcImageDisposableBag) lease.Dispose();
		mNpcImageDisposableBag.Clear();
	}

	private void UpdateUI()
	{
		if (mNpcData == null)
		{
			return;
		}
		RefreshCollectionImg();
		PartnerStarGodNpcDefinition npcInfo = services.GetDefinition(mNpcData.NpcClassId);
		if (npcInfo != null)
		{
			mMeetStatus = services.MeetStatusOf(mNpcData.NpcClassId);
			mNpcMeetStatusSwitcher.SetState(mMeetStatus);
			if ((Object)(object)mNpcNameText != (Object)null)
			{
				mNpcNameText.SetKey(npcInfo.NameKey);
			}
			string locationKey = npcInfo.LocationDescKey;
			if (locationKey != null)
			{
				mNpcLocationText.SetKey(locationKey);
			}
			RefreshLinkLevel();
			if ((Object)(object)mNpcImage != (Object)null && !string.IsNullOrEmpty(npcInfo.StaticFullBodyImagePath))
			{
				IProfessionAssetOperation<Sprite> assetOperationHandle = services.LoadSprite(npcInfo.StaticFullBodyImagePath);
				mNpcImageDisposableBag.Add(assetOperationHandle);
				mNpcImage.sprite = assetOperationHandle.Result;
				mNpcImageLocator.Refresh();
			}
			RefreshMeetItems();
		}
	}

	private void RefreshLinkLevel()
	{
		if (!((Object)(object)mNpcLinklLevelText == (Object)null))
		{
			int num = ((mNpcData != null) ? mNpcData.FriendshipLevel : 0);
			mNpcLinklLevelText.SetKey("{0}", num);
		}
	}

	private void RefreshCollectionImg()
	{
		((Component)mCollectionImage).gameObject.SetActive(false);
		if (mNpcData != null && services.CanPinNpc)
		{
			((Component)mCollectionImage).gameObject.SetActive(mNpcData.IsCollected);
		}
	}

	private void OnItemClick()
	{
		if (mNpcData == null)
		{
			return;
		}
		if (services.IsStarGodNpcDisabled)
		{
			services.ShowError(53897);
		}
		else if (mMeetStatus == NpcMeetStatus.CanMeet)
		{
			services.RequestMeetNpc(mNpcData.NpcClassId);
		}
		else
		{
			if (mMeetStatus != NpcMeetStatus.HadMeet)
			{
				return;
			}
			if (!services.OwnedNpcs.Any())
			{
				return;
			}
			List<NpcRosterItem> hadMeetStarGodNpcList = services.OwnedNpcs.Where((NpcRosterItem npc) => services.GetDefinition(npc.NpcClassId)?.IsStarGodNpc ?? false).ToList();
			int index = hadMeetStarGodNpcList.FindIndex((NpcRosterItem npc) => npc.NpcClassId == mNpcData.NpcClassId);
			if (index != -1)
			{
				services.ShowStarGodDetails(hadMeetStarGodNpcList, index);
			}
		}
	}

	private void RefreshMeetItems()
	{
		List<ItemIcon> list = ((mMeetStatus == NpcMeetStatus.CanMeet) ? mCanMeetItems : mWaitMeetItems);
		if (list == null || list.Count == 0)
		{
			return;
		}
		int num = 0;
		List<(int, int)> meetNpcItems = services.GetMeetNpcItems(mNpcData.NpcClassId);
		if (meetNpcItems != null)
		{
			num = meetNpcItems.Count;
		}
		for (int i = 0; i < list.Count; i++)
		{
			bool flag = i < num;
			((Component)list[i]).gameObject.SetActive(flag);
			if (flag)
			{
				var (num2, num3) = meetNpcItems[i];
				services.PrepareItemIcon(list[i]);
				list[i].Init(num2);
				list[i].SetClickPopupInfoDialog();
				bool flag2 = services.GetItemAmount(num2) >= num3;
				list[i].SetUIGray(!flag2);
			}
		}
	}
}

}
