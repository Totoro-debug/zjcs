// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoryMaskGraphic : MaskableGraphic, ICanvasRaycastFilter
{
	[Header("圆角圆半径")]
	public float Radius = 10f;

	[Header("每个扇形三角形个数")]
	public int TriangleNum = 6;

	[Header("是否显示镂空")]
	[Space(25f)]
	public bool ShowHollowOut = true;

	public GameObject TargetAreaPrefab;

	public CanvasGroup MaskCG;

	[Header("指引文本处理")]
	public ProcessGuideHint ProcessGuideHint;

	[Header("特效部分Go")]
	public List<GameObject> EffectGameObejects;

	private RectTransform mTarget;

	private Vector2 mTargetMin;

	private Vector2 mTargetMax;

	private Vector2 mInner_rt;

	private Vector2 mInner_lb;

	private Vector2 mOuter_rt;

	private Vector2 mOuter_lb;

	private RectTransform mTargetArea;

	private Vector3 mTmpScale;

	private Coroutine mCoroutine;

	private WaitForEndOfFrame mWait = new WaitForEndOfFrame();

	private bool mShowMask;

	private IStoryMaskManager MaskManager { get; set; }
	public void Bind(IStoryMaskManager manager) => MaskManager = manager;

	public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
	{

		return !RectTransformUtility.RectangleContainsScreenPoint(mTargetArea, sp, eventCamera);
	}

	public void Close()
	{
		if ((Object)(object)mTargetArea != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)mTargetArea).gameObject);
		}
		mTarget = null;
		((Component)this).gameObject.SetActive(false);
		if (mCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mCoroutine);
		}
		MaskManager.CloseColorMask();
	}

	public void SetMaskState(bool isShow)
	{
		if (isShow)
		{
			MaskCG.alpha = (mShowMask ? 1 : 0);
		}
		if ((Object)(object)mTargetArea != (Object)null)
		{
			((Component)mTargetArea).gameObject.SetActive(isShow);
		}
		((Component)this).gameObject.SetActive(isShow);
	}

	private void SetEffectActiveState(bool state)
	{
		foreach (GameObject effectGameObeject in EffectGameObejects)
		{
			effectGameObeject.SetActive(state);
		}
	}

	public void Play(RectTransform target, Vector3 scale, Color maskColor, bool showMask = true, string hintKey = "", params object[] args)
	{

		if ((Object)(object)mTargetArea != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)mTargetArea).gameObject);
		}
		mShowMask = showMask;
		bool flag = MaskManager.CanShowMask();
		if (flag)
		{
			MaskCG.alpha = (showMask ? 1 : 0);
		}
		else
		{
			MaskCG.alpha = 0f;
		}
		((Graphic)this).color = maskColor;
		SetEffectActiveState(showMask);
		Camera uiCamera = StoryMaskProjection.GetUiCamera(this);
		Vector2 val = RectTransformUtility.WorldToScreenPoint(uiCamera, ((Transform)target).position);
		Vector2 val2 = default;
		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(((Graphic)this).rectTransform, val, uiCamera, out val2))
		{
			((Component)this).gameObject.SetActive(true);
			mTarget = target;
			GameObject val3 = StoryMaskProjection.Instantiate(TargetAreaPrefab, ((Transform)mTarget).parent);
			mTargetArea = val3.GetComponent<RectTransform>();
			((Transform)mTargetArea).localScale = scale;
			((Component)mTargetArea).gameObject.SetActive(flag);
			mTmpScale = scale;
			LateUpdate();
			if (!string.IsNullOrEmpty(hintKey))
			{
				((Component)ProcessGuideHint).gameObject.SetActive(true);
				ProcessGuideHint.SetHint(hintKey, args);
				ProcessGuideHint.SetTransform(mTargetArea.anchoredPosition, (mTargetArea.anchoredPosition), mTargetArea.rect);
			}
			else
			{
				((Component)ProcessGuideHint).gameObject.SetActive(false);
			}
			if (mCoroutine != null)
			{
				((MonoBehaviour)this).StopCoroutine(mCoroutine);
			}
			MaskManager.SetColorMask();
			mCoroutine = ((MonoBehaviour)this).StartCoroutine(WaitToCloseFullMask(flag));
		}
	}

	private IEnumerator WaitToCloseFullMask(bool canShow)
	{
		yield return mWait;
		MaskManager.CloseColorMask();
		if (!canShow)
		{
			SetMaskState(isShow: false);
		}
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{

		vh.Clear();
		Rect rect = ((Graphic)this).rectTransform.rect;
		mInner_lb = mTargetMin;
		mInner_rt = mTargetMax;
		mOuter_rt = new Vector2(rect.width / 2f, rect.height / 2f);
		mOuter_lb = new Vector2((0f - rect.width) / 2f, (0f - rect.height) / 2f);
		float tw = Mathf.Abs(mInner_lb.x - mInner_rt.x);
		float th = Mathf.Abs(mInner_lb.y - mInner_rt.y);
		if (TriangleNum <= 0)
		{
			TriangleNum = 1;
		}
		UIVertex simpleVert = UIVertex.simpleVert;
		simpleVert.color = (((Graphic)this).color);
		simpleVert.position = (new Vector2(mOuter_lb.x, mOuter_lb.y));
		vh.AddVert(simpleVert);
		simpleVert.position = (new Vector2(mOuter_lb.x, mOuter_rt.y));
		vh.AddVert(simpleVert);
		simpleVert.position = (new Vector2(mOuter_rt.x, mOuter_rt.y));
		vh.AddVert(simpleVert);
		simpleVert.position = (new Vector2(mOuter_rt.x, mOuter_lb.y));
		vh.AddVert(simpleVert);
		simpleVert.position = new Vector3(mInner_lb.x, mInner_lb.y);
		vh.AddVert(simpleVert);
		simpleVert.position = new Vector3(mInner_lb.x, mInner_rt.y);
		vh.AddVert(simpleVert);
		simpleVert.position = new Vector3(mInner_rt.x, mInner_rt.y);
		vh.AddVert(simpleVert);
		simpleVert.position = new Vector3(mInner_rt.x, mInner_lb.y);
		vh.AddVert(simpleVert);
		if (!ShowHollowOut)
		{
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(2, 3, 0);
			return;
		}
		vh.AddTriangle(0, 1, 4);
		vh.AddTriangle(1, 4, 5);
		vh.AddTriangle(1, 5, 2);
		vh.AddTriangle(2, 5, 6);
		vh.AddTriangle(2, 6, 3);
		vh.AddTriangle(6, 3, 7);
		vh.AddTriangle(4, 7, 3);
		vh.AddTriangle(0, 4, 3);
		List<Vector2> list = new List<Vector2>();
		Vector2 item = new Vector2(mInner_lb.x, mInner_lb.y);
		Vector2 item2 = new Vector2(mInner_lb.x, mInner_lb.y + Radius);
		list.Add(item);
		list.Add(item2);
		item = new Vector2(mInner_lb.x, mInner_rt.y);
		item2 = new Vector2(mInner_lb.x + Radius, mInner_rt.y);
		list.Add(item);
		list.Add(item2);
		item = new Vector2(mInner_rt.x, mInner_rt.y);
		item2 = new Vector2(mInner_rt.x, mInner_rt.y - Radius);
		list.Add(item);
		list.Add(item2);
		item = new Vector2(mInner_rt.x, mInner_lb.y);
		item2 = new Vector2(mInner_rt.x - Radius, mInner_lb.y);
		list.Add(item);
		list.Add(item2);
		List<Vector2> list2 = new List<Vector2>();
		item = new Vector2(mInner_lb.x + Radius, mInner_lb.y + Radius);
		item2 = new Vector2(mInner_lb.x, mInner_lb.y + Radius);
		list2.Add(item);
		list2.Add(item2);
		item = new Vector2(mInner_lb.x + Radius, mInner_rt.y - Radius);
		item2 = new Vector2(mInner_lb.x + Radius, mInner_rt.y);
		list2.Add(item);
		list2.Add(item2);
		item = new Vector2(mInner_rt.x - Radius, mInner_rt.y - Radius);
		item2 = new Vector2(mInner_rt.x, mInner_rt.y - Radius);
		list2.Add(item);
		list2.Add(item2);
		item = new Vector2(mInner_rt.x - Radius, mInner_lb.y + Radius);
		item2 = new Vector2(mInner_rt.x - Radius, mInner_lb.y);
		list2.Add(item);
		list2.Add(item2);
		float num = (float)Math.PI / 2f / (float)TriangleNum;
		List<float> list3 = new List<float>
		{
			(float)Math.PI,
			(float)Math.PI / 2f,
			0f,
			4.712389f
		};
		for (int i = 0; i < list2.Count; i += 2)
		{
			float num2 = list3[i / 2];
			AddVert(list[i], tw, th, vh, simpleVert);
			int num3 = vh.currentVertCount;
			int num4 = vh.currentVertCount - 1;
			List<Vector2> list4 = new List<Vector2>();
			for (int j = 0; j < TriangleNum; j++)
			{
				num2 += num;
				if (list4.Count == 0)
				{
					AddVert(list2[i + 1], tw, th, vh, simpleVert);
				}
				else
				{
					simpleVert.position = (list4[j - 1]);
					simpleVert.uv0 = (new Vector2(list4[j - 1].x + 0.5f, list4[j - 1].y + 0.5f));
				}
				Vector2 pos = new Vector2(list2[i].x + Radius * Mathf.Cos(num2), list2[i].y + Radius * Mathf.Sin(num2));
				AddVert(pos, tw, th, vh, simpleVert);
				vh.AddTriangle(num4, num3, num3 + 1);
				num3++;
				list4.Add((simpleVert.position));
			}
		}
	}

	protected Vector2[] GetTextureUVS(Vector2[] vhs, float tw, float th)
	{

		Vector2[] array = new Vector2[vhs.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new Vector2(vhs[i].x / tw + 0.5f, vhs[i].y / th + 0.5f);
		}
		return array;
	}

	protected void AddVert(Vector2 pos0, float tw, float th, VertexHelper vh, UIVertex vert)
	{

		vert.position = (pos0);
		vert.uv0 = (GetTextureUVS(new Vector2[1]
		{
			new Vector2(pos0.x, pos0.y)
		}, tw, th)[0]);
		vh.AddVert(vert);
	}

	private void LateUpdate()
	{

		if (!((Object)(object)mTarget == (Object)null))
		{
			StoryMaskProjection.CopyRectTransformData(((Graphic)this).rectTransform, mTarget, mTargetArea);
			RectTransform val = mTargetArea;
			val.sizeDelta *= (Vector2)mTmpScale;
			RefreshView();
		}
	}

	private void RefreshView()
	{

		Vector2 val2;
		Vector2 val3;
		if ((Object)(object)mTargetArea != (Object)null && ((Component)mTargetArea).gameObject.activeSelf)
		{
			Rect val = StoryMaskProjection.CalculateRelativeRect((Transform)(object)((Graphic)this).rectTransform, (Transform)(object)mTargetArea);
			val2 = val.min;
			val3 = val.max;
		}
		else
		{
			val2 = Vector2.zero;
			val3 = Vector2.zero;
		}
		if (mTargetMin != val2 || mTargetMax != val3)
		{
			mTargetMin = val2;
			mTargetMax = val3;
			((Graphic)this).SetAllDirty();
		}
		ProcessGuideHint.SetTransform(mTargetArea.anchoredPosition, (mTargetArea.anchoredPosition), mTargetArea.rect);
	}

	public StoryMaskGraphic()
	{

	}
}

}
