using Object = UnityEngine.Object;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class ImageProxy : IDisposable
{
	[SerializeField]
	private ImageLoaderProxy mImageLoaderProxy;

	[SerializeField]
	private Image mImage;

	public bool IsVaild => (Object)(object)mImage != (Object)null;

	public GameObject GameObject => ((Component)mImage).gameObject;

	public Transform Transform => ((Component)mImage).transform;

	public RectTransform RectTransform => ((Graphic)mImage).rectTransform;

	public Sprite Sprite => mImage.sprite;

	public Sprite overrideSprite => mImage.overrideSprite;

	public bool HasBorder => mImage.hasBorder;

	public float PixelsPerUnit => mImage.pixelsPerUnit;

	public float MinWidth => mImage.minWidth;

	public float PreferredWidth => mImage.preferredWidth;

	public float FlexibleWidth => mImage.flexibleWidth;

	public float MinHeight => mImage.minHeight;

	public float PreferredHeight => mImage.preferredHeight;

	public float FlexibleHeight => mImage.flexibleHeight;

	public int LayoutPriority => mImage.layoutPriority;

	public Image.Type Type
	{
		get
		{
			return mImage.type;
		}
		set
		{
			mImage.type = value;
		}
	}

	public bool PreserveAspect
	{
		get
		{
			return mImage.preserveAspect;
		}
		set
		{
			mImage.preserveAspect = value;
		}
	}

	public bool FillCenter
	{
		get
		{
			return mImage.fillCenter;
		}
		set
		{
			mImage.fillCenter = value;
		}
	}

	public Image.FillMethod FillMethod
	{
		get
		{
			return mImage.fillMethod;
		}
		set
		{
			mImage.fillMethod = value;
		}
	}

	public float FillAmount
	{
		get
		{
			return mImage.fillAmount;
		}
		set
		{
			mImage.fillAmount = value;
		}
	}

	public bool FillClockwise
	{
		get
		{
			return mImage.fillClockwise;
		}
		set
		{
			mImage.fillClockwise = value;
		}
	}

	public int FillOrigin
	{
		get
		{
			return mImage.fillOrigin;
		}
		set
		{
			mImage.fillOrigin = value;
		}
	}

	public float AlphaHitTestMinimumThreshold
	{
		get
		{
			return mImage.alphaHitTestMinimumThreshold;
		}
		set
		{
			mImage.alphaHitTestMinimumThreshold = value;
		}
	}

	public bool UseSpriteMesh
	{
		get
		{
			return mImage.useSpriteMesh;
		}
		set
		{
			mImage.useSpriteMesh = value;
		}
	}

	public Texture MainTexture => ((Graphic)mImage).mainTexture;

	public float PixelsPerUnitMultiplier
	{
		get
		{
			return mImage.pixelsPerUnitMultiplier;
		}
		set
		{
			mImage.pixelsPerUnitMultiplier = value;
		}
	}

	public Material Material
	{
		get
		{
			return ((Graphic)mImage).material;
		}
		set
		{
			((Graphic)mImage).material = value;
		}
	}

	public Color Color
	{
		get
		{
			return ((Graphic)mImage).color;
		}
		set
		{
			((Graphic)mImage).color = value;
		}
	}

	public void SetNativeSize()
	{
		((Graphic)mImage).SetNativeSize();
	}

	public void CalculateLayoutInputHorizontal()
	{
		mImage.CalculateLayoutInputHorizontal();
	}

	public void CalculateLayoutInputVertical()
	{
		mImage.CalculateLayoutInputVertical();
	}

	public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
	{
		return mImage.IsRaycastLocationValid(screenPoint, eventCamera);
	}

	public void DisableSpriteOptimizations()
	{
		mImage.DisableSpriteOptimizations();
	}

	public void Load(string spriteName)
	{
		mImageLoaderProxy.Load(spriteName);
	}

	public void LoadAsync(string spriteName)
	{
		mImageLoaderProxy.LoadAsync(spriteName);
	}

	public void LoadByGuid(string guid)
	{
		mImageLoaderProxy.LoadByGuid(guid);
	}

	public void LoadByGuidAsync(string guid)
	{
		mImageLoaderProxy.LoadByGuidAsync(guid);
	}

	public void Clear()
	{
		if ((Object)(object)mImage != (Object)null)
		{
			mImage.sprite = null;
		}
		mImageLoaderProxy.Clear();
	}

	public void Dispose()
	{
		if ((Object)(object)mImage != (Object)null)
		{
			mImage.sprite = null;
		}
		mImageLoaderProxy.Dispose();
	}
}

}
