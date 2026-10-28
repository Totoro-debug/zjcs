// Original client-source/LoopVerticalScrollWithMaxHeight.cs; logging and local UI service bindings adapted.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
public class LoopVerticalScrollWithMaxHeight : LoopVerticalScrollRect
{
	[SerializeField]
	private float mMaxHeight;

	private Action mRefreshCompletedCallback;

	public override float preferredHeight
	{
		get
		{
			float contentHeight = ContentHeight;
			if (contentHeight < mMaxHeight)
			{
				return contentHeight;
			}
			return mMaxHeight;
		}
	}

	private float ContentHeight
	{
		get
		{
			if ((UnityEngine.Object)(object)content == (UnityEngine.Object)null)
			{
				return 0f;
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(content);
			Rect rect = content.rect;
			return rect.height * ((Transform)content).localScale.y;
		}
	}

	protected override void InternalRefillCells(int startItem = 0, float contentOffset = 0f, Action onCompletedCallback = null)
	{
		SetRectSize();
		base.InternalRefillCells(startItem, contentOffset, onCompletedCallback);
		FillRectArea();
	}

	private void SetRectSize()
	{
		Vector2 sizeDelta = viewport.sizeDelta;
		sizeDelta.y = mMaxHeight;
		viewport.sizeDelta = sizeDelta;
	}

	private void FillRectArea()
	{
		viewport.anchorMin = Vector2.zero;
		viewport.anchorMax = Vector2.one;
		viewport.offsetMin = Vector2.zero;
		viewport.offsetMax = Vector2.zero;
		SetDirty();
	}
}
}
