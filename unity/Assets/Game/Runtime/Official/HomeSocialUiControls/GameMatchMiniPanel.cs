// Original selected home matching UI; service and namespace bindings only.
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.WorldUI;
using CultureTextArg=Zjcs.Official.World.NativeLabelUI.Framework.CultureTextArg;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeSocialUiControls
{
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameMatchMiniPanel : MonoBehaviour
{
	private const string kLeftTimeShowFormatStr = "DateTimeFormat_mmss";

	[SerializeField]
	private GameObject mContentGo;

	[SerializeField]
	private MultiLanguageText mTitleText;

	[SerializeField]
	private MultiLanguageText mMatchTipText;

	[SerializeField]
	private MultiLanguageText mLeftTimeText;

	[SerializeField]
	private Button mCancelMatcBtn;

	[SerializeField]
	private List<TeamTargetCatalog> mTargetCatalog;

	private WaitForSeconds mWaitForSeconds;

	private Coroutine mCoroutine;

	private HomePanelDisposableBag mDisposableBag;

	public IHomeMatchServices Services { protected get; set; }

	protected virtual void Awake()
	{
		mWaitForSeconds = new WaitForSeconds(0.1f);
		if (mCancelMatcBtn != null)
		{
			mCancelMatcBtn.onClick.AddListener(OnCancelMatchClicked);
		}
	}

	protected virtual void OnDestory()
	{
		if (mCancelMatcBtn != null)
		{
			mCancelMatcBtn.onClick.RemoveListener(OnCancelMatchClicked);
		}
	}

	protected void OnEnable()
	{
		mDisposableBag = default;
		Services.Player.OnGameMatchUpdate += MainPlayer_OnGameMatchUpdate;
		mDisposableBag.Add(Services.SubscribeConfirmStart(Instance_OnGameMatchConfirmStart));
		RefreshUI();
	}

	protected void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnGameMatchUpdate -= MainPlayer_OnGameMatchUpdate;
		}
		mDisposableBag.Dispose();
		if (mCoroutine != null)
		{
			StopCoroutine(mCoroutine);
			mCoroutine = null;
		}
	}

	private void Instance_OnGameMatchConfirmStart()
	{
		HideContent();
	}

	private void MainPlayer_OnGameMatchUpdate()
	{
		RefreshUI();
	}

	private IEnumerator OnCounterTime()
	{
		while (true)
		{
			yield return mWaitForSeconds;
			if (Services.Player == null)
			{
				break;
			}
			if (Services.Player.IsInGameMatch)
			{
				IHomeMatchInfo gameMatchInfo = Services.Player.GameMatchInfo;
				DateTime matchStartTime = gameMatchInfo.MatchStartTime;
				double val = (Services.GetDateTime() - matchStartTime).TotalSeconds + (double)gameMatchInfo.AlreadyWaitSec;
				val = Math.Max(val, 0.0);
				DateTime minValue = DateTime.MinValue;
				string leftTimeText = minValue.AddSeconds(val).ToString(Services.GetText("DateTimeFormat_mmss"));
				SetLeftTimeText(leftTimeText);
			}
		}
		mCoroutine = null;
	}

	protected virtual void SetLeftTimeText(string timeTxt)
	{
		mLeftTimeText.SetKey(timeTxt);
	}

	private void RefreshUI()
	{
		if (CheckTeamTargetCatalogVaild())
		{
			IHomeMatchInfo gameMatchInfo = Services.Player.GameMatchInfo;
			if (mTitleText != null)
			{
				CultureTextArg cultureTextArg = new CultureTextArg(gameMatchInfo.MatchTarget.GetItemName());
				mTitleText.Format(cultureTextArg);
			}
			if (mCancelMatcBtn != null)
			{
				mCancelMatcBtn.gameObject.SetActive(Services.IsLeader);
			}
			TimeSpan timeSpan = gameMatchInfo.MatchTargetTime - gameMatchInfo.MatchStartTime;
			DateTime minValue = DateTime.MinValue;
			DateTime dateTime = minValue.AddSeconds(timeSpan.TotalSeconds + (double)gameMatchInfo.AlreadyWaitSec);
			mMatchTipText.Format(dateTime.ToString(Services.GetText("DateTimeFormat_mmss")));
			if (!mContentGo.activeSelf)
			{
				ShowContent();
			}
			if (mCoroutine == null)
			{
				mCoroutine = StartCoroutine(OnCounterTime());
			}
		}
		else
		{
			if (mContentGo.activeSelf)
			{
				HideContent();
			}
			if (mCoroutine != null)
			{
				StopCoroutine(mCoroutine);
				mCoroutine = null;
			}
		}
	}

	private void OnCancelMatchClicked()
	{
		if (Services.Player.IsInGameMatch && Services.IsLeader)
		{
			Services.OperPlayerStopGameMatch(OnCancelMatch);
		}
	}

	private void OnCancelMatch(object obj)
	{
		HideContent();
	}

	protected virtual void HideContent()
	{
		mContentGo.SetActive(value: false);
	}

	protected virtual void ShowContent()
	{
		mContentGo.SetActive(value: true);
	}

	private bool CheckTeamTargetCatalogVaild()
	{
		IHomeMatchPlayer mainPlayer = Services.Player;
		if (!mainPlayer.IsInGameMatch)
		{
			return false;
		}
		if (mTargetCatalog == null)
		{
			return true;
		}
		if (mTargetCatalog.Contains(TeamTargetCatalog.All))
		{
			return true;
		}
		if (mTargetCatalog.Contains(mainPlayer.GameMatchInfo.MatchTarget.TargetCatalog))
		{
			return true;
		}
		return false;
	}
}
}
