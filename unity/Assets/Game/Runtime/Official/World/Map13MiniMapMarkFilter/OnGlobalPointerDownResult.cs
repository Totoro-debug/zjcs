using System.Runtime.CompilerServices;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
public struct OnGlobalPointerDownResult : IInputOperationResult
{
	public Vector2 ScreenPos { get;private set; }

	public OnGlobalPointerDownResult(Vector2 screenPos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		ScreenPos = screenPos;
	}

	public static implicit operator OnGlobalPointerDownResult(Vector2 screenPos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return new OnGlobalPointerDownResult(screenPos);
	}

	public static implicit operator OnGlobalPointerDownResult(Vector3 screenPos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return new OnGlobalPointerDownResult((Vector2)screenPos);
	}

	public static implicit operator Vector2(OnGlobalPointerDownResult result)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return result.ScreenPos;
	}

	public static implicit operator Vector3(OnGlobalPointerDownResult result)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return (Vector3)result.ScreenPos;
	}
}

}
