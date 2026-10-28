namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public class AppearanceSkinClip : MonoBehaviour
{
	[SerializeField]
	private SkinType mSkinType;

	[SerializeField]
	private string mClipMeshProvideSlotName;

	[SerializeField]
	private Component mAppearanceCharacter;

	[SerializeField]
	private Component mAppearanceSpine;

	private static readonly int sClipLineX = Shader.PropertyToID("_ClipLineX");

	private static readonly int sClipLineY = Shader.PropertyToID("_ClipLineY");

	private static readonly int sClipLineC = Shader.PropertyToID("_ClipLineC");

	private static readonly int sClipDiag = Shader.PropertyToID("_ClipDiag");

	private float[] mVertices = new float[8];

	private IProfessionSkinClipSlot mSlot;

	private IProfessionSkinClipBoundingBox mBoundingBoxAttachment;

	private Transform mSkeletonTransform;

	private IProfessionSkinClipGraphic mSkeletonGraphic;
    private IProfessionSkinClipAppearance mSpineGraph;
    private IProfessionSkinClipCharacter mCharacterGraph;

	private Action mOnSkinUpdate;

	private Action<IProfessionSkinClipGraphic> mOnGraphicMeshAndMaterialsUpdated;

	private void Awake()
	{

		if ((Object)(object)mAppearanceCharacter == (Object)null)
		{
			mAppearanceCharacter = ProfessionAppearanceSkinClipBindings.Required.FindCharacter(this);
		}
		if ((Object)(object)mAppearanceSpine == (Object)null)
		{
			mAppearanceSpine = ProfessionAppearanceSkinClipBindings.Required.FindSpine(this);
		}
		if ((Object)(object)mAppearanceSpine != (Object)null)
		{
			mOnSkinUpdate = OnSkinUpdate;
			mSkeletonTransform = ((Component)mAppearanceSpine).transform;
            mSpineGraph = ProfessionAppearanceSkinClipBindings.Required.GetAppearance(mAppearanceSpine);
            mCharacterGraph = mAppearanceCharacter ? ProfessionAppearanceSkinClipBindings.Required.GetCharacter(mAppearanceCharacter) : null;
            IProfessionSkinClipGraphic val = mSpineGraph.Graphic;
			if (val != null)
			{
				mSkeletonGraphic = val;
				mOnGraphicMeshAndMaterialsUpdated = OnGraphicMeshAndMaterialsUpdated;
			}
		}
	}

	private void OnEnable()
	{
		if ((Object)(object)mAppearanceSpine != (Object)null)
		{
			IProfessionSkinClipAppearance appearanceSpine = mSpineGraph;
			appearanceSpine.OnSkinUpdate = (Action)Delegate.Combine(appearanceSpine.OnSkinUpdate, mOnSkinUpdate);
			InitData();
		}
		if (mSkeletonGraphic != null && mSkeletonGraphic.Instance)
		{
			mSkeletonGraphic.OnMeshAndMaterialsUpdated += mOnGraphicMeshAndMaterialsUpdated;
		}
	}

	private void OnDisable()
	{
		if ((Object)(object)mAppearanceSpine != (Object)null)
		{
			IProfessionSkinClipAppearance appearanceSpine = mSpineGraph;
			appearanceSpine.OnSkinUpdate = (Action)Delegate.Remove(appearanceSpine.OnSkinUpdate, mOnSkinUpdate);
		}
		if (mSkeletonGraphic != null && mSkeletonGraphic.Instance)
		{
			mSkeletonGraphic.OnMeshAndMaterialsUpdated -= mOnGraphicMeshAndMaterialsUpdated;
		}
	}

	private void Update()
	{
		if (mSkeletonGraphic == null || !mSkeletonGraphic.Instance)
		{
			UpdateClipMaterials();
		}
	}

	private void OnSkinUpdate()
	{
		if (mSkeletonGraphic != null && mSkeletonGraphic.Instance)
		{
			ResetCanvasRendererClipMaterials();
		}
		InitData();
	}

	private void InitData()
	{
		mBoundingBoxAttachment = null;
		IProfessionSkinClipSkeleton skeleton = mSpineGraph.Skeleton;
		if (skeleton == null)
		{
			return;
		}
		mSlot = skeleton.FindSlot(mClipMeshProvideSlotName);
		if (mSlot == null)
		{
			return;
		}
		IProfessionSkinClipSlot val = mSlot;
		if (val == null)
		{
			return;
		}
		object attachment = val.Attachment;
		IProfessionSkinClipBoundingBox val2 = attachment as IProfessionSkinClipBoundingBox;
		if (val2 == null)
		{
			return;
		}
		if (val2.WorldVerticesLength != mVertices.Length)
		{
            Debug.LogError(val2.Name + " 的顶点数数量错误。预期:" + mVertices.Length + "  实际:" + val2.WorldVerticesLength);
		}
		else
		{
			mBoundingBoxAttachment = val2;
		}
	}

	private void OnGraphicMeshAndMaterialsUpdated(IProfessionSkinClipGraphic skeletonGraphic)
	{
		UpdateClipMaterials();
	}

	public void UpdateClipMaterials()
	{

		if (mBoundingBoxAttachment != null && mCharacterGraph.TryGetSkinName(mSkinType, out var skinName) && mSpineGraph.TryGetMaterial(skinName, out var material))
		{
			mBoundingBoxAttachment.ComputeWorldVertices(mSlot, mVertices);
			float num = ((mSkeletonGraphic != null && mSkeletonGraphic.Instance) ? mSkeletonGraphic.MeshScale : 1f);
			Vector3 val = mSkeletonTransform.TransformPoint((Vector3)(new Vector2(mVertices[0], mVertices[1]) * num));
			Vector3 val2 = mSkeletonTransform.TransformPoint((Vector3)(new Vector2(mVertices[2], mVertices[3]) * num));
			Vector3 val3 = mSkeletonTransform.TransformPoint((Vector3)(new Vector2(mVertices[4], mVertices[5]) * num));
			Vector3 val4 = mSkeletonTransform.TransformPoint((Vector3)(new Vector2(mVertices[6], mVertices[7]) * num));
			float num2 = Cross2D(val3 - val, val2 - val);
			float num3 = Cross2D(val3 - val, val4 - val);
			if (num2 * num3 >= 0f)
			{
				Vector3 val5 = val2;
				Vector3 val6 = val3;
				Vector3 val7 = val4;
				Vector3 val8 = val;
				val = val5;
				val2 = val6;
				val3 = val7;
				val4 = val8;
			}
			Vector3 a = mSkeletonTransform.TransformVector(Vector3.right);
			Vector3 b = mSkeletonTransform.TransformVector(Vector3.up);
			float num4 = ((Cross2D(a, b) >= 0f) ? 1f : (-1f));
			Vector3 l = MakeLine((Vector2)val, (Vector2)val2) * num4;
			Vector3 l2 = MakeLine((Vector2)val2, (Vector2)val3) * num4;
			Vector3 l3 = MakeLine((Vector2)val3, (Vector2)val4) * num4;
			Vector3 l4 = MakeLine((Vector2)val4, (Vector2)val) * num4;
			Vector3 lAC = MakeLine((Vector2)val, (Vector2)val3) * num4;
			if (mSkeletonGraphic != null && mSkeletonGraphic.Instance)
			{
				SetCanvasRendererClipMaterials(material, l, l2, l3, l4, lAC);
			}
			else
			{
				SetClipMaterial(material, l, l2, l3, l4, lAC);
			}
		}
	}

	private static void SetClipMaterial(Material material, Vector3 l0, Vector3 l1, Vector3 l2, Vector3 l3, Vector3 lAC)
	{

		material.SetVector(sClipLineX, new Vector4(l0.x, l1.x, l2.x, l3.x));
		material.SetVector(sClipLineY, new Vector4(l0.y, l1.y, l2.y, l3.y));
		material.SetVector(sClipLineC, new Vector4(l0.z, l1.z, l2.z, l3.z));
		material.SetVector(sClipDiag, new Vector4(lAC.x, lAC.y, lAC.z, 0f));
		material.SetFloat(AppearanceMaterialUtils.sEnableHairClip, 1f);
	}

	private void SetCanvasRendererClipMaterials(Material sourceMaterial, Vector3 l0, Vector3 l1, Vector3 l2, Vector3 l3, Vector3 lAC)
	{

		List<CanvasRenderer> canvasRenderers = mSkeletonGraphic.canvasRenderers;
		Texture mainTexture = sourceMaterial.mainTexture;
		int i = 0;
		for (int count = canvasRenderers.Count; i < count; i++)
		{
			Material material = canvasRenderers[i].GetMaterial();
			if (!((Object)(object)material == (Object)null) && ((Object)(object)material == (Object)(object)sourceMaterial || (Object)(object)material.mainTexture == (Object)(object)mainTexture))
			{
				SetClipMaterial(material, l0, l1, l2, l3, lAC);
			}
		}
	}

	private void ResetCanvasRendererClipMaterials()
	{
		List<CanvasRenderer> canvasRenderers = mSkeletonGraphic.canvasRenderers;
		int i = 0;
		for (int count = canvasRenderers.Count; i < count; i++)
		{
			Material material = canvasRenderers[i].GetMaterial();
			if (!((Object)(object)material == (Object)null))
			{
				material.SetFloat(AppearanceMaterialUtils.sEnableHairClip, 0f);
			}
		}
	}

	private static Vector3 MakeLine(Vector2 a, Vector2 b)
	{

		Vector2 val = b - a;
		return new Vector3(val.y, 0f - val.x, (0f - val.y) * a.x + val.x * a.y);
	}

	private static float Cross2D(Vector3 a, Vector3 b)
	{

		return a.x * b.y - a.y * b.x;
	}
}

}
