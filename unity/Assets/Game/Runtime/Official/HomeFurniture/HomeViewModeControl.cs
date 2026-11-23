// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class HomeViewModeControl : MonoBehaviour
{
	[SerializeField]
	private WorldStateSwitcher<HomeViewMode> mViewModeSwitcher;

	private IDisposable mDisposable;

	private Action<HomeViewMode> mOnHomeViewModeChange;
	public IHomeFurnitureViewModes ViewModes { get; set; }

	private void Awake()
	{
		mOnHomeViewModeChange = OnHomeViewModeChange;
	}

	private void OnEnable()
	{
		mDisposable = ViewModes.Subscribe(mOnHomeViewModeChange);
		OnHomeViewModeChange(ViewModes.HomeViewMode);
	}

	private void OnDisable()
	{
		mDisposable?.Dispose();
		mDisposable = null;
	}

	private void OnHomeViewModeChange(HomeViewMode viewMode)
	{
		mViewModeSwitcher.SetState(viewMode);
	}
}

}
