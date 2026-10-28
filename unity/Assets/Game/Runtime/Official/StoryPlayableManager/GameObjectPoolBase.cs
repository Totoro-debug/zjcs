// Migrated official framework source; see playable manager receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryPlayableManager
{
public class GameObjectPoolBase<T> where T : Component
{
	protected GameObject mGoPrefab;

	protected Func<Transform, GameObject, bool, GameObject> mCreateFunc;

	protected Transform mGoSlotNode;

	protected readonly Queue<GameObject> mGoQueue = new Queue<GameObject>();

	protected Transform mPoolTransform;

	public string RootName
	{
		get
		{
			return ((Object)mPoolTransform).name;
		}
		set
		{
			((Object)mPoolTransform).name = value;
		}
	}

	public static Func<Transform, Transform, GameObject, bool, GameObject> DefaulCreateFunc => (Transform rootNode, Transform slot, GameObject goPrefab, bool autoActive) =>
	{
		if ((Object)(object)rootNode == (Object)null)
		{
			return (GameObject)null;
		}
		GameObject val = StoryPlayableInstantiation.Instantiate(rootNode, goPrefab, slot, false);
		if (autoActive)
		{
			val.SetActive(true);
		}
		return val;
	};

	protected Transform GetFinalSlotNode(Transform slot)
	{
		if (!((Object)(object)slot == (Object)null) || !((Object)(object)mGoSlotNode != (Object)null))
		{
			return slot;
		}
		return mGoSlotNode;
	}

	public void Extend(int count = 1)
	{
		for (int i = 0; i < count; i++)
		{
			mGoQueue.Enqueue(mCreateFunc(mPoolTransform, mGoPrefab, arg3: false));
		}
	}

	public void MakeSureCount(int count)
	{
		int num = count - mGoQueue.Count;
		if (num > 0)
		{
			Extend(num);
		}
	}

	public void Release(int count)
	{
		if (IsUsable())
		{
			if (count > mGoQueue.Count)
			{
				count = mGoQueue.Count;
			}
			for (int i = 0; i < count; i++)
			{
				Object.Destroy((Object)(object)mGoQueue.Dequeue());
			}
		}
	}

	public void Release()
	{
		Release(mGoQueue.Count);
	}

	public T Borrow(Transform slot = null, bool autoActive = true)
	{
		GameObject val = BorrowGo(slot, autoActive);
		if ((Object)(object)val != (Object)null)
		{
			return val.GetComponent<T>();
		}
		return default;
	}

	public virtual GameObject BorrowGo(Transform slot = null, bool autoActive = true)
	{
		if (!IsUsable())
		{
			return null;
		}
		GameObject val = null;
		if (mGoQueue.Count > 0)
		{
			val = mGoQueue.Dequeue();
		}
		if ((Object)(object)val == (Object)null)
		{
			val = mCreateFunc(GetFinalSlotNode(slot), mGoPrefab, autoActive);
		}
		else
		{
			val.transform.SetParent(GetFinalSlotNode(slot), false);
			if (autoActive)
			{
				val.SetActive(true);
			}
		}
		if ((Object)(object)val != (Object)null)
		{
			Reset(val.GetComponent<T>());
		}
		else
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
		return val;
	}

	public void RecycleMany(List<T> list, int count)
	{
		if (count >= list.Count)
		{
			list.ForEach((T t) =>
			{
				Reset(t);
			});
			for (int num = list.Count; num < count; num++)
			{
				list.Add(Borrow());
			}
			return;
		}
		for (int num2 = list.Count - 1; num2 >= count; num2--)
		{
			T val = list[num2];
			list.Remove(val);
			Return(val);
		}
		list.ForEach((T t) =>
		{
			Reset(t);
		});
	}

	public void Reset(T component)
	{
		if ((Object)(object)component != (Object)null)
		{
			if (component is IGameObjectPoolItem gameObjectPoolItem)
			{
				gameObjectPoolItem.OnGameObjectPoolReset();
			}
			return;
		}
		UnityEngine.Debug.LogWarning("Official playable diagnostic");
	}

	public virtual void Return(T component)
	{
		if ((Object)(object)component != (Object)null)
		{
			if (component is IGameObjectPoolItem gameObjectPoolItem)
			{
				gameObjectPoolItem.OnGameObjectPoolClear();
			}
			GameObject gameObject = ((Component)component).gameObject;
			if ((Object)(object)gameObject != (Object)null && IsUsable())
			{
				gameObject.SetActive(false);
				gameObject.transform.SetParent(mPoolTransform, false);
				mGoQueue.Enqueue(gameObject);
			}
		}
		else
		{
			UnityEngine.Debug.LogWarning("Official playable diagnostic");
		}
	}

	public void ReturnMany(List<T> list)
	{
		for (int i = 0; i < list.Count; i++)
		{
			Return(list[i]);
		}
		list.Clear();
	}

	public virtual void Destory()
	{
	}

	protected virtual bool IsUsable()
	{
		if ((Object)(object)mPoolTransform == (Object)null)
		{
			return false;
		}
		return true;
	}
}

}
