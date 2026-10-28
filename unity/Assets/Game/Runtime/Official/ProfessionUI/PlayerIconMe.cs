namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class PlayerIconMe : UIBase
{
	public enum MeInfoType
	{
		Detail
	}

	public PlayerInfoUI InfoUI;

	public bool AutoInit;

	public MeInfoType PopInfoType;

	public Component IconMeHpSlider;

	public int PanelPopupSceneType;

    private IProfessionPlayerInfoBindings mBindings;
    private IProfessionPlayerInfoBindings InfoBindings => mBindings ?? ProfessionPlayerInfoBindings.Required;
    public void ConfigureInfo(IProfessionPlayerInfoBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	private int mStatus;

	private object mPlayerLiteInfo;

	private IProfessionInfoTarget mTargetData;

	public bool InitFinished { get; set; }

	private void Reset()
	{
		InfoUI = ((Component)this).GetComponent<PlayerInfoUI>();
	}

	protected override void Start()
	{
		base.Start();
		if (AutoInit)
		{
			Init();
		}
	}

	public void Init(object playerLiteInfo = null, IProfessionInfoTarget actorTargetData = null, bool isNameWithPlanesName = false)
	{
		InitWrapper(() =>
		{
			mPlayerLiteInfo = playerLiteInfo;
			if ((Object)(object)IconMeHpSlider != (Object)null)
			{
				((IProfessionPlayerInfoHpSlider)IconMeHpSlider).Init();
			}
			InfoUI.IsNameWithPlanesName = isNameWithPlanesName;
			InitFinished = true;
		});
	}

	public override void ResetInitState()
	{
		if ((Object)(object)InfoUI != (Object)null && (Object)(object)InfoUI.PlayerHeadPortrait != (Object)null)
		{
			((IProfessionPlayerInfoHead)InfoUI.PlayerHeadPortrait).ResetInitState();
		}
		base.ResetInitState();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		InfoBindings.OnPlayerUpdate.AddListener(InitByPlayer);
		InitByPlayer();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		InitFinished = false;
		if (InfoBindings.ManagerAvailable)
		{
			InfoBindings.OnPlayerUpdate.RemoveListener(InitByPlayer);
		}
		if (InfoBindings.MainPlayer != null)
		{
			InfoBindings.MainPlayer.OnNameUpdate -= MainPlayer_OnNameUpdate;
			InfoBindings.MainPlayer.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
			InfoBindings.MainPlayer.OnLevelUpdate -= MainPlayer_OnLevelUpdate;
			InfoBindings.MainPlayer.OnCombatRatingChanged -= MainPlayer_OnCombatRatingChanged;
			InfoBindings.MainPlayer.OnProfessionChange -= MainPlayer_OnProfessionChange;
			InfoBindings.MainPlayer.OnProfessionPromote -= MainPlayer_OnProfessionPromote;
			InfoBindings.MainPlayer.OnAppearanceUpdate -= MainPlayer_OnAppearanceUpdate;
			InfoBindings.MainPlayer.OnIconUpdate -= MainPlayer_OnIconUpdate;
			InfoBindings.MainPlayer.OnSexUpdate -= MainPlayer_OnSexUpdate;
		}
	}

	private void InitByPlayer()
	{
		if (InfoBindings.MainPlayer != null)
		{
			if (mPlayerLiteInfo == null)
			{
				InfoUI.Init(InfoBindings.BuildData(InfoBindings.MainPlayer), HandleOnClick);
			}
			else
			{
				InfoUI.Init(InfoBindings.BuildLiteData(mPlayerLiteInfo, mTargetData), HandleOnClick);
			}
			InfoUI.Refresh();
			InfoBindings.MainPlayer.OnNameUpdate += MainPlayer_OnNameUpdate;
			InfoBindings.MainPlayer.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
			InfoBindings.MainPlayer.OnLevelUpdate += MainPlayer_OnLevelUpdate;
			InfoBindings.MainPlayer.OnCombatRatingChanged += MainPlayer_OnCombatRatingChanged;
			InfoBindings.MainPlayer.OnProfessionChange += MainPlayer_OnProfessionChange;
			InfoBindings.MainPlayer.OnProfessionPromote += MainPlayer_OnProfessionPromote;
			InfoBindings.MainPlayer.OnAppearanceUpdate += MainPlayer_OnAppearanceUpdate;
			InfoBindings.MainPlayer.OnIconUpdate += MainPlayer_OnIconUpdate;
			InfoBindings.MainPlayer.OnSexUpdate += MainPlayer_OnSexUpdate;
			InitFinished = true;
		}
	}

	private void MainPlayer_OnIconUpdate()
	{
		InfoUI.SetPlayerIcon(InfoBindings.MainPlayer.Icon);
	}

	private void MainPlayer_OnAppearanceUpdate(AppearanceOperateSource operateSource)
	{
		InfoUI.SetAppearance(InfoBindings.MainPlayer.AppearanceData);
	}

	private void MainPlayer_OnLevelUpdate(int level)
	{
		InfoUI.SetLevelText(level);
	}

	private void MainPlayer_OnSubRankUpdate(int subRank)
	{
		InfoUI.SetSubRankText(subRank);
	}

	private void MainPlayer_OnNameUpdate()
	{
		InfoUI.SetNameText(InfoBindings.MainPlayer.Name);
	}

	private void MainPlayer_OnCombatRatingChanged(long from, long to)
	{
		InfoUI.SetCombatRatingText(to);
	}

	private void MainPlayer_OnProfessionPromote(int profession)
	{
		InfoUI.SetProfessionIcon(profession);
	}

	private void MainPlayer_OnProfessionChange(int from, int to)
	{
		InfoUI.SetProfessionIcon(to);
	}

	private void MainPlayer_OnSexUpdate(Sex sex)
	{
		InfoUI.SetSex(sex);
	}

	public void HandleOnClick()
	{
		if (InitFinished)
		{
			_ = PopInfoType;
			ShowCurrentDetailInfoPanel(InfoBindings.DuelSkillGroup(-1L, -1L));
		}
	}

	private void ShowCurrentDetailInfoPanel(int skillPlanUsedPos)
	{
		InfoBindings.Popup(this, panel =>
		{
			panel.Init(InfoBindings.BuildBriefInfo(InfoBindings.MainPlayer, online: true), PanelPopupSceneType, skillPlanUsedPos);
		});
	}

	public void SetStatus(int status)
	{
		if (mStatus != status)
		{
			mStatus = status;
			MainPlayer_OnIconUpdate();
		}
	}

	public void SetNameTextActive(bool isActive)
	{
		InfoUI.SetNameTextActive(isActive);
	}
}

}
