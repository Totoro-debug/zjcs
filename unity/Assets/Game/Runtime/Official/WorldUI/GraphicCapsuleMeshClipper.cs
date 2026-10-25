using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Ported from the pinned official client; native mesh generation and UV interpolation.
namespace Zjcs.Official.WorldUI
{
[RequireComponent(typeof(Graphic))]
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class GraphicCapsuleMeshClipper : MonoBehaviour, IMeshModifier
{
	[SerializeField]
	public MeshClipperCapsuleArgs mMeshClipperCapsuleArgs;

	[NonSerialized]
	public bool DrawGizmos;

	[NonSerialized]
	public bool MeshDataDirty = true;

	private List<Vector3> mListCapsuleMeshPos = new List<Vector3>();

	private List<int> mListCapsuleMeshIndices = new List<int>();

	private Vector2 mLastPivot = Vector2.one * float.MaxValue;

	private RectTransform mRectTransform;

	private Graphic mGraphic;

	private RectTransform RectTransform
	{
		get
		{
			if ((Object)(object)mRectTransform == (Object)null)
			{
				Transform transform = ((Component)this).transform;
				mRectTransform = (RectTransform)(object)((transform is RectTransform) ? transform : null);
			}
			return mRectTransform;
		}
	}

	private Graphic Graphic
	{
		get
		{
			if ((Object)(object)mGraphic == (Object)null)
			{
				mGraphic = ((Component)this).GetComponent<Graphic>();
			}
			return mGraphic;
		}
	}

	public void ModifyMesh(Mesh mesh)
	{
	}

	public void ModifyMesh(VertexHelper vh)
	{













		if (mMeshClipperCapsuleArgs.Enable)
		{
			if (MeshDataDirty || mListCapsuleMeshIndices.Count == 0 || mListCapsuleMeshPos.Count == 0 || mLastPivot != RectTransform.pivot)
			{
				Vector2 pivotCenterOffset = RectTransform.sizeDelta * (RectTransform.pivot - Vector2.one * 0.5f);
				MeshClipperAlgorithms.GenMeshCapsule(mListCapsuleMeshPos, mListCapsuleMeshIndices, pivotCenterOffset, mMeshClipperCapsuleArgs);
				MeshDataDirty = false;
				mLastPivot = RectTransform.pivot;
			}
			MeshClipperAlgorithms.SetMeshData2VhTightMesh(mListCapsuleMeshPos, mListCapsuleMeshIndices, vh, Graphic.color);
		}
	}

	public GraphicCapsuleMeshClipper()
	{



	}
}

}
