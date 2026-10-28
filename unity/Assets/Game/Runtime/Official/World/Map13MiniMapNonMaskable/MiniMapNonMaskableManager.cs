using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
public class MiniMapNonMaskableManager : MonoBehaviour
{
	public const int kOrthographicZoomToScaleRate = 2;

	private const string kMiniMapRenderShaderMainTexPropName = "_MainTex";

	[SerializeField]
	private Transform mContentTsf;

	[SerializeField]
	private MeshRenderer mMashRenderer;

	[SerializeField]
	private Camera mNonMaskableCamera;

	[Header("全屏时展示和隐藏的节点，true是表示打开全屏")]
	public StateSwitcher<bool> FullScreenSwitcher;

	private Transform mCameraTsf;

	public Camera NonMaskableCamera => mNonMaskableCamera;

	private void Awake()
	{
		DisableCamera();
	}

	public void OnShow(MiniMapCameraController cameraController)
	{
		FullScreenSwitcher.SetState(state: true);
		mCameraTsf = ((Component)cameraController.MiniMapCamera).transform;
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Combine(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		((Renderer)mMashRenderer).sharedMaterial.SetTexture("_MainTex", (Texture)(object)cameraController.GetRenderTexture());
		RefreshMeshRendererScale(cameraController.MiniMapCamera);
		((Behaviour)mNonMaskableCamera).enabled = true;
	}

	public void OnHide(MiniMapCameraController cameraController)
	{
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Remove(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(OnCameraOrthographicSizeUpdate));
		mCameraTsf = null;
		DisableCamera();
		FullScreenSwitcher.SetState(state: false);
	}

	private void OnCameraOrthographicSizeUpdate(MiniMapCameraController cameraController)
	{
		RefreshMeshRendererScale(cameraController.MiniMapCamera);
	}

	private void RefreshMeshRendererScale(Camera camera)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		mNonMaskableCamera.orthographicSize = camera.orthographicSize;
		float num = camera.orthographicSize * 2f;
		mContentTsf.localScale = new Vector3(num * camera.aspect, num, mContentTsf.localScale.z);
	}

	private void DisableCamera()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		GL.Clear(true, true, Color.clear);
		((Behaviour)mNonMaskableCamera).enabled = false;
	}

	private void LateUpdate()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mCameraTsf == (Object)null))
		{
			mContentTsf.position = mCameraTsf.position;
		}
	}
}

}
