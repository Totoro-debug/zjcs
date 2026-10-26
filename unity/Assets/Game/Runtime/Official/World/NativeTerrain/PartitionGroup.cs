// Migrated from selected official terrain code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeTerrain
{
using System;

public class PartitionGroup : PartitionElement
{
	public enum HierarchyMode
	{
		Flat,
		Keep
	}

	public enum PartitionCriteria
	{
		Default = 0,
		Renderer = 1,
		Position = 2,
		Recursive = 4,
		Light2D = 8,
		PolygonLight2D = 0x10,
		LargeVFX = 0x20
	}

	[Flags]
	public enum Options
	{
		Unpack = 1
	}

	public PartitionCriteria Criteria;

	public HierarchyMode ModeOfHierarchy;

	public Options OptionConfig;

	private void Reset()
	{
		Keep = true;
	}
}

}
