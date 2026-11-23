// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class EntityClickSound : MonoBehaviour
{
	public AudioClip Sound;
    [NonSerialized] public Action<AudioClip, GameObject> Post;

	private HashSet<GameObject> mHitBox;
	public IInputOperationModule InputOperationModule { get; set; }

	private void Awake()
	{
		mHitBox = new HashSet<GameObject>();
		Collider[] componentsInChildren = ((Component)this).GetComponentsInChildren<Collider>(true);
		foreach (Collider val in componentsInChildren)
		{
			mHitBox.Add(((Component)val).gameObject);
		}
		Collider2D[] componentsInChildren2 = ((Component)this).GetComponentsInChildren<Collider2D>(true);
		foreach (Collider2D val2 in componentsInChildren2)
		{
			mHitBox.Add(((Component)val2).gameObject);
		}
	}

	private void OnEnable()
	{
		InputOperationModule.HookOnInput<OnGameObjectClickResult>(OnGameObjectClick);
	}

	private void OnDisable()
	{
		InputOperationModule.UnhookOnInput<OnGameObjectClickResult>(OnGameObjectClick);
	}

	private void OnGameObjectClick(OnGameObjectClickResult result)
	{
		if (mHitBox.Contains(result.Value.Target))
		{
			Post?.Invoke(Sound, ((Component)this).gameObject);
		}
	}
}

}
