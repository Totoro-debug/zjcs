using System;
using System.Collections.Generic;


namespace UnityEngine.UI
{

[Serializable]
public class LoopScrollSinglePrefabSource : LoopScrollPrefabSource
{
	public GameObject Prefab;

	private Transform mPoolRoot;



	private readonly Stack<GameObject> mPool = new Stack<GameObject>();

	public void Init(Transform root)
	{
		GameObject val = new GameObject("Pool", new Type[1] { typeof(RectTransform) });

		val.transform.SetParent(root);
		mPoolRoot = val.transform;
		mPoolRoot.localPosition = Vector3.zero;
		mPoolRoot.localScale = Vector3.one;
		val.SetActive(false);
	}

	public Transform GetPoolRoot()
	{
		return mPoolRoot;
	}

	public GameObject GetObject(int index)
	{
		if (mPool.Count > 0)
		{
			return mPool.Pop();
		}
		return Object.Instantiate(Prefab, mPoolRoot, false);
	}

	public void ReturnObject(Transform trans, int index)
	{
		GameObject gameObject = ((Component)trans).gameObject;
		gameObject.SetActive(false);
		trans.SetParent(mPoolRoot, false);
		mPool.Push(gameObject);
	}
}

}
