using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Official sliced fill mesh; stack buffers adapted to managed arrays.
namespace Zjcs.Official.WorldUI
{
public class SlicedFilledImage : Image
{
	protected override void OnPopulateMesh(VertexHelper vh)
	{





































































































		if ((int)((Image)this).type != 3 || !((Image)this).hasBorder)
		{
			base.OnPopulateMesh(vh);
			return;
		}
		if ((int)((Image)this).fillMethod != 0 && (int)((Image)this).fillMethod != 1)
		{
			base.OnPopulateMesh(vh);
			return;
		}
		vh.Clear();
		Rect pixelAdjustedRect = ((Graphic)this).GetPixelAdjustedRect();
		Vector4 outerUV = DataUtility.GetOuterUV(((Image)this).overrideSprite);
		Vector4 innerUV = DataUtility.GetInnerUV(((Image)this).overrideSprite);
		Vector4 padding = DataUtility.GetPadding(((Image)this).overrideSprite);
		Vector4 adjustedBorders = GetAdjustedBorders(((Image)this).overrideSprite.border / ((Image)this).pixelsPerUnit, pixelAdjustedRect);
		padding /= ((Image)this).pixelsPerUnit;
		Vector2 val = new Vector2(padding.x + pixelAdjustedRect.x, padding.y + pixelAdjustedRect.y);
		Vector2[] val2 = new Vector2[4]
		{
			val,
			new Vector2(adjustedBorders.x + pixelAdjustedRect.x, adjustedBorders.y + pixelAdjustedRect.y),
			new Vector2(pixelAdjustedRect.width - adjustedBorders.z + pixelAdjustedRect.x, pixelAdjustedRect.height - adjustedBorders.w + pixelAdjustedRect.y),
			new Vector2(pixelAdjustedRect.width - padding.z + pixelAdjustedRect.x, pixelAdjustedRect.height - padding.w + pixelAdjustedRect.y)
		};

		Vector2[] val3 = new Vector2[4]
		{
			new Vector2(outerUV.x, outerUV.y),
			new Vector2(innerUV.x, innerUV.y),
			new Vector2(innerUV.z, innerUV.w),
			new Vector2(outerUV.z, outerUV.w)
		};

		Vector2 val4 = new Vector2(((Vector2)val2[3]).x - val.x, ((Vector2)val2[3]).y - val.y);
		val4.x = ((val4.x > 0f) ? (1f / val4.x) : 1f);
		val4.y = ((val4.y > 0f) ? (1f / val4.y) : 1f);
		for (int i = 0; i < 3; i++)
		{
			int num = i + 1;
			for (int j = 0; j < 3; j++)
			{
				if (!((Image)this).fillCenter && i == 1 && j == 1)
				{
					continue;
				}
				int num2 = j + 1;
				float num3 = 0f;
				float num4 = 0f;
				Vector4 vertices = Vector4.zero;
				Vector4 uvs = Vector4.zero;
				FillMethod fillMethod = ((Image)this).fillMethod;
				switch ((int)fillMethod)
				{
				case 0:
					switch (((Image)this).fillOrigin)
					{
					case 0:
						num3 = (((Vector2)val2[i]).x - val.x) * val4.x;
						num4 = (((Vector2)val2[num]).x - val.x) * val4.x;
						vertices = new Vector4(((Vector2)val2[i]).x, ((Vector2)val2[j]).y, ((Vector2)val2[num]).x, ((Vector2)val2[num2]).y);
						uvs = new Vector4(((Vector2)val3[i]).x, ((Vector2)val3[j]).y, ((Vector2)val3[num]).x, ((Vector2)val3[num2]).y);
						break;
					case 1:
						num3 = 1f - (((Vector2)val2[num]).x - val.x) * val4.x;
						num4 = 1f - (((Vector2)val2[i]).x - val.x) * val4.x;
						vertices = new Vector4(((Vector2)val2[num]).x, ((Vector2)val2[j]).y, ((Vector2)val2[i]).x, ((Vector2)val2[num2]).y);
						uvs = new Vector4(((Vector2)val3[num]).x, ((Vector2)val3[j]).y, ((Vector2)val3[i]).x, ((Vector2)val3[num2]).y);
						break;
					}
					break;
				case 1:
					switch (((Image)this).fillOrigin)
					{
					case 0:
						num3 = (((Vector2)val2[j]).y - val.y) * val4.y;
						num4 = (((Vector2)val2[num2]).y - val.y) * val4.y;
						vertices = new Vector4(((Vector2)val2[i]).x, ((Vector2)val2[j]).y, ((Vector2)val2[num]).x, ((Vector2)val2[num2]).y);
						uvs = new Vector4(((Vector2)val3[i]).x, ((Vector2)val3[j]).y, ((Vector2)val3[num]).x, ((Vector2)val3[num2]).y);
						break;
					case 1:
						num3 = 1f - (((Vector2)val2[num2]).y - val.y) * val4.y;
						num4 = 1f - (((Vector2)val2[j]).y - val.y) * val4.y;
						vertices = new Vector4(((Vector2)val2[i]).x, ((Vector2)val2[num2]).y, ((Vector2)val2[num]).x, ((Vector2)val2[j]).y);
						uvs = new Vector4(((Vector2)val3[i]).x, ((Vector2)val3[num2]).y, ((Vector2)val3[num]).x, ((Vector2)val3[j]).y);
						break;
					}
					break;
				}
				if (!(num3 >= ((Image)this).fillAmount))
				{
					GenerateFilledSprite(vh, vertices, uvs, (((Image)this).fillAmount - num3) / (num4 - num3));
				}
			}
		}
	}

	private void GenerateFilledSprite(VertexHelper vh, Vector4 vertices, Vector4 uvs, float fillAmount)
	{

















































		float x = uvs.x;
		float y = uvs.y;
		float num = uvs.z;
		float num2 = uvs.w;
		if (fillAmount < 1f)
		{
			FillMethod fillMethod = ((Image)this).fillMethod;
			switch ((int)fillMethod)
			{
			case 0:
				vertices.z = vertices.x + (vertices.z - vertices.x) * fillAmount;
				num = x + (num - x) * fillAmount;
				break;
			case 1:
				vertices.w += (vertices.y - vertices.w) * (1f - fillAmount);
				num2 += (y - num2) * (1f - fillAmount);
				break;
			}
		}
		Vector3[] val = new Vector3[4]
		{
			new Vector3(vertices.x, vertices.y),
			new Vector3(vertices.x, vertices.w),
			new Vector3(vertices.z, vertices.w),
			new Vector3(vertices.z, vertices.y)
		};

		Vector2[] val2 = new Vector2[4]
		{
			new Vector2(x, y),
			new Vector2(x, num2),
			new Vector2(num, num2),
			new Vector2(num, y)
		};

		int currentVertCount = vh.currentVertCount;
		for (int i = 0; i < 4; i++)
		{
			vh.AddVert(val[i], (Color32)color, (Vector4)val2[i]);
		}
		vh.AddTriangle(currentVertCount, currentVertCount + 1, currentVertCount + 2);
		vh.AddTriangle(currentVertCount + 2, currentVertCount + 3, currentVertCount);
	}

	private Vector4 GetAdjustedBorders(Vector4 border, Rect adjustedRect)
	{













		Rect rect = ((Graphic)this).rectTransform.rect;
		for (int i = 0; i <= 1; i++)
		{
			Vector2 size = rect.size;
			if (size[i] != 0f)
			{
				size = adjustedRect.size;
				float num = size[i];
				size = rect.size;
				float num2 = num / size[i];
				ref Vector4 reference = ref border;
				int num3 = i;
				reference[num3] *= num2;
				reference = ref border;
				num3 = i + 2;
				reference[num3] *= num2;
			}
			float num4 = border[i] + border[i + 2];
			size = adjustedRect.size;
			if (size[i] < num4 && num4 != 0f)
			{
				size = adjustedRect.size;
				float num2 = size[i] / num4;
				ref Vector4 reference = ref border;
				int num3 = i;
				reference[num3] *= num2;
				reference = ref border;
				num3 = i + 2;
				reference[num3] *= num2;
			}
		}
		return border;
	}
}

}
