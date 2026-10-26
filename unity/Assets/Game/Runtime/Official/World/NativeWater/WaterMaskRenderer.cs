// Migrated from selected official CustomWater.dll; see Map13 source receipt.
namespace Zjcs.Official.World.NativeWater
{
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class WaterMaskRenderer : MonoBehaviour
{
	private static readonly int sMaskTexId = Shader.PropertyToID("_MaskTex");

	public MeshRenderer SeaRenderer;

	public SeaRendererReplacement SeaRendererReplacement;

	public List<MeshRenderer> LakeRenderers;

	private RenderTexture mRT;

	private RenderTexture mSceneViewRT;

	private ShaderTagId mShaderTagId;

	private uint mCullingMask;

	private Action<ScriptableRenderContext, Camera> mActionCache;

	private static readonly ProfilingSampler sProfilingDrawWaterMask = new ProfilingSampler("Draw Water Mask");

	private MaterialPropertyBlock mMPB;

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
		mShaderTagId = new ShaderTagId("WaterMask");
		mCullingMask = (uint)(1 << LayerMask.NameToLayer("WaterMask"));
		mActionCache = RenderPipelineManager_beginCameraRendering;
		if (Application.isPlaying && SeaRendererReplacement != null)
		{
			SeaRenderer.gameObject.SetActive(value: false);
		}
		float num = 1f;
		RenderPipelineAsset currentRenderPipeline = GraphicsSettings.currentRenderPipeline;
		UniversalRenderPipelineAsset val = (UniversalRenderPipelineAsset)(object)((currentRenderPipeline is UniversalRenderPipelineAsset) ? currentRenderPipeline : null);
		if ((UnityEngine.Object)(object)val != null)
		{
			num = val.renderScale;
		}
		int width = (int)((float)Screen.width * num);
		int height = (int)((float)Screen.height * num);
		mRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R8);
		mRT.name = "WaterMaskRT";
		ApplyPropertyBlock(mRT);
	}

	private void OnEnable()
	{
		RenderPipelineManager.beginCameraRendering += mActionCache;
	}

	private void OnDisable()
	{
		RenderPipelineManager.beginCameraRendering -= mActionCache;
	}

	private void OnDestroy()
	{
		if (mRT != null)
		{
			RenderTexture.ReleaseTemporary(mRT);
			mRT = null;
		}
		if (mSceneViewRT != null)
		{
			RenderTexture.ReleaseTemporary(mSceneViewRT);
			mSceneViewRT = null;
		}
	}

	private void ApplyPropertyBlock(RenderTexture rt)
	{
		MPB.SetTexture(sMaskTexId, rt);
		if (SeaRendererReplacement != null)
		{
			SeaRendererReplacement.ApplyPropertyBlock(MPB);
		}
		if (!Application.isPlaying || SeaRendererReplacement == null)
		{
			SeaRenderer.SetPropertyBlock(MPB);
		}
		if (LakeRenderers != null)
		{
			int i = 0;
			for (int count = LakeRenderers.Count; i < count; i++)
			{
				LakeRenderers[i].SetPropertyBlock(MPB);
			}
		}
	}

	private bool IsWaterVisible()
	{
		if (SeaRendererReplacement != null)
		{
			if (SeaRendererReplacement.IsVisible())
			{
				return true;
			}
		}
		else if (SeaRenderer.isVisible)
		{
			return true;
		}
		if (LakeRenderers != null)
		{
			for (int i = 0; i < LakeRenderers.Count; i++)
			{
				if (LakeRenderers[i].isVisible)
				{
					return true;
				}
			}
		}
		return false;
	}

	private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		RenderTexture renderTexture = mRT;
		if ((Application.isPlaying && !IsWaterVisible()) || !camera.CompareTag("MainCamera"))
		{
			return;
		}
		CommandBuffer commandBuffer = CommandBufferPool.Get();
		ProfilingScope val = new ProfilingScope(commandBuffer, sProfilingDrawWaterMask);
		try
		{
			camera.TryGetCullingParameters(out var cullingParameters);
			cullingParameters.cullingMask = mCullingMask;
			CullingResults cullingResults = context.Cull(ref cullingParameters);
			Matrix4x4 worldToCameraMatrix = camera.worldToCameraMatrix;
			Matrix4x4 projectionMatrix = camera.projectionMatrix;
			commandBuffer.SetRenderTarget(renderTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
			commandBuffer.ClearRenderTarget(clearDepth: false, clearColor: true, new Color(0f, 0f, 0f, 0f));
			commandBuffer.SetViewProjectionMatrices(worldToCameraMatrix, projectionMatrix);
			context.ExecuteCommandBuffer(commandBuffer);
			commandBuffer.Clear();
			CommandBufferPool.Release(commandBuffer);
			FilteringSettings filteringSettings = new FilteringSettings
			{
				renderQueueRange = RenderQueueRange.all,
				layerMask = -1,
				renderingLayerMask = uint.MaxValue,
				sortingLayerRange = SortingLayerRange.all
			};
			SortingSettings sortingSettings = new SortingSettings(camera);
			sortingSettings.criteria = SortingCriteria.CommonTransparent;
			SortingSettings sortingSettings2 = sortingSettings;
			DrawingSettings drawingSettings = new DrawingSettings(mShaderTagId, sortingSettings2);
			drawingSettings.enableDynamicBatching = true;
			drawingSettings.enableInstancing = false;
			drawingSettings.perObjectData = PerObjectData.None;
			DrawingSettings drawingSettings2 = drawingSettings;
			context.DrawRenderers(cullingResults, ref drawingSettings2, ref filteringSettings);
		}
		finally
		{
			((IDisposable)val/*cast due to constrained. prefix*/).Dispose();
		}
	}

	static WaterMaskRenderer()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected Obj, but got Unknown
	}
}

}
