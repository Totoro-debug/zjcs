namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AppearanceMaterialUtils
{
	public static readonly int sDyeTexId = Shader.PropertyToID("_DyeTex");

	public static readonly int sMainTexId = Shader.PropertyToID("_MainTex");

	public static readonly int sStencil = Shader.PropertyToID("_Stencil");

	public static readonly int sStencilOp = Shader.PropertyToID("_StencilOp");

	public static readonly int sStencilComp = Shader.PropertyToID("_StencilComp");

	public static readonly int sStencilReadMask = Shader.PropertyToID("_StencilReadMask");

	public static readonly int sStencilWriteMask = Shader.PropertyToID("_StencilWriteMask");

	public static readonly int sColorMask = Shader.PropertyToID("_ColorMask");

	public static readonly int sUseUIAlphaClip = Shader.PropertyToID("_UseUIAlphaClip");

	public static readonly int sLight = Shader.PropertyToID("_Light");

	public static readonly int sLightAtten = Shader.PropertyToID("_LightAtten");

	public static readonly int sWorldClipHeight = Shader.PropertyToID("_WorldClipHeight");

	public static readonly int sZWrite = Shader.PropertyToID("_ZWrite");

	public static readonly int sHue = Shader.PropertyToID("_Hue");

	public static readonly int sSaturation = Shader.PropertyToID("_Saturation");

	public static readonly int sValue = Shader.PropertyToID("_Value");

	public static readonly int sIntensity = Shader.PropertyToID("_Intensity");

	public static readonly int sEnableHairClip = Shader.PropertyToID("_EnableHairClip");

	private const float kDefaultLightAtten = 0.565f;

	private static int sMaxChannel;

	private static readonly Dictionary<int, int> sDyePropertyDic = new Dictionary<int, int>();

	public static void SetDye(Material material, int channel, Vector3 vector3)
	{

		if (channel > sMaxChannel)
		{
			sMaxChannel = channel;
		}
		material.SetVector(GetDyePropertyId(channel), (Vector4)vector3);
	}

	public static int GetDyePropertyId(int channel)
	{
		if (!sDyePropertyDic.TryGetValue(channel, out var value))
		{
			value = Shader.PropertyToID($"_DyeHsv{channel}");
			sDyePropertyDic.Add(channel, value);
		}
		return value;
	}

	public static void ResetRenderMaterial(Material material, Material originalMaterial)
	{
		Texture texture = originalMaterial.GetTexture(sMainTexId);
		material.SetFloat(sLightAtten, 0.565f);
		material.SetFloat(sEnableHairClip, 0f);
		SetMaterialWithMainTexture(material, texture);
		ResetMaterialKeyword(material);
	}

	public static Material CreateUIMaterial(Shader shader)
	{

		return new Material(shader);
	}

	public static void InitUIMaterial(Material material, Texture mainTexture, Material originalMaterial)
	{
		material.SetTexture(sMainTexId, mainTexture);
		SetMaterialStencil(material, (int)originalMaterial.GetFloat(sStencil), originalMaterial.GetFloat(sStencilOp), originalMaterial.GetFloat(sStencilComp), originalMaterial.GetFloat(sColorMask), originalMaterial.GetFloat(sStencilReadMask), originalMaterial.GetFloat(sStencilWriteMask));
	}

	public static void SetMaterialWithMainTexture(Material material, Texture texture)
	{

		for (int i = 1; i <= sMaxChannel; i++)
		{
			material.SetVector(GetDyePropertyId(i), Vector4.zero);
		}
		material.SetTexture(sMainTexId, texture);
		material.SetTexture(sDyeTexId, (Texture)null);
	}

	public static void SetMaterialStencil(Material material, int stencilID, float operation, float compareFunction, float colorWriteMask, float readMask, float writeMask)
	{
		bool flag = (int)operation != 0 && writeMask > 0f;
		material.SetFloat(sStencil, (float)stencilID);
		material.SetFloat(sStencilOp, operation);
		material.SetFloat(sStencilComp, compareFunction);
		material.SetFloat(sStencilReadMask, readMask);
		material.SetFloat(sStencilWriteMask, writeMask);
		material.SetFloat(sColorMask, colorWriteMask);
		material.SetFloat(sUseUIAlphaClip, flag ? 1f : 0f);
		if (flag)
		{
			material.EnableKeyword("UNITY_UI_ALPHACLIP");
		}
		else
		{
			material.DisableKeyword("UNITY_UI_ALPHACLIP");
		}
	}

	public static bool IsMaskable(Component skeletonGraphic)
	{
		Transform parent = ((Component)skeletonGraphic).transform.parent;
		Mask val = default;
		while ((Object)(object)parent != (Object)null)
		{
			if (((Component)parent).TryGetComponent<Mask>(out val) && ((Behaviour)val).enabled && (Object)(object)val.graphic != (Object)null && ((Behaviour)val.graphic).enabled)
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	public static void ResetMaterialKeyword(Material material)
	{
		material.DisableKeyword("_DISSOLVE");
		material.DisableKeyword("_HITEFFECT");
		material.DisableKeyword("_FROZEN");
		material.DisableKeyword("_FORDING");
	}

	public static int GetStencilValue(Transform transform)
	{
		Transform val = MaskUtilities.FindRootSortOverrideCanvas(transform);
		return MaskUtilities.GetStencilDepth(transform, val);
	}

	public static void Clear()
	{
		sMaxChannel = 0;
	}
}

}
