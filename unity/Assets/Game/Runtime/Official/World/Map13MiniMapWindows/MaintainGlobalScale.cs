using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeLabelUI;
using OfficialWwiseEvent=Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
[ExecuteAlways]
public class MaintainGlobalScale : MonoBehaviour
{
	[SerializeField]
	private Vector3 mFixedGlobalScale;

	private Vector3 mLastGlobalScale;

	private Vector3 mOriginFixedGlobalScale;

	private void Awake()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		mOriginFixedGlobalScale = mFixedGlobalScale;
	}

	protected virtual void Start()
	{
		UpdateLocalScale();
	}

	private void LateUpdate()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		if (mLastGlobalScale != ((Component)this).transform.lossyScale)
		{
			UpdateLocalScale();
		}
	}

	protected virtual Vector3 GetParentLossyScale()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)((Component)this).transform.parent == (Object)null)
		{
			return Vector3.zero;
		}
		return ((Component)this).transform.parent.lossyScale;
	}

	protected void UpdateLocalScale()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if (!(mFixedGlobalScale == ((Component)this).transform.lossyScale))
		{
			Vector3 parentLossyScale = GetParentLossyScale();
			if (HasZeroScale(parentLossyScale))
			{
				((Component)this).transform.localScale = mFixedGlobalScale;
			}
			else
			{
				((Component)this).transform.localScale = new Vector3(SafeDivide(mFixedGlobalScale.x, parentLossyScale.x), SafeDivide(mFixedGlobalScale.y, parentLossyScale.y), SafeDivide(mFixedGlobalScale.z, parentLossyScale.z));
			}
			mLastGlobalScale = ((Component)this).transform.lossyScale;
		}
	}

	protected float SafeDivide(float a, float b)
	{
		if (Mathf.Abs(b) > Mathf.Epsilon)
		{
			return a / b;
		}
		return a;
	}

	protected bool HasZeroScale(Vector3 scale)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (!(Mathf.Abs(scale.x) < Mathf.Epsilon))
		{
			return Mathf.Abs(scale.y) < Mathf.Epsilon;
		}
		return true;
	}

	private void OnValidate()
    {
        var children = GetComponentsInChildren<MaintainGlobalScale>();
        for (int i=0;i<children.Length;i++)
            if (children[i] != this) Debug.LogError("MaintainGlobalScale脚本在子节点中存在，需要删除当前节点的MaintainGlobalScale " + GetFullName(gameObject));
    }
    private static string GetFullName(GameObject value)
    {
        string name=value.name;
        for (Transform parent=value.transform.parent; parent!=null; parent=parent.parent) name=parent.name+"/"+name;
        return name;
    }

	public void RevertFixedScale()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		mFixedGlobalScale = mOriginFixedGlobalScale;
	}

	public void SetFixedScale(Vector3 v)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		mFixedGlobalScale = v;
	}
}

}
