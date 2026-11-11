// Original home scene UI; explicit local service bindings.
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.World.NativeCamera;
using SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;
namespace Zjcs.Official.HomeSceneUiControls
{
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public abstract class AUIDirectionGuider : MonoBehaviour
{
	public IHomeSceneUiServices Services { protected get; set; }

	[SerializeField]
	private Button mGuiderBtn;

	[SerializeField]
	private Transform mGuideIcon;

	[SerializeField]
	private GameObject mMask;

	protected Vector3 mPosition;

	protected RectTransform mRootRectTransform;

	protected bool mIsUpdateGuider;

	protected bool mIsPosInit;

	private bool mIsActive = true;

	private Camera mCamera;

	private Coroutine mCoroutine;

	private DragControllerBase mBoundDragController;



	protected virtual void Awake()
	{
		mCamera = Camera.main;
		mRootRectTransform = GetComponentInParent<SafeArea>().gameObject.GetComponent<RectTransform>();
		SetGuiderBtnActive(value: false);
		InitPos();
		mGuiderBtn.onClick.AddListener(HandleGuideBtnOnClick);
	}

	protected virtual void OnEnable()
	{
		Services.Camera.ViewPosUpdated.AddListener(OnViewPosUpdated);
		Services.DragControllerManager.OnCurrentControllerChanged += OnDragCurrentControllerChanged;
		BindDragController(Services.DragControllerManager.CurrentController);
	}

	protected virtual void OnDisable()
	{
		Services.Camera.ViewPosUpdated.RemoveListener(OnViewPosUpdated);
		Services.DragControllerManager.OnCurrentControllerChanged -= OnDragCurrentControllerChanged;
		BindDragController(null);
		if (mCoroutine != null)
		{
			StopCoroutine(mCoroutine);
			mMask.SetActive(value: false);
		}
	}

	private void OnDragCurrentControllerChanged(DragControllerBase controller)
	{
		BindDragController(controller);
	}

	private void BindDragController(DragControllerBase controller)
	{
		if (mBoundDragController == controller)
		{
			return;
		}
		if (mBoundDragController != null)
		{
			mBoundDragController.OnPosChanged.RemoveListener(DragController_OnFocusPosUpdated);
		}
		mBoundDragController = controller;
		if (mBoundDragController != null)
		{
			mBoundDragController.OnPosChanged.AddListener(DragController_OnFocusPosUpdated);
			if (mIsPosInit && mIsActive)
			{
				SetActive(mIsActive);
			}
		}
	}

	protected virtual void Update()
	{
		if (mIsUpdateGuider)
		{
			mIsUpdateGuider = false;
			UpdateDirectionGuide(mPosition, mIsPosInit);
		}
	}

	private void DragController_OnFocusPosUpdated(Vector2 vec)
	{
		if (mIsPosInit)
		{
			UpdateDirectionGuide(mPosition, mIsPosInit);
		}
	}

	private void OnViewPosUpdated(Vector3 pos)
	{
		if (mIsPosInit)
		{
			UpdateDirectionGuide(mPosition, mIsPosInit);
		}
	}

	protected void UpdateDirectionGuide(Vector3 position, bool isPosInit = true)
	{
		if (!(mCamera == null))
		{
			if (!isPosInit || IsInView(position))
			{
				SetGuiderBtnActive(value: false);
				mIsUpdateGuider = false;
			}
			else
			{
				SetGuiderBtnActive(value: true);
				SetPositionAndAngles(position);
				mIsUpdateGuider = true;
			}
		}
	}

	protected abstract float GetFocusDuration();

	protected virtual IEnumerator MoveToTarget()
	{
		float duration = GetFocusDuration();
		float timer = 0f;
		mMask.SetActive(value: true);
		IHomeDirectionFocusController controller = Services.Camera.GetHomeController();
		while (timer < duration)
		{
			yield return null;
			timer += Time.deltaTime;
			float posX = Mathf.Lerp(Services.Camera.GetViewPos().x, mPosition.x, timer / duration);
			controller.SetFocusPositionXForce(posX);
		}
		controller.SetFocusPositionXForce(mPosition.x);
		mMask.SetActive(value: false);
	}

	private bool IsInView(Vector3 worldPos)
	{
		Vector2 vector = mCamera.WorldToViewportPoint(worldPos);
		if (vector.x >= 0f && vector.x <= 1f && vector.y >= 0f && vector.y <= 1f)
		{
			return true;
		}
		return false;
	}

	private void SetPositionAndAngles(Vector3 position)
	{
		if (!(Services.Camera.ControlBrainIdentity == null))
		{
			Vector2 focusObjViewPort = mCamera.WorldToViewportPoint(Services.Camera.CameraFollowPosition);
			Vector2 itemViewPort = mCamera.WorldToViewportPoint(position);
			float num = (float)(Math.Atan2(focusObjViewPort.y - itemViewPort.y, focusObjViewPort.x - itemViewPort.x) * 180.0 / Math.PI);
			Vector2 guiderPosition = DirectionGuiderUtils.GetGuiderPosition(focusObjViewPort, itemViewPort, num);
			Vector2 vector = new Vector2(Mathf.Clamp(guiderPosition.x, 0f, 1f), Mathf.Clamp(guiderPosition.y, 0f, 1f));
			Vector2 sizeDelta = mGuiderBtn.GetComponent<RectTransform>().sizeDelta;
			float num2 = sizeDelta.x * 0.75f;
			float num3 = sizeDelta.y * 0.75f;
			Rect rect = mRootRectTransform.rect;
			float width = rect.width;
			float height = rect.height;
			float x = Mathf.Clamp(vector.x * width, num2, width - num2);
			float y = Mathf.Clamp(vector.y * height, num3, height - num3);
			mGuiderBtn.transform.localPosition = new Vector3(x, y, 0f);
			mGuideIcon.localEulerAngles = new Vector3(0f, 0f, num + 90f);
		}
	}

	protected void HandleGuideBtnOnClick()
	{
		mCoroutine = StartCoroutine(MoveToTarget());
	}

	public virtual void Hide()
	{
		SetGuiderBtnActive(value: false);
	}

	protected abstract Vector3 GetInitPos();

	protected void InitPos()
	{
		mPosition = GetInitPos();
		mIsPosInit = true;
		UpdateDirectionGuide(mPosition, mIsPosInit);
	}

	protected void SetGuiderBtnActive(bool value)
	{
		mGuiderBtn.gameObject.SetActive(mIsActive & value);
	}

	public void SetActive(bool value)
	{
		mIsActive = value;
		mIsUpdateGuider = true;
	}
}
}
