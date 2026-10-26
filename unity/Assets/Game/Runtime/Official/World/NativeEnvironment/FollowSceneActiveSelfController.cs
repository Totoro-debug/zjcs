// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1)]
public class FollowSceneActiveSelfController : MonoBehaviour
{
	private void Awake()
	{
		SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
	}

	private void OnEnable()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		((Component)this).gameObject.SetActive(currentScene == ((Component)this).gameObject.scene);
	}
}

}
