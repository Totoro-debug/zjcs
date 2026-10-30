using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "WaterConfig", menuName = "ScriptableConfig/WaterConfig", order = 1)]
public class WaterConfig : ScriptableObject, ISerializationCallbackReceiver
{
	[Serializable]
	public class Config
	{
		public Sprite Normal;

		public Sprite Mask;

		public bool Special;
	}

	public Material WaterMaskMat;

	public Material SubmersionAboveMat;

	public Material SubmersionUnderMat;

	public Tile DefaultMaskTile;

	public List<Config> Configs = new List<Config>();

	private Dictionary<Sprite, Config> mMap = new Dictionary<Sprite, Config>();

	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize()
	{
		int i = 0;
		for (int count = Configs.Count; i < count; i++)
		{
			Config config = Configs[i];
			if (!(config.Normal == null) && !(config.Mask == null))
			{
				mMap.Add(config.Normal, config);
			}
		}
	}

	private void OnEnable()
	{
		int i = 0;
		for (int count = Configs.Count; i < count; i++)
		{
			Config config = Configs[i];
			if (config.Normal == null || config.Mask == null)
			{
				Debug.LogError($"WaterConfig-{name}, 第{i}个为空");
			}
		}
	}

	public bool FindMask(Sprite normal, out Sprite result, out bool special)
	{
		if (mMap.TryGetValue(normal, out var value))
		{
			result = value.Mask;
			special = value.Special;
			return true;
		}
		result = null;
		special = false;
		return false;
	}

	private void RemoveDuplicatesByName(Sprite normal)
	{
		string text = normal.name;
		List<Sprite> list = new List<Sprite>();
		foreach (KeyValuePair<Sprite, Config> item in mMap)
		{
			if (item.Key == null)
			{
				Debug.LogError("WaterConfig:" + name + " has null Object");
			}
			else if (item.Key != normal && item.Key.name == text)
			{
				list.Add(item.Key);
			}
		}
		foreach (Sprite item2 in list)
		{
			mMap.Remove(item2);
			for (int num = Configs.Count - 1; num >= 0; num--)
			{
				if (Configs[num].Normal == item2)
				{
					Configs.RemoveAt(num);
				}
			}
		}
	}

	public void AddPair(Sprite normal, Sprite mask, bool fromMultiple = false)
	{
		RemoveDuplicatesByName(normal);
		if (mMap.TryGetValue(normal, out var value))
		{
			value.Mask = mask;
			value.Special = fromMultiple;
			return;
		}
		value = new Config
		{
			Normal = normal,
			Mask = mask,
			Special = fromMultiple
		};
		mMap.Add(normal, value);
		Configs.Add(value);
	}

	public void Remove(Sprite normal)
	{
		if (!mMap.Remove(normal))
		{
			return;
		}
		for (int num = Configs.Count - 1; num >= 0; num--)
		{
			if (Configs[num].Normal == normal)
			{
				Configs.RemoveAt(num);
				break;
			}
		}
	}

	public Config GetAnySpecialConfig()
	{
		foreach (Config config in Configs)
		{
			if (config.Special)
			{
				return config;
			}
		}
		return null;
	}
}
