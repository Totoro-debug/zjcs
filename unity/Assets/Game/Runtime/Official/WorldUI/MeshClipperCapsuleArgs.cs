using System;
using UnityEngine;

// Ported from the pinned official client; native mesh generation and UV interpolation.
namespace Zjcs.Official.WorldUI
{
[Serializable]
public class MeshClipperCapsuleArgs
{
	[SerializeField]
	public bool Enable = true;

	[SerializeField]
	public Vector2 CenterRectSize = new Vector2(100f, 100f);

	[SerializeField]
	public Vector2 MeshPosOffset = Vector2.zero;

	[SerializeField]
	public bool NeedLeftCircle = true;

	[SerializeField]
	public bool NeedRightCircle = true;

	[SerializeField]
	public int CircleSegments = 30;

	public MeshClipperCapsuleArgs()
	{




	}
}

}
