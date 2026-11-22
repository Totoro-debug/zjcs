// Original selected native scene input; explicit pointer, camera and UI owners.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeSceneInput
{
using Object = UnityEngine.Object;

public abstract class ClickableHomeObj : MonoBehaviour
{
	[SerializeField]
	private List<GameObject> mTouchList;

	public SafeAction OnClickAction = new SafeAction();
	public IInputOperationModule InputOperationModule { get; set; }
	public IHomeSceneClickContext HomeData { get; set; }

	protected virtual void Start()
	{
		TryCollectClickGos();
		InputOperationModule.HookOnInput<OnGameObjectClickResult>(OnGameObjectClick);
	}

	private void TryCollectClickGos()
	{
		if (mTouchList == null) mTouchList = new List<GameObject>();
		if (mTouchList.Count == 0)
		{
			Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
			foreach (Collider collider in componentsInChildren)
			{
				mTouchList.Add(collider.gameObject);
			}
		}
	}

	protected virtual void OnDestroy()
	{
		InputOperationModule.UnhookOnInput<OnGameObjectClickResult>(OnGameObjectClick);
	}

	public void OnGameObjectClick(OnGameObjectClickResult result)
	{
		if ((!HomeData.InMyHome || HomeData.AllNoTipWindowsClosed) && IsAllowClickGameObject(result.Value.Target))
		{
			OnClickAction.Invoke();
			OnValidClick();
		}
	}

	protected virtual bool IsAllowClickHomeData(IHomeSceneClickContext homeData)
	{
		return homeData.InMyHome;
	}

	protected bool IsAllowClickGameObject(GameObject clickObject)
	{
		if (mTouchList.Contains(clickObject))
		{
			return IsAllowClickHomeData(HomeData);
		}
		return false;
	}

	protected abstract void OnValidClick();
}

}
