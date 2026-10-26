// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class CullingGroupWrapper : MonoBehaviour
{
	public float CullingRadius = 1f;

	public Vector3 Offset;

	public Transform Target;

	public List<Animator> Animators;

	public List<ParticleSystem> ParticleSystems;

	public List<Behaviour> SkeletonAnimations;

	private CullingGroup mCullingGroup;

	private readonly BoundingSphere[] mSpheres = new BoundingSphere[1];

	private void OnEnable()
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected Obj, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected Obj, but got Unknown
		if (!((Object)(object)Target == (Object)null))
		{
			if (mCullingGroup == null)
			{
				mCullingGroup = new CullingGroup();
				mCullingGroup.targetCamera = Camera.main;
				mCullingGroup.SetBoundingSpheres(mSpheres);
				mCullingGroup.SetBoundingSphereCount(1);
				mCullingGroup.onStateChanged = OnStateChanged;
			}
			mSpheres[0] = new BoundingSphere(Target.position + Offset, CullingRadius);
			mCullingGroup.enabled = true;
			Cull(mCullingGroup.IsVisible(0));
		}
	}

	private void OnDisable()
	{
		if (!((Object)(object)Target == (Object)null))
		{
			mCullingGroup.enabled = false;
			Cull(visible: true);
		}
	}

	private void Update()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)Target == (Object)null) && ((Component)this).transform.hasChanged)
		{
			((Component)this).transform.hasChanged = false;
			mSpheres[0] = new BoundingSphere(Target.position + Offset, CullingRadius);
		}
	}

	private void OnDestroy()
	{
		CullingGroup val = mCullingGroup;
		if (val != null)
		{
			val.Dispose();
		}
		mCullingGroup = null;
	}

	private void OnStateChanged(CullingGroupEvent sphere)
	{
		Cull(sphere.isVisible);
	}

	private void Cull(bool visible)
	{
		ToggleAnimator(visible);
		ToggleParticles(visible);
		ToggleSkeletonAnimations(visible);
	}

	private void ToggleAnimator(bool value)
	{
		int i = 0;
		for (int count = Animators.Count; i < count; i++)
		{
			if (!((Object)(object)Animators[i] == (Object)null))
			{
				((Behaviour)Animators[i]).enabled = value;
			}
		}
	}

	private void ToggleParticles(bool value)
	{
		int i = 0;
		for (int count = ParticleSystems.Count; i < count; i++)
		{
			ParticleSystem val = ParticleSystems[i];
			if (!((Object)(object)val == (Object)null))
			{
				if (value)
				{
					val.Play(true);
				}
				else
				{
					val.Pause(true);
				}
			}
		}
	}

	private void ToggleSkeletonAnimations(bool value)
	{
		int i = 0;
		for (int count = SkeletonAnimations.Count; i < count; i++)
		{
			Behaviour val = SkeletonAnimations[i];
			if (!((Object)(object)val == (Object)null))
			{
				((Behaviour)val).enabled = value;
			}
		}
	}
}

}
