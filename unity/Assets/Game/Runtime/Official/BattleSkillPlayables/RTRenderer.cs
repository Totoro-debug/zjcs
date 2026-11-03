using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class RTRenderer : MonoBehaviour
{
	private static readonly int sMainTex = Shader.PropertyToID("_MainTex");
	private static readonly int sColor = Shader.PropertyToID("_Color");
	private MeshRenderer mMeshRenderer;
	private MaterialPropertyBlock mPropertyBlock;

	private MeshRenderer MeshRenderer
	{
		get
		{
			if (mMeshRenderer == null)
				mMeshRenderer = GetComponent<MeshRenderer>();
			return mMeshRenderer;
		}
	}

	public void SetRT(RenderTexture rt)
	{
		if (mPropertyBlock == null)
			mPropertyBlock = new MaterialPropertyBlock();
		MeshRenderer.GetPropertyBlock(mPropertyBlock);
		mPropertyBlock.SetTexture(sMainTex, rt);
		MeshRenderer.SetPropertyBlock(mPropertyBlock);
	}

	public void SetColor(Color color)
	{
		if (mPropertyBlock == null)
			mPropertyBlock = new MaterialPropertyBlock();
		mPropertyBlock.SetColor(sColor, color);
		MeshRenderer.SetPropertyBlock(mPropertyBlock);
	}
}
