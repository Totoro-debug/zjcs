
// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
[DefaultExecutionOrder(101)]
public class DamageFloatSize : MonoBehaviour
{
	[SerializeField]
	private float mSize = 5f;

	private Transform mTransformCache;

	private Camera mMainCamera;

	private float mLastOrthographicSize = -1f;

	private void Awake()
	{
		mTransformCache = ((Component)this).transform;
	}

	private void OnEnable()
	{
		RefreshCameraReference();
	}

	private void LateUpdate()
	{
		RefreshCameraReference();
		FixedScale();
	}

	private void RefreshCameraReference()
	{
		Camera main = Camera.main;
		if ((Object)(object)main != (Object)(object)mMainCamera)
		{
			mMainCamera = main;
			mLastOrthographicSize = -1f;
		}
	}

	private void FixedScale()
	{

		if (!((Object)(object)mTransformCache == (Object)null) && !((Object)(object)mMainCamera == (Object)null))
		{
			float orthographicSize = mMainCamera.orthographicSize;
			if (!Mathf.Approximately(orthographicSize, mLastOrthographicSize))
			{
				mLastOrthographicSize = orthographicSize;
				float num = orthographicSize / mSize;
				mTransformCache.localScale = new Vector3(num, num, num);
			}
		}
	}
}
}
