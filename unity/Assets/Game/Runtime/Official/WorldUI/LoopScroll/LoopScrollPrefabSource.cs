namespace UnityEngine.UI
{

public interface LoopScrollPrefabSource
{
	Transform GetPoolRoot();

	GameObject GetObject(int index);

	void ReturnObject(Transform trans, int index);
}

}
