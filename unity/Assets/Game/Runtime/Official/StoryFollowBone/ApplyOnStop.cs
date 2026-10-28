// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryFollowBone
{
	public class ApplyOnStop : MonoBehaviour
	{
		public MyPlayable myPlayable { get; set; }

		private void Awake()
		{
			myPlayable = ((Component)this).GetComponent<MyPlayable>();
			if ((Object)(object)myPlayable != (Object)null)
			{
				myPlayable.OnStop += new Action<MyPlayable>(HandleOnStop);
				myPlayable.OnFadeOut += new Action<MyPlayable, float>(HandleOnFadeOut);
			}
		}

		private void OnDestroy()
		{
			if ((Object)(object)myPlayable != (Object)null)
			{
				myPlayable.OnStop -= new Action<MyPlayable>(HandleOnStop);
				myPlayable.OnFadeOut -= new Action<MyPlayable, float>(HandleOnFadeOut);
			}
		}

		protected virtual void HandleOnFadeOut(MyPlayable playable, float duration)
		{
		}

		protected virtual void HandleOnStop(MyPlayable obj)
		{
		}
	}

}
