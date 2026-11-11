using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
namespace Zjcs.Official.HomeMainUiControls
{
// Original selected home UI control; local service bindings only.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FunctionEntrysPanelBase : MonoBehaviour
{
	public GAction OnExpandPanel = new GAction();

	public GAction OnFoldEndPanel = new GAction();

	public List<RectTransform> SelfRect;

	public List<Button> EntryBtns;

	public WorldStateSwitcher<bool> StateSwitcher;

	[SerializeField]
	private bool mIsInogreWindows;

	private bool mExpanded;

	public IHomeMainUiInput InputOperationModule { private get; set; }
	public IHomeMainUiWindows Windows { protected get; set; }
	public Camera UiCamera { private get; set; }

	protected virtual void Awake()
	{
		foreach (Button entryBtn in EntryBtns)
		{
			entryBtn.onClick.AddListener(OnEntryBtnClick);
		}
	}

	protected virtual void OnEnable()
	{
		InputOperationModule.HookGlobalPointerDown(OnGameObjectPointerDown);
	}

	protected virtual void OnDisable()
	{
		InputOperationModule.UnhookGlobalPointerDown(OnGameObjectPointerDown);
	}

	private void OnGameObjectPointerDown(HomeUiGlobalPointerDownResult result)
	{
		if (mExpanded && (mIsInogreWindows || (!Windows.HasPanel && !Windows.HasDialog)) && !CheckIfClickInsideUI(result.ScreenPos))
		{
			FoldPanel();
		}
	}

	public void RequestExpandPanel()
	{
		ExpandPanel();
	}

	public void RequestFoldPanel()
	{
		FoldPanel();
	}

	protected virtual void ExpandPanel()
	{
		StateSwitcher.SetState(state: true);
		OnExpandPanel.Invoke();
	}

	protected virtual void FoldPanel()
	{
		StateSwitcher.SetState(state: false);
		mExpanded = false;
	}

	protected virtual void OnEntryBtnClick()
	{
		if (mExpanded)
		{
			FoldPanel();
		}
		else
		{
			ExpandPanel();
		}
	}

	public void OnExpandEnd()
	{
		mExpanded = true;
	}

	public virtual void OnFoldEnd()
	{
		OnFoldEndPanel.Invoke();
	}

	private bool CheckIfClickInsideUI(Vector2 clickPosition)
	{
		foreach (RectTransform item in SelfRect)
		{
			if (HomeMainUiUtils.CheckIfClickInsideUI(item, clickPosition, UiCamera))
			{
				return true;
			}
		}
		return false;
	}
}
}
