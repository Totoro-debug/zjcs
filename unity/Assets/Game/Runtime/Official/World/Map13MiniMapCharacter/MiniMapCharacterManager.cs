using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
public class MiniMapCharacterManager : MiniMapManagerBase, IFullScrMiniMapStateListener
{
	private const string kMinimapRenderTextureName = "MinimapRenderTextureName";

	public GameObject CharacterMe;

	public Camera UiCamera;

	[SerializeField]
	private Vector2Int mMiniMapUiTextureSize;

	private UnityEngine.Object mMoveAgent;

	private RenderTexture mRenderTexture;

	private static readonly ProfilingSampler sProfilingDrawMiniMap = new ProfilingSampler("Draw Mini Map");

	private bool mIsCameraActive;

	private static ShaderTagId sCombinedRenderingPassName;

	private static ShaderTagId sLegacyPassName;

	private MiniMapCharacterContext MapRootManagerProxy => (MiniMapCharacterContext)MiniMap;

	public override bool MapActived
	{
		get
		{
			if (MapRootManagerProxy.HasMapRoot && myMapController != null)
			{
				return base.MapActived;
			}
			return false;
		}
	}

	private void Awake()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		((Behaviour)UiCamera).enabled = false;
		sCombinedRenderingPassName = new ShaderTagId("Universal2D");
		sLegacyPassName = new ShaderTagId("SRPDefaultUnlit");
	}

	protected override void Start()
	{
		base.Start();
		if (MiniMap.MiniMapSetting != null)
		{
			UiCamera.orthographicSize = MiniMap.MiniMapSetting.MiniMapUICameraOrthographicSize;
		}
	}

	protected override void OnEnableAndInited()
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		base.OnEnableAndInited();
		if (!MapActived)
		{
			((Behaviour)this).enabled = false;
			CharacterMe.SetActive(false);
		}
		else
		{
			RenderPipelineManager.beginCameraRendering += RenderPipelineManager_beginCameraRendering;
			SetPosition(myMapController.MyGridTransform.GetPosition());
			mMoveAgent = MapRootManagerProxy.GetMoveAgent(myMapController);
		}
	}

	protected override void OnDisableAndInited()
	{
		mMoveAgent = null;
		RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_beginCameraRendering;
		base.OnDisableAndInited();
	}

	private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		if (!mIsCameraActive || !((Component)camera).CompareTag("MainCamera"))
		{
			return;
		}
		CommandBuffer val = CommandBufferPool.Get();
		ProfilingScope val2 = new ProfilingScope(val, sProfilingDrawMiniMap);
		try
		{
			ScriptableCullingParameters val3 = default;
			UiCamera.TryGetCullingParameters(out val3);
			CullingResults val4 = context.Cull(ref val3);
			Matrix4x4 projectionMatrix = UiCamera.projectionMatrix;
			Matrix4x4 worldToCameraMatrix = UiCamera.worldToCameraMatrix;
			val.SetRenderTarget(new RenderTargetIdentifier(GetOrAddRenderTexture()), (RenderBufferLoadAction)2, (RenderBufferStoreAction)0, (RenderBufferLoadAction)2, (RenderBufferStoreAction)3);
			val.ClearRenderTarget(false, true, Color.clear);
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
			SortingSettings val7 = new SortingSettings(camera);
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

	public void OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		mIsCameraActive = false;
	}

	public void OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		mIsCameraActive = true;
	}

	public bool TrySetCameraPos(Transform cameraTsf)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (!((Behaviour)this).enabled || (Object)(object)CharacterMe == (Object)null || !CharacterMe.activeSelf)
		{
			return false;
		}
		cameraTsf.position = new Vector3(CharacterMe.transform.position.x, CharacterMe.transform.position.y, cameraTsf.position.z);
		return true;
	}

	public RenderTexture GetOrAddRenderTexture()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		if ((Object)(object)mRenderTexture == (Object)null)
		{
			mIsCameraActive = true;
			mRenderTexture = new RenderTexture(mMiniMapUiTextureSize.x, mMiniMapUiTextureSize.y, 0);
			((Object)mRenderTexture).name = "MinimapRenderTextureName";
			UiCamera.aspect = (float)mMiniMapUiTextureSize.x / (float)mMiniMapUiTextureSize.y;
		}
		return mRenderTexture;
	}

	protected void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mMoveAgent == (Object)null))
		{
			SetPosition(MapRootManagerProxy.GetCharacterWorldPosition(myMapController));
		}
	}

	private void SetPosition(Vector3 pos)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		pos.z = 0f;
		CharacterMe.transform.localPosition = pos;
	}

	static MiniMapCharacterManager()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected Obj, but got Unknown
	}
}

}
