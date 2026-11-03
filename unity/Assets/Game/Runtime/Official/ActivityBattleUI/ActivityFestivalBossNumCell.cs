// Migrated selected official activity selector; see activity-selector-runtime-source-manifest.json.
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.ActivityBattleUI
{
using System;
using Zjcs.Official.SkillsUI.FancyScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ActivityFestivalBossNumCell : FancyCell<int, ActivityFestivalBossNumContext>
{
	public enum Status
	{
		SelectAndUnlock,
		SelectAndLock,
		UnselectAndUnlock,
		UnselectAndLock
	}

	[Serializable]
	public class StatusData
	{
		public Color TextColor;

		public Color EffectColor;

		public bool ShowEffect;
	}

	[SerializeField]
	private TMPEffect mTMPEffect;

	[SerializeField]
	private RectTransform mEffectRoot;

	[SerializeField]
	private Image mLockImg;

	[SerializeField]
	private MultiLanguageText mNumTxt;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private GameObject mRedDot;

	[SerializeField]
	private Animator mAnimator;

	[SerializeField]
	private string mAnimName;

	[SerializeField]
	private float mEffectThreshold;

	[SerializeField]
	private float mEffectSoundThreshold;

	[SerializeField]
	private MonoDictionary<Status, StatusData> mStatusDataDict;

	[SerializeField]
	private WorldStateSwitcher<bool> mUnlockStateSwitcher;

	private RectTransform mRectTrans;

	private float mCurrentPosition;

	private void Awake()
	{
		mRectTrans = ((Component)((Component)this).transform.parent).GetComponent<RectTransform>();
	}

	private void OnEnable()
	{
		if (Context != null)
		{
			UpdatePosition(mCurrentPosition);
		}
	}

	public override void Initialize()
	{
		((UnityEvent)mButton.onClick).AddListener((UnityAction)(() =>
		{
			Context.OnCellClicked?.Invoke(Index);
		}));
	}

	public override void UpdateContent(int index)
	{
		mNumTxt.Format(index + 1);
		mRedDot.SetActive(Context.ShowRedDotDelegate(Index) && Context.SelectedIndex != Index);
		bool state = Context.IsUnlockedDelegate(Index);
		mUnlockStateSwitcher.SetState(state);
	}

	public override void UpdatePosition(float position)
	{
		mCurrentPosition = position;
		if (((Behaviour)mAnimator).isActiveAndEnabled)
		{
			mAnimator.Play(mAnimName, -1, position);
		}
		mAnimator.speed = 0f;
		Transform transform = ((Component)this).transform;
		Rect rect = mRectTrans.rect;
		transform.localPosition = new Vector3(Mathf.Lerp(0f, rect.width, position), 0f, 0f);
		float num = Mathf.Abs(position - 0.5f) * 100f;
		if (Context.DataUpdateCompleted && num <= mEffectSoundThreshold && Context.PreSelectedIndex != Index)
		{
			Context.PreSelectedIndex = Index;
			Context.OnCellPreSelected?.Invoke();
		}
		bool selected = num <= mEffectThreshold;
		bool isUnlock = Context.IsUnlockedDelegate(Index);
		Status status = GetStatus(selected, isUnlock);
		if (!mStatusDataDict.TryGetValue(status, out var value))
		{
			Debug.LogError("ActivityFestivalBossNumCell Not Support Status:" + status);
			return;
		}
		mNumTxt.Color = value.TextColor;
		((Graphic)mLockImg).color = value.TextColor;
		if (value.ShowEffect)
		{
			mTMPEffect.outlineColor = value.EffectColor;
			((Behaviour)mTMPEffect).enabled = true;
			mTMPEffect.SetupEffect(true);
		}
		else
		{
			((Behaviour)mTMPEffect).enabled = false;
		}
	}

	private Status GetStatus(bool selected, bool isUnlock)
	{
		if (selected)
		{
			if (isUnlock)
			{
				return Status.SelectAndUnlock;
			}
			return Status.SelectAndLock;
		}
		if (isUnlock)
		{
			return Status.UnselectAndUnlock;
		}
		return Status.UnselectAndLock;
	}
}

}
