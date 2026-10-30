using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class TransparentMask : MonoBehaviour
{
	private Action<ScriptableRenderContext, Camera> mActionCache;

	private RenderTexture mRT;

	private RenderTexture mSceneViewRT;

	private ShaderTagId mShaderTagId;

	private Vector3 mLastCameraPosition = sInvalidCameraPosition;

	private float mLastCameraOrthographicSize = float.MinValue;

	public List<Rect> EffectiveRects = new List<Rect>();

	private GraphicsFormat mGraphicsFormat;

	public int Layer = 1;

	private static readonly int sMaskTexId = Shader.PropertyToID("_TransparentMaskTex");

	private static readonly Vector3 sInvalidCameraPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);

	private static readonly ProfilingSampler sProfilingDrawTransparentMask = new ProfilingSampler("Draw Transparent Mask");

	private void Awake()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		mShaderTagId = new ShaderTagId("TransparentMask");
		mActionCache = RenderPipelineManager_beginCameraRendering;
		if (Layer == 1)
		{
			mGraphicsFormat = (GraphicsFormat)5;
		}
		else
		{
			mGraphicsFormat = (GraphicsFormat)6;
		}
	}

	private void OnEnable()
	{
		RenderPipelineManager.beginCameraRendering += mActionCache;
	}

	private void OnDisable()
	{
		RenderPipelineManager.beginCameraRendering -= mActionCache;
		ReleaseAndResetMaskTexture();
	}

	private void ReleaseAndResetMaskTexture()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		ResetMaskTex();
		mLastCameraPosition = sInvalidCameraPosition;
		mLastCameraOrthographicSize = float.MinValue;
		if ((Object)(object)mRT != (Object)null)
		{
			RenderTexture.ReleaseTemporary(mRT);
			mRT = null;
		}
		if ((Object)(object)mSceneViewRT != (Object)null)
		{
			RenderTexture.ReleaseTemporary(mSceneViewRT);
			mSceneViewRT = null;
		}
	}

	private void ResetMaskTex()
	{
		Shader.SetGlobalTexture(sMaskTexId, (Texture)(object)Texture2D.whiteTexture);
	}

	private bool IsVisbile(Camera camera)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)camera == (Object)null || EffectiveRects == null || EffectiveRects.Count <= 0)
		{
			return false;
		}
		float orthographicSize = camera.orthographicSize;
		float num = orthographicSize * camera.aspect;
		Vector3 position = ((Component)camera).transform.position;
		Rect val = Rect.MinMaxRect(position.x - num, position.y - orthographicSize, position.x + num, position.y + orthographicSize);
		int i = 0;
		for (int count = EffectiveRects.Count; i < count; i++)
		{
			Rect val2 = EffectiveRects[i];
			if (val2.Overlaps(val))
			{
				return true;
			}
		}
		return false;
	}

	private bool CanReuseMaskTexture(Camera camera)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)camera).transform.position;
		float orthographicSize = camera.orthographicSize;
		if (mLastCameraPosition == position && mLastCameraOrthographicSize == orthographicSize)
		{
			return true;
		}
		mLastCameraPosition = position;
		mLastCameraOrthographicSize = orthographicSize;
		return false;
	}

	private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		RenderTexture val;
		if ((int)camera.cameraType == 2)
		{
			if ((Object)(object)mSceneViewRT != (Object)null)
			{
				RenderTexture.ReleaseTemporary(mSceneViewRT);
				mSceneViewRT = null;
			}
			mSceneViewRT = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 0, mGraphicsFormat);
			val = mSceneViewRT;
		}
		else
		{
			if (!((Component)camera).CompareTag("MainCamera"))
			{
				return;
			}
			if ((Object)(object)mRT == (Object)null)
			{
				float num = 1f;
				RenderPipelineAsset currentRenderPipeline = GraphicsSettings.currentRenderPipeline;
				UniversalRenderPipelineAsset val2 = (UniversalRenderPipelineAsset)(object)((currentRenderPipeline is UniversalRenderPipelineAsset) ? currentRenderPipeline : null);
				if ((Object)(object)val2 != (Object)null)
				{
					num = val2.renderScale;
				}
				int num2 = (int)((float)Screen.width * num);
				int num3 = (int)((float)Screen.height * num);
				mRT = RenderTexture.GetTemporary(num2, num3, 0, mGraphicsFormat);
				((Object)mRT).name = "TransparentMaskTex";
			}
			val = mRT;
			if (Application.isPlaying)
			{
				if (CanReuseMaskTexture(camera))
				{
					return;
				}
				if (!IsVisbile(camera))
				{
					ResetMaskTex();
					return;
				}
			}
		}
		CommandBuffer val3 = CommandBufferPool.Get();
		ProfilingScope val4 = new ProfilingScope(val3, sProfilingDrawTransparentMask);
		try
		{
			ScriptableCullingParameters val5 = default;
			camera.TryGetCullingParameters(out val5);
			Matrix4x4 worldToCameraMatrix = camera.worldToCameraMatrix;
			Matrix4x4 projectionMatrix = camera.projectionMatrix;
			CullingResults val6 = context.Cull(ref val5);
			val3.SetRenderTarget(new RenderTargetIdentifier(val), (RenderBufferLoadAction)2, (RenderBufferStoreAction)0, (RenderBufferLoadAction)2, (RenderBufferStoreAction)3);
			val3.ClearRenderTarget(false, true, new Color(1f, 1f, 1f, 1f));
			val3.SetViewProjectionMatrices(worldToCameraMatrix, projectionMatrix);
			val3.SetGlobalTexture(sMaskTexId, new RenderTargetIdentifier(val));
			context.ExecuteCommandBuffer(val3);
			val3.Clear();
			FilteringSettings val7 = default;
			val7.renderQueueRange = RenderQueueRange.all;
			val7.layerMask = -1;
			val7.renderingLayerMask = uint.MaxValue;
			val7.sortingLayerRange = SortingLayerRange.all;
			FilteringSettings val8 = val7;
			SortingSettings val9 = new SortingSettings(camera);
			val9.criteria = (SortingCriteria)23;
			SortingSettings val10 = val9;
			DrawingSettings val11 = new DrawingSettings(mShaderTagId, val10);
			val11.enableDynamicBatching = true;
			val11.enableInstancing = false;
			val11.perObjectData = (PerObjectData)0;
			DrawingSettings val12 = val11;
			context.DrawRenderers(val6, ref val12, ref val8);
		}
		finally
		{
			((IDisposable)val4/*cast due to constrained. prefix*/).Dispose();
		}
		context.ExecuteCommandBuffer(val3);
		CommandBufferPool.Release(val3);
	}

	public TransparentMask()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
	}

	static TransparentMask()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
	}
}
