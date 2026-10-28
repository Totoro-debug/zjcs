// Migrated official source; see map14-story-sound-runtime source receipt.
using Zjcs.Official.StoryPlayables;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StorySounds
{
	public class MyPlayableTimeLine : MyPlayable
	{
		[Header("TimeLine的基本配置")]
		public PlayableDirector myDirector;

		public List<string> mySourceBindings;

		public List<string> myTargetBindings;

		public float myFadeinTime;

		protected override void InitImpl()
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			foreach (PlayableBinding output in myDirector.playableAsset.outputs)
			{
				PlayableBinding current = output;
				if (mySourceBindings.Contains(current.streamName))
				{
					myDirector.SetGenericBinding(current.sourceObject, (Object)(object)mySource);
				}
				else if (myTargetBindings.Contains(current.streamName))
				{
					myDirector.SetGenericBinding(current.sourceObject, (Object)(object)myTarget);
				}
			}
			myDirector.time = 0.0;
			myDirector.timeUpdateMode = (DirectorUpdateMode)3;
		}

		protected override bool ProcessFrameImpl(float deltaTime)
		{
			PlayableDirector val = myDirector;
			val.time += (double)deltaTime;
			myDirector.Evaluate();
			return false;
		}

		public override float GetDuration()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			if ((int)myDirector.extrapolationMode == 2)
			{
				return (float)myDirector.duration;
			}
			return -1f;
		}

		protected override void PlayImpl()
		{
			myDirector.Play();
		}

		protected override void StopImpl()
		{
			myDirector.Stop();
		}
	}
}
