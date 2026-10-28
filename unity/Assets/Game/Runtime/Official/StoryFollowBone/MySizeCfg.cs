// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using UnityEngine;

namespace Zjcs.Official.StoryFollowBone
{
public class MySizeCfg : MonoBehaviour
{
	[Tooltip("体型半径")]
	public float myRadius = 0.5f;

	[Tooltip("体型高度")]
	public float myHeight = 1.8f;

	[Tooltip("如果为true，则使用gridTransform的格子数开根号计算大小")]
	public bool myAutoGridSize = true;

	[Tooltip("如果AutoGridSize为false，则使用此处配置的格子大小")]
	public int myManualGridSize = 1;

	// Explicit offline entity-binding boundary. Null result retains the original
	// missing GridTransform/Info branch; an unbound provider fails rather than
	// silently inventing an automatic body size.
	public System.Func<int?> BodyRangeInfoCount { private get; set; }

	public int GetGridSize()
	{
		if (!myAutoGridSize)
		{
			return myManualGridSize;
		}
		int? bodyRangeCount = BodyRangeInfoCount();
		if (!bodyRangeCount.HasValue)
		{
			return myManualGridSize;
		}
		return Mathf.RoundToInt(Mathf.Sqrt((float)bodyRangeCount.Value));
	}

	public Vector3 GetUIAttachPos(Vector2 anchor)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (Mathf.Abs(anchor.x) < 0.0001f)
		{
			return ((Component)this).transform.position + Vector3.up * myHeight * anchor.y;
		}
		Quaternion rotation = ((Component)Camera.main).transform.rotation;
		float y = rotation.eulerAngles.y;
		float num = myRadius * anchor.x * 2f;
		return ((Component)this).transform.position + new Vector3(Mathf.Cos(y) * num, myHeight * anchor.y, Mathf.Sin(y) * num);
	}
}

}
