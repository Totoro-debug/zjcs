// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using EmissionModule = UnityEngine.ParticleSystem.EmissionModule;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.StoryFollowBone
{
public class MyEffectFadeOut : MonoBehaviour
{
	[SerializeField]
	private AnimationCurve mFadeCurve = new AnimationCurve(new Keyframe[2]
	{
		new Keyframe(0f, 1f),
		new Keyframe(1f, 0f)
	});

	[SerializeField]
	private float mFadeoutTime = 1f;

	public ParticleSystem[] myParticles;

	public SpriteRenderer[] myRenderers;

	public TMP_Text[] mTMPText;

	[Tooltip("是否使用启用粒子状态来处理淡出，不启用则用粒子的颜色来淡出")]
	[SerializeField]
	private bool myParticlesUseEnabledForFade = true;

	private readonly string kParticleMatColorPropName = "_MainColor";

	private List<bool> mParticlesEnabledList = new List<bool>();

	private List<Material> mParticlesMaterialList = new List<Material>();

	private List<Color> mRendererColorList = new List<Color>();

	private List<Color32> mTMPTextColorList = new List<Color32>();

	private CancellationTokenSource mCancellationTokenSource;

	private void OnValidate()
	{
		if (myParticles != null)
		{
			ParticleSystem[] array = myParticles;
			for (int i = 0; i < array.Length; i++)
			{
				if ((Object)(object)array[i] == (Object)null)
				{
					UnityEngine.Debug.LogError("Official playable diagnostic");
				}
			}
		}
		if (myRenderers != null)
		{
			SpriteRenderer[] array2 = myRenderers;
			for (int i = 0; i < array2.Length; i++)
			{
				if ((Object)(object)array2[i] == (Object)null)
				{
					UnityEngine.Debug.LogError("Official playable diagnostic");
				}
			}
		}
		if (mTMPText == null)
		{
			return;
		}
		TMP_Text[] array3 = mTMPText;
		for (int i = 0; i < array3.Length; i++)
		{
			if ((Object)(object)array3[i] == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
		}
	}

	public void Resume(MyPlayable playable)
	{
		StopFadeoutTask();
		if (myParticlesUseEnabledForFade)
		{
			ResumParticlesEnable();
		}
		ResumeRenderersColor();
		ResumeTMPTextColor();
		playable.OnStop -= new Action<MyPlayable>(Resume);
	}

	public void FadeOut()
	{
		FadeOut(mFadeoutTime);
	}

	public void FadeOut(float duration)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (myParticlesUseEnabledForFade)
		{
			GetParticleEnableList();
		}
		else
		{
			GetParticleMaterialList();
		}
		GetRendererColorList();
		GetTMPTextColor();
		if (myParticlesUseEnabledForFade)
		{
			SetParticlesEnable(enable: false);
		}
		StopFadeoutTask();
		mCancellationTokenSource = new CancellationTokenSource();
		UniTaskVoid val = FadeOutColor(duration, mCancellationTokenSource.Token);
		val.Forget();
	}

	public void StopFadeoutTask()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	private async UniTaskVoid FadeOutColor(float duration, CancellationToken cancellationToken)
	{
		float time = 0f;
		while (true)
		{
			time += UnityEngine.Time.deltaTime;
			float alpha = mFadeCurve.Evaluate(time / duration);
			if (!myParticlesUseEnabledForFade) SetParticlesColor(alpha);
			SetRendersColor(alpha);
			SetTMPTextAlpha(alpha, mTMPTextColorList);
			if (time >= duration) return;
			await UniTask.NextFrame(cancellationToken, false);
		}
	}

	private void SetParticlesEnable(bool enable)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		ParticleSystem[] array = myParticles;
		for (int i = 0; i < array.Length; i++)
		{
			EmissionModule emission = array[i].emission;
			emission.enabled = false;
		}
	}

	private void SetParticlesColor(float value)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		foreach (Material mParticlesMaterial in mParticlesMaterialList)
		{
			Color color = mParticlesMaterial.GetColor(kParticleMatColorPropName);
			color.a = value;
			mParticlesMaterial.SetColor(kParticleMatColorPropName, color);
		}
	}

	private void SetRendersColor(float value)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < myRenderers.Length; i++)
		{
			myRenderers[i].color = mRendererColorList[i] * value;
		}
	}

	private void ResumParticlesEnable()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		int i = 0;
		for (int num = myParticles.Length; i < num; i++)
		{
			EmissionModule emission = myParticles[i].emission;
			emission.enabled = mParticlesEnabledList[i];
		}
	}

	private void ResumeRenderersColor()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		int i = 0;
		for (int num = myRenderers.Length; i < num; i++)
		{
			myRenderers[i].color = mRendererColorList[i];
		}
	}

	private void ResumeTMPTextColor()
	{
		if (mTMPTextColorList.Count > 0)
		{
			SetTMPTextAlpha(1f, mTMPTextColorList);
		}
	}

	private void GetParticleEnableList()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		mParticlesEnabledList.Clear();
		ParticleSystem[] array = myParticles;
		foreach (ParticleSystem val in array)
		{
			List<bool> list = mParticlesEnabledList;
			EmissionModule emission = val.emission;
			list.Add(emission.enabled);
		}
	}

	private void GetParticleMaterialList()
	{
		mParticlesMaterialList.Clear();
		int i = 0;
		for (int num = myParticles.Length; i < num; i++)
		{
			ParticleSystemRenderer component = ((Component)myParticles[i]).GetComponent<ParticleSystemRenderer>();
			mParticlesMaterialList.Add(((Renderer)component).material);
		}
	}

	private void GetRendererColorList()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		mRendererColorList.Clear();
		SpriteRenderer[] array = myRenderers;
		foreach (SpriteRenderer val in array)
		{
			mRendererColorList.Add(val.color);
		}
	}

	private void GetTMPTextColor()
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		mTMPTextColorList.Clear();
		TMP_Text[] array = mTMPText;
		for (int i = 0; i < array.Length; i++)
		{
			TMP_TextInfo textInfo = array[i].textInfo;
			for (int j = 0; j < textInfo.characterCount; j++)
			{
				if (textInfo.characterInfo[j].isVisible)
				{
					int materialReferenceIndex = textInfo.characterInfo[j].materialReferenceIndex;
					Color32[] colors = textInfo.meshInfo[materialReferenceIndex].colors32;
					int vertexIndex = textInfo.characterInfo[j].vertexIndex;
					mTMPTextColorList.Add(colors[vertexIndex]);
					mTMPTextColorList.Add(colors[vertexIndex + 1]);
					mTMPTextColorList.Add(colors[vertexIndex + 2]);
					mTMPTextColorList.Add(colors[vertexIndex + 3]);
				}
			}
		}
	}

	private void SetTMPTextAlpha(float value, List<Color32> rawVertexColors)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		TMP_Text[] array = mTMPText;
		foreach (TMP_Text val in array)
		{
			TMP_TextInfo textInfo = val.textInfo;
			for (int j = 0; j < textInfo.characterCount; j++)
			{
				if (textInfo.characterInfo[j].isVisible)
				{
					int materialReferenceIndex = textInfo.characterInfo[j].materialReferenceIndex;
					ref TMP_MeshInfo originalMeshInfo = ref textInfo.meshInfo[materialReferenceIndex];
					int vertexIndex = textInfo.characterInfo[j].vertexIndex;
					textInfo.meshInfo[materialReferenceIndex].colors32[vertexIndex].a = (byte)((float)(int)rawVertexColors[num++].a * value);
					textInfo.meshInfo[materialReferenceIndex].colors32[vertexIndex + 1].a = (byte)((float)(int)rawVertexColors[num++].a * value);
					textInfo.meshInfo[materialReferenceIndex].colors32[vertexIndex + 2].a = (byte)((float)(int)rawVertexColors[num++].a * value);
					textInfo.meshInfo[materialReferenceIndex].colors32[vertexIndex + 3].a = (byte)((float)(int)rawVertexColors[num++].a * value);
				}
			}
			val.UpdateVertexData((TMP_VertexDataUpdateFlags)16);
		}
	}

	public static void EffectFadeOut(GameObject go, float duration, MyPlayable playable)
	{
		MyEffectFadeOut component = go.GetComponent<MyEffectFadeOut>();
		if ((Object)(object)component == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		else
		{
			EffectFadeOut(component, duration, playable);
		}
	}

	public static void EffectFadeOut(MyEffectFadeOut effectFadeOut, float duration, MyPlayable playable)
	{
		if (!((Object)(object)effectFadeOut == (Object)null))
		{
			playable.OnStop += new Action<MyPlayable>(effectFadeOut.Resume);
			effectFadeOut.FadeOut(duration);
		}
	}

	public static void EffectFadeOut(MyEffectFadeOut effectFadeOut, MyPlayable playable)
	{
		if (!((Object)(object)effectFadeOut == (Object)null))
		{
			EffectFadeOut(effectFadeOut, effectFadeOut.mFadeoutTime, playable);
		}
	}

	public MyEffectFadeOut()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected Obj, but got Unknown
	}
}

}
