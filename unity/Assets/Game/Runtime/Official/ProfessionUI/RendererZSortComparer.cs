namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class RendererZSortComparer : Comparer<Renderer>
{
	public override int Compare(Renderer x, Renderer y)
	{

		return ((Component)y).transform.position.z.CompareTo(((Component)x).transform.position.z);
	}
}

}
