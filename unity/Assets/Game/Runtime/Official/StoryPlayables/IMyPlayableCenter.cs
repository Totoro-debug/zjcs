// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
	public interface IMyPlayableCenter
	{
		IEnumerable<OrderedFastList<MyPlayable>> AllPlayables { get; }

		bool CanPlayPlayable(MyPlayable playable, out MyPlayable mutexPlayable);

		void AddPlayable(MyPlayable playable, object comp);

		void RemovePlayable(MyPlayable playable, object comp);

		void ForceFlushDependence(Component component);

		void RemoveAll();

		void AddPlayableRef(EcEntityIdentifier owner, int classId, MyPlayable playable = null);

		int GetPlayableRef(EcEntityIdentifier owner, int classId);

		void RemovePlayableRef(EcEntityIdentifier owner, int classId);

		void PauseAllPlayable();

		void ResumeAllPlayable();

		void HideAllPlayable();

		void ShowAllPlayable();
	}
}
