// Migrated current official application source; see status material runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleMaterialPlayables
{
[DisallowMultipleComponent]
public class MeshMaterialProperty : MonoBehaviour
{
	public MeshRenderer MeshRenderer;

	[SerializeField]
	private AppearanceRenderer mAppearanceRenderer;

	[SerializeField]
	private AppearanceRendererLite mAppearanceRendererLite;

	private readonly Dictionary<string, List<bool>> mKeywordDefaultDic = new Dictionary<string, List<bool>>(1);

	private readonly Dictionary<string, List<float>> mFloatDefaultDic = new Dictionary<string, List<float>>(1);

	private readonly Dictionary<string, List<Color>> mColorDefaultDic = new Dictionary<string, List<Color>>(1);

	private readonly Dictionary<string, int> mCounterDic = new Dictionary<string, int>(2);

	private IEnumerable<Material> mMaterials;

	private List<Material> mAppearanceRendererMaterials;

	private bool mValid;

	public Vector2 RendererBoundsSize
	{
		get
		{
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)MeshRenderer != (Object)null))
			{
				return Vector2.one;
			}
			Bounds localBounds = ((Renderer)MeshRenderer).localBounds;
			return (Vector2)localBounds.size;
		}
	}

	public bool Valid => mValid;

	private void OnDestroy()
	{
		Clear();
	}

	public void Init()
	{
		mValid = false;
		if ((Object)(object)mAppearanceRenderer != (Object)null)
		{
			mMaterials = CollectAppearanceRendererMaterials();
		}
		else if ((Object)(object)mAppearanceRendererLite != (Object)null)
		{
			mMaterials = mAppearanceRendererLite.GetIndependentMaterials();
		}
		else if ((Object)(object)MeshRenderer != (Object)null)
		{
			mMaterials = ((Renderer)MeshRenderer).sharedMaterials;
		}
		if (mMaterials == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return;
		}
		foreach (Material mMaterial in mMaterials)
		{
			if (!(bool)mMaterial)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
				return;
			}
		}
		mValid = true;
	}

	public void SetAppearanceTarget(GameObject go)
	{
		Clear();
		AppearanceRendererLite appearanceRendererLite = default;
		AppearanceRenderer appearanceRenderer = default;
		if (go.TryGetComponent<AppearanceRendererLite>(out appearanceRendererLite))
		{
			mAppearanceRendererLite = appearanceRendererLite;
		}
		else if (go.TryGetComponent<AppearanceRenderer>(out appearanceRenderer))
		{
			mAppearanceRenderer = appearanceRenderer;
		}
	}

	private IEnumerable<Material> CollectAppearanceRendererMaterials()
	{
		if (mAppearanceRendererMaterials == null)
		{
			mAppearanceRendererMaterials = new List<Material>();
		}
		mAppearanceRendererMaterials.Clear();
		Material val = default;
		Material val2 = default;
		foreach (KeyValuePair<Material, Material> item2 in mAppearanceRenderer.IndependentMaterialDict)
		{
			val = item2.Key; val2 = item2.Value;
			Material item = val2;
			mAppearanceRendererMaterials.Add(item);
		}
		SkinType skinType = default;
		AppearanceSkeletonPartEntry appearanceSkeletonPartEntry = default;
		AppearanceRendererLite appearanceRendererLite = default;
		foreach (KeyValuePair<SkinType, AppearanceSkeletonPartEntry> item3 in mAppearanceRenderer.PartHandler.LoadedSkeletonDict)
		{
			skinType = item3.Key; appearanceSkeletonPartEntry = item3.Value;
			AppearanceSkeletonPartEntry appearanceSkeletonPartEntry2 = appearanceSkeletonPartEntry;
			if (appearanceSkeletonPartEntry2.Part.SkeletonTarget.TryGetComponent<AppearanceRendererLite>(out appearanceRendererLite))
			{
				Material[] independentMaterials = appearanceRendererLite.GetIndependentMaterials();
				mAppearanceRendererMaterials.AddRange(independentMaterials);
			}
		}
		return mAppearanceRendererMaterials;
	}

	public bool HasFloat(string key)
	{
		bool enabled;
		if (!mValid)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return false;
		}
		foreach (Material mMaterial in mMaterials)
		{
			if (!mMaterial.HasFloat(key))
			{
				enabled = false;
				return enabled;
			}
		}
		return true;
	}

	public bool HasColor(string key)
	{
		bool enabled;
		if (!mValid)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return false;
		}
		foreach (Material mMaterial in mMaterials)
		{
			if (!mMaterial.HasColor(key))
			{
				enabled = false;
				return enabled;
			}
		}
		return true;
	}

	public void UseKeyword(string keyword, bool enable)
	{
		UseAndCacheDefaultValue(keyword, mKeywordDefaultDic, GetMaterialKeyword);
		SetKeyword(keyword, enable);
	}

	public void UseFloat(string propertyName)
	{
		UseAndCacheDefaultValue(propertyName, mFloatDefaultDic, GetMaterialFloat);
	}

	public void UseColor(string propertyName)
	{
		UseAndCacheDefaultValue<Color>(propertyName, mColorDefaultDic, (Func<Material, string, Color>)GetMaterialColor);
	}

	public void SetKeyword(string key, bool value)
	{
		if (!mValid)
		{
			return;
		}
		foreach (Material mMaterial in mMaterials)
		{
			SetMaterialKeyword(mMaterial, key, value);
		}
	}

	public void SetFloat(string key, float value)
	{
		if (!mValid)
		{
			return;
		}
		foreach (Material mMaterial in mMaterials)
		{
			SetMaterialFloat(mMaterial, key, value);
		}
	}

	public void SetColor(string key, Color value)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (!mValid)
		{
			return;
		}
		foreach (Material mMaterial in mMaterials)
		{
			SetMaterialColor(mMaterial, key, value);
		}
	}

	public void SetVector(string key, Vector4 value)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (!mValid)
		{
			return;
		}
		foreach (Material mMaterial in mMaterials)
		{
			SetMaterialVector(mMaterial, key, value);
		}
	}

	public void ReturnKeyword(string keyword)
	{
		ReturnAndReset(keyword, mKeywordDefaultDic, SetMaterialKeyword);
	}

	public void ReturnFloat(string propertyName)
	{
		ReturnAndReset(propertyName, mFloatDefaultDic, SetMaterialFloat);
	}

	public void ReturnColor(string propertyName)
	{
		ReturnAndReset<Color>(propertyName, mColorDefaultDic, (Action<Material, string, Color>)SetMaterialColor);
	}

	private void Clear()
	{
		Clear(mKeywordDefaultDic, ReturnKeyword);
		Clear(mFloatDefaultDic, ReturnFloat);
		Clear(mColorDefaultDic, ReturnColor);
		mMaterials = null;
		mValid = false;
		mAppearanceRendererMaterials?.Clear();
	}

	private void Clear<T>(Dictionary<string, List<T>> cacheDic, Action<string> ret)
	{
		foreach (KeyValuePair<string, List<T>> item in cacheDic)
		{
			if (item.Value.Count != 0)
			{
				mCounterDic[item.Key] = 0;
				ret(item.Key);
			}
		}
	}

	private bool Use(string key)
	{
		if (!mCounterDic.TryGetValue(key, out var value))
		{
			mCounterDic.Add(key, 1);
			return true;
		}
		mCounterDic[key] = value + 1;
		return false;
	}

	private void UseAndCacheDefaultValue<T>(string key, Dictionary<string, List<T>> cacheDic, Func<Material, string, T> getter) where T : struct
	{
		if (!Use(key))
		{
			return;
		}
		List<T> orAddValue = GetOrAddValue(cacheDic, key);
		if (mMaterials == null)
		{
			return;
		}
		foreach (Material mMaterial in mMaterials)
		{
			orAddValue.Add(getter(mMaterial, key));
		}
	}

	private bool Return(string key)
	{
		if (!mCounterDic.TryGetValue(key, out var value))
		{
			return false;
		}
		value--;
		if (value <= 0)
		{
			mCounterDic.Remove(key);
			return true;
		}
		mCounterDic[key] = value;
		return false;
	}

	private void ReturnAndReset<T>(string key, Dictionary<string, List<T>> cacheDic, Action<Material, string, T> setter) where T : struct
	{
		if (!Return(key))
		{
			return;
		}
		List<T> orAddValue = GetOrAddValue(cacheDic, key);
		int num = 0;
		foreach (Material mMaterial in mMaterials)
		{
			if (!((Object)(object)mMaterial == (Object)null))
			{
				setter(mMaterial, key, orAddValue[num++]);
			}
		}
		orAddValue.Clear();
	}

	private List<T> GetOrAddValue<T>(Dictionary<string, List<T>> dict, string key) where T : struct
	{
		if (!dict.TryGetValue(key, out var value))
		{
			value = new List<T>();
			dict.Add(key, value);
		}
		return value;
	}

	private string GetTargetName()
	{
		if ((Object)(object)mAppearanceRenderer != (Object)null)
		{
			return ((Object)mAppearanceRenderer).name;
		}
		if ((Object)(object)mAppearanceRendererLite != (Object)null)
		{
			return ((Object)mAppearanceRendererLite).name;
		}
		if ((Object)(object)MeshRenderer != (Object)null)
		{
			return ((Object)MeshRenderer).name;
		}
		return ((Object)this).name;
	}

	private float GetMaterialFloat(Material material, string key)
	{
		if (material.HasFloat(key))
		{
			return material.GetFloat(key);
		}
		return 0f;
	}

	private Color GetMaterialColor(Material material, string key)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		if (material.HasColor(key))
		{
			return material.GetColor(key);
		}
		return default;
	}

	private bool GetMaterialKeyword(Material material, string key)
	{
		return material.IsKeywordEnabled(key);
	}

	private void SetMaterialFloat(Material material, string key, float value)
	{
		material.SetFloat(key, value);
	}

	private void SetMaterialColor(Material material, string key, Color color)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		material.SetColor(key, color);
	}

	private void SetMaterialKeyword(Material material, string key, bool value)
	{
		if (value)
		{
			material.EnableKeyword(key);
		}
		else
		{
			material.DisableKeyword(key);
		}
	}

	private void SetMaterialVector(Material material, string key, Vector4 value)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		material.SetVector(key, value);
	}
}

}
