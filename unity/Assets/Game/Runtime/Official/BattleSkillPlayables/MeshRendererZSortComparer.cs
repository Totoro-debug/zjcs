using System.Collections.Generic;
using UnityEngine;

public class MeshRendererZSortComparer : Comparer<MeshRenderer>
{
    public override int Compare(MeshRenderer x, MeshRenderer y)
    {
        return y.transform.position.z.CompareTo(x.transform.position.z);
    }
}
