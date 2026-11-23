// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public class HomeEntityCreateData : MonoBehaviour
{
	[SerializeField]
	private Vector3 mLocalPosOffset;

	public Vector3 LocalPosOffset
	{
		get
		{
			return mLocalPosOffset;
		}
	}
}
}
