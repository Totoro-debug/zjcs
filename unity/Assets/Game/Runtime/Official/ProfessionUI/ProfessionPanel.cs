namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionPanel : UIBase
{
    private IProfessionDetailBindings mBindings;
    private IProfessionDetailBindings Bindings => mBindings ?? ProfessionDetailBindings.Required;
    public void Configure(IProfessionDetailBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	private enum PanelState
	{
		InfoPanel,
		UpgradePanelNormal,
		UpgradePanelReachMax
	}

	public Component UpgradePanel;

	public Component InfoPanel;

	public WorldTabGroup ToggleGroup;

	public WorldTabItem InfoToggle;

	public WorldTabItem UpgradeToggle;

	public TipDotNodeLinker TipDotLinker;

	public GameObject PropItemPrefab;

	[Header("通用信息")]
	public Image FrontProfessionIcon;

	public Image BackProfessionIcon;

	public ImageLocator MaleRole;

	public ImageLocator FemaleRole;

	public WorldMultiLanguageText LevelText;

	public WorldMultiLanguageText NextLevelText;

	public WorldMultiLanguageText ProfessionTitle;

	public Image BgImg;

	public Image SplitLineImg;

	private int mCurProfession;

	private GameObjectPool<ProfessionPlayerPropItem> mPool;

	private ProfessionSkillBadgeRewarder SkillBadgeRewarder;

	[SerializeField]
	private WorldStateSwitcher<PanelState> mStateSwitcher;

	[SerializeField]
	private ProfessionChangeJumpBtn mJumpBtn;

	private ProfessionResourceOperationBag mDisposableBag;



	private void Awake()
	{
		ToggleGroup.OnTabItemChanged.AddListener(OnSwitch);
		mPool = new GameObjectPool<ProfessionPlayerPropItem>(((Component)this).transform, PropItemPrefab.gameObject);
		SkillBadgeRewarder = new ProfessionSkillBadgeRewarder();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new ProfessionResourceOperationBag();
		SetProfession(mCurProfession);
		SkillBadgeRewarder.Configure(Bindings);
		SkillBadgeRewarder.Init(mCurProfession, this);
		((Component)mJumpBtn).gameObject.SetActive(mCurProfession != Bindings.MainPlayer.Profession);
		mJumpBtn.Configure(Bindings);
		mJumpBtn.SetProfession(mCurProfession);
		Bindings.MainPlayer.OnProfessionUpgrade += OnUpgrade;
	}

	protected override void OnDisableAndInited()
	{
		mDisposableBag.Dispose();
		if (Bindings.MainPlayer != null)
		{
			Bindings.MainPlayer.OnProfessionUpgrade -= OnUpgrade;
		}
		base.OnDisableAndInited();
	}

	public void Init(int pro)
	{
		InitWrapper(() =>
		{
			mCurProfession = pro;
		});
	}

	public void SetProfession(int pro)
	{
		mCurProfession = pro;
		FrontProfessionIcon.sprite = mDisposableBag.Add(Bindings.ResourceLoader.LoadAsset<Sprite>(Bindings.GetSecondIconPath(pro))).Result;
		BackProfessionIcon.sprite = FrontProfessionIcon.sprite;
		string assetGuid = Bindings.GetFullBodyGuid(pro, Sex.Male);
		string assetGuid2 = Bindings.GetFullBodyGuid(pro, Sex.Female);
		MaleRole.SetSprite(mDisposableBag.Add(Bindings.ResourceLoader.LoadAssetByGuid<Sprite>(assetGuid)).Result);
		FemaleRole.SetSprite(mDisposableBag.Add(Bindings.ResourceLoader.LoadAssetByGuid<Sprite>(assetGuid2)).Result);
		int professionLevel = Bindings.MainPlayer.GetProfessionLevel(pro);
		SetLevel(professionLevel);
		ProfessionTitle.SetKey(Bindings.GetProfessionNameKey(pro));
		((Graphic)BgImg).color = Bindings.GetPanelFadeColor(pro);
		((Graphic)SplitLineImg).color = Bindings.GetPanelLineColor(pro);
		ToggleGroup.ResetTabItem(needNotify: true, InfoToggle);
	}

	private void OnSwitch(WorldTabItem toggle, bool value)
	{
		if (value)
		{
			PanelState panelState = ((!((Object)(object)toggle == (Object)(object)InfoToggle)) ? PanelState.UpgradePanelNormal : PanelState.InfoPanel);
			if (panelState == PanelState.UpgradePanelNormal && CheckLevelMax())
			{
				panelState = PanelState.UpgradePanelReachMax;
			}
			SetState(panelState);
		}
	}

	private void SetState(PanelState state)
	{
		mStateSwitcher.SetState(state);
		TipDotLinker.SetNodeInstanceInfo(new ProfessionNodeInstanceInfo(mCurProfession));
		if (state == PanelState.InfoPanel)
		{
			((IProfessionDetailInfoPanel)InfoPanel).Init(mCurProfession, mPool);
		}
		else
		{
			((IProfessionDetailUpgradePanel)UpgradePanel).Init(mCurProfession);
		}
	}

	private void OnUpgrade(int profession, int level)
	{
		if (profession == mCurProfession)
		{
			SetLevel(level);
		}
	}

	private void SetLevel(int level)
	{
		if (mStateSwitcher.State == PanelState.UpgradePanelNormal && CheckLevelMax())
		{
			SetState(PanelState.UpgradePanelReachMax);
		}
		LevelText.Format(level);
		NextLevelText.Format(level + 1);
	}

	private bool CheckLevelMax()
	{
		int professionLevel = Bindings.MainPlayer.GetProfessionLevel(mCurProfession);
		return !Bindings.HasLevelCost(mCurProfession, professionLevel + 1);
	}
}

}
