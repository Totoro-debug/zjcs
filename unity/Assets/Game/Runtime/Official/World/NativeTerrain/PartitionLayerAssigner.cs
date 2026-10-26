// Migrated from selected official terrain code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using UnityEngine;

public class PartitionLayerAssigner : MonoBehaviour
{
	public enum RegionMode
	{
		Dynamic,
		Fixed
	}

	public int Region;

	public RegionMode Mode;

	public PartitionLayer AttachLayer;
}

}
