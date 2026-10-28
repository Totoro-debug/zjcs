namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CharacterPortraitPhotographer : MonoBehaviour, IProfessionPortraitStudio
{
	[SerializeField]
	private Camera mCamera;

	[SerializeField]
	private Component mAppearance;

	[SerializeField]
	private Component mAppearanceAnimation;

	[SerializeField]
	private MeshFilter mMeshFilter;

	protected CommandBuffer mCommandBuffer;

	private CharacterPortraitTexturePool mTexturePool;

	private readonly List<Material> mMaterials = new List<Material>();

	private readonly List<Renderer> mTempRenderers = new List<Renderer>();

	private readonly RendererZSortComparer mRenderComparer = new RendererZSortComparer();

	private readonly Dictionary<ProfessionPortraitKind, ICharacterPortraitBuilder> mBuilders = new Dictionary<ProfessionPortraitKind, ICharacterPortraitBuilder>();

    private IProfessionPortraitPhotographerBindings mEnvironment;
    public Component Instance => this;
    public void Configure(IProfessionPortraitPhotographerBindings environment)
    { mEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); }

	private void OnDestroy()
	{
		CommandBuffer val = mCommandBuffer;
		if (val != null)
		{
			val.Dispose();
		}
	}

	public void Init(CharacterPortraitTexturePool texturePool)
	{

		((Behaviour)mCamera).enabled = false;
		if (mCommandBuffer == null)
		{
			mCommandBuffer = new CommandBuffer();
		}
		mTexturePool = texturePool;
	}

	public CharacterPortraitTexture2D CapturePortraitTexture(ICharacterPortraitRequestor requestor, out byte[] bsSaveImgData)
	{

		object appearanceData = null;
		bsSaveImgData = null;
		PlayerPortraitSettings.PhotographyParameters param;
		try
		{
			PlayerPortraitSettings value = mEnvironment.Settings;
			ICharacterPortraitBuilder builder = GetBuilder(requestor.GetPortraitType());
			param = requestor.GetParam(value);
			appearanceData = requestor.GetAppearanceData();
			builder.HandleAppearanceCharacter(mAppearance, requestor);
		}
        catch (Exception ex)
        {
            if (requestor == null)
            {
                Debug.LogError("请求对象丢失！\n 堆栈：\n " + ex.StackTrace);
                return null;
            }
            string text = appearanceData != null ? mEnvironment.DescribeAppearance(appearanceData) : "皮肤数据无法获取";
            Debug.LogError("皮肤设置错误 皮肤数据 " + text + " \n堆栈：\n " + ex.Message + " \n " + ex.StackTrace);
            return null;
        }
		RenderTexture temporary = RenderTexture.GetTemporary(param.TextureWidth, param.TextureHeight, param.DepthBuffer, (RenderTextureFormat)0, (RenderTextureReadWrite)0, 1);
		mCamera.aspect = (float)param.TextureWidth / (float)param.TextureHeight;
		Capture(temporary, ((Component)mAppearance).transform, param);
		CharacterPortraitTexture2D texture = mTexturePool.GetTexture(param.TextureWidth, param.TextureHeight);
		try
		{
			texture.ReadPixelsFromRT(temporary, out bsSaveImgData);
		}
        catch (Exception ex3)
        {
            Debug.LogError("CapturePortraitTexture发生异常 msg:" + ex3.Message + "\n stackTrace:\n" + ex3.StackTrace);
        }
		finally
		{
			RenderTexture.ReleaseTemporary(temporary);
			mEnvironment.ReleaseAppearance(mAppearance, tryUnloadAllAssets: true);
		}
		return texture;
	}

	private void Capture(RenderTexture target, Transform targetRenderer, PlayerPortraitSettings.PhotographyParameters parameters)
	{

		mCommandBuffer.Clear();
		mCommandBuffer.SetRenderTarget(new RenderTargetIdentifier(target));
		mCommandBuffer.ClearRenderTarget(true, true, Color.clear);
		mCamera.orthographicSize = parameters.Size;
		((Component)mCamera).transform.SetPositionAndRotation(targetRenderer.position + parameters.GetOffset(), Quaternion.identity);
		mCommandBuffer.SetProjectionMatrix(mCamera.projectionMatrix);
		mCommandBuffer.SetViewMatrix(mCamera.worldToCameraMatrix);
		ProfessionPortraitRendererCollector.CollectPortraitAndParticleRenderers(mEnvironment.GetRendererGraph(mAppearanceAnimation), mTempRenderers, mRenderComparer, parameters.CaptureParticle);
		int count = mTempRenderers.Count;
		for (int i = 0; i < count; i++)
		{
			Renderer val = mTempRenderers[i];
			val.GetSharedMaterials(mMaterials);
			int j = 0;
			for (int count2 = mMaterials.Count; j < count2; j++)
			{
				Material val2 = mMaterials[j];
				if (!((Object)(object)val2 == (Object)null))
				{
					mCommandBuffer.DrawRenderer(val, val2, j);
				}
			}
		}
		Graphics.ExecuteCommandBuffer(mCommandBuffer);
	}

	private ICharacterPortraitBuilder GetBuilder(ProfessionPortraitKind portraitType)
	{
		if (!mBuilders.TryGetValue(portraitType, out var value))
		{
			value = mEnvironment.CreateBuilder(portraitType);
			mBuilders.Add(portraitType, value);
		}
		return value;
	}
}

}
