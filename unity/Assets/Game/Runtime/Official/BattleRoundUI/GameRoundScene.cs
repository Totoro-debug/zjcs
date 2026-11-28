// Current official position, scale and cache implementation; Windows overlay/camera binding is explicit.
namespace Zjcs.Official.BattleRoundUI
{
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class GameRoundScene : MonoBehaviour
{
	[SerializeField]
	private float mSize = 5f;

	[SerializeField]
	public Transform mIconRoot;

	[SerializeField]
	public SpriteRenderer mLine;

	[SerializeField]
	private float mZValue = -16f;

	private Transform mTransformCache;

	private RectTransform mAttachUITrans;

	private RectTransform mCanvasRect;

	private Camera mUICamera;

	private Camera mSceneCamera;

	private Camera SceneCamera => mSceneCamera != null ? mSceneCamera : Camera.main;

	private Vector3 mLastPosition = Vector3.positiveInfinity;

	private float mLastOrthographicSize = -1f;

	private float mReferenceHeight = -1f;

	private float mLastCanvasHeight = -1f;

	private static readonly Vector3 sFarPos = new Vector3(0f, 1000f, 0f);

	public Transform IconRoot => mIconRoot;

	public SpriteRenderer Line => mLine;

	public Transform Transform => mTransformCache;

	private void Awake()
	{
		mTransformCache = ((Component)this).transform;
		SetBarFarAway();
	}

	private void OnEnable() => Canvas.willRenderCanvases += FixedPosAndScale;

	private void OnDisable() => Canvas.willRenderCanvases -= FixedPosAndScale;

	private void OnDestroy()
	{
		mAttachUITrans = null;
		mCanvasRect = null;
		mUICamera = null;
		mSceneCamera = null;
	}

	public void SetVisible(bool visible)
	{
		((Behaviour)this).enabled = visible;
		if (!visible)
		{
			SetBarFarAway();
		}
	}

	public void SetAttachUI(RectTransform uiTrans, Camera sceneCamera = null)
	{
		mAttachUITrans = uiTrans;
		mSceneCamera = sceneCamera;
		CanvasScaler componentInParent = ((Component)uiTrans).GetComponentInParent<CanvasScaler>();
		mCanvasRect = ((Component)componentInParent).GetComponent<RectTransform>();
		mReferenceHeight = componentInParent.referenceResolution.y;
		mUICamera = ((Component)componentInParent).GetComponent<Canvas>().worldCamera;
		mLastCanvasHeight = -1f;
		mLastOrthographicSize = -1f;
	}

	private void LateUpdate()
	{
		FixedPosAndScale();
	}

	private void FixedPosAndScale()
	{
		if (!((Object)(object)mTransformCache == (Object)null) && !((Object)(object)mAttachUITrans == (Object)null) && !((Object)(object)mCanvasRect == (Object)null))
		{
			Vector3 attachUIWorldPos = GetAttachUIWorldPos();
			if (attachUIWorldPos != mLastPosition)
			{
				mLastPosition = attachUIWorldPos;
				mTransformCache.position = attachUIWorldPos;
			}
			UpdateScale();
		}
	}

	private void UpdateScale()
	{
		Camera main = SceneCamera;
		if (!((Object)(object)main == (Object)null))
		{
			float orthographicSize = main.orthographicSize;
			Rect rect = mCanvasRect.rect;
			float height = rect.height;
			if ((!Mathf.Approximately(height, mLastCanvasHeight) || !Mathf.Approximately(orthographicSize, mLastOrthographicSize)) && !(height <= 0f))
			{
				mLastCanvasHeight = height;
				mLastOrthographicSize = orthographicSize;
				float num = mReferenceHeight / height;
				float num2 = orthographicSize / mSize * num;
				mTransformCache.localScale = new Vector3(num2, num2, num2);
			}
		}
	}

	private Vector3 GetAttachUIWorldPos()
	{
		Vector2 val = RectTransformUtility.WorldToScreenPoint(mUICamera, ((Transform)mAttachUITrans).position);
		Vector3 val2 = new Vector3(val.x, val.y, Mathf.Abs(((Component)SceneCamera).transform.position.z));
		Vector3 result = SceneCamera.ScreenToWorldPoint(val2);
		result.z = mZValue;
		return result;
	}

	private void SetBarFarAway()
	{
		Vector3 val = (mTransformCache.position = sFarPos);
		mLastPosition = val;
	}

	public GameRoundScene()
	{
	}

	static GameRoundScene()
	{
	}
}

}
