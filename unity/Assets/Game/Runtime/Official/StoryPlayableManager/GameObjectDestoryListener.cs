// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
public class GameObjectDestoryListener : MonoBehaviour
{
	public GAction OnDestoryed = new GAction();

	public bool IsBeingDestroyed { get; private set; }

	private void OnDestroy()
	{
		IsBeingDestroyed = true;
		OnDestoryed.Invoke();
	}
}

}
