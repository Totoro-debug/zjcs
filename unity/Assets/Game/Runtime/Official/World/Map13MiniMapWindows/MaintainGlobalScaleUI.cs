using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
[ExecuteAlways]
public class MaintainGlobalScaleUI : MaintainGlobalScale
{
	private RectTransform mRootCanvasRectTransform;

	private Transform mRootCanvasRectTransformParent;

	private bool mIsTransformParentDirty;

	protected override void Start()
	{
		RefreshRootCanvas();
		base.Start();
	}

	protected void OnTransformParentChanged()
	{
		if (!((Component)this).gameObject.activeInHierarchy)
		{
			mIsTransformParentDirty = true;
		}
		else
		{
			ParentChanged();
		}
	}

	private void OnEnable()
	{
		if (mIsTransformParentDirty)
		{
			ParentChanged();
		}
	}

	private void ParentChanged()
	{
		RefreshRootCanvas();
		UpdateLocalScale();
		mIsTransformParentDirty = false;
	}

	private void RefreshRootCanvas()
	{
		if (!IsParentDirty())
		{
			return;
		}
		Transform val = ((Component)this).transform;
		Canvas val2 = null;
		Canvas val3 = default;
		while ((Object)(object)val != (Object)null)
		{
			if (((Component)val).TryGetComponent<Canvas>(out val3))
			{
				val2 = val3;
			}
			val = val.parent;
		}
		if ((Object)(object)val2 != (Object)null)
		{
			mRootCanvasRectTransform = ((Component)val2).GetComponent<RectTransform>();
			mRootCanvasRectTransformParent = ((Transform)mRootCanvasRectTransform).parent;
		}
	}

	private bool IsParentDirty()
	{
		if ((Object)(object)mRootCanvasRectTransform == (Object)null)
		{
			return true;
		}
		if ((Object)(object)mRootCanvasRectTransformParent != (Object)(object)((Transform)mRootCanvasRectTransform).parent)
		{
			return true;
		}
		if (!((Component)this).transform.IsChildOf((Transform)(object)mRootCanvasRectTransform))
		{
			return true;
		}
		return false;
	}

	protected override Vector3 GetParentLossyScale()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		Vector3 parentLossyScale = base.GetParentLossyScale();
		Vector3 val = (((Object)(object)mRootCanvasRectTransform != (Object)null) ? ((Transform)mRootCanvasRectTransform).localScale : Vector3.one);
		return new Vector3(SafeDivide(parentLossyScale.x, val.x), SafeDivide(parentLossyScale.y, val.y), SafeDivide(parentLossyScale.z, val.z));
	}

	public void ForceUpdateScale()
	{
		mIsTransformParentDirty = true;
		mRootCanvasRectTransformParent = null;
		mRootCanvasRectTransform = null;
		RefreshRootCanvas();
		UpdateLocalScale();
	}
}

}
