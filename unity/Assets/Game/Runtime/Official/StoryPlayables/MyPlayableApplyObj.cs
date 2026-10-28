// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using MainModule = UnityEngine.ParticleSystem.MainModule;
using EmissionModule = UnityEngine.ParticleSystem.EmissionModule;
using SubEmittersModule = UnityEngine.ParticleSystem.SubEmittersModule;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public class MyPlayableApplyObj : MonoBehaviour
	{
		public enum EParticleFadeOutType
		{
			None,
			ChangeAlpha,
			CloseEmission
		}

		[Serializable]
		public class ParticleMatInfo
		{
			public ParticleSystem ParticleSystem;

			public ParticleSystemRenderer Renderer;

			public int MatIndex;

			public Material Mat;

			public bool HasMainColorProp;

			public float RawAlpha;
		}

		public Animator[] myAnimators;

		private MyPlayable mPlayable;

		private float mFadeInDuration;

		private float mFadeInTime;

		[Header("粒子")]
		public ParticleSystem[] myParticles;

		public readonly string kParticleMatColorPropName = "_MainColor";

		public ParticleMatInfo[] ParticleMats;

		public EParticleFadeOutType ParticleFadeOutType = EParticleFadeOutType.ChangeAlpha;

		private Dictionary<ParticleMatInfo, MaterialPropertyBlock> mParticleMatInfoToPorpBlock;

		private bool mInvalidParticleMatReported;

		public void Init(MyPlayable playable)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			mPlayable = playable;
			if (myParticles != null)
			{
				ParticleSystem[] array = myParticles;
				foreach (ParticleSystem val in array)
				{
					if (!((Object)(object)val == (Object)null))
					{
						MainModule main = val.main;
						main.simulationSpeed = mPlayable.Speed;
					}
				}
			}
			if (myAnimators != null)
			{
				Animator[] array2 = myAnimators;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].speed = mPlayable.Speed;
				}
			}
		}

		private void Update()
		{
			if (mFadeInDuration > 0f)
			{
				if (mFadeInTime >= mFadeInDuration)
				{
					ResumeFadeIn();
					mFadeInDuration = 0f;
					mFadeInTime = 0f;
				}
				else
				{
					FadeIn(mFadeInTime / mFadeInDuration);
					mFadeInTime += Time.deltaTime;
				}
			}
		}

		public void FadeOut(float time)
		{
			FadeOutParticle(time);
		}

		public void ResumeFadeOut()
		{
			ResumeFadeOutParticle();
		}

		public void FadeIn(float time)
		{
			FadeInParticle(time);
		}

		public void FadeInAsync(float duration)
		{
			mFadeInDuration = duration;
			mFadeInTime = 0f;
		}

		public void ResumeFadeIn()
		{
			ResumeFadeInParticle();
		}

		private bool IsParticleMatInfoValid(ParticleMatInfo matInfo)
		{
			if (matInfo != null && (Object)(object)matInfo.Mat != (Object)null && (Object)(object)matInfo.Renderer != (Object)null)
			{
				return true;
			}
			if (!mInvalidParticleMatReported)
			{
				mInvalidParticleMatReported = true;
			}
			return false;
		}

		private MaterialPropertyBlock GetParticlePorpBlock(ParticleMatInfo particleMatInfo)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Expected Obj, but got Unknown
			if (mParticleMatInfoToPorpBlock == null)
			{
				mParticleMatInfoToPorpBlock = new Dictionary<ParticleMatInfo, MaterialPropertyBlock>();
			}
			if (!mParticleMatInfoToPorpBlock.TryGetValue(particleMatInfo, out var value))
			{
				value = new MaterialPropertyBlock();
				mParticleMatInfoToPorpBlock.Add(particleMatInfo, value);
			}
			return value;
		}

		public bool TryGetMainColor(Material material, out Color color)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)material == (Object)null || !material.HasColor(kParticleMatColorPropName))
			{
				color = default;
				return false;
			}
			color = material.GetColor(kParticleMatColorPropName);
			return true;
		}

		private void FadeOutParticle(float time)
		{
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			if (ParticleFadeOutType == EParticleFadeOutType.ChangeAlpha)
			{
				if (ParticleMats == null || ParticleMats.Length == 0)
				{
					return;
				}
				ParticleMatInfo[] particleMats = ParticleMats;
				foreach (ParticleMatInfo particleMatInfo in particleMats)
				{
					if (IsParticleMatInfoValid(particleMatInfo) && particleMatInfo.HasMainColorProp)
					{
						MaterialPropertyBlock particlePorpBlock = GetParticlePorpBlock(particleMatInfo);
						Color color = particleMatInfo.Mat.GetColor(kParticleMatColorPropName);
						color.a = Mathf.Lerp(particleMatInfo.RawAlpha, 0f, time);
						particlePorpBlock.SetColor(kParticleMatColorPropName, color);
						((Renderer)particleMatInfo.Renderer).SetPropertyBlock(particlePorpBlock, particleMatInfo.MatIndex);
					}
				}
			}
			else
			{
				if (ParticleFadeOutType != EParticleFadeOutType.CloseEmission)
				{
					return;
				}
				ParticleSystem[] array = myParticles;
				foreach (ParticleSystem val in array)
				{
					if (!((Object)(object)val == (Object)null))
					{
						EmissionModule emission = val.emission;
						emission.enabled = false;
						SubEmittersModule subEmitters = val.subEmitters;
						subEmitters.enabled = false;
					}
				}
			}
		}

		private void ResumeFadeOutParticle()
		{
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			if (ParticleFadeOutType == EParticleFadeOutType.ChangeAlpha)
			{
				if (ParticleMats == null || ParticleMats.Length == 0)
				{
					return;
				}
				ParticleMatInfo[] particleMats = ParticleMats;
				foreach (ParticleMatInfo particleMatInfo in particleMats)
				{
					if (IsParticleMatInfoValid(particleMatInfo) && particleMatInfo.HasMainColorProp)
					{
						MaterialPropertyBlock particlePorpBlock = GetParticlePorpBlock(particleMatInfo);
						particlePorpBlock.Clear();
						((Renderer)particleMatInfo.Renderer).SetPropertyBlock(particlePorpBlock, particleMatInfo.MatIndex);
					}
				}
			}
			else
			{
				if (ParticleFadeOutType != EParticleFadeOutType.CloseEmission)
				{
					return;
				}
				ParticleSystem[] array = myParticles;
				foreach (ParticleSystem val in array)
				{
					if (!((Object)(object)val == (Object)null))
					{
						EmissionModule emission = val.emission;
						emission.enabled = true;
						SubEmittersModule subEmitters = val.subEmitters;
						subEmitters.enabled = true;
					}
				}
			}
		}

		private void FadeInParticle(float time)
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			if (ParticleMats == null || ParticleMats.Length == 0)
			{
				return;
			}
			ParticleMatInfo[] particleMats = ParticleMats;
			foreach (ParticleMatInfo particleMatInfo in particleMats)
			{
				if (IsParticleMatInfoValid(particleMatInfo) && particleMatInfo.HasMainColorProp)
				{
					MaterialPropertyBlock particlePorpBlock = GetParticlePorpBlock(particleMatInfo);
					Color color = particleMatInfo.Mat.GetColor(kParticleMatColorPropName);
					color.a = Mathf.Lerp(0f, particleMatInfo.RawAlpha, time);
					particlePorpBlock.SetColor(kParticleMatColorPropName, color);
					((Renderer)particleMatInfo.Renderer).SetPropertyBlock(particlePorpBlock, particleMatInfo.MatIndex);
				}
			}
		}

		private void ResumeFadeInParticle()
		{
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			if (ParticleMats == null || ParticleMats.Length == 0)
			{
				return;
			}
			ParticleMatInfo[] particleMats = ParticleMats;
			foreach (ParticleMatInfo particleMatInfo in particleMats)
			{
				if (IsParticleMatInfoValid(particleMatInfo) && particleMatInfo.HasMainColorProp)
				{
					MaterialPropertyBlock particlePorpBlock = GetParticlePorpBlock(particleMatInfo);
					Color color = particleMatInfo.Mat.GetColor(kParticleMatColorPropName);
					color.a = particleMatInfo.RawAlpha;
					particlePorpBlock.SetColor(kParticleMatColorPropName, color);
					((Renderer)particleMatInfo.Renderer).SetPropertyBlock(particlePorpBlock, particleMatInfo.MatIndex);
				}
			}
		}

		public static void Bind(GameObject go, MyPlayable playable)
		{
			MyPlayableApplyObj component = go.GetComponent<MyPlayableApplyObj>();
			if (!((Object)(object)component == (Object)null))
			{
				component.Init(playable);
			}
		}
	}
}
