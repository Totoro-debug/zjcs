// Ported from client-source/NumCell.cs. Dependency injection, Wwise and animator calls use local bindings.
using Zjcs.Official.SkillsUI.FancyScrollView;
using System;
using UnityEngine;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
public class NumCell : FancyCell<int, NumContext>
{
	[SerializeField]
	private RectTransform mEffectRoot;

	[SerializeField]
	private MultiLanguageText mNumTxt;

	[SerializeField]
	private Button mButton;

	[SerializeField]
	private GameObject mRedDot;

	[SerializeField] private AnimationCurve scaleX;
	[SerializeField] private AnimationCurve scaleY;
	[SerializeField] private AnimationCurve scaleZ;

	[SerializeField]
	private string mAnimName;

	[Header("表现相关")]
	[SerializeField]
	private float mEffectThreshold;

	[SerializeField]
	private float mEffectSoundThreshold;

	[SerializeField]
	private Color mSelectColor;

	[SerializeField]
	private Color mPassedColor;

	[SerializeField]
	private Color mLockedColor;

	private RectTransform mRectTrans;

	private float mCurrentPosition;

	private void Awake()
	{
		mRectTrans = ((Component)((Component)this).transform.parent).GetComponent<RectTransform>();
	}

	public override void Initialize()
	{

		((UnityEvent)mButton.onClick).AddListener((UnityAction)(() =>
		{
			Context.OnCellClicked?.Invoke(Index);
		}));
	}

	public override void UpdateContent(int num)
	{
		mNumTxt.SetKey("{0}", num.ToString("D2"));
		mRedDot.SetActive(Context.ShowRedDotDelegate(Index) && Context.SelectedIndex != Index);
	}

	public override void UpdatePosition(float position)
	{






		mCurrentPosition = position;
		mEffectRoot.localScale = new Vector3(scaleX.Evaluate(position), scaleY.Evaluate(position), scaleZ.Evaluate(position));
		Transform transform = ((Component)this).transform;
		Rect rect = mRectTrans.rect;
		transform.localPosition = new Vector3(Mathf.Lerp(0f, rect.width, position), 0f, 0f);
		float num = Mathf.Abs(position - 0.5f) * 100f;
		if (Context.DataUpdateCompleted && num <= mEffectSoundThreshold && Context.PreSelectedIndex != Index)
		{
			Context.PreSelectedIndex = Index;
			Context.OnCellPreSelected?.Invoke();
		}
		Color color;
		if (num <= mEffectThreshold)
		{
			color = mSelectColor;
		}
		else if (Context != null)
		{
			color = (Context.IsUnlockedDelegate(Index) ? mPassedColor : mLockedColor);
		}
		else
		{
			color = mLockedColor;
		}
		mNumTxt.Color = color;
	}

	private void OnEnable()
	{
		if (Context != null)
		{
			UpdatePosition(mCurrentPosition);
		}
	}
}

}
