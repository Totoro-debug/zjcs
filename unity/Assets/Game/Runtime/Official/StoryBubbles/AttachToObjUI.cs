// Migrated original source; see map14-story-bubble-runtime source receipt.
using Object = UnityEngine.Object;
using UnityEngine;

namespace Zjcs.Official.StoryBubbles
{
public class AttachToObjUI : MonoBehaviour
{
	[Tooltip("3D物体的偏移")]
	public Vector3 myOffset;

	[Tooltip("UI物体的偏移")]
	public Vector3 UIOffset;

	protected Canvas mCanvas;

	private GameObject mAttachObject;

	public GameObject AttachObject
	{
		get
		{
			return mAttachObject;
		}
		set
		{
			mAttachObject = value;
			UpdateUI();
		}
	}

	public Canvas Canvas
	{
		get
		{
			if ((Object)(object)mCanvas == (Object)null)
			{
				mCanvas = ((Component)this).GetComponentInParent<Canvas>();
			}
			return mCanvas;
		}
	}

	protected virtual void Awake()
	{
	}

	protected virtual void OnEnable()
	{
		UpdateUI();
	}

	protected virtual void LateUpdate()
	{
		UpdateUI();
	}

	public void UpdateUI()
	{
		if (((Behaviour)this).isActiveAndEnabled)
		{
			UpdatePosition();
		}
	}

	protected virtual void UpdatePosition()
	{
		if (!((Object)(object)AttachObject == (Object)null))
		{
			Vector3 position = BubbleProjection.WorldToUIPos(AttachObject.transform.position + myOffset, Canvas);
			((Component)this).transform.position = position;
			Transform transform = ((Component)this).transform;
			transform.localPosition += UIOffset;
		}
	}
}

}
