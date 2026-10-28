using System;
using System.Runtime.CompilerServices;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13EntityRendering
{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlicedFilledRender : UIBehaviour, ISerializationCallbackReceiver
{
	public enum FillMode
	{
		Left,
		Right,
		Bottom,
		Top,
		Radial
	}

	public enum FillOrigin
	{
		Bottom,
		Right,
		Top,
		Left
	}

	private static readonly VertexHelper sVertexHelper = new VertexHelper();

	[SerializeField]
	private Material mMaterial;

	[SerializeField]
	private Sprite mSprite;

	[SerializeField]
	private Vector2 mSize;

	[SerializeField]
	private Color mColor = Color.white;

	[SerializeField]
	private FillMode mFillMode;

	[SerializeField]
	private FillOrigin mFillOrigin;

	[SerializeField]
	private bool mAnticlockwise;

	[Range(0f, 1f)]
	[SerializeField]
	private float mFillAmount = 1f;

	private MeshFilter mFilter;

	private MeshRenderer mRenderer;

	private Mesh mMesh;

	private RectTransform mRectTransform;

	private Transform mTransform;

	private Color mLinearColor;

	private bool mInited;

	private bool mDirty;

	public void BindOriginalSprite(Sprite sprite)
	{
		if (mInited) throw new InvalidOperationException("Bind original Sprite before entity activation.");
		mSprite = sprite;
	}

	public float FillAmount
	{
		get
		{
			return mFillAmount;
		}
		set
		{
			float num = Mathf.Clamp01(value);
			if (mFillAmount != num)
			{
				mFillAmount = num;
				SetDirty();
			}
		}
	}

	public Vector2 Size
	{
		get
		{
			return mSize;
		}
		set
		{
			if (!(mSize == value))
			{
				mSize = value;
				SetDirty();
			}
		}
	}

	public Color Color
	{
		get
		{
			return mColor;
		}
		set
		{
			if (!(mColor == value))
			{
				mColor = value;
				mLinearColor = mColor.linear;
				SetDirty();
			}
		}
	}

	private SlicedFilledRenderService mSlicedFilledRenderService => SlicedFilledRenderService.Current;

	protected override void Awake()
	{
		Init();
		SetDirty();
	}

	protected override void OnDestroy()
	{
		if (mDirty)
		{
			mDirty = false;
			mSlicedFilledRenderService.UnregisterForRefresh(this);
		}
		if (mInited)
		{
			mInited = false;
			mFilter.mesh = null;
			Object.Destroy((Object)(object)mMesh);
		}
	}

	public void Init()
	{
		if (!mInited)
		{
			mFilter = ((Component)this).GetComponent<MeshFilter>();
			mRenderer = ((Component)this).GetComponent<MeshRenderer>();
			mRectTransform = ((Component)this).GetComponent<RectTransform>();
			mTransform = ((Component)this).transform;
			((Renderer)mRenderer).sharedMaterial = mMaterial;
			if ((Object)(object)((Renderer)mRenderer).sharedMaterial != (Object)null && (Object)(object)mSprite != (Object)null)
			{
				MaterialPropertyBlock val = new MaterialPropertyBlock();
				((Renderer)mRenderer).GetPropertyBlock(val);
				val.SetTexture("_MainTex", (Texture)(object)mSprite.texture);
				((Renderer)mRenderer).SetPropertyBlock(val);
			}
			mMesh = new Mesh();
			((Object)mMesh).name = "SlicedFilledMesh";
			mFilter.mesh = mMesh;
			mInited = true;
		}
	}

	protected override void OnRectTransformDimensionsChange()
	{
		SetDirty();
	}

	public void SetDirty()
	{
		if (!mDirty)
		{
			mDirty = true;
			mSlicedFilledRenderService.RegisterForRefresh(this);
		}
	}

	public unsafe void RefreshMesh()
	{
		mDirty = false;
		if (!mInited)
		{
			return;
		}
		if ((Object)(object)mSprite == (Object)null || mFillAmount == 0f)
		{
			mMesh.Clear();
			return;
		}
		Vector2 size;
		if (!((Object)(object)mRectTransform == (Object)null))
		{
			Rect rect = mRectTransform.rect;
			size = rect.size;
		}
		else
		{
			size = mSize;
		}
		Vector2 val = size;
		Vector3 localPosition = mTransform.localPosition;
		float pixelsPerUnit = mSprite.pixelsPerUnit;
		Rect rect2 = mSprite.rect;
		Vector2 pivot = mSprite.pivot;
		Rect adjustedRect = new Rect(localPosition.x / pixelsPerUnit - val.x * pivot.x / rect2.width, localPosition.y / pixelsPerUnit - val.y * pivot.y / rect2.height, rect2.width / pixelsPerUnit, rect2.height / pixelsPerUnit);
		Vector4 outerUV = DataUtility.GetOuterUV(mSprite);
		Vector4 padding = DataUtility.GetPadding(mSprite);
		padding /= pixelsPerUnit;
		Vector2 val2 = new Vector2(padding.x + adjustedRect.x, padding.y + adjustedRect.y);
		if (mFillMode == FillMode.Radial)
		{
			GenerateClockFilledSprite(sVertexHelper, new Vector4(val2.x, val2.y, val2.x + adjustedRect.width * val.x * pixelsPerUnit / rect2.width, val2.y + adjustedRect.height * val.y * pixelsPerUnit / rect2.height), outerUV, mFillAmount);
			sVertexHelper.FillMesh(mMesh);
			sVertexHelper.Clear();
			return;
		}
		Vector4 innerUV = DataUtility.GetInnerUV(mSprite);
		Vector4 adjustedBorders = GetAdjustedBorders(mSprite.border, adjustedRect);
		Vector2* ptr = stackalloc Vector2[4]
		{
			val2,
			new Vector2(adjustedBorders.x + adjustedRect.x, adjustedBorders.y + adjustedRect.y),
			new Vector2(val.x - adjustedBorders.z + adjustedRect.x, val.y - adjustedBorders.w + adjustedRect.y),
			new Vector2(val.x - padding.z + adjustedRect.x, val.y - padding.w + adjustedRect.y)
		};
		ReadOnlySpan<Vector2> val3 = new ReadOnlySpan<Vector2>((void*)ptr, 4);
		Vector2* ptr2 = stackalloc Vector2[4]
		{
			new Vector2(outerUV.x, outerUV.y),
			new Vector2(innerUV.x, innerUV.y),
			new Vector2(innerUV.z, innerUV.w),
			new Vector2(outerUV.z, outerUV.w)
		};
		ReadOnlySpan<Vector2> val4 = new ReadOnlySpan<Vector2>((void*)ptr2, 4);
		Vector2 val5 = new Vector2(((Vector2)val3[3]).x - val2.x, ((Vector2)val3[3]).y - val2.y);
		val5.x = ((val5.x > 0f) ? (1f / val5.x) : 1f);
		val5.y = ((val5.y > 0f) ? (1f / val5.y) : 1f);
		for (int i = 0; i < 3; i++)
		{
			int num = i + 1;
			for (int j = 0; j < 3; j++)
			{
				int num2 = j + 1;
				float num3 = 0f;
				float num4 = 0f;
				Vector4 vertices = Vector4.zero;
				Vector4 uvs = Vector4.zero;
				switch (mFillMode)
				{
				case FillMode.Left:
					num3 = (((Vector2)val3[i]).x - val2.x) * val5.x;
					num4 = (((Vector2)val3[num]).x - val2.x) * val5.x;
					vertices = new Vector4(((Vector2)val3[i]).x, ((Vector2)val3[j]).y, ((Vector2)val3[num]).x, ((Vector2)val3[num2]).y);
					uvs = new Vector4(((Vector2)val4[i]).x, ((Vector2)val4[j]).y, ((Vector2)val4[num]).x, ((Vector2)val4[num2]).y);
					break;
				case FillMode.Right:
					num3 = 1f - (((Vector2)val3[num]).x - val2.x) * val5.x;
					num4 = 1f - (((Vector2)val3[i]).x - val2.x) * val5.x;
					vertices = new Vector4(((Vector2)val3[num]).x, ((Vector2)val3[j]).y, ((Vector2)val3[i]).x, ((Vector2)val3[num2]).y);
					uvs = new Vector4(((Vector2)val4[num]).x, ((Vector2)val4[j]).y, ((Vector2)val4[i]).x, ((Vector2)val4[num2]).y);
					break;
				case FillMode.Bottom:
					num3 = (((Vector2)val3[j]).y - val2.y) * val5.y;
					num4 = (((Vector2)val3[num2]).y - val2.y) * val5.y;
					vertices = new Vector4(((Vector2)val3[i]).x, ((Vector2)val3[j]).y, ((Vector2)val3[num]).x, ((Vector2)val3[num2]).y);
					uvs = new Vector4(((Vector2)val4[i]).x, ((Vector2)val4[j]).y, ((Vector2)val4[num]).x, ((Vector2)val4[num2]).y);
					break;
				case FillMode.Top:
					num3 = 1f - (((Vector2)val3[num2]).y - val2.y) * val5.y;
					num4 = 1f - (((Vector2)val3[j]).y - val2.y) * val5.y;
					vertices = new Vector4(((Vector2)val3[i]).x, ((Vector2)val3[num2]).y, ((Vector2)val3[num]).x, ((Vector2)val3[j]).y);
					uvs = new Vector4(((Vector2)val4[i]).x, ((Vector2)val4[num2]).y, ((Vector2)val4[num]).x, ((Vector2)val4[j]).y);
					break;
				}
				if (!(num3 >= mFillAmount))
				{
					GenerateFilledSprite(sVertexHelper, vertices, uvs, (mFillAmount - num3) / (num4 - num3));
				}
			}
		}
		sVertexHelper.FillMesh(mMesh);
		sVertexHelper.Clear();
	}

	private unsafe void GenerateFilledSprite(VertexHelper vh, Vector4 vertices, Vector4 uvs, float fillAmount)
	{
		float x = uvs.x;
		float y = uvs.y;
		float num = uvs.z;
		float num2 = uvs.w;
		if (fillAmount < 1f)
		{
			switch (mFillMode)
			{
			case FillMode.Left:
			case FillMode.Right:
				vertices.z = vertices.x + (vertices.z - vertices.x) * fillAmount;
				num = x + (num - x) * fillAmount;
				break;
			case FillMode.Bottom:
			case FillMode.Top:
				vertices.w += (vertices.y - vertices.w) * (1f - fillAmount);
				num2 += (y - num2) * (1f - fillAmount);
				break;
			}
		}
		Vector3* ptr = stackalloc Vector3[4]
		{
			new Vector3(vertices.x, vertices.y),
			new Vector3(vertices.x, vertices.w),
			new Vector3(vertices.z, vertices.w),
			new Vector3(vertices.z, vertices.y)
		};
		ReadOnlySpan<Vector3> quadPositions = new ReadOnlySpan<Vector3>((void*)ptr, 4);
		Vector3* ptr2 = stackalloc Vector3[4]
		{
			(Vector3)new Vector2(x, y),
			(Vector3)new Vector2(x, num2),
			(Vector3)new Vector2(num, num2),
			(Vector3)new Vector2(num, y)
		};
		ReadOnlySpan<Vector3> quadUVs = new ReadOnlySpan<Vector3>((void*)ptr2, 4);
		AddQuad(vh, quadPositions, (Color32)mLinearColor, quadUVs);
	}

	private void GenerateClockFilledSprite(VertexHelper vh, Vector4 vertices, Vector4 uvs, float fillAmount)
	{
		int num = mFillOrigin switch
		{
			FillOrigin.Bottom => 0,
			FillOrigin.Right => 1,
			FillOrigin.Top => 2,
			FillOrigin.Left => 3,
			_ => 0,
		};
		Vector3[] array = new Vector3[4];
		Vector3[] array2 = new Vector3[4];
		for (int i = 0; i < 4; i++)
		{
			float num2 = ((i < 2) ? 0f : 0.5f);
			float num3 = ((i < 2) ? 0.5f : 1f);
			float num4 = ((i == 0 || i == 3) ? 0f : 0.5f);
			float num5 = ((i == 0 || i == 3) ? 0.5f : 1f);
			array[0].x = Mathf.Lerp(vertices.x, vertices.z, num2);
			array[1].x = array[0].x;
			array[2].x = Mathf.Lerp(vertices.x, vertices.z, num3);
			array[3].x = array[2].x;
			array[0].y = Mathf.Lerp(vertices.y, vertices.w, num4);
			array[1].y = Mathf.Lerp(vertices.y, vertices.w, num5);
			array[2].y = array[1].y;
			array[3].y = array[0].y;
			array2[0].x = Mathf.Lerp(uvs.x, uvs.z, num2);
			array2[1].x = array2[0].x;
			array2[2].x = Mathf.Lerp(uvs.x, uvs.z, num3);
			array2[3].x = array2[2].x;
			array2[0].y = Mathf.Lerp(uvs.y, uvs.w, num4);
			array2[1].y = Mathf.Lerp(uvs.y, uvs.w, num5);
			array2[2].y = array2[1].y;
			array2[3].y = array2[0].y;
			bool flag = mAnticlockwise;
			float num6 = (flag ? (fillAmount * 4f - (float)(3 - (i + num) % 4)) : (fillAmount * 4f - (float)((i + num) % 4)));
			int num7 = (i + 2) % 4;
			if ((num7 & 1) == 1)
			{
				flag = !flag;
			}
			float num8 = Mathf.Clamp01(num6);
			if (!flag)
			{
				num8 = 1f - num8;
			}
			num8 *= (float)Math.PI / 2f;
			float cos = Mathf.Cos(num8);
			float sin = Mathf.Sin(num8);
			RadialCut(array, cos, sin, !flag, num7);
			RadialCut(array2, cos, sin, !flag, num7);
			AddQuad(vh, new ReadOnlySpan<Vector3>(array), (Color32)mLinearColor, new ReadOnlySpan<Vector3>(array2));
		}
	}

	private void RadialCut(Vector3[] xy, float cos, float sin, bool invert, int corner)
	{
		int num = (corner + 1) % 4;
		int num2 = (corner + 2) % 4;
		int num3 = (corner + 3) % 4;
		if ((corner & 1) == 1)
		{
			if (sin > cos)
			{
				cos /= sin;
				sin = 1f;
				if (invert)
				{
					xy[num].x = Mathf.Lerp(xy[corner].x, xy[num2].x, cos);
					xy[num2].x = xy[num].x;
				}
			}
			else if (cos > sin)
			{
				sin /= cos;
				cos = 1f;
				if (!invert)
				{
					xy[num2].y = Mathf.Lerp(xy[corner].y, xy[num2].y, sin);
					xy[num3].y = xy[num2].y;
				}
			}
			else
			{
				cos = 1f;
				sin = 1f;
			}
			if (!invert)
			{
				xy[num3].x = Mathf.Lerp(xy[corner].x, xy[num2].x, cos);
			}
			else
			{
				xy[num].y = Mathf.Lerp(xy[corner].y, xy[num2].y, sin);
			}
			return;
		}
		if (cos > sin)
		{
			sin /= cos;
			cos = 1f;
			if (!invert)
			{
				xy[num].y = Mathf.Lerp(xy[corner].y, xy[num2].y, sin);
				xy[num2].y = xy[num].y;
			}
		}
		else if (sin > cos)
		{
			cos /= sin;
			sin = 1f;
			if (invert)
			{
				xy[num2].x = Mathf.Lerp(xy[corner].x, xy[num2].x, cos);
				xy[num3].x = xy[num2].x;
			}
		}
		else
		{
			cos = 1f;
			sin = 1f;
		}
		if (invert)
		{
			xy[num3].y = Mathf.Lerp(xy[corner].y, xy[num2].y, sin);
		}
		else
		{
			xy[num].x = Mathf.Lerp(xy[corner].x, xy[num2].x, cos);
		}
	}

	private unsafe void AddQuad(VertexHelper vertexHelper, ReadOnlySpan<Vector3> quadPositions, Color32 color, ReadOnlySpan<Vector3> quadUVs)
	{
		int currentVertCount = vertexHelper.currentVertCount;
		for (int i = 0; i < 4; i++)
		{
			vertexHelper.AddVert(quadPositions[i], color, (Vector4)quadUVs[i]);
		}
		vertexHelper.AddTriangle(currentVertCount, currentVertCount + 1, currentVertCount + 2);
		vertexHelper.AddTriangle(currentVertCount + 2, currentVertCount + 3, currentVertCount);
	}

	private Vector4 GetAdjustedBorders(Vector4 border, Rect adjustedRect)
	{
		Rect rect = mSprite.rect;
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

	protected override void OnDidApplyAnimationProperties()
	{
		mLinearColor = mColor.linear;
		SetDirty();
	}

	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize()
	{
		mLinearColor = mColor.linear;
	}

	public SlicedFilledRender()
	{
		Color white = Color.white;
		mLinearColor = white.linear;
	}

	static SlicedFilledRender()
	{
	}
}

}
