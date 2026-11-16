using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
namespace Zjcs.Official.HomeCamera
{
[DefaultExecutionOrder(-1)]
public class FollowSceneActiveSelfController : MonoBehaviour
{
	private void Awake()
	{
		SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
	}
	private void OnEnable()
	{
		if (SceneManager.GetActiveScene() != ((Component)this).gameObject.scene)
		{
			((Component)this).gameObject.SetActive(false);
		}
	}
	private void OnDestroy()
	{
		SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
	}
	private void SceneManager_activeSceneChanged(Scene preScene, Scene currentScene)
	{
		((Component)this).gameObject.SetActive(currentScene == ((Component)this).gameObject.scene);
	}
}
}
