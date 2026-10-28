// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public interface IMyPlayableStateMutex
	{
		bool IsOpenStateMutex();

		string GetStateMutexID();

		GameObject GetStateMutexGo();

		void GenerateStateMutexID(bool force);

		ActionBehaviour GetMutexAction();
	}
}
