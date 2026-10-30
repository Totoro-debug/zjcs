using System;
using UnityEngine;
using Zjcs.Official.World.NativeWater;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class Pond : MonoBehaviour
{
	public MeshRenderer WaterRenderer;

	public Camera MaskCamera;

	public Vector2 Size = new Vector2(10f, 10f);

	private static readonly int sMaskTexId = Shader.PropertyToID("_MaskTex");

	private static readonly int sIsPondId = Shader.PropertyToID("_IsPond");

	private RenderTexture mRT;

	private MaterialPropertyBlock mMPB;

	private Action<ScriptableRenderContext, Camera> mActionCache;

	private MaterialPropertyBlock MPB
	{
		get
		{
			if (mMPB == null)
			{
				mMPB = new MaterialPropertyBlock();
			}
			return mMPB;
		}
	}

	private void Awake()
	{
		MaskCamera.enabled = false;
		MaskCamera.aspect = Size.x / Size.y;
		mActionCache = RenderPipelineManager_beginCameraRendering;
	}

	private void OnDestroy()
	{
		if (mRT != null)
		{
			RenderTexture.ReleaseTemporary(mRT);
			mRT = null;
		}
	}

	private void OnEnable()
	{
		RenderPipelineManager.beginCameraRendering += mActionCache;
	}

	private void OnDisable()
	{
		RenderPipelineManager.beginCameraRendering -= mActionCache;
	}

	private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		Camera maskCamera = MaskCamera;
		if (!WaterRenderer.isVisible || !camera.CompareTag("MainCamera"))
		{
			return;
		}
		if (mRT == null)
		{
			int num = 512;
			int num2 = 512;
			if (Size.x > Size.y)
			{
				num2 = Mathf.RoundToInt((float)num * Size.y / Size.x);
			}
			else
			{
				num = Mathf.RoundToInt((float)num2 * Size.x / Size.y);
			}
			mRT = RenderTexture.GetTemporary(num, num2, 0, RenderTextureFormat.R8);
		}
		maskCamera.TryGetCullingParameters(out var cullingParameters);
		CullingResults cullingResults = context.Cull(ref cullingParameters);
		Matrix4x4 projectionMatrix = MaskCamera.projectionMatrix;
		Matrix4x4 worldToCameraMatrix = MaskCamera.worldToCameraMatrix;
		MPB.Clear();
		MPB.SetFloat(sIsPondId, 1f);
		MPB.SetTexture(sMaskTexId, mRT);
		WaterRenderer.SetPropertyBlock(MPB);
		CommandBuffer commandBuffer = CommandBufferPool.Get();
		commandBuffer.SetRenderTarget(mRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
		commandBuffer.ClearRenderTarget(clearDepth: false, clearColor: true, new Color(0f, 0f, 0f, 0f));
		commandBuffer.SetViewProjectionMatrices(worldToCameraMatrix, projectionMatrix);
		context.ExecuteCommandBuffer(commandBuffer);
		CommandBufferPool.Release(commandBuffer);
		FilteringSettings filteringSettings = new FilteringSettings
		{
			renderQueueRange = RenderQueueRange.all,
			layerMask = -1,
			renderingLayerMask = uint.MaxValue,
			sortingLayerRange = SortingLayerRange.all
		};
		SortingSettings sortingSettings = new SortingSettings(MaskCamera);
		sortingSettings.criteria = SortingCriteria.CommonTransparent;
		SortingSettings sortingSettings2 = sortingSettings;
		ShaderTagId shaderPassName = new ShaderTagId("WaterMask");
		DrawingSettings drawingSettings = new DrawingSettings(shaderPassName, sortingSettings2);
		drawingSettings.enableDynamicBatching = true;
		drawingSettings.enableInstancing = false;
		drawingSettings.perObjectData = PerObjectData.None;
		DrawingSettings drawingSettings2 = drawingSettings;
		context.DrawRenderers(cullingResults, ref drawingSettings2, ref filteringSettings);
		if (Application.isPlaying)
		{
			RenderPipelineManager.beginCameraRendering -= mActionCache;
		}
	}
}
