using TMPro;
// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
[ExecuteAlways]
[DisallowMultipleComponent]
public class DamageAutoSizeTextBack : MonoBehaviour
{
	public enum AlignMode
	{
		Left,
		Right
	}

	[Header("References")]
	[SerializeField]
	private SpriteRenderer mBackground;

	[SerializeField]
	private MultiLanguageText mText;

	[Header("Layout")]
	[SerializeField]
	private AlignMode mAlignMode = AlignMode.Right;

	[Header("Padding (World Units)")]
	[SerializeField]
	private float mPaddingX = 0.5f;

	[SerializeField]
	private float mPaddingY = 0.3f;

	[Header("Size Constraints")]
	[SerializeField]
	private float mMinWidth;

	[SerializeField]
	private float mMaxWidth;

	[SerializeField]
	private float mMinHeight;

	[SerializeField]
	private float mMaxHeight;

	[Header("Options")]
	[SerializeField]
	private bool mAutoUpdateInEditor = true;

	private float mAnchorLocalX;

	private TMP_Text mTMPText;

	private void Awake()
	{
		if ((Object)(object)mText != (Object)null)
		{
			mText.OnTextRefresh += OnRefresh;
		}
		mTMPText = ((Component)mText).GetComponent<TMP_Text>();
		CacheAnchorEdge();
	}

	private void OnDestroy()
	{
		if ((Object)(object)mText != (Object)null)
		{
			mText.OnTextRefresh -= OnRefresh;
		}
	}

	private void CacheAnchorEdge()
	{



		if (!((Object)(object)mBackground == (Object)null))
		{
			float num = mBackground.size.x * 0.5f * ((Component)mBackground).transform.localScale.x;
			float x = ((Component)mBackground).transform.localPosition.x;
			if (mAlignMode == AlignMode.Right)
			{
				mAnchorLocalX = x + num;
			}
			else
			{
				mAnchorLocalX = x - num;
			}
		}
	}

	public void OnRefresh()
	{

















		if ((Object)(object)mBackground == (Object)null || (Object)(object)mTMPText == (Object)null)
		{
			return;
		}
		mTMPText.ComputeMarginSize();
		Vector2 preferredValues = mTMPText.GetPreferredValues();
		Vector3 lossyScale = mTMPText.transform.lossyScale;
		float num = preferredValues.x * lossyScale.x;
		float num2 = preferredValues.y * lossyScale.y;
		float num3 = num + mPaddingX * 2f;
		float num4 = num2 + mPaddingY * 2f;
		if (num3 < mMinWidth)
		{
			num3 = mMinWidth;
		}
		if (mMaxWidth > 0f && num3 > mMaxWidth)
		{
			num3 = mMaxWidth;
		}
		if (num4 < mMinHeight)
		{
			num4 = mMinHeight;
		}
		if (mMaxHeight > 0f && num4 > mMaxHeight)
		{
			num4 = mMaxHeight;
		}
		Vector3 lossyScale2 = ((Component)mBackground).transform.lossyScale;
		if (lossyScale2.x != 0f && lossyScale2.y != 0f)
		{
			float num5 = num3 / lossyScale2.x;
			float num6 = num4 / lossyScale2.y;
			mBackground.size = new Vector2(num5, num6);
			float num7 = num5 * 0.5f * ((Component)mBackground).transform.localScale.x;
			Vector3 localPosition = ((Component)mBackground).transform.localPosition;
			if (mAlignMode == AlignMode.Right)
			{
				localPosition.x = mAnchorLocalX - num7;
			}
			else
			{
				localPosition.x = mAnchorLocalX + num7;
			}
			((Component)mBackground).transform.localPosition = localPosition;
		}
	}
}
}
