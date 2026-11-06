// Original client character view; see mainline character views source receipt.
using Zjcs.Official.EntityLifecycle;
using UnityEngine;

namespace Zjcs.Official.EntityClientComponents
{
public class MapEntityTag : MonoBehaviour
{
	[SerializeField]
	private EMapEntityTagType TagType;

	public bool HaveTag(EMapEntityTagType type)
	{
		return (TagType & type) != 0;
	}
}

}
