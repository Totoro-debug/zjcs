using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
public class MiniMapCameraController : MonoBehaviour
{
	public Action<MiniMapCameraController> OnCameraOrthographicSizeUpdate;

	[SerializeField]
	private Camera mMiniMapCamera;

	private RenderTexture mRT;

	public UITouchInput myInput;

	public float mySizeMin;

	public float mySizeMax;

	public float ScaleFactor = 10f;

	private static readonly ProfilingSampler sProfilingDrawMiniMap = new ProfilingSampler("Draw Mini Map Full");

	private static ShaderTagId sCombinedRenderingPassName;

	private static ShaderTagId sLegacyPassName;

	public Camera MiniMapCamera => mMiniMapCamera;

	public MiniMapSettings Settings { get; set; }

	private void Awake()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		sCombinedRenderingPassName = new ShaderTagId("Universal2D");
		sLegacyPassName = new ShaderTagId("SRPDefaultUnlit");
		((Behaviour)mMiniMapCamera).enabled = false;
		mMiniMapCamera.aspect = (float)Screen.width / (float)Screen.height;
	}

	private void OnDestroy()
	{
		if ((Object)(object)mRT != (Object)null)
		{
			Object.Destroy((Object)(object)mRT);
			mRT = null;
		}
	}

	public RenderTexture GetRenderTexture()
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected Obj, but got Unknown
		if ((Object)(object)mRT == (Object)null)
		{
			float num = 1f;
			RenderPipelineAsset currentRenderPipeline = GraphicsSettings.currentRenderPipeline;
			UniversalRenderPipelineAsset val = (UniversalRenderPipelineAsset)(object)((currentRenderPipeline is UniversalRenderPipelineAsset) ? currentRenderPipeline : null);
			if ((Object)(object)val != (Object)null)
			{
				num = val.renderScale;
			}
			float num2 = Settings.MiniMapFullScreenTextureQuality * num;
			RenderTexture val2 = new RenderTexture((int)((float)Screen.width * num2), (int)((float)Screen.height * num2), 0);
			((Object)val2).name = ((Object)mMiniMapCamera).name;
			mRT = val2;
		}
		return mRT;
	}

	private void OnEnable()
	{
		myInput.OnZoom += OnZoom;
		RenderPipelineManager.beginCameraRendering += RenderPipelineManager_beginCameraRendering;
	}

	private void OnDisable()
	{
		myInput.OnZoom -= OnZoom;
		RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_beginCameraRendering;
	}

	private void OnZoom(float factor, Vector3 ScreenPos)
	{
		SetCameraOrthographicSize(Mathf.Max(1f, mMiniMapCamera.orthographicSize + factor * ScaleFactor));
	}

	public void SetCameraOrthographicSize(float orthographicSize)
	{
		orthographicSize = ClampOrthographicSize(orthographicSize);
		mMiniMapCamera.orthographicSize = orthographicSize;
		myInput.SetScreenOffsetScale(mMiniMapCamera);
		OnCameraOrthographicSizeUpdate?.Invoke(this);
	}

	public float GetZoomRate()
	{
		return (mMiniMapCamera.orthographicSize - mySizeMin) / (mySizeMax - mySizeMin);
	}

	private float ClampOrthographicSize(float orthographicSize)
	{
		if (orthographicSize < mySizeMin)
		{
			return mySizeMin;
		}
		return Mathf.Clamp(orthographicSize, mySizeMin, mySizeMax);
	}

	private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		CameraType cameraType = camera.cameraType;
		if (!((Component)camera).CompareTag("MainCamera") && (int)cameraType != 2)
		{
			return;
		}
		CommandBuffer val = CommandBufferPool.Get();
		ProfilingScope val2 = new ProfilingScope(val, sProfilingDrawMiniMap);
		try
		{
			ScriptableCullingParameters val3 = default;
			mMiniMapCamera.TryGetCullingParameters(out val3);
			CullingResults val4 = context.Cull(ref val3);
			Matrix4x4 worldToCameraMatrix = mMiniMapCamera.worldToCameraMatrix;
			Matrix4x4 projectionMatrix = mMiniMapCamera.projectionMatrix;
			RenderTexture renderTexture = GetRenderTexture();
			val.SetRenderTarget(new RenderTargetIdentifier(renderTexture), (RenderBufferLoadAction)2, (RenderBufferStoreAction)0, (RenderBufferLoadAction)2, (RenderBufferStoreAction)3);
			val.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
			val.SetViewProjectionMatrices(worldToCameraMatrix, projectionMatrix);
			context.ExecuteCommandBuffer(val);
			val.Clear();
			CommandBufferPool.Release(val);
			FilteringSettings val5 = default;
			val5.renderQueueRange = RenderQueueRange.all;
			val5.layerMask = -1;
			val5.renderingLayerMask = uint.MaxValue;
			val5.sortingLayerRange = SortingLayerRange.all;
			FilteringSettings val6 = val5;
			SortingSettings val7 = new SortingSettings(mMiniMapCamera);
			val7.criteria = (SortingCriteria)23;
			SortingSettings val8 = val7;
			DrawingSettings val9 = new DrawingSettings(sLegacyPassName, val8);
			val9.enableDynamicBatching = true;
			val9.enableInstancing = false;
			val9.perObjectData = (PerObjectData)0;
			DrawingSettings val10 = val9;
			val10.SetShaderPassName(1, sCombinedRenderingPassName);
			context.DrawRenderers(val4, ref val10, ref val6);
		}
		finally
		{
			((IDisposable)val2/*cast due to constrained. prefix*/).Dispose();
		}
	}

	static MiniMapCameraController()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected Obj, but got Unknown
	}
}

}
