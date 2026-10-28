namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class OffScreenUISpinePlayer : MonoBehaviour
{
	private CancellationTokenSource mCancellationTokenSource;

	private Component mPlayerSpinePartPartHandler;

	public List<IProfessionOffScreenGraphic> ListUISpines { get; private set; } = new List<IProfessionOffScreenGraphic>();

	public Component PlayerSpinePartPartPartPartHandler
	{
		get
		{
			if ((Object)(object)mPlayerSpinePartPartHandler == (Object)null)
			{
				mPlayerSpinePartPartHandler = ((Component)((Component)this).transform).GetComponent<IProfessionOffScreenPartHandler>() as Component;
			}
			return mPlayerSpinePartPartHandler;
		}
	}

	private void Awake()
	{
		if ((Object)(object)PlayerSpinePartPartPartPartHandler != (Object)null)
		{
			((IProfessionOffScreenPartHandler)PlayerSpinePartPartPartPartHandler).OnSkinChanged.AddListener(OnSpineSkinChanged);
		}
	}

	private void OnDisable()
	{
		TryDisposeToken();
	}

	private void OnDestroy()
	{
		if ((Object)(object)PlayerSpinePartPartPartPartHandler != (Object)null)
		{
			((IProfessionOffScreenPartHandler)PlayerSpinePartPartPartPartHandler).OnSkinChanged.RemoveListener(OnSpineSkinChanged);
		}
	}

	private void TryDisposeToken()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	private void OnSpineSkinChanged()
	{

		if (((Component)this).gameObject.activeInHierarchy)
		{
			UniTaskExtensions.Forget(DelayOffScreenSet(2));
		}
	}

    private async UniTask DelayOffScreenSet(int waitFrameNum)
    {
        TryDisposeToken();
        mCancellationTokenSource = new CancellationTokenSource();
        await UniTask.DelayFrame(waitFrameNum, (PlayerLoopTiming)8, mCancellationTokenSource.Token, false);
        TryDisposeToken();
        OffScreenSet();
    }

	private void OffScreenSet()
	{
		if (((Component)this).gameObject.activeInHierarchy)
		{
			ListUISpines.Clear();
			foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is IProfessionOffScreenGraphic graphic) ListUISpines.Add(graphic);
            }
			SetUISpineOffScreenArgs();
			SetUISpineAutoSizeDelta();
		}
	}

	private void SetUISpineOffScreenArgs()
	{

		foreach (IProfessionOffScreenGraphic listUISpine in ListUISpines)
		{
			if (listUISpine != null && listUISpine.Instance)
			{
				listUISpine.updateWhenInvisible = 0;
			}
		}
	}

	private void SetUISpineAutoSizeDelta()
	{
		foreach (IProfessionOffScreenGraphic listUISpine in ListUISpines)
		{
			if (listUISpine != null && listUISpine.Instance && !((Object)(object)listUISpine.Instance.transform == (Object)(object)((Component)this).transform) && listUISpine.Instance.gameObject.activeInHierarchy)
			{
				listUISpine.MatchRectTransformWithBounds();
			}
		}
	}
}

}
