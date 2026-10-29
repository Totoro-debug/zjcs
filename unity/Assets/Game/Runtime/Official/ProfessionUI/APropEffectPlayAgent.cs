namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class APropEffectPlayAgent : MonoBehaviour
{
	[SerializeField]
	private float mEffectInterval = 0.1f;

	protected abstract IEnumerable<ProfessionPlayerPropItem> GetPropItems();

	protected virtual void OnDisable()
	{
		StopPlayEffect();
	}

	public void StartPlayEffect(Action callback = null, bool isForce = false)
	{
		StopPlayEffect();
		((MonoBehaviour)this).StartCoroutine(PlayEffectImpl(mEffectInterval, GetPropItems(), callback, isForce));
	}

	public void StopPlayEffect()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		StopEffect(GetPropItems());
	}

	public IEnumerator PlayEffectImpl(float interval, IEnumerable<ProfessionPlayerPropItem> propItems, Action callback, bool isForce = false)
	{
		Coroutine coroutine = null;
		WaitForSeconds wait = new WaitForSeconds(interval);
		foreach (ProfessionPlayerPropItem propItem in propItems)
		{
			if (((Component)propItem).gameObject.activeSelf)
			{
				coroutine = ((MonoBehaviour)this).StartCoroutine(propItem.PlayEffect(isForce));
				if (interval != 0f)
				{
					yield return wait;
				}
			}
		}
		if (coroutine != null)
		{
			yield return coroutine;
		}
		callback?.Invoke();
	}

	public void StopEffect(IEnumerable<ProfessionPlayerPropItem> propItems)
	{
		foreach (ProfessionPlayerPropItem propItem in propItems)
		{
			if (((Component)propItem).gameObject.activeSelf)
			{
				propItem.StopEffect();
			}
		}
	}
}

}
