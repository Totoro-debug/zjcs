// Original client-source/LayoutElementClamper.cs; logging and local UI service bindings adapted.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.SkillsUI
{
[ExecuteAlways]
[DisallowMultipleComponent]
public class LayoutElementClamper : MonoBehaviour, ILayoutElement
{
	private RectTransform mRectTransform;

	[SerializeField]
	private int mMaxPreferredWidth;

	[SerializeField]
	private int mMaxPreferredHeight;

	public RectTransform RectTransform
	{
		get
		{
			if ((UnityEngine.Object)(object)mRectTransform == (UnityEngine.Object)null)
			{
				mRectTransform = ((Component)this).GetComponent<RectTransform>();
			}
			return mRectTransform;
		}
	}

	public int MaxPreferredWidth => mMaxPreferredWidth;

	public int MaxPreferredHeight => mMaxPreferredHeight;

	public float minWidth => -1f;

	public float preferredWidth
	{
		get
		{
			float layoutProperty = LayoutUtility.GetLayoutProperty(RectTransform, (Func<ILayoutElement, float>)((ILayoutElement layout) => ((object)layout == this) ? (-1f) : layout.preferredWidth), 0f);
			if (mMaxPreferredWidth < 0)
			{
				return layoutProperty;
			}
			return Mathf.Clamp(layoutProperty, 0f, (float)mMaxPreferredWidth);
		}
	}

	public float flexibleWidth => 0f;

	public float minHeight => -1f;

	public float preferredHeight
	{
		get
		{
			float layoutProperty = LayoutUtility.GetLayoutProperty(RectTransform, (Func<ILayoutElement, float>)((ILayoutElement layout) => ((object)layout == this) ? (-1f) : layout.preferredHeight), 0f);
			if (mMaxPreferredHeight < 0)
			{
				return layoutProperty;
			}
			return Mathf.Clamp(layoutProperty, 0f, (float)mMaxPreferredHeight);
		}
	}

	public float flexibleHeight => 0f;

	public int layoutPriority => 1;

	private void Awake()
	{
		LayoutElementClamper[] components = ((Component)this).GetComponents<LayoutElementClamper>();
		if (components.Length > 1)
		{
			Debug.LogError("Multiple LayoutElementClamper components found on GameObject.");
			for (int i = 1; i < components.Length; i++)
			{
				UnityEngine.Object.DestroyImmediate((UnityEngine.Object)(object)components[i]);
			}
		}
	}

	private void Start()
	{
	}

	public void CalculateLayoutInputHorizontal()
	{
	}

	public void CalculateLayoutInputVertical()
	{
	}
}
}
