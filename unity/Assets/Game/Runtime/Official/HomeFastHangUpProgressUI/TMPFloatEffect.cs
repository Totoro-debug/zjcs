// Selected original client; local ownership and native animation endpoints.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;
using AppearanceUI = Zjcs.Official.CombatAppearance.AppearanceUI;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpProgressUI
{

public class TMPFloatEffect : MonoBehaviour
{
	public TMP_Text TMPText;

	[Header("浮动增幅")]
	[Tooltip("倍数增加在 Curve 上的偏移")]
	[Range(0f, 100f)]
	public float FloatDegree = 8f;

	[Header("浮动速度（单位：字符/秒）")]
	[Tooltip("倍数增加时间流逝速度，速度一为正常流逝速度")]
	[Range(0f, 10f)]
	public float FloatSpeed = 1f;

	[Header("浮动间隔（单位：字符数）")]
	[Tooltip("归一化浮动间隔，单位一表示间隔一个字符")]
	[Range(0f, 3f)]
	public float FloatSpan = 1f;

	[Header("垂直偏移轨迹")]
	public AnimationCurve Curve = new AnimationCurve(new Keyframe[3]
	{
		new Keyframe(0f, 0f),
		new Keyframe(0.5f, 1f),
		new Keyframe(1f, 0f)
	});

	private TMP_TextInfo mTextInfo;

	private List<float> mVertexY;

	private bool mDirty = true;

	private int mLength;

	private float mStartTime;

	private int mBufferFrameCount;

	private void Awake()
	{
		SetDirty();
		mVertexY = new List<float>();
	}

	private void OnEnable()
	{
		SetDirty();
		((Graphic)TMPText).RegisterDirtyVerticesCallback((UnityAction)SetDirty);
	}

	private void OnDisable()
	{
		((Graphic)TMPText).UnregisterDirtyVerticesCallback((UnityAction)SetDirty);
	}

	private void SetDirty()
	{
		mDirty = true;
		mBufferFrameCount = 2;
	}

	private void Rebuild()
	{
		mVertexY.Clear();
		mTextInfo = TMPText.textInfo;
		for (int i = 0; i < mTextInfo.characterCount; i++)
		{
			TMP_CharacterInfo val = mTextInfo.characterInfo[i];
			if (val.isVisible)
			{
				int materialReferenceIndex = mTextInfo.characterInfo[i].materialReferenceIndex;
				Vector3[] vertices = mTextInfo.meshInfo[materialReferenceIndex].vertices;
				for (int j = 0; j < 4; j++)
				{
					mVertexY.Add(vertices[val.vertexIndex + j].y);
				}
			}
		}
		mLength = mVertexY.Count / 4;
		mStartTime = Time.time;
		mDirty = false;
	}

	private void TextEffect()
	{
		TMP_TextInfo val = mTextInfo;
		int num = 0;
		float num2 = (float)mLength * FloatSpan + 1f;
		float num3 = (Time.time - mStartTime) * FloatSpeed + num2 - 1f;
		for (int i = 0; i < val.characterCount; i++)
		{
			TMP_CharacterInfo val2 = val.characterInfo[i];
			if (val2.isVisible)
			{
				int materialReferenceIndex = val.characterInfo[i].materialReferenceIndex;
				Vector3[] vertices = val.meshInfo[materialReferenceIndex].vertices;
				float num4 = num / 4;
				float num5 = (num3 - num4 * FloatSpan) % num2;
				num5 = Mathf.Clamp01(num5 - num2 + 1f);
				float num6 = FloatDegree * Curve.Evaluate(num5);
				for (int j = 0; j < 4; j++)
				{
					vertices[val2.vertexIndex + j].y = mVertexY[num++] + num6;
				}
			}
		}
	}

	private void Update()
	{
		if (mDirty)
		{
			if (mBufferFrameCount-- <= 0)
			{
				Rebuild();
			}
		}
		else
		{
			TextEffect();
			TMPText.UpdateVertexData((TMP_VertexDataUpdateFlags)1);
		}
	}

	public TMPFloatEffect()
	{
	}
}

}
