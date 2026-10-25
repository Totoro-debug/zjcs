using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ported from the pinned official client; native mesh generation and UV interpolation.
namespace Zjcs.Official.WorldUI
{
public static class MeshClipperAlgorithms
{
	private const int kQuadVertNum = 6;

	private static List<UIVertex> sListOriginVertsTemp = new List<UIVertex>();

	public static void SetMeshData2VhTightMesh(List<Vector3> listMeshPos, List<int> listMeshIndices, VertexHelper vh, Color graphicColor)
	{


		if (vh == null)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (listMeshIndices == null || listMeshIndices.Count % 3 != 0)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (listMeshPos == null || listMeshPos.Count == 0)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (sListOriginVertsTemp == null)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (listMeshPos.Count < 3)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		sListOriginVertsTemp.Clear();
		vh.GetUIVertexStream(sListOriginVertsTemp);
		if (sListOriginVertsTemp.Count > 6)
		{
			Debug.LogError("Invalid official capsule mesh input.");
		}
		else
		{
			ParseVerts(sListOriginVertsTemp, out var tightMeshCenter, out var quadMeshSize, out var uvCenter, out var uvSize);
			SetMeshData2Vh(listMeshPos, listMeshIndices, vh, graphicColor, tightMeshCenter, quadMeshSize, uvCenter, uvSize);
		}
	}

	public static void GenMeshCapsule(List<Vector3> listForFill, List<int> listIndices, Vector2 pivotCenterOffset, MeshClipperCapsuleArgs args)
	{




































		if (listForFill == null || listIndices == null)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		listForFill.Clear();
		listIndices.Clear();
		float y = args.CenterRectSize.y;
		float x = args.CenterRectSize.x;
		if (y <= 0f)
		{
			return;
		}
		args.CircleSegments = Math.Max(3, args.CircleSegments);
		float num = y * 0.5f;
		float num2 = x * 0.5f;
		Vector3 val = (Vector3)(args.MeshPosOffset - pivotCenterOffset);
		listForFill.Add(Vector3.zero + val);
		int num3 = args.CircleSegments / 2;
		Vector3 val2 = new Vector3(num2, num, 0f);
		listForFill.Add(val2 + val);
		if (args.NeedRightCircle)
		{
			Vector3 val3 = new Vector3(num2, 0f, 0f);
			for (int i = 1; i < num3; i++)
			{
				float num4 = (float)Math.PI / 2f - (float)Math.PI * (float)i / (float)num3;
				float num5 = val3.x + num * Mathf.Cos(num4);
				float num6 = val3.y + num * Mathf.Sin(num4);
				listForFill.Add(new Vector3(num5, num6, 0f) + val);
			}
		}
		Vector3 val4 = new Vector3(num2, 0f - num, 0f);
		listForFill.Add(val4 + val);
		Vector3 val5 = new Vector3(0f - num2, 0f - num, 0f);
		listForFill.Add(val5 + val);
		if (args.NeedLeftCircle)
		{
			Vector3 val6 = new Vector3(0f - num2, 0f, 0f);
			for (int j = 1; j < num3; j++)
			{
				float num7 = -(float)Math.PI / 2f - (float)Math.PI * (float)j / (float)num3;
				float num8 = val6.x + num * Mathf.Cos(num7);
				float num9 = val6.y + num * Mathf.Sin(num7);
				listForFill.Add(new Vector3(num8, num9, 0f) + val);
			}
		}
		Vector3 val7 = new Vector3(0f - num2, num, 0f);
		listForFill.Add(val7 + val);
		int num10 = listForFill.Count - 1;
		for (int k = 0; k < num10; k++)
		{
			int item = k + 1;
			int item2 = (k + 1) % num10 + 1;
			listIndices.Add(0);
			listIndices.Add(item);
			listIndices.Add(item2);
		}
	}

	private static void SetMeshData2Vh(List<Vector3> listMeshPos, List<int> listMeshIndices, VertexHelper vh, Color graphicColor, Vector2 tightMeshCenter, Vector2 sizeDelta, Vector2 uvCenter, Vector2 uvSize)
	{


		if (vh == null)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (listMeshIndices == null || listMeshIndices.Count % 3 != 0)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (listMeshPos == null || listMeshPos.Count == 0)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		if (listMeshPos.Count < 3)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		vh.Clear();
		float x = uvCenter.x;
		float y = uvCenter.y;
		float x2 = uvSize.x;
		float y2 = uvSize.y;
		float num = x2 / sizeDelta.x;
		float num2 = y2 / sizeDelta.y;
		Color32 color = (Color32)graphicColor;
		for (int i = 0; i < listMeshPos.Count; i++)
		{
			Vector3 val = listMeshPos[i];
			float num3 = x + (val.x - tightMeshCenter.x) * num;
			float num4 = y + (val.y - tightMeshCenter.y) * num2;
			vh.AddVert(new UIVertex
			{
				color = color,
				position = val,
				uv0 = (Vector4)new Vector2(num3, num4)
			});
		}
		for (int j = 0; j < listMeshIndices.Count; j += 3)
		{
			vh.AddTriangle(listMeshIndices[j], listMeshIndices[j + 1], listMeshIndices[j + 2]);
		}
	}

	private static void ParseVerts(List<UIVertex> listVerts, out Vector2 tightMeshCenter, out Vector2 quadMeshSize, out Vector2 uvCenter, out Vector2 uvSize)
	{






































		tightMeshCenter = Vector2.zero;
		quadMeshSize = Vector2.zero;
		uvCenter = Vector2.zero;
		uvSize = Vector2.zero;
		if (listVerts == null || listVerts.Count > 6)
		{
			Debug.LogError("Invalid official capsule mesh input.");
			return;
		}
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MinValue;
		float num5 = float.MaxValue;
		float num6 = float.MaxValue;
		float num7 = float.MinValue;
		float num8 = float.MinValue;
		for (int i = 0; i < listVerts.Count; i++)
		{
			UIVertex val = listVerts[i];
			Vector3 position = val.position;
			Vector4 uv = val.uv0;
			num = ((num < position.x) ? num : position.x);
			num2 = ((num2 < position.y) ? num2 : position.y);
			num3 = ((num3 > position.x) ? num3 : position.x);
			num4 = ((num4 > position.y) ? num4 : position.y);
			num5 = ((num5 < uv.x) ? num5 : uv.x);
			num6 = ((num6 < uv.y) ? num6 : uv.y);
			num7 = ((num7 > uv.x) ? num7 : uv.x);
			num8 = ((num8 > uv.y) ? num8 : uv.y);
		}
		tightMeshCenter = new Vector2((num3 + num) * 0.5f, (num4 + num2) * 0.5f);
		quadMeshSize = new Vector2(num3 - num, num4 - num2);
		uvSize = new Vector2(num7 - num5, num8 - num6);
		uvCenter = new Vector2((num7 + num5) * 0.5f, (num8 + num6) * 0.5f);
	}
}

}
