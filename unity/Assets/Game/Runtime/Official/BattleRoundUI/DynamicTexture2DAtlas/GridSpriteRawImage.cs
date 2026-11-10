// Official client 884_156692; deterministic C# / logging adaptation only.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas
{
using Object = UnityEngine.Object;

public class GridSpriteRawImage : RawImage
{
	private static List<UIVertex> sListTempVertsBuffer = new List<UIVertex>();

	public GridSpriteInfo GridSpriteInfo { get; private set; }

	public void SetGridSpriteInfo(GridSpriteInfo info)
	{
		if (info == null)
		{
			((RawImage)this).texture = null;
			return;
		}
		GridSpriteInfo = info;
		GridSpriteInfo.OnTextureUpdated = OnSpriteUpdated;
		OnSpriteUpdated();
	}

	private void OnSpriteUpdated()
	{
		((RawImage)this).texture = (Texture)(object)((GridSpriteInfo == null) ? null : GridSpriteInfo.TexAtlas);
		((Graphic)this).SetVerticesDirty();
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		base.OnPopulateMesh(vh);
		if (GridSpriteInfo != null)
		{
			FillVh(vh, GridSpriteInfo, (Color32)color);
		}
	}

	protected override void OnDestroy()
	{
		if (GridSpriteInfo != null)
		{
			GridSpriteInfo.OnTextureUpdated = null;
		}
		base.OnDestroy();
	}

	private static bool ParseVerts(List<UIVertex> listVerts, out Vector2 meshCenter, out Vector2 quadMeshSize)
	{
		meshCenter = Vector2.zero;
		quadMeshSize = Vector2.zero;
		if (listVerts == null || listVerts.Count > 6)
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(51, 0, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("ParseVerts listVerts == null || listVerts.Count > 6");
			}
			Log.Error(ref message, null, "ParseVerts", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Components/GridSpriteRawImage.cs", 77);
			return false;
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
		meshCenter = new Vector2((num3 + num) * 0.5f, (num4 + num2) * 0.5f);
		quadMeshSize = new Vector2(num3 - num, num4 - num2);
		return true;
	}

	private static void FillVh(VertexHelper vh, GridSpriteInfo gridSpriteInfo, Color32 color)
	{
		sListTempVertsBuffer.Clear();
		vh.GetUIVertexStream(sListTempVertsBuffer);
		if (ParseVerts(sListTempVertsBuffer, out var meshCenter, out var quadMeshSize))
		{
			vh.Clear();
			float num = meshCenter.x - quadMeshSize.x * 0.5f;
			float num2 = meshCenter.x + quadMeshSize.x * 0.5f;
			float num3 = meshCenter.y - quadMeshSize.y * 0.5f;
			float num4 = meshCenter.y + quadMeshSize.y * 0.5f;
			Vector3 position = new Vector3(num, num3, 0);
			Vector3 position2 = new Vector3(num, num4, 0);
			Vector3 position3 = new Vector3(num2, num4, 0);
			Vector3 position4 = new Vector3(num2, num3, 0);
			vh.AddVert(new UIVertex
			{
				color = color,
				position = position,
				uv0 = (Vector4)gridSpriteInfo.UVLB
			});
			vh.AddVert(new UIVertex
			{
				color = color,
				position = position2,
				uv0 = (Vector4)gridSpriteInfo.UVLT
			});
			vh.AddVert(new UIVertex
			{
				color = color,
				position = position3,
				uv0 = (Vector4)gridSpriteInfo.UVRT
			});
			vh.AddVert(new UIVertex
			{
				color = color,
				position = position4,
				uv0 = (Vector4)gridSpriteInfo.UVRB
			});
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(0, 2, 3);
		}
		else
		{
			RoundAtlasDiagnostic message = new RoundAtlasDiagnostic(44, 0, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("FillVh 失败, 解析原有的Vert失败, 可能OnPopulateMesh被覆写了");
			}
			Log.Error(ref message, null, "FillVh", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/DynamicTexture2DAtlas/Components/GridSpriteRawImage.cs", 145);
		}
	}
}
}
