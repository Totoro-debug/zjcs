using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
public class MirrorImage : Image
{
	public enum MirrorType
	{
		Horizontal,
		Vertical,
		Quarter
	}

	[SerializeField]
	private MirrorType m_mirrorType;

	[SerializeField]
	private bool mReversed;

	private List<UIVertex> mVertexBuffer = new List<UIVertex>();

	public MirrorType MirrorStyle
	{
		get
		{
			return m_mirrorType;
		}
		set
		{
			if (m_mirrorType != value)
			{
				m_mirrorType = value;
				((Graphic)this).SetVerticesDirty();
			}
		}
	}

	public bool Reversed
	{
		get
		{
			return mReversed;
		}
		set
		{
			if (mReversed != value)
			{
				mReversed = value;
				((Graphic)this).SetVerticesDirty();
			}
		}
	}

	public override void SetNativeSize()
	{
		if ((Object)(object)((Image)this).overrideSprite == (Object)null)
		{
			base.SetNativeSize();
			return;
		}
		Rect rect = ((Image)this).overrideSprite.rect;
		float num = rect.width / ((Image)this).pixelsPerUnit;
		rect = ((Image)this).overrideSprite.rect;
		float num2 = rect.height / ((Image)this).pixelsPerUnit;
		((Graphic)this).rectTransform.anchorMax = ((Graphic)this).rectTransform.anchorMin;
		switch (MirrorStyle)
		{
		case MirrorType.Horizontal:
			((Graphic)this).rectTransform.sizeDelta = new Vector2(num * 2f, num2);
			break;
		case MirrorType.Vertical:
			((Graphic)this).rectTransform.sizeDelta = new Vector2(num, num2 * 2f);
			break;
		case MirrorType.Quarter:
			((Graphic)this).rectTransform.sizeDelta = new Vector2(num * 2f, num2 * 2f);
			break;
		}
		((Graphic)this).SetVerticesDirty();
	}

	private void GenerateFilledSprite(VertexHelper vh, Vector4 vertices, Vector4 uvs)
	{
		float x = uvs.x;
		float y = uvs.y;
		float z = uvs.z;
		float w = uvs.w;
		Vector3[] array = new Vector3[4]
		{
			new Vector3(vertices.x, vertices.y),
			new Vector3(vertices.x, vertices.w),
			new Vector3(vertices.z, vertices.w),
			new Vector3(vertices.z, vertices.y)
		};
		Vector2[] array2 = new Vector2[4]
		{
			new Vector2(x, y),
			new Vector2(x, w),
			new Vector2(z, w),
			new Vector2(z, y)
		};
		int currentVertCount = vh.currentVertCount;
		for (int i = 0; i < 4; i++)
		{
			vh.AddVert(array[i], (Color32)color, (Vector4)array2[i]);
		}
		vh.AddTriangle(currentVertCount, currentVertCount + 1, currentVertCount + 2);
		vh.AddTriangle(currentVertCount + 2, currentVertCount + 3, currentVertCount);
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		Type type = ((Image)this).type;
		switch ((int)type)
		{
		case 2:
		case 3:
			return;
		case 0:
			DrawSimple(vh);
			break;
		case 1:
			DrawSliced(vh);
			break;
		}
		mVertexBuffer.Clear();
	}

	private void DrawSimple(VertexHelper vh)
	{
		base.OnPopulateMesh(vh);
		vh.GetUIVertexStream(mVertexBuffer);
		Rect pixelAdjustedRect = ((Graphic)this).GetPixelAdjustedRect();
		Vector2 center = pixelAdjustedRect.center;
		int count = mVertexBuffer.Count;
		SimpleScale(pixelAdjustedRect);
		switch (MirrorStyle)
		{
		case MirrorType.Horizontal:
			MirrorVerts(center, count, isHorizontal: true, canReverse: true);
			break;
		case MirrorType.Vertical:
			MirrorVerts(center, count, isHorizontal: false, canReverse: true);
			break;
		case MirrorType.Quarter:
			MirrorVerts(center, count);
			MirrorVerts(center, count * 2, isHorizontal: false);
			break;
		}
		vh.Clear();
		vh.AddUIVertexTriangleStream(mVertexBuffer);
	}

	protected void DrawSliced(VertexHelper vh)
	{
		if (!((Image)this).hasBorder)
		{
			DrawSimple(vh);
			return;
		}
		vh.Clear();
		Rect pixelAdjustedRect = ((Graphic)this).GetPixelAdjustedRect();
		Vector4 outerUV = DataUtility.GetOuterUV(((Image)this).overrideSprite);
		Vector4 innerUV = DataUtility.GetInnerUV(((Image)this).overrideSprite);
		Vector4 val = DataUtility.GetPadding(((Image)this).overrideSprite) / ((Image)this).pixelsPerUnit;
		Vector4 adjustedBorders = GetAdjustedBorders(pixelAdjustedRect);
		Vector2 val2 = new Vector2(val.x + pixelAdjustedRect.x, val.y + pixelAdjustedRect.y);
		float num = ((MirrorStyle == MirrorType.Horizontal || MirrorStyle == MirrorType.Quarter) ? 0.5f : 1f);
		float num2 = ((MirrorStyle == MirrorType.Vertical || MirrorStyle == MirrorType.Quarter) ? 0.5f : 1f);
		Vector2[] array = new Vector2[4]
		{
			val2,
			new Vector2(adjustedBorders.x + pixelAdjustedRect.x, adjustedBorders.y + pixelAdjustedRect.y),
			new Vector2(pixelAdjustedRect.width * num - adjustedBorders.z + pixelAdjustedRect.x, pixelAdjustedRect.height * num2 - adjustedBorders.w + pixelAdjustedRect.y),
			new Vector2(pixelAdjustedRect.width * num - val.z + pixelAdjustedRect.x, pixelAdjustedRect.height * num2 - val.w + pixelAdjustedRect.y)
		};
		Vector2[] array2 = new Vector2[4]
		{
			new Vector2(outerUV.x, outerUV.y),
			new Vector2(innerUV.x, innerUV.y),
			new Vector2(innerUV.z, innerUV.w),
			new Vector2(outerUV.z, outerUV.w)
		};
		for (int i = 0; i < 3; i++)
		{
			int num3 = i + 1;
			for (int j = 0; j < 3; j++)
			{
				if (((Image)this).fillCenter || i != 1 || j != 1)
				{
					int num4 = j + 1;
					Vector4 vertices = new Vector4(array[num3].x, array[j].y, array[i].x, array[num4].y);
					Vector4 uvs = new Vector4(array2[num3].x, array2[j].y, array2[i].x, array2[num4].y);
					GenerateFilledSprite(vh, vertices, uvs);
				}
			}
		}
		vh.GetUIVertexStream(mVertexBuffer);
		Vector2 center = pixelAdjustedRect.center;
		int num5 = SliceExcludeVerts();
		switch (MirrorStyle)
		{
		case MirrorType.Horizontal:
			MirrorVerts(center, num5, isHorizontal: true, canReverse: true);
			break;
		case MirrorType.Vertical:
			MirrorVerts(center, num5, isHorizontal: false, canReverse: true);
			break;
		case MirrorType.Quarter:
			if (mReversed)
			{
				for (int k = 0; k < num5; k++)
				{
					UIVertex val3 = mVertexBuffer[k];
					Vector3 position = val3.position;
					position.y = 0f - position.y - pixelAdjustedRect.height * 0.5f;
					val3.position = position;
					mVertexBuffer[k] = val3;
				}
			}
			MirrorVerts(center, num5);
			MirrorVerts(center, num5 * 2, isHorizontal: false);
			break;
		}
		vh.Clear();
		vh.AddUIVertexTriangleStream(mVertexBuffer);
	}

	private void SimpleScale(Rect rect)
	{
		int count = mVertexBuffer.Count;
		for (int i = 0; i < count; i++)
		{
			UIVertex val = mVertexBuffer[i];
			Vector3 position = val.position;
			if (MirrorStyle == MirrorType.Horizontal || MirrorStyle == MirrorType.Quarter)
			{
				position.x = (position.x + rect.x) * 0.5f;
			}
			if (MirrorStyle == MirrorType.Vertical || MirrorStyle == MirrorType.Quarter)
			{
				position.y = (position.y + rect.y) * 0.5f;
			}
			val.position = position;
			mVertexBuffer[i] = val;
		}
	}

	private void MirrorVerts(Vector2 center, int count, bool isHorizontal = true, bool canReverse = false)
	{
		bool flag = canReverse && mReversed;
		for (int i = 0; i < count; i++)
		{
			int index = (flag ? i : (count - 1 - i));
			UIVertex val = mVertexBuffer[index];
			Vector3 position = val.position;
			if (isHorizontal)
			{
				position.x = center.x * 2f - position.x;
				if (flag)
				{
					position.y = center.y * 2f - position.y;
				}
			}
			else
			{
				position.y = center.y * 2f - position.y;
				if (flag)
				{
					position.x = center.x * 2f - position.x;
				}
			}
			val.position = position;
			mVertexBuffer.Add(val);
		}
	}

	private int SliceExcludeVerts()
	{
		int count = mVertexBuffer.Count;
		int num = count;
		int num2 = 0;
		while (num2 < num)
		{
			UIVertex val = mVertexBuffer[num2];
			UIVertex val2 = mVertexBuffer[num2 + 1];
			UIVertex val3 = mVertexBuffer[num2 + 2];
			if (val.position == val2.position || val2.position == val3.position || val3.position == val.position)
			{
				mVertexBuffer[num2] = mVertexBuffer[num - 3];
				mVertexBuffer[num2 + 1] = mVertexBuffer[num - 2];
				mVertexBuffer[num2 + 2] = mVertexBuffer[num - 1];
				num -= 3;
			}
			else
			{
				num2 += 3;
			}
		}
		if (num >= count)
		{
			return num;
		}
		mVertexBuffer.RemoveRange(num, count - num);
		return num;
	}

	private Vector4 GetAdjustedBorders(Rect rect)
	{
		Vector4 val = ((Image)this).overrideSprite.border;
		val /= ((Image)this).pixelsPerUnit;
		for (int i = 0; i <= 1; i++)
		{
			float num = val[i] + val[i + 2];
			Vector2 size = rect.size;
			if (size[i] < num && num != 0f)
			{
				size = rect.size;
				float num2 = size[i] / num;
				ref Vector4 reference = ref val;
				int num3 = i;
				reference[num3] *= num2;
				reference = ref val;
				num3 = i + 2;
				reference[num3] *= num2;
			}
		}
		return val;
	}
}
}
