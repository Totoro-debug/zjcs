using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public abstract class WindowMaskBase : MonoBehaviour
{
	[SerializeField]
	private WindowMaskType mMaskType;

	[SerializeField]
	private Button mBtn;

	[SerializeField]
	private Image mMaskImg;

	private Canvas mCanvase;

	private List<WindowMaskEffectBase> mMaskEffects = new List<WindowMaskEffectBase>();

	public WindowMaskType MaskType => mMaskType;

	public Image MaskImg => mMaskImg;


	public PopupWindow AttachWindow { get; private set; }

	public Canvas Canvas
	{
		get
		{
			if ((Object)(object)mCanvase == (Object)null)
			{
				mCanvase = ((Component)this).GetComponent<Canvas>();
			}
			return mCanvase;
		}
	}

	private void Start()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		((UnityEvent)mBtn.onClick).AddListener((UnityAction)MaskClickHandler);
	}

	private void MaskClickHandler()
	{
		if ((Object)(object)AttachWindow != (Object)null)
		{
			AttachWindow.OnMaskClick();
		}
	}

	public abstract UniTask Prepare(PopupWindow window, bool reopenWindowInTop, CancellationToken token);

	public void AttachToWindow(PopupWindow window)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		ApplyMaskEffect(window);
		PopupWindow attachWindow = AttachWindow;
		AttachWindow = window;
		if ((Object)(object)attachWindow != (Object)null)
		{
			OnUnattachToWindow(attachWindow);
		}
		if ((Object)(object)window == (Object)null)
		{
			Debug.LogError("AttachToWindow -> window is null");
		}
		else
		{
			((Behaviour)Canvas).enabled = !AttachWindow.IsPause;
			Canvas.sortingOrder = AttachWindow.Canvas.sortingOrder - 1;
			((Component)this).gameObject.SetActive(true);
			Canvas.overrideSorting = true;
			OnAttachToWindow(AttachWindow);
		}
	}

	protected abstract void OnAttachToWindow(PopupWindow target);

	public void UnattachToWindow()
	{
		PopupWindow attachWindow = AttachWindow;
		AttachWindow = null;
		ClearMaskEffect();
		((Component)this).gameObject.SetActive(false);
		if ((Object)(object)attachWindow != (Object)null)
		{
			OnUnattachToWindow(attachWindow);
		}
	}

	protected abstract void OnUnattachToWindow(PopupWindow target);

	public virtual void Refresh()
	{
		((Behaviour)Canvas).enabled = !AttachWindow.IsPause;
	}

	private void ApplyMaskEffect(PopupWindow window)
	{
		ClearMaskEffect();
		if ((Object)(object)window == (Object)null)
		{
			return;
		}
		((Component)window).GetComponents<WindowMaskEffectBase>(mMaskEffects);
		foreach (WindowMaskEffectBase mMaskEffect in mMaskEffects)
		{
			mMaskEffect.ApplyEffect(this);
		}
	}

	private void ClearMaskEffect()
	{
		foreach (WindowMaskEffectBase mMaskEffect in mMaskEffects)
		{
			mMaskEffect.CancelEffect(this);
		}
		mMaskEffects.Clear();
	}
}

}
