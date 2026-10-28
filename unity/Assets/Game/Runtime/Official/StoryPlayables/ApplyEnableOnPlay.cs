// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public class ApplyEnableOnPlay : ApplyOnInit
	{
		private bool? mIsActiveInitBefore;

		protected override void OnDestroy()
		{
			myPlayable.OnStop -= new Action<MyPlayable>(SetInitBeforeActive);
			base.OnDestroy();
		}

		protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
			if (!mIsActiveInitBefore.HasValue)
			{
				mIsActiveInitBefore = playable.myApplyComponent.gameObject.activeInHierarchy;
				playable.OnStop += new Action<MyPlayable>(SetInitBeforeActive);
			}
		}

		protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
		{
			if (SetActive(playable, active: true))
			{
				MyPlayableApplyObj.Bind(playable.myApplyComponent.gameObject, playable);
			}
		}

		private void SetInitBeforeActive(MyPlayable myPlayable)
		{
			if (mIsActiveInitBefore.HasValue)
			{
				SetActive(myPlayable, mIsActiveInitBefore.Value);
			}
		}

		private bool SetActive(MyPlayable myPlayable, bool active)
		{
			if ((Object)(object)myPlayable == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
				return false;
			}
			Component myApplyComponent = myPlayable.myApplyComponent;
			if ((Object)(object)myApplyComponent == (Object)null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
				return false;
			}
			myApplyComponent.gameObject.SetActive(active);
			return true;
		}
	}
}
