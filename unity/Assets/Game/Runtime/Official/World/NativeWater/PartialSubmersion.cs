// Migrated from selected official CustomWater.dll; see Map13 source receipt.
namespace Zjcs.Official.World.NativeWater
{
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(SpriteRenderer))]
public class PartialSubmersion : MonoBehaviour
{
	[SerializeField]
	[HideInInspector]
	private SpriteRenderer mSR;

	[SerializeField]
	[HideInInspector]
	private SpriteRenderer mAbove;

	[SerializeField]
	[HideInInspector]
	private SpriteRenderer mMask;

	public Sprite Mask;

	public bool Updown;

	[Range(-1f, 1f)]
	public float UpdownValue;

	private static readonly int sMaskId = Shader.PropertyToID("_HeightMap");

	private static readonly int sWaterId = Shader.PropertyToID("_Water");

	private void OnEnable()
	{
		SetUpdownValue(UpdownValue);
	}

	public void SetUpdownValue(float value)
	{
		if (Updown)
		{
			Color color = ((!(value > 0f)) ? new Color(0f, Mathf.Abs(value), 0f, 1f) : new Color(value, 0f, 0f, 1f));
			mSR.color = color;
			mAbove.color = color;
			mMask.color = color;
		}
	}

	private void OnDidApplyAnimationProperties()
	{
		SetUpdownValue(UpdownValue);
	}
}

}
