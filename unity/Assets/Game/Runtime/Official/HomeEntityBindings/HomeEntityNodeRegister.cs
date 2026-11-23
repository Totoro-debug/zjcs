// Selected original home binding implementation; see the source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityBindings
{
using Object = UnityEngine.Object;
public class HomeEntityNodeRegister : MonoBehaviour
{
	public MonoDictionary<string, GameObject> GameObjectDict;

	public GameObject GetGameObject(string key)
	{
		if (string.IsNullOrEmpty(key) || GameObjectDict == null)
		{
			return null;
		}
		GameObjectDict.TryGetValue(key, out var value);
		return value;
	}
}
}
