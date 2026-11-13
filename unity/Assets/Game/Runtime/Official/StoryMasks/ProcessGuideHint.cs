// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

public class ProcessGuideHint : MonoBehaviour
{
	[SerializeField]
	private MultiLanguageText mHintTxt;

	[SerializeField]
	private RectTransform mRootTransform;

	[SerializeField]
	private GameObject mRootGo;

	[SerializeField]
	private RectTransform mHintBgTransform;

	[SerializeField]
	private float mHeadIconOffset = 120f;

	[SerializeField]
	private float mUpOffset = 30f;

	public void SetHint(string hintKey, params object[] args)
	{
		mHintTxt.SetKey(hintKey, args);
		ForceUpdateLayout();
	}

	private void ForceUpdateLayout()
	{
		mRootTransform.ForceUpdateRectTransforms();
		LayoutRebuilder.ForceRebuildLayoutImmediate(mRootTransform);
	}

	public void SetTransform(Vector2 targetScreenPos, Vector3 targetLocalPos, Rect targetRect)
	{

		Vector2 zero = Vector2.zero;
		if (targetScreenPos.x > zero.x && targetScreenPos.y > zero.y)
		{
			mRootTransform.anchorMin = new Vector2(1f, 1f);
			mRootTransform.anchorMax = new Vector2(1f, 1f);
			mRootTransform.pivot = new Vector2(0.5f + 0.5f * Mathf.Abs(targetLocalPos.x) / (float)(Screen.width / 2), 0.5f);
			((Transform)mRootTransform).localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y - targetRect.height / 2f - mHeadIconOffset);
			return;
		}
		if (targetScreenPos.x < zero.x && targetScreenPos.y > zero.y)
		{
			mRootTransform.anchorMin = new Vector2(0f, 1f);
			mRootTransform.anchorMax = new Vector2(0f, 1f);
			mRootTransform.pivot = new Vector2(0.5f * (1f - Mathf.Abs(targetLocalPos.x) / (float)(Screen.width / 2)), 0.5f);
			((Transform)mRootTransform).localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y - targetRect.height / 2f - mHeadIconOffset);
			return;
		}
		if (targetScreenPos.x < zero.x && targetScreenPos.y < zero.y)
		{
			mRootTransform.anchorMin = new Vector2(0f, 0f);
			mRootTransform.anchorMax = new Vector2(0f, 0f);
			mRootTransform.pivot = new Vector2(0.5f * (1f - Mathf.Abs(targetLocalPos.x) / (float)(Screen.width / 2)), 0.5f);
			((Transform)mRootTransform).localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y + targetRect.height / 2f + mHintBgTransform.sizeDelta.y / 2f + mUpOffset);
			return;
		}
		if (targetScreenPos.x > zero.x && targetScreenPos.y < zero.y)
		{
			mRootTransform.anchorMin = new Vector2(1f, 0f);
			mRootTransform.anchorMax = new Vector2(1f, 0f);
			mRootTransform.pivot = new Vector2(0.5f + 0.5f * Mathf.Abs(targetLocalPos.x) / (float)(Screen.width / 2), 0.5f);
			((Transform)mRootTransform).localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y + targetRect.height / 2f + mHintBgTransform.sizeDelta.y / 2f + mUpOffset);
			return;
		}
		mRootTransform.anchorMin = new Vector2(0.5f, 0.5f);
		mRootTransform.anchorMax = new Vector2(0.5f, 0.5f);
		mRootTransform.pivot = new Vector2(0.5f, 0.5f);
		if (targetLocalPos.y <= 0f)
		{
			((Transform)mRootTransform).localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y + targetRect.height / 2f + mHintBgTransform.sizeDelta.y / 2f + mUpOffset);
		}
		else
		{
			((Transform)mRootTransform).localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y - targetRect.height / 2f - mHintBgTransform.sizeDelta.y / 2f - mHeadIconOffset);
		}
	}
}

}
