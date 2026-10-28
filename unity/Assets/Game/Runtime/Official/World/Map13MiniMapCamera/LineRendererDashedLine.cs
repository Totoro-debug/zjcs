using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LineRendererDashedLine : MonoBehaviour
{
	private const double mSegmentDeviation = 0.001;

	[Header("虚线宽度")]
	[SerializeField]
	private float mLineWidth = 0.1f;

	[Header("实线部分的长度")]
	[SerializeField]
	private float mDashLength = 0.2f;

	[Header("空白部分的长度")]
	[SerializeField]
	private float mGapLength = 0.1f;

	[SerializeField]
	private Color mColor = Color.white;

	private Vector3[] mPoints;

	private Mesh mMesh;

	private bool mDirty = true;

	private VertexHelper mVertexHelper;

	private void Start()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected Obj, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected Obj, but got Unknown
		mVertexHelper = new VertexHelper();
		mMesh = new Mesh();
		((Object)mMesh).name = "DashedLineMesh";
		((Component)this).GetComponent<MeshFilter>().mesh = mMesh;
	}

	private void OnDestroy()
	{
		VertexHelper val = mVertexHelper;
		if (val != null)
		{
			val.Dispose();
		}
		if ((Object)(object)mMesh != (Object)null)
		{
			Object.Destroy((Object)(object)mMesh);
			mMesh = null;
		}
	}

	public void SetCount(int count)
	{
		mPoints = new Vector3[count];
	}

	public void SetPoint(int index, Vector3 pos)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		mPoints[index] = pos;
		mDirty = true;
	}

	public void SetPoints(Vector3[] pointList)
	{
		mPoints = pointList;
		mDirty = true;
	}

	private void Update()
	{
		if (mDirty && mPoints != null && mPoints.Length >= 2)
		{
			mDirty = false;
			GenerateMesh();
		}
	}

	public void GenerateMesh()
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		int num = mPoints?.Length ?? 0;
		if (num < 2)
		{
			mMesh.Clear();
			return;
		}
		mVertexHelper.Clear();
		float num2 = mLineWidth * 0.5f;
		bool flag = true;
		for (int i = 0; i < num - 1; i++)
		{
			Vector3 val = mPoints[i];
			Vector3 val2 = mPoints[i + 1] - val;
			float magnitude = val2.magnitude;
			val2.Normalize();
			Vector3 val3 = Vector3.Cross(val2, Vector3.forward);
			val3.Normalize();
			float num3 = 0f;
			while (num3 < magnitude)
			{
				float num4 = (flag ? mDashLength : mGapLength);
				if ((double)(magnitude - num3) + 0.001 < (double)num4)
				{
					break;
				}
				if (flag)
				{
					Vector3 val4 = val + val2 * num3;
					Vector3 val5 = val + val2 * (num3 + num4);
					int currentVertCount = mVertexHelper.currentVertCount;
					Vector3 val6 = val3 * num2;
					mVertexHelper.AddVert(val4 + val6, (Color32)(mColor), (Vector4)(new Vector2(0f, 0f)));
					mVertexHelper.AddVert(val4 - val6, (Color32)(mColor), (Vector4)(new Vector2(1f, 0f)));
					mVertexHelper.AddVert(val5 + val6, (Color32)(mColor), (Vector4)(new Vector2(0f, 1f)));
					mVertexHelper.AddVert(val5 - val6, (Color32)(mColor), (Vector4)(new Vector2(1f, 1f)));
					mVertexHelper.AddTriangle(currentVertCount, currentVertCount + 2, currentVertCount + 1);
					mVertexHelper.AddTriangle(currentVertCount + 2, currentVertCount + 3, currentVertCount + 1);
				}
				num3 += num4;
				flag = !flag;
			}
		}
		mMesh.Clear();
		mVertexHelper.FillMesh(mMesh);
		mVertexHelper.Clear();
	}

	public LineRendererDashedLine()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
