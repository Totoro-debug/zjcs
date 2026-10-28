using UnityEngine;

namespace Zjcs.Official.World.Map13Rendering
{
public class ResetMaterialPropertyBlock : MonoBehaviour
{
	[SerializeField]
	private Renderer mRenderer;

	private MaterialPropertyBlock mPropertyBlock;

	private void OnEnable()
	{
		if (mPropertyBlock == null)
		{
			mPropertyBlock = new MaterialPropertyBlock();
		}
		mRenderer.SetPropertyBlock(mPropertyBlock);
	}
}

}
