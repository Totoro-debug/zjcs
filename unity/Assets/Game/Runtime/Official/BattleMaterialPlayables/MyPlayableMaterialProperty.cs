// Migrated current official application source; see status material runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleMaterialPlayables
{
[MyPlayableApply(new Type[] { typeof(MeshMaterialProperty) })]
public class MyPlayableMaterialProperty : MyPlayable
{
	[Header("材质属性的基本配置")]
	public string Keyword;

	public bool KeywordBoolValue;

	public string ColorPropertyName;

	public Gradient ColorCurve;

	public string FloatPropertyName;

	public AnimationCurve FloatCurve;

	public bool NeedResume = true;

	[Tooltip("持续时间，<=0时会直接设置成最终值")]
	public float Duration = 0.3f;

	public List<PlayableMaterialPropertyProvider> PropertyProviderList;

	private bool mHasFloat;

	private bool mHasColor;

	private float mTimer;

	private bool mResumed;

	private MeshMaterialProperty mMeshMaterialProperty;

	protected override void PlayImpl()
	{
		base.PlayImpl();
		mMeshMaterialProperty = myApplyComponent as MeshMaterialProperty;
		if ((Object)(object)mMeshMaterialProperty == (Object)null)
		{
			return;
		}
		mMeshMaterialProperty.Init();
		if (!mMeshMaterialProperty.Valid)
		{
			Stop(immediate: true);
			return;
		}
		mHasFloat = !string.IsNullOrEmpty(FloatPropertyName) && mMeshMaterialProperty.HasFloat(FloatPropertyName);
		mHasColor = !string.IsNullOrEmpty(ColorPropertyName) && mMeshMaterialProperty.HasColor(ColorPropertyName);
		mTimer = 0f;
		mResumed = false;
		if (!string.IsNullOrEmpty(Keyword))
		{
			mMeshMaterialProperty.UseKeyword(Keyword, KeywordBoolValue);
		}
		if (mHasFloat)
		{
			mMeshMaterialProperty.UseFloat(FloatPropertyName);
		}
		if (mHasColor)
		{
			mMeshMaterialProperty.UseColor(ColorPropertyName);
		}
		if (PropertyProviderList == null)
		{
			return;
		}
		foreach (PlayableMaterialPropertyProvider propertyProvider in PropertyProviderList)
		{
			propertyProvider.Play(mMeshMaterialProperty);
		}
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		base.ProcessFrameImpl(deltaTime);
		if (!mMeshMaterialProperty.Valid)
		{
			return false;
		}
		mTimer += deltaTime;
		if ((Object)(object)mMeshMaterialProperty == (Object)null)
		{
			return false;
		}
		float time = GetTime();
		Color value = ColorCurve.Evaluate(time);
		float value2 = FloatCurve.Evaluate(time);
		if (mHasColor)
		{
			mMeshMaterialProperty.SetColor(ColorPropertyName, value);
		}
		if (mHasFloat)
		{
			mMeshMaterialProperty.SetFloat(FloatPropertyName, value2);
		}
		if (PropertyProviderList != null)
		{
			foreach (PlayableMaterialPropertyProvider propertyProvider in PropertyProviderList)
			{
				propertyProvider.ProcessFrame(mMeshMaterialProperty);
			}
		}
		return false;
	}

	private float GetTime()
	{
		if (mTimer == 0f)
		{
			return 0f;
		}
		float num = ((Duration > 0f) ? Duration : mTimer);
		if (num == 0f)
		{
			return 0f;
		}
		return mTimer / num;
	}

	protected override void StopImpl()
	{
		TryResumeParam();
		base.StopImpl();
	}

	public override float GetDuration()
	{
		return Duration;
	}

	private void TryResumeParam()
	{
		if (!NeedResume || mResumed || (Object)(object)mMeshMaterialProperty == (Object)null)
		{
			return;
		}
		mResumed = true;
		if (!string.IsNullOrEmpty(Keyword))
		{
			mMeshMaterialProperty.ReturnKeyword(Keyword);
		}
		if (mHasColor)
		{
			mMeshMaterialProperty.ReturnColor(ColorPropertyName);
		}
		if (mHasFloat)
		{
			mMeshMaterialProperty.ReturnFloat(FloatPropertyName);
		}
		if (PropertyProviderList == null)
		{
			return;
		}
		foreach (PlayableMaterialPropertyProvider propertyProvider in PropertyProviderList)
		{
			propertyProvider.Resume(mMeshMaterialProperty);
		}
	}
}

}
