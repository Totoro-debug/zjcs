// Original StencilMask; scene-container instantiation maps to local Unity objects.
namespace Zjcs.Official.HomeScreenUI
{
using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[RequireComponent(typeof(Image))]
public class StencilMask : MonoBehaviour
{
	private GameObject mCopy;

	private Image mCopyImage;

	private int mCompId = Shader.PropertyToID("_StencilComp");

	private int mStencilId = Shader.PropertyToID("_Stencil");

	private int mOpId = Shader.PropertyToID("_StencilOp");

	private int mColorId = Shader.PropertyToID("_ColorMask");

	private Image mMyImage;

	public int StencilVal = 5;

	private bool mInited;

	private void Start()
	{
		Init();
	}

	private void Init()
	{


		if (!mInited)
		{
			mMyImage = ((Component)this).GetComponent<Image>();
			mCopy = Object.Instantiate(((Component)this).gameObject, ((Component)this).transform.parent, false);
			((Object)mCopy).name = "Stencil";
			Object.Destroy((Object)(object)mCopy.GetComponent<StencilMask>());
			mCopyImage = mCopy.GetComponent<Image>();
			Material val = new Material(Shader.Find("UI/Default"));
			val.SetFloat(mCompId, 8f);
			val.SetFloat(mStencilId, (float)StencilVal);
			val.SetFloat(mOpId, 2f);
			val.SetFloat(mColorId, 0f);
			((Graphic)mCopyImage).material = val;
			Canvas val2 = mCopy.AddComponent<Canvas>();
			val2.overrideSorting = true;
			val2.sortingOrder = -1;
			mInited = true;
		}
	}

	public void SetVisible(bool value)
	{
		if ((Object)(object)mCopy != (Object)null)
		{
			mCopy.SetActive(value);
		}
		((Component)this).gameObject.SetActive(value);
	}

	public void SetColor(Color color)
	{

		if (!mInited)
		{
			Init();
		}
		((Graphic)mMyImage).color = color;
	}
}

}
