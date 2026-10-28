namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

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
