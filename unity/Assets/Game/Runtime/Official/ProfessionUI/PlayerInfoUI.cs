namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerInfoUI : MonoBehaviour
{
	public WorldMultiLanguageText NameText;

	public WorldMultiLanguageText SubRankText;

	public WorldMultiLanguageText LevelText;

	public WorldMultiLanguageText CombatRatingText;

	public GameObject ProfessionIconGo;

	public Image ProfessionIcon;

	public Image ProfessionBigIcon;

	public WorldMultiLanguageText ProfessionName;

	public Image PlayerIcon;

	public Image PlayerFigure;

	public SpriteRenderer PlayerIconSprite;

	public PlayerAppearance PlayerAppearance;

	public Image CollectionImg;

	public Image PinMask;

	public Component PlayerAppearanceScene;

	public GameObject NpcAppearanceSlot;

	public Component PlayerHeadPortrait;

	public Button Btn;

	public Component HonorTitleItemProvider;

	public WorldStateSwitcher<ActorTargetType> mStateSwitcher;

	public Component PlanesIconItem;

	public bool UseUISpine = true;

	[SerializeField]
	private WorldStateSwitcher<bool> mIsMyselfSwitcher;

	public Action OnClickHandler;

	private PlayerInfoData mPlayerInfoData;

	private bool mShowNpcCollectionImg;

	private ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();

	private GameObject mNpcModel;

    private IProfessionPlayerInfoBindings mBindings;
    private IProfessionPlayerInfoBindings InfoBindings => mBindings ?? ProfessionPlayerInfoBindings.Required;
    public void ConfigureInfo(IProfessionPlayerInfoBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public PlayerInfoData PlayerInfoData => mPlayerInfoData;

	public bool IsNameWithPlanesName { get; set; }

	private void Awake()
	{

		if ((Object)(object)Btn != (Object)null)
		{
			((UnityEvent)Btn.onClick).AddListener((UnityAction)HandleOnClick);
		}
	}

	private void OnDisable()
	{
		if ((Object)(object)HonorTitleItemProvider != (Object)null)
		{
			((IProfessionPlayerInfoTitle)HonorTitleItemProvider).ClearTitleItem();
		}
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
		DestroyNpcModel();
	}

	public void Init(PlayerInfoData playerInfoData, Action onClickHandler)
	{
		mPlayerInfoData = playerInfoData;
		OnClickHandler = onClickHandler;
	}

	public void SetShowCollectionImg(bool isShow)
	{
		mShowNpcCollectionImg = isShow;
		RefreshImgCollection();
		RefreshPinMask();
	}

	public void Refresh()
	{
		RefreshLevelText();
		RefreshSubRankText();
		RefreshNameText();
		RefreshCombatRatingText();
		RefreshPlayerIcon();
		RefreshProfessionIcon();
		RefreshAppearance();
		RefreshHonorTitle();
		RefreshImgCollection();
		RefreshPinMask();
	}

	public void SetPlayerIcon(string icon)
	{
		mPlayerInfoData.Icon = icon;
		RefreshPlayerIcon();
	}

	public void SetAppearance(object appearanceData)
	{
		mPlayerInfoData.AppearanceData = appearanceData;
		RefreshAppearance();
		RefreshHonorTitle();
	}

	public void SetLevelText(int level)
	{
		mPlayerInfoData.Level = level;
		RefreshLevelText();
	}

	public void SetSubRankText(int subRank)
	{
		mPlayerInfoData.SubRank = subRank;
		RefreshSubRankText();
	}

	public void SetNameText(string name)
	{
		mPlayerInfoData.Name = name;
		mPlayerInfoData.NameArg = InfoBindings.CreateNameArg(mPlayerInfoData.Name, mPlayerInfoData.TargetData.IsNpc);
		RefreshNameText();
	}

	public void SetNameTextActive(bool isActive)
	{
		((Component)NameText).gameObject.SetActive(isActive);
	}

	public void SetCombatRatingText(long combatRating)
	{
		mPlayerInfoData.CombatRating = combatRating;
		RefreshCombatRatingText();
	}

	public void SetProfessionIcon(int profession)
	{
		mPlayerInfoData.Profession = profession;
		RefreshProfessionIcon();
		RefreshPinMask();
	}

	public void SetSex(Sex sex)
	{
		mPlayerInfoData.Sex = sex;
	}

	private void RefreshPlayerIcon()
	{
		mStateSwitcher.SetState(mPlayerInfoData.TargetData.TargetType);
		if (!((Object)(object)PlayerFigure != (Object)null))
		{
			return;
		}
		if (mPlayerInfoData.TargetData.TargetType == ActorTargetType.Player)
		{
			string assetGuid = InfoBindings.GetFullBodyGuid(mPlayerInfoData.Profession, mPlayerInfoData.Sex);
			PlayerFigure.sprite = mDisposableBag.Add(InfoBindings.Resources.LoadAssetByGuid<Sprite>(assetGuid)).Result;
			return;
		}
		string figurePath = mPlayerInfoData.TargetData.GetFigurePath(mPlayerInfoData.Sex);
		if (!string.IsNullOrEmpty(figurePath))
		{
			PlayerFigure.sprite = mDisposableBag.Add(InfoBindings.Resources.LoadAsset<Sprite>(figurePath)).Result;
		}
	}

	private bool IsMySelf()
	{
		bool result = false;
		if (InfoBindings.MainPlayer != null)
		{
			result = mPlayerInfoData.TargetData.TargetId == InfoBindings.MainPlayer.Id;
		}
		return result;
	}

	private void RefreshAppearance()
	{
		bool flag = IsMySelf();
		mIsMyselfSwitcher.SetState(flag);
		ActorTargetType targetType = mPlayerInfoData.TargetData.TargetType;
		mStateSwitcher.SetState(targetType);
		if (targetType != ActorTargetType.Player && targetType == ActorTargetType.Npc)
		{
			DestroyNpcModel();
			LoadNpcModel();
			if ((Object)(object)PlayerIcon != (Object)null)
			{
				PlayerIcon.sprite = mDisposableBag.Add(InfoBindings.Resources.LoadAsset<Sprite>(mPlayerInfoData.Icon)).Result;
			}
			return;
		}
		if ((!flag || (Object)(object)PlayerAppearance == (Object)null) && (Object)(object)PlayerHeadPortrait != (Object)null)
		{
			RefreshHeadPortrait();
			return;
		}
		if (!UseUISpine && (Object)(object)PlayerAppearanceScene != (Object)null)
		{
			PlayerInfoData playerInfoData = mPlayerInfoData;
			((IProfessionPlayerInfoScene)PlayerAppearanceScene).RefreshAppearance(playerInfoData.TargetData.TargetId, playerInfoData.Sex, playerInfoData.Profession, playerInfoData.AppearanceData);
		}
		if (UseUISpine && (Object)(object)PlayerAppearance != (Object)null)
		{
			PlayerInfoData playerInfoData2 = mPlayerInfoData;
			PlayerAppearance.RefreshAppearance(playerInfoData2.TargetData.TargetId, playerInfoData2.Sex, playerInfoData2.Profession, playerInfoData2.AppearanceData);
		}
	}

	private void RefreshHonorTitle()
	{
		if (!((Object)(object)HonorTitleItemProvider == (Object)null))
		{
			if (!IsShowHonorTitle())
			{
				((IProfessionPlayerInfoTitle)HonorTitleItemProvider).ClearTitleItem();
			}
			else
			{
				((IProfessionPlayerInfoTitle)HonorTitleItemProvider).Init(mPlayerInfoData.AppearanceData);
			}
		}
	}

	private bool IsShowHonorTitle()
	{
		if (mPlayerInfoData.TargetData.TargetType != ActorTargetType.Player)
		{
			return false;
		}
		object appearanceData = mPlayerInfoData.AppearanceData;
		if (appearanceData == null || !InfoBindings.HasHonorTitle(appearanceData))
		{
			return false;
		}
		return true;
	}

	private void LoadNpcModel()
	{
		if ((Object)(object)NpcAppearanceSlot != (Object)null)
		{
			int npcClassId = ((IProfessionInfoNpcTarget)mPlayerInfoData.TargetData).NpcClassId;
			ProfessionInfoNpcDefinition npcInfo = InfoBindings.GetNpc(npcClassId);
			GameObject result = mDisposableBag.Add(InfoBindings.Resources.LoadAsset<GameObject>(npcInfo.FigureModelUI)).Result;
			mNpcModel = InfoBindings.InstantiateKeepScale(result, NpcAppearanceSlot.transform);
			InfoBindings.OverrideNpcInitialAnimation(mNpcModel, npcInfo.Id);
		}
	}

	private void DestroyNpcModel()
	{
		if ((Object)(object)mNpcModel != (Object)null)
		{
			Object.Destroy((Object)(object)mNpcModel);
			mNpcModel = null;
		}
	}

	private void RefreshHeadPortrait()
	{
		if (!((Object)(object)PlayerHeadPortrait != (Object)null))
		{
			return;
		}
		try
		{
			PlayerInfoData playerInfoData = mPlayerInfoData;
			((IProfessionPlayerInfoHead)PlayerHeadPortrait).GeneratePortrait(playerInfoData.Id, playerInfoData.Sex, playerInfoData.Profession, playerInfoData.AppearanceData);
		}
		catch (Exception exception)
		{
			Debug.LogError("肖像生成失败. 玩家ID为" + mPlayerInfoData.Id + "\n" + exception);
		}
	}

	private void RefreshLevelText()
	{
		if ((Object)(object)LevelText != (Object)null)
		{
			LevelText.Format(mPlayerInfoData.Level);
		}
	}

	private void RefreshSubRankText()
	{
		if ((Object)(object)SubRankText != (Object)null)
		{
			SubRankText.SetKey(InfoBindings.SubRankKey(mPlayerInfoData.SubRank));
		}
	}

	private void RefreshNameText()
	{
		if ((Object)(object)NameText == (Object)null)
		{
			return;
		}
		if (IsNameWithPlanesName)
		{
			if ((Object)(object)PlanesIconItem != (Object)null)
			{
				((IProfessionPlayerInfoPlanesIcon)PlanesIconItem).Init(mPlayerInfoData.TargetData.ServerId);
				NameText.SetKey("{0}", mPlayerInfoData.NameArg);
			}
			else
			{
				ProfessionInfoText playerNameWithPlanesNameText = InfoBindings.NameWithPlanes(mPlayerInfoData.NameArg, mPlayerInfoData.TargetData.ServerId);
				NameText.SetKey(playerNameWithPlanesNameText.Key, playerNameWithPlanesNameText.Args);
			}
		}
		else
		{
			NameText.SetKey("{0}", mPlayerInfoData.NameArg);
		}
		if ((Object)(object)PlanesIconItem != (Object)null)
		{
			((Component)PlanesIconItem).gameObject.SetActive(IsNameWithPlanesName && !IsMySelf());
		}
	}

	private void RefreshCombatRatingText()
	{
		if ((Object)(object)CombatRatingText != (Object)null)
		{
			CombatRatingText.SetKey("{0}", InfoBindings.CompactNumber(mPlayerInfoData.CombatRating));
		}
	}

	private void RefreshProfessionIcon()
	{
		if ((Object)(object)ProfessionIcon != (Object)null || (Object)(object)ProfessionBigIcon != (Object)null)
		{
			IProfessionPlayerInfoBindings professionBaseInfoParser = InfoBindings;
			int profession = mPlayerInfoData.Profession;
			bool hasProfession = profession != 0;
			SetFirstProfessionIcon(professionBaseInfoParser, profession, hasProfession);
			SetThirdProfessionIcon(professionBaseInfoParser, profession, hasProfession);
		}
		if ((Object)(object)ProfessionName != (Object)null)
		{
			ProfessionName.SetKey(InfoBindings.ProfessionNameKey(mPlayerInfoData.Profession));
		}
	}

	private void RefreshImgCollection()
	{
		if (!((Object)(object)CollectionImg == (Object)null))
		{
			((Component)CollectionImg).gameObject.SetActive(ShouldShowCollectionImg());
		}
	}

	private bool ShouldShowCollectionImg()
	{
		if (InfoBindings.MainPlayer == null)
		{
			return false;
		}
		IProfessionInfoNpcTarget npcTargetData = mPlayerInfoData.TargetData as IProfessionInfoNpcTarget;
		if (npcTargetData == null)
		{
			return false;
		}
		if (InfoBindings.MainPlayer.IsNpcPinLimited)
		{
			return false;
		}
		if (!mShowNpcCollectionImg)
		{
			return false;
		}
		return IsCollectedNpc(npcTargetData.NpcClassId);
	}

	private bool IsCollectedNpc(int npcClassId)
	{
		return InfoBindings.MainPlayer.IsNpcCollected(npcClassId) ?? false;
	}

	private void RefreshPinMask()
	{
		if (!((Object)(object)PinMask == (Object)null))
		{
			if ((Object)(object)CollectionImg == (Object)null || !ShouldShowCollectionImg())
			{
				((Component)PinMask).gameObject.SetActive(false);
			}
			else
			{
				((Component)PinMask).gameObject.SetActive(ShouldShowPinMask());
			}
		}
	}

	private bool ShouldShowPinMask()
	{
		TeamTargetCatalog? teamTargetCatalog = InfoBindings.TeamCatalog(this);
		if (teamTargetCatalog == TeamTargetCatalog.JointStrike)
		{
			return !InfoBindings.CanJointTogether(mPlayerInfoData.Profession, InfoBindings.MainPlayer.Profession);
		}
		if (teamTargetCatalog == TeamTargetCatalog.ExploreBossTeam)
		{
			return IsExploreBossAssistOnCd();
		}
		return false;
	}

	private bool IsExploreBossAssistOnCd()
	{
		ProfessionInfoRobotInvite playerRobotInvite = InfoBindings.RobotInvite(this);
		if (playerRobotInvite == null)
		{
			playerRobotInvite = InfoBindings.DefaultRobotInvite();
		}
		if (playerRobotInvite.OnePlayerMaxInviteCount <= 0)
		{
			return false;
		}
		return !mPlayerInfoData.TargetData.CheckActorCanAssist(playerRobotInvite.PlayType);
	}

	private void SetThirdProfessionIcon(IProfessionPlayerInfoBindings parser, int profession, bool hasProfession)
	{
		if ((Object)(object)ProfessionBigIcon != (Object)null)
		{
			((Component)ProfessionBigIcon).gameObject.SetActive(hasProfession);
			if (hasProfession)
			{
				string thirdIconPath = parser.ThirdProfessionIcon(profession);
				ProfessionBigIcon.sprite = mDisposableBag.Add(InfoBindings.Resources.LoadAsset<Sprite>(thirdIconPath)).Result;
			}
		}
	}

	private void SetFirstProfessionIcon(IProfessionPlayerInfoBindings parser, int profession, bool hasProfession)
	{
		if ((Object)(object)ProfessionIcon != (Object)null && (Object)(object)ProfessionIconGo != (Object)null)
		{
			ProfessionIconGo.SetActive(hasProfession);
			if (hasProfession)
			{
				string firstIconPath = parser.FirstProfessionIcon(profession);
				ProfessionIcon.sprite = mDisposableBag.Add(InfoBindings.Resources.LoadAsset<Sprite>(firstIconPath)).Result;
			}
		}
	}

	private void HandleOnClick()
	{
		OnClickHandler?.Invoke();
	}

	public GameObject GetSceneModelGo()
	{
		if (mPlayerInfoData.TargetData.TargetType == ActorTargetType.Npc)
		{
			return mNpcModel;
		}
		return ((Component)PlayerAppearanceScene).gameObject;
	}

	public GameObject GetModelGo()
	{
		if (mPlayerInfoData.TargetData.TargetType == ActorTargetType.Npc)
		{
			return mNpcModel;
		}
		return ((Component)PlayerAppearance).gameObject;
	}
}

}
